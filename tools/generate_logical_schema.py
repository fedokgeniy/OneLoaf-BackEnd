#!/usr/bin/env python3
"""
Generates the logical schema of the OneLoaf database from the live PostgreSQL schema:
  docs/db/logical-schema.svg  colour-coded diagram of all tables by block (Graphviz)
  docs/db/logical-schema.md   the diagram, legend, blocks, relationship types and one Mermaid ER diagram per block

    PGHOST=localhost PGUSER=app_admin PGDATABASE=oneloaf PGPASSWORD=... python3 tools/generate_logical_schema.py
Requires Graphviz (dot). DOCS_OUT overrides the output directory.
"""
import json
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

# Placement of the blocks in the overview: rows of the grid (None = empty cell) and rows of tables inside each block.
# Related blocks are neighbours, so lines between blocks are short and do not cross other blocks.
LAYOUT_GRID = [
    ["ref"],
    ["acc", "cat", "ai"],
    ["hist", "rec", None],
    ["lib", "prod", "start"],
]
# Horizontal alignment of the table rows inside a block (default: center).
LAYOUT_ALIGN = {"hist": "right", "lib": "right"}
# Position of a block inside its grid cell when the block is narrower than the column (default: center).
LAYOUT_CELL_ALIGN = {"hist": "left", "rec": "right"}
LAYOUT_BLOCKS = {
    "ref": [["units", "currencies", "stage_types", "ingredient_categories", "flour_types"]],
    "acc": [["users", "kitchen_settings"], ["push_subscriptions"]],
    "cat": [["ingredient_prices", "ingredients", "flours"]],
    "ai": [["chat_conversations"], ["chat_messages"]],
    "lib": [["recipe_reactions", "published_recipes"], ["content_reports", "recipe_comments"]],
    "rec": [["recipe_ingredients"], ["recipes", "recipe_stages"]],
    "start": [["starters", "starter_feedings"], ["reminders"]],
    "hist": [["bake_record_photos", "bake_record_items"], ["bake_records"]],
    "prod": [["batches", "batch_items", "batch_stages"]],
}

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

    # ---------------------------------------------------------------- Overview diagram (Graphviz)
    # Every table is shown with its key columns only (PK, FK); all columns are in the data dictionary and in the
    # per-block Mermaid diagrams. Blocks are placed on a fixed grid (LAYOUT) so that related blocks are neighbours;
    # Graphviz (neato -n2) only routes orthogonal lines between the fixed positions, and the block frames are drawn
    # behind the tables. References to reference tables, to the owner (users) and to the same table are written in
    # the column row as "-> table" instead of a line.
    fk_target = {(c, col): p for c, col, p, *_ in fks}

    def secondary(child, col, parent):
        return BLOCK_OF[parent][0] == "ref" or parent == "users" or child == parent

    def node_label(t, head):
        rows, shown = [], 0
        for c, typ, _ in cols[t]:
            mark = "PK" if c in pk[t] else ("FK" if c in fk_cols[t] else "")
            if not mark:
                continue
            shown += 1
            target = fk_target.get((t, c))
            typ_txt = f"-&gt; {target}" if target and secondary(t, c, target) else typ
            name = f"<b>{c}</b>" if mark == "PK" else c
            rows.append(f'<tr><td align="left">{name}</td><td align="left"><font color="#6b7280">{typ_txt}</font></td>'
                        f'<td align="left"><font color="{head}">{mark}</font></td></tr>')
        other = len(cols[t]) - shown
        if other:
            rows.append(f'<tr><td align="left" colspan="3"><font color="#9ca3af">+ {other} more column{"s" if other > 1 else ""}</font></td></tr>')
        return (f'<<table border="1" cellborder="0" cellspacing="0" cellpadding="3" bgcolor="white" color="{head}">'
                f'<tr><td colspan="3" bgcolor="{head}"><font color="white"><b>{t}</b></font></td></tr>' + "".join(rows) + '</table>>')

    labels = {t: node_label(t, BLOCK_OF[t][2]) for t in cols}

    # 1. measure the tables
    measure = ["digraph m {", '  node [shape=plaintext, fontname="Helvetica", fontsize=11, margin=0];']
    measure += [f"  {t} [label={labels[t]}];" for t in cols] + ["}"]
    js = json.loads(subprocess.run(["dot", "-Tjson"], input="\n".join(measure), capture_output=True, text=True, check=True).stdout)
    size = {o["name"]: (float(o["width"]) * 72, float(o["height"]) * 72) for o in js["objects"]}

    # 2. place blocks on the grid and tables inside the blocks (top-down coordinates, points)
    PAD, TITLE, GAP_T, GAP_B, PAD_BOTTOM = 30, 26, 34, 90, 46
    block_size, block_rows = {}, {}
    for key, rows in LAYOUT_BLOCKS.items():
        row_dims = [(sum(size[t][0] for t in r) + GAP_T * (len(r) - 1), max(size[t][1] for t in r)) for r in rows]
        block_rows[key] = row_dims
        block_size[key] = (max(w for w, _ in row_dims) + 2 * PAD,
                           sum(h for _, h in row_dims) + GAP_T * (len(rows) - 1) + PAD + PAD_BOTTOM + TITLE)
    ncols = max(len(r) for r in LAYOUT_GRID)
    col_w = [0.0] * ncols
    for grid_row in LAYOUT_GRID:
        if len(grid_row) == 1 and ncols > 1:
            continue
        for i, key in enumerate(grid_row):
            if key:
                col_w[i] = max(col_w[i], block_size[key][0])
    total_w = sum(col_w) + GAP_B * (ncols - 1)
    pos, frames, y = {}, [], 60.0
    for grid_row in LAYOUT_GRID:
        row_h = max(block_size[k][1] for k in grid_row if k)
        x = 0.0
        for i, key in enumerate(grid_row):
            cell_w = total_w if len(grid_row) == 1 else col_w[i]
            if key:
                bw, bh = block_size[key]
                if len(grid_row) == 1:
                    bw = total_w
                cell_align = LAYOUT_CELL_ALIGN.get(key, "center")
                bx = x + {"left": 0, "center": (cell_w - bw) / 2, "right": cell_w - bw}[cell_align]
                by = y
                frames.append((key, bx, by, bw, bh))
                ty = by + PAD + TITLE
                for r, (rw, rh) in zip(LAYOUT_BLOCKS[key], block_rows[key]):
                    inner_w = bw - 2 * PAD
                    gap = GAP_T if len(grid_row) > 1 or len(r) == 1 else (inner_w - sum(size[t][0] for t in r)) / (len(r) - 1)
                    row_w = sum(size[t][0] for t in r) + gap * (len(r) - 1)
                    align = LAYOUT_ALIGN.get(key, "center")
                    tx = bx + PAD + {"left": 0, "center": (inner_w - row_w) / 2, "right": inner_w - row_w}[align]
                    for t in r:
                        w, h = size[t]
                        pos[t] = (tx + w / 2, ty + rh / 2)
                        tx += w + gap
                    ty += rh + GAP_T
            x += cell_w + GAP_B
        y += row_h + GAP_B
    height = y - GAP_B + 20

    # 3. route orthogonal lines between the fixed positions.
    # A sub-point offset per table avoids degenerate (exactly aligned) geometry, on which the orthogonal router of
    # Graphviz can fail; the offsets are invisible. If routing still fails, the next offset pattern is tried
    # (deterministic: the same schema always gives the same drawing with the same Graphviz version).
    def build(seed):
        g = ["digraph oneloaf {",
             '  graph [splines=ortho, outputorder=edgesfirst, pad=0.3, bgcolor="white"];',
             '  node [shape=plaintext, fontname="Helvetica", fontsize=11, margin=0];',
             '  edge [color="#374151", penwidth=1.3, dir=both, arrowsize=0.9];',
             # invisible corner anchors: the drawing area starts at (0, 0), so SVG coordinates are (x, -y)
             '  anchor_ll [shape=point, style=invis, width=0.01, pos="0,0!"];',
             f'  anchor_ur [shape=point, style=invis, width=0.01, pos="{total_w:.1f},{height:.1f}!"];']
        for i, t in enumerate(sorted(cols)):
            px, py = pos[t]
            px += 0.13 * ((i + seed) % 7) + 0.07 * seed
            py += 0.11 * ((i * 3 + seed) % 5)
            g.append(f'  {t} [label={labels[t]}, pos="{px:.2f},{height - py:.2f}!"];')
        drawn = set()
        for child, col, parent, notnull, one_to_one, _ in fks:
            if secondary(child, col, parent) or (child, parent) in drawn:
                continue
            drawn.add((child, parent))
            head_shape = "teetee" if notnull else "teeodot"          # parent side: exactly one / zero or one
            tail_shape = "teeodot" if one_to_one else "crowodot"     # child side: zero or one / zero or many
            g.append(f'  {child} -> {parent} [arrowhead={head_shape}, arrowtail={tail_shape}, color="{BLOCK_OF[child][2]}"];')
        g.append("}")
        return "\n".join(g)

    for seed in range(40):
        dot_src = build(seed)
        result = subprocess.run(["neato", "-n2", "-Tsvg"], input=dot_src, capture_output=True, text=True)
        if result.returncode == 0:
            svg = result.stdout
            break
    else:
        raise SystemExit("Graphviz could not route the overview diagram")

    # 4. block frames and titles behind the tables (Graphviz SVG uses y = -y_graphviz inside the graph group)
    frame_svg, title_svg = [], []
    halo = 'stroke-linejoin="round" paint-order="stroke"'
    for key, bx, by, bw, bh in frames:
        _, title, head, bg, _, _ = next(b for b in BLOCKS if b[0] == key)
        top = -(height - by)
        frame_svg.append(f'<rect x="{bx:.1f}" y="{top:.1f}" width="{bw:.1f}" height="{bh:.1f}" rx="10" ry="10" '
                         f'fill="{bg}" stroke="{head}" stroke-width="2"/>')
        # block titles are drawn over the lines with a halo in the frame colour, so a line passing behind a title
        # is interrupted instead of crossing the text
        title_svg.append(f'<text x="{bx + 14:.1f}" y="{top + 22:.1f}" text-anchor="start" font-family="Helvetica,sans-Serif" '
                         f'font-size="15" font-weight="bold" fill="{head}" stroke="{bg}" stroke-width="6" {halo}>{title}</text>')
    frame_svg.append(f'<text x="{total_w / 2:.1f}" y="{-(height - 22):.1f}" text-anchor="middle" font-family="Helvetica,sans-Serif" '
                     f'font-size="20" font-weight="bold" fill="#111827">OneLoaf - logical database schema</text>')
    frame_svg.append(f'<text x="{total_w / 2:.1f}" y="{-(height - 42):.1f}" text-anchor="middle" font-family="Helvetica,sans-Serif" '
                     f'font-size="12" fill="#374151">Key columns only (PK - primary key, FK - foreign key). '
                     f'"-&gt; table" - reference to a reference table, to the owner (users) or to the same table; lines - other relationships</text>')
    marker = svg.index("<polygon", svg.index('<g id="graph0"'))
    marker = svg.index("\n", marker) + 1                   # after the background polygon
    svg = svg[:marker] + "\n".join(frame_svg) + "\n" + svg[marker:]
    end = svg.rindex("</g>")                               # end of the graph group: titles go on top
    svg = svg[:end] + "\n".join(title_svg) + "\n" + svg[end:]
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
    w("The overview shows every table with its key columns; all columns are in the per-block diagrams below and in the data dictionary. "
      "The frame colour shows the functional block. Lines are relationships between tables of the domain. "
      "References to reference tables, to the owner and to the same table (recipe variants) are shown in the column row as "
      "\"-> table\" to keep the diagram readable: every workspace table references `users` (`owner_id`, `ON DELETE CASCADE`). "
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
