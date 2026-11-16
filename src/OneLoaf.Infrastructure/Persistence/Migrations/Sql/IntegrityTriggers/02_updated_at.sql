-- updated_at is maintained by the database, not by each caller.

CREATE OR REPLACE FUNCTION trg_set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;

COMMENT ON FUNCTION trg_set_updated_at() IS 'BEFORE UPDATE trigger: sets updated_at to the current time.';

CREATE TRIGGER trg_users_updated_at              BEFORE UPDATE ON users              FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_kitchen_settings_updated_at   BEFORE UPDATE ON kitchen_settings   FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_flours_updated_at             BEFORE UPDATE ON flours             FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_ingredients_updated_at        BEFORE UPDATE ON ingredients        FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_recipes_updated_at            BEFORE UPDATE ON recipes            FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_batches_updated_at            BEFORE UPDATE ON batches            FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_starters_updated_at           BEFORE UPDATE ON starters           FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_bake_records_updated_at       BEFORE UPDATE ON bake_records       FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
CREATE TRIGGER trg_chat_conversations_updated_at BEFORE UPDATE ON chat_conversations FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();
