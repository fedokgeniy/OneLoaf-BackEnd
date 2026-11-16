#!/usr/bin/env python3
"""
Generates the logical schema of the OneLoaf database from the live PostgreSQL schema:
  docs/db/logical-schema.svg  colour-coded diagram of all tables by block (Graphviz)
  docs/db/logical-schema.md   the diagram, legend, blocks, relationship types and one Mermaid ER diagram per block

    PGHOST=localhost PGUSER=app_admin PGDATABASE=oneloaf PGPASSWORD=... python3 tools/generate_logical_schema.py
Requires Graphviz (dot). DOCS_OUT overrides the output directory.
"""
import os
import re
import subprocess
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.environ.get("DOCS_OUT", os.path.join(ROOT, "docs", "db"))

# block: (title, header colour, background colour, tables, description)
BLOCKS = [
    ("ref", "Reference data", "#6b7280", "#f3f4f6",
     ["units", "currencies", "stage_types", "ingredient_categories", "flour_types"],
     "Fixed data: units, currencies, stage types, ingredient categories, flour types. Inserted by a migration; the services only read them."),
    ("acc", "E1 Accounts and kitchen", "#2563eb", "#eff6ff",
     ["users", "kitchen_settings", "push_subscriptions"],
     "The user is the root of a workspace. All data of a baker references the user through owner_id and is deleted with the account."),
    ("cat", "E3 Catalog and prices", "#059669", "#ecfdf5",
     ["flours", "ingredients", "ingredient_prices"],
     "Flours, ingredients and price history - the basis of the cost on any date."),
    ("rec", "E2 Recipes and variants", "#d97706", "#fffbeb",
     ["recipes", "recipe_ingredients", "recipe_stages"],
     "Recipe knowledge base. A variant references its parent and stores only the differences: replacing lines and variant_overrides."),
    ("prod", "E5 Production", "#dc2626", "#fef2f2",
     ["batches", "batch_items", "batch_stages"],
     "Batch - items (recipe x loaves) - stage log with plan and actual times."),
    ("start", "E6 Starters and reminders", "#7c3aed", "#f5f3ff",
     ["starters", "starter_feedings", "reminders"],
     "Starters, feeding log and the reminder queue of the Notification service."),
    ("hist", "E7 Baking history", "#0891b2", "#ecfeff",
     ["bake_records", "bake_record_items", "bake_record_photos"],
     "A record is created by a trigger when a batch is finished and keeps a snapshot on the bake date."),
    ("ai", "E8 AI assistant", "#ca8a04", "#fefce8",
     ["chat_conversations", "chat_messages"],
     "Conversations and the log of backend tool calls."),
    ("lib", "E9 Library and moderation", "#db2777", "#fdf2f8",
     ["published_recipes", "recipe_comments", "recipe_reactions", "content_reports"],
     "Immutable snapshots of published recipes, comments, reactions, reports."),
]
BLOCK_OF = {t: b for b in BLOCKS for t in b[4]}

TYPE_SHORT = [(r"^character varying.*", "varchar"), (r"^character\(.*", "char"), (r"^numeric.*", "numeric"),
              (r"^timestamp with time zone$", "timestamptz"), (r"^integer$", "int"), (r"^boolean$", "bool")]


def q(sql):
    out = subprocess.run(["psql", "-X", "-tA", "-F", "\t", "-v", "ON_ERROR_STOP=1", "-c", sql],
                         check=True, capture_output=True, text=True).stdout
    return [line.split("\t") for line in out.splitlines() if line]


def short(t):
    for p, r in TYPE_SHORT:
        if re.match(p, t):
            return r
    return t


def main():
    cols = defaultdict(list)
    for t, c, typ, notnull in q("""
        SELECT c.relname, a.attname, format_type(a.atttypid, a.atttypmod), a.attnotnull
        FROM pg_attribute a JOIN pg_class c ON c.oid = a.attrelid
        WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r' AND a.attnum > 0 AND NOT a.attisdropped
          AND c.relname <> '__ef_migrations_history'
        ORDER BY c.relname, a.attnum"""):
        cols[t].append((c, short(typ), notnull == "t"))
    missing = set(cols) - set(BLOCK_OF)
    if missing:
        raise SystemExit(f"Tables without a block: {sorted(missing)}")

    pk = defaultdict(set)
    for t, c in q("""
        SELECT cl.relname, a.attname FROM pg_constraint con JOIN pg_class cl ON cl.oid = con.conrelid
        JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = ANY (con.conkey)
        WHERE con.contype = 'p' AND cl.relnamespace = 'public'::regnamespace"""):
        pk[t].add(c)
    uniq1 = defaultdict(set)
    for t, c in q("""
        SELECT t.relname, a.attname FROM pg_index ix JOIN pg_class t ON t.oid = ix.indrelid
        JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ix.indkey[0]
        WHERE ix.indisunique AND ix.indnatts = 1 AND ix.indpred IS NULL AND t.relnamespace = 'public'::regnamespace"""):
        uniq1[t].add(c)

    fks = []
    fk_cols = defaultdict(set)
    for child, col, parent, notnull, deltype in q("""
        SELECT cl.relname, a.attname, rcl.relname, a.attnotnull, con.confdeltype
        FROM pg_constraint con JOIN pg_class cl ON cl.oid = con.conrelid JOIN pg_class rcl ON rcl.oid = con.confrelid
        JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = con.conkey[1]
        WHERE con.contype = 'f' AND cl.relnamespace = 'public'::regnamespace ORDER BY cl.relname, a.attname"""):
        one_to_one = col in uniq1[child] or (col in pk[child] and len(pk[child]) == 1)
        fks.append((child, col, parent, notnull == "t", one_to_one, deltype))
        fk_cols[child].add(col)

    fk_target = {(c, col): p for c, col, p, *_ in fks}

    def secondary(child, col, parent):
        return BLOCK_OF[parent][0] == "ref" or parent == "users"

    # ---------------------------------------------------------------- Graphviz
    g = ['digraph oneloaf {',
         '  graph [rankdir=LR, splines=true, nodesep=0.4, ranksep=0.9, newrank=true, fontname="Helvetica", fontsize=20, '
         'label=<<b>OneLoaf - logical database schema</b><br/><font point-size="12">PK - primary key, FK - foreign key; '
         '"-&gt; table" - reference to a reference table or to the owner (users); lines - relationships of the domain</font>>, labelloc=t, pad=0.4];',
         '  node [shape=plaintext, fontname="Helvetica", fontsize=11];',
         '  edge [fontname="Helvetica", fontsize=9, color="#374151", penwidth=1.2, dir=both];']
    for key, title, head, bg, tables, _ in BLOCKS:
        g.append(f'  subgraph cluster_{key} {{ label=<<b>{title}</b>>; fontsize=15; fontcolor="{head}"; '
                 f'style="rounded,filled"; color="{head}"; fillcolor="{bg}"; penwidth=2;')
        for t in tables:
            rows = []
            for c, typ, nn in cols[t]:
                mark = "PK" if c in pk[t] else ("FK" if c in fk_cols[t] else "")
                if not mark and c in ("created_at", "updated_at"):
                    continue
                name = f"<b>{c}</b>" if mark == "PK" else (f"<i>{c}</i>" if mark == "FK" else c)
                target = fk_target.get((t, c))
                typ_txt = f"-&gt; {target}" if target and secondary(t, c, target) else typ
                rows.append(f'<tr><td align="left" port="{c}">{name}</td><td align="left"><font color="#6b7280">{typ_txt}</font></td>'
                            + (f'<td align="left"><font color="{head}">{mark}</font></td></tr>' if mark else '<td> </td></tr>'))
            hidden = sum(1 for c, _, _ in cols[t] if c in ("created_at", "updated_at") and c not in pk[t])
            if hidden:
                rows.append(f'<tr><td align="left" colspan="3"><font color="#9ca3af">+ {", ".join(c for c, _, _ in cols[t] if c in ("created_at", "updated_at"))}</font></td></tr>')
            g.append(f'    {t} [label=<<table border="0" cellborder="0" cellspacing="0" cellpadding="3" bgcolor="white" '
                     f'style="rounded" color="{head}"><tr><td colspan="3" bgcolor="{head}"><font color="white"><b>{t}</b></font></td></tr>'
                     + "".join(rows) + '</table>>];')
        g.append('  }')
    for child, col, parent, notnull, one_to_one, deltype in fks:
        head_shape = "teetee" if notnull else "teeodot"          # parent side: exactly one / zero or one
        tail_shape = "teeodot" if one_to_one else "crowodot"     # child side: zero or one / zero or many
        if secondary(child, col, parent):
            continue
        style = f'color="{BLOCK_OF[child][2]}"'
        g.append(f'  {child}:{col} -> {parent} [arrowhead={head_shape}, arrowtail={tail_shape}, {style}];')
    g.append('}')
    dot_src = "\n".join(g)
    svg = subprocess.run(["dot", "-Tsvg"], input=dot_src, capture_output=True, text=True, check=True).stdout
    with open(os.path.join(OUT, "logical-schema.svg"), "w") as f:
        f.write(svg)
    with open(os.path.join(OUT, "logical-schema.dot"), "w") as f:
        f.write(dot_src + "\n")

    # ---------------------------------------------------------------- Markdown
    m = []
    w = m.append
    w("# OneLoaf logical database schema\n")
    w("> Generated by `tools/generate_logical_schema.py` from the PostgreSQL schema created by the migrations. "
      "Columns, types and constraints of every table are in the [data dictionary](data-dictionary.md).\n")
    w("## Overview\n")
    w("![OneLoaf logical schema](logical-schema.svg)\n")
    w("The frame colour shows the functional block. Lines are relationships between tables of the domain. "
      "References to reference tables and to the owner are shown in the column row as \"-> table\" to keep the diagram readable: "
      "every workspace table references `users` (`owner_id`, `ON DELETE CASCADE`). "
      "Line ends: \"||\" - exactly one, \"o|\" - zero or one, \"o<\" - zero or many. "
      "Source of the diagram: [`logical-schema.dot`](logical-schema.dot).\n")
    w("## Blocks\n")
    w("| Block | Tables | Purpose |\n|---|---|---|")
    for _, title, head, _, tables, desc in BLOCKS:
        w(f"| {title} | {', '.join(f'`{t}`' for t in tables)} | {desc} |")
    w("")
    w("## Relationship types\n")
    w("| Type | Where in the schema |\n|---|---|")
    w("| 1 : 1 | `users` - `kitchen_settings` (PK = FK); `batches` - `bake_records` (unique `batch_id`) |")
    w("| 1 : N | `users` - every workspace table; `recipes` - `recipe_ingredients`, `recipe_stages`; "
      "`batches` - `batch_items` - `batch_stages`; `starters` - `starter_feedings`; `chat_conversations` - `chat_messages` |")
    w("| M : N | `recipes` and `ingredients` through `recipe_ingredients`; `users` and `published_recipes` through `recipe_reactions` "
      "(composite PK) and through `recipe_comments` |")
    w("| Recursive (hierarchy) | `recipes.parent_recipe_id` references `recipes` (variants of any level); "
      "`recipe_ingredients.replaces_item_id`, `recipe_stages.replaces_stage_id` (a line of a variant replaces a line of the parent) |")
    w("| Reference (N : 1 to a reference table) | `ingredients` to `units` and `ingredient_categories`; `flours` to `flour_types`; "
      "`recipe_stages`, `batch_stages` to `stage_types`; prices and amounts to `currencies` |")
    w("| Optional (0..1) | `ingredients.flour_id`, `batch_items.starter_id`, `bake_records.batch_id`, "
      "`published_recipes.source_recipe_id`, targets of `reminders` and `content_reports` |")
    w("")
    w("## Blocks in detail\n")
    w("Mermaid ER notation: `||` - exactly one, `|o` - zero or one, `o{` - zero or many. The label of a line is the foreign key column.\n")
    for key, title, head, _, tables, desc in BLOCKS:
        if key == "ref":
            continue
        related = set(tables)
        for child, col, parent, *_ in fks:
            if child in tables and BLOCK_OF[parent][0] not in ("ref",) and parent != "users":
                related.add(parent)
            if parent in tables and child not in tables and BLOCK_OF[child][0] != "ref" and parent != "users":
                related.add(child)
        if key != "acc":
            related.add("users")
        w(f"### {title}\n\n{desc}\n")
        w("```mermaid\nerDiagram")
        for t in sorted(related, key=lambda x: (x not in tables, x)):
            full = t in tables
            w(f"    {t} {{")
            for c, typ, _ in cols[t]:
                marks = []
                if c in pk[t]:
                    marks.append("PK")
                if c in fk_cols[t]:
                    marks.append("FK")
                if c in uniq1[t] and c not in pk[t]:
                    marks.append("UK")
                if not full and not marks:
                    continue
                w(f"        {typ} {c}{(' ' + ','.join(marks)) if marks else ''}")
            w("    }")
        for child, col, parent, notnull, one_to_one, _ in fks:
            if child in related and parent in related and (child in tables or parent in tables):
                left = "||" if notnull else "|o"
                right = "o|" if one_to_one else "o{"
                w(f'    {parent} {left}--{right} {child} : "{col}"')
        w("```\n")
    with open(os.path.join(OUT, "logical-schema.md"), "w") as f:
        f.write("\n".join(m))


if __name__ == "__main__":
    main()
