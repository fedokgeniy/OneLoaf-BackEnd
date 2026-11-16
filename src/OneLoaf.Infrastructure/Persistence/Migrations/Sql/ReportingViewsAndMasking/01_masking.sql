-- Masking of personal data (GDPR data minimisation).
-- Roles that do not need personal data (app_read: reporting, support analytics) get masked views only.

CREATE OR REPLACE FUNCTION fn_mask_email(p_email text)
RETURNS text
LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
AS $$
    -- baker@example.com -> b***@e***.com
    SELECT CASE
               WHEN position('@' IN p_email) = 0 THEN left(p_email, 1) || '***'
               ELSE left(split_part(p_email, '@', 1), 1) || '***@'
                    || left(split_part(p_email, '@', 2), 1) || '***'
                    || coalesce('.' || nullif(substring(split_part(p_email, '@', 2) FROM '\.([^.]+)$'), ''), '')
           END;
$$;

COMMENT ON FUNCTION fn_mask_email(text) IS 'Masks an e-mail address: baker@example.com -> b***@e***.com.';

CREATE OR REPLACE FUNCTION fn_mask_name(p_name text)
RETURNS text
LANGUAGE sql IMMUTABLE STRICT PARALLEL SAFE
AS $$
    -- Demo Baker -> D*** B***
    SELECT string_agg(left(part, 1) || '***', ' ' ORDER BY ord)
    FROM regexp_split_to_table(btrim(p_name), '\s+') WITH ORDINALITY AS t(part, ord);
$$;

COMMENT ON FUNCTION fn_mask_name(text) IS 'Masks a person name word by word: Demo Baker -> D*** B***.';

CREATE VIEW v_users_masked AS
SELECT u.id,
       fn_mask_email(u.email)       AS email_masked,
       fn_mask_name(u.display_name) AS display_name_masked,
       u.role,
       u.created_at::date           AS registered_on,
       u.last_login_at::date        AS last_login_on
FROM users u;

COMMENT ON VIEW v_users_masked IS
    'Users without personal data in clear text: masked e-mail and name, dates without time. For the app_read role.';
