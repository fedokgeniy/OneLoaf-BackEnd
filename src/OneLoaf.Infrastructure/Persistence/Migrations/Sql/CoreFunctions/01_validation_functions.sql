-- Validation functions used by CHECK constraints.
-- IMMUTABLE: the result depends only on the argument, so PostgreSQL allows them in CHECK constraints.

-- Password hash format (NFR-04). Accepts only ASP.NET Core Identity v3 hashes:
--   byte 0      = 0x01 (format v3)
--   bytes 1-4   = PRF: 1 = HMAC-SHA256, 2 = HMAC-SHA512 (0 = HMAC-SHA1 is rejected as outdated)
--   bytes 5-8   = PBKDF2 iteration count, at least 100 000
--   bytes 9-12  = salt length, at least 16 bytes
--   then salt and a derived key of at least 32 bytes.
-- Plain text, MD5, SHA-1 or unsalted values do not pass.
CREATE OR REPLACE FUNCTION fn_is_strong_password_hash(p_hash text)
RETURNS boolean
LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE
AS $$
DECLARE
    b          bytea;
    prf        bigint;
    iterations bigint;
    salt_len   bigint;
BEGIN
    IF p_hash !~ '^[A-Za-z0-9+/]+={0,2}$' OR length(p_hash) % 4 <> 0 THEN
        RETURN false;
    END IF;
    b := decode(p_hash, 'base64');
    IF length(b) < 13 OR get_byte(b, 0) <> 1 THEN
        RETURN false;
    END IF;
    prf        := (get_byte(b, 1)::bigint << 24) | (get_byte(b, 2) << 16) | (get_byte(b, 3) << 8) | get_byte(b, 4);
    iterations := (get_byte(b, 5)::bigint << 24) | (get_byte(b, 6) << 16) | (get_byte(b, 7) << 8) | get_byte(b, 8);
    salt_len   := (get_byte(b, 9)::bigint << 24) | (get_byte(b, 10) << 16) | (get_byte(b, 11) << 8) | get_byte(b, 12);
    RETURN prf IN (1, 2)
       AND iterations >= 100000
       AND salt_len >= 16
       AND length(b) >= 13 + salt_len + 32;
END;
$$;

COMMENT ON FUNCTION fn_is_strong_password_hash(text) IS
    'True for an ASP.NET Core Identity v3 PBKDF2 hash (HMAC-SHA256/512, >= 100000 iterations, salt >= 16 bytes).';

-- Structure of recipes.variant_overrides.
-- A JSON object whose keys are a subset of: description (string|null), loafWeightG (number > 0|null), notes (string|null).
-- Key present = the variant changes the value (null clears it); key absent = the value is inherited from the parent.
CREATE OR REPLACE FUNCTION fn_is_valid_variant_overrides(p jsonb)
RETURNS boolean
LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE
AS $$
DECLARE
    k text;
BEGIN
    IF jsonb_typeof(p) <> 'object' THEN
        RETURN false;
    END IF;
    FOR k IN SELECT jsonb_object_keys(p) LOOP
        CASE k
            WHEN 'description' THEN
                IF jsonb_typeof(p -> k) NOT IN ('string', 'null') OR length(coalesce(p ->> k, '')) > 4000 THEN RETURN false; END IF;
            WHEN 'notes' THEN
                IF jsonb_typeof(p -> k) NOT IN ('string', 'null') OR length(coalesce(p ->> k, '')) > 4000 THEN RETURN false; END IF;
            WHEN 'loafWeightG' THEN
                IF jsonb_typeof(p -> k) = 'null' THEN
                    NULL;
                ELSIF jsonb_typeof(p -> k) <> 'number' OR (p ->> k)::numeric <= 0 THEN
                    RETURN false;
                END IF;
            ELSE
                RETURN false;
        END CASE;
    END LOOP;
    RETURN true;
END;
$$;

COMMENT ON FUNCTION fn_is_valid_variant_overrides(jsonb) IS
    'Validates recipes.variant_overrides: object with optional keys description, loafWeightG, notes.';

-- Structure of published_recipes.snapshot (schemaVersion 1):
-- {
--   "schemaVersion": 1,
--   "yieldLoaves": <integer > 0>,
--   "loafWeightG": <number > 0 | null>,
--   "ingredients": [ { "name": <non-empty string>, "category": <string>, "unit": <string>,
--                      "quantity": <number >= 0>, "isOptional": <boolean> }, ... ]   (at least one),
--   "stages":      [ { "name": <non-empty string>, "stageType": <string>, "minMinutes": <integer >= 0>,
--                      "maxMinutes": <integer >= minMinutes>, "temperatureC": <number | null>,
--                      "proofMode": "warm" | "cold" | null, "isOptional": <boolean> }, ... ]
-- }
CREATE OR REPLACE FUNCTION fn_is_valid_recipe_snapshot(p jsonb)
RETURNS boolean
LANGUAGE plpgsql IMMUTABLE STRICT PARALLEL SAFE
AS $$
DECLARE
    k    text;
    item jsonb;
BEGIN
    IF jsonb_typeof(p) <> 'object'
       OR NOT (p ?& ARRAY['schemaVersion', 'yieldLoaves', 'ingredients', 'stages']) THEN
        RETURN false;
    END IF;
    FOR k IN SELECT jsonb_object_keys(p) LOOP
        IF k NOT IN ('schemaVersion', 'yieldLoaves', 'loafWeightG', 'ingredients', 'stages') THEN
            RETURN false;
        END IF;
    END LOOP;

    IF p -> 'schemaVersion' <> '1'::jsonb THEN RETURN false; END IF;
    IF jsonb_typeof(p -> 'yieldLoaves') <> 'number'
       OR (p ->> 'yieldLoaves')::numeric <= 0
       OR (p ->> 'yieldLoaves')::numeric <> trunc((p ->> 'yieldLoaves')::numeric) THEN
        RETURN false;
    END IF;
    IF p ? 'loafWeightG' AND jsonb_typeof(p -> 'loafWeightG') <> 'null'
       AND (jsonb_typeof(p -> 'loafWeightG') <> 'number' OR (p ->> 'loafWeightG')::numeric <= 0) THEN
        RETURN false;
    END IF;

    IF jsonb_typeof(p -> 'ingredients') <> 'array' OR jsonb_array_length(p -> 'ingredients') = 0 THEN
        RETURN false;
    END IF;
    FOR item IN SELECT jsonb_array_elements(p -> 'ingredients') LOOP
        IF jsonb_typeof(item) <> 'object'
           OR jsonb_typeof(item -> 'name') IS DISTINCT FROM 'string' OR length(btrim(item ->> 'name')) = 0
           OR jsonb_typeof(item -> 'category') IS DISTINCT FROM 'string'
           OR jsonb_typeof(item -> 'unit') IS DISTINCT FROM 'string'
           OR jsonb_typeof(item -> 'quantity') IS DISTINCT FROM 'number' OR (item ->> 'quantity')::numeric < 0
           OR jsonb_typeof(item -> 'isOptional') IS DISTINCT FROM 'boolean' THEN
            RETURN false;
        END IF;
    END LOOP;

    IF jsonb_typeof(p -> 'stages') <> 'array' THEN
        RETURN false;
    END IF;
    FOR item IN SELECT jsonb_array_elements(p -> 'stages') LOOP
        IF jsonb_typeof(item) <> 'object'
           OR jsonb_typeof(item -> 'name') IS DISTINCT FROM 'string' OR length(btrim(item ->> 'name')) = 0
           OR jsonb_typeof(item -> 'stageType') IS DISTINCT FROM 'string'
           OR jsonb_typeof(item -> 'minMinutes') IS DISTINCT FROM 'number'
           OR jsonb_typeof(item -> 'maxMinutes') IS DISTINCT FROM 'number'
           OR (item ->> 'minMinutes')::numeric < 0
           OR (item ->> 'maxMinutes')::numeric < (item ->> 'minMinutes')::numeric
           OR jsonb_typeof(item -> 'isOptional') IS DISTINCT FROM 'boolean'
           OR (item ? 'temperatureC' AND jsonb_typeof(item -> 'temperatureC') NOT IN ('number', 'null'))
           OR (item ? 'proofMode' AND jsonb_typeof(item -> 'proofMode') <> 'null'
               AND item ->> 'proofMode' NOT IN ('warm', 'cold')) THEN
            RETURN false;
        END IF;
    END LOOP;
    RETURN true;
END;
$$;

COMMENT ON FUNCTION fn_is_valid_recipe_snapshot(jsonb) IS
    'Validates published_recipes.snapshot against the documented structure (schemaVersion 1).';
