-- CHECK constraints that call validation functions (created in the CoreFunctions migration).

-- NFR-04: only strong salted password hashes, never plain text or outdated algorithms.
ALTER TABLE users
    ADD CONSTRAINT ck_users_password_hash_strong CHECK (fn_is_strong_password_hash(password_hash));

-- JSON columns follow a documented structure (see docs/db/design.md, section JSON).
ALTER TABLE recipes
    ADD CONSTRAINT ck_recipes_variant_overrides_valid
        CHECK (variant_overrides IS NULL OR fn_is_valid_variant_overrides(variant_overrides));

ALTER TABLE published_recipes
    ADD CONSTRAINT ck_published_recipes_snapshot_valid CHECK (fn_is_valid_recipe_snapshot(snapshot));
