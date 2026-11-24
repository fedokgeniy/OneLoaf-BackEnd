-- Used by the Down method of RefreshTokens: sp_anonymize_user as the BusinessProcedures migration created it.
CREATE OR REPLACE PROCEDURE sp_anonymize_user(p_user_id uuid)
LANGUAGE plpgsql
AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM users WHERE id = p_user_id) THEN
        RAISE EXCEPTION 'User % not found', p_user_id USING ERRCODE = 'P0002';
    END IF;

    DELETE FROM push_subscriptions WHERE user_id = p_user_id;
    DELETE FROM chat_conversations WHERE owner_id = p_user_id;
    DELETE FROM reminders          WHERE owner_id = p_user_id;
    DELETE FROM bake_records       WHERE owner_id = p_user_id;
    DELETE FROM batches            WHERE owner_id = p_user_id;
    DELETE FROM starters           WHERE owner_id = p_user_id;
    DELETE FROM recipes            WHERE owner_id = p_user_id;
    DELETE FROM ingredients        WHERE owner_id = p_user_id;
    DELETE FROM flours             WHERE owner_id = p_user_id;
    DELETE FROM kitchen_settings   WHERE user_id  = p_user_id;

    UPDATE users
    SET email            = 'anonymized-' || id || '@anonymized.invalid',
        normalized_email = upper('anonymized-' || id || '@anonymized.invalid'),
        display_name     = 'Anonymous baker',
        password_hash    = fn_random_disabled_password_hash(),
        role             = 'user',
        last_login_at    = NULL
    WHERE id = p_user_id;
END;
$$;

COMMENT ON PROCEDURE sp_anonymize_user(uuid) IS
    'Removes personal data and the private workspace of a user, keeps public contributions as "Anonymous baker" (GDPR).';
