using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OneLoaf.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "currencies",
                columns: table => new
                {
                    code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    minor_units = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_currencies", x => x.code);
                    table.CheckConstraint("ck_currencies_code_format", "code ~ '^[A-Z]{3}$'");
                    table.CheckConstraint("ck_currencies_minor_units", "minor_units BETWEEN 0 AND 4");
                },
                comment: "Reference. ISO 4217 currencies allowed for prices and costs.");

            migrationBuilder.CreateTable(
                name: "flour_types",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flour_types", x => x.code);
                    table.CheckConstraint("ck_flour_types_code_format", "code ~ '^[a-z_]{1,32}$'");
                },
                comment: "Reference. Grain types of flour.");

            migrationBuilder.CreateTable(
                name: "ingredient_categories",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_flour = table.Column<bool>(type: "boolean", nullable: false),
                    is_liquid = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredient_categories", x => x.code);
                    table.CheckConstraint("ck_ingredient_categories_code_format", "code ~ '^[a-z_]{1,32}$'");
                    table.CheckConstraint("ck_ingredient_categories_flour_not_liquid", "NOT (is_flour AND is_liquid)");
                },
                comment: "Reference. Ingredient categories; is_flour / is_liquid drive baker's percentages and hydration.");

            migrationBuilder.CreateTable(
                name: "stage_types",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_fermentation = table.Column<bool>(type: "boolean", nullable: false),
                    default_uses_oven = table.Column<bool>(type: "boolean", nullable: false),
                    default_uses_mixer = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stage_types", x => x.code);
                    table.CheckConstraint("ck_stage_types_code_format", "code ~ '^[a-z_]{1,32}$'");
                },
                comment: "Reference. Types of production stages with default equipment use.");

            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dimension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    factor_to_base = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_units", x => x.code);
                    table.CheckConstraint("ck_units_code_format", "code ~ '^[a-z]{1,16}$'");
                    table.CheckConstraint("ck_units_dimension", "dimension IN ('mass', 'volume', 'count')");
                    table.CheckConstraint("ck_units_factor_to_base", "factor_to_base > 0");
                },
                comment: "Reference. Units of measure and conversion to the base unit of their dimension.");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_display_name_not_blank", "length(btrim(display_name)) > 0");
                    table.CheckConstraint("ck_users_email_format", "email ~ '^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$'");
                    table.CheckConstraint("ck_users_normalized_email", "normalized_email = upper(email)");
                    table.CheckConstraint("ck_users_role", "role IN ('user', 'admin')");
                },
                comment: "E1. Registered bakers and administrators; each user owns a private workspace.");

            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    planned_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    planned_ready_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    planned_ready_to = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actual_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    price_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batches", x => x.id);
                    table.CheckConstraint("ck_batches_completed_after_start", "completed_at >= actual_start_at");
                    table.CheckConstraint("ck_batches_done_has_completed_at", "status <> 'done' OR completed_at IS NOT NULL");
                    table.CheckConstraint("ck_batches_name_not_blank", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_batches_ready_after_start", "planned_ready_from >= planned_start_at");
                    table.CheckConstraint("ck_batches_ready_window", "planned_ready_to >= planned_ready_from");
                    table.CheckConstraint("ck_batches_source", "source IN ('manual', 'assistant')");
                    table.CheckConstraint("ck_batches_status", "status IN ('planned', 'in_progress', 'done', 'cancelled')");
                    table.CheckConstraint("ck_batches_total_cost", "total_cost >= 0");
                    table.ForeignKey(
                        name: "fk_batches_currencies_currency",
                        column: x => x.currency,
                        principalTable: "currencies",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_batches_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E5, US-20, US-26. Production batches with plan, status and cost snapshot.");

            migrationBuilder.CreateTable(
                name: "chat_conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_conversations", x => x.id);
                    table.ForeignKey(
                        name: "fk_chat_conversations_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E8. Conversations with the AI assistant.");

            migrationBuilder.CreateTable(
                name: "flours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    flour_type_code = table.Column<string>(type: "character varying(32)", nullable: false),
                    protein_percent = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    grind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_flours", x => x.id);
                    table.CheckConstraint("ck_flours_protein_percent", "protein_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_flours_flour_types_flour_type_code",
                        column: x => x.flour_type_code,
                        principalTable: "flour_types",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_flours_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E3, US-11. Flour catalog of a user.");

            migrationBuilder.CreateTable(
                name: "kitchen_settings",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oven_power_kw = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    oven_capacity_loaves = table.Column<int>(type: "integer", nullable: true),
                    mixer_bowl_capacity_kg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    electricity_tariff_per_kwh = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    packaging_cost_per_loaf = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    loss_rate_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    labor_rate_per_hour = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kitchen_settings", x => x.user_id);
                    table.CheckConstraint("ck_kitchen_settings_electricity_tariff_per_kwh", "electricity_tariff_per_kwh >= 0");
                    table.CheckConstraint("ck_kitchen_settings_labor_rate_per_hour", "labor_rate_per_hour >= 0");
                    table.CheckConstraint("ck_kitchen_settings_loss_rate_percent", "loss_rate_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_kitchen_settings_mixer_bowl_capacity_kg", "mixer_bowl_capacity_kg > 0");
                    table.CheckConstraint("ck_kitchen_settings_oven_capacity_loaves", "oven_capacity_loaves > 0");
                    table.CheckConstraint("ck_kitchen_settings_oven_power_kw", "oven_power_kw > 0");
                    table.CheckConstraint("ck_kitchen_settings_packaging_cost_per_loaf", "packaging_cost_per_loaf >= 0");
                    table.ForeignKey(
                        name: "fk_kitchen_settings_currencies_currency",
                        column: x => x.currency,
                        principalTable: "currencies",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_kitchen_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E3, US-13, US-47-49. Equipment and cost parameters, one row per user.");

            migrationBuilder.CreateTable(
                name: "push_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    p256dh = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    auth = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_push_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "fk_push_subscriptions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Web Push subscriptions of users' devices (Notification service).");

            migrationBuilder.CreateTable(
                name: "bake_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    baked_on = table.Column<DateOnly>(type: "date", nullable: false),
                    total_loaves = table.Column<int>(type: "integer", nullable: false),
                    total_cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: true),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    assistant_analysis = table.Column<string>(type: "text", nullable: true),
                    analysis_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bake_records", x => x.id);
                    table.CheckConstraint("ck_bake_records_rating", "rating BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_bake_records_total_cost", "total_cost >= 0");
                    table.CheckConstraint("ck_bake_records_total_loaves", "total_loaves >= 0");
                    table.ForeignKey(
                        name: "fk_bake_records_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_bake_records_currencies_currency",
                        column: x => x.currency,
                        principalTable: "currencies",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_bake_records_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E7, US-31, US-32, US-36. Baking history: one record per finished batch.");

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    tool_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tool_payload = table.Column<string>(type: "jsonb", nullable: true),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chat_messages", x => x.id);
                    table.CheckConstraint("ck_chat_messages_role", "role IN ('user', 'assistant', 'tool')");
                    table.CheckConstraint("ck_chat_messages_tokens", "input_tokens >= 0 AND output_tokens >= 0");
                    table.CheckConstraint("ck_chat_messages_tool_name", "role <> 'tool' OR tool_name IS NOT NULL");
                    table.CheckConstraint("ck_chat_messages_tool_payload", "tool_payload IS NULL OR (tool_name IS NOT NULL AND jsonb_typeof(tool_payload) = 'object')");
                    table.ForeignKey(
                        name: "fk_chat_messages_chat_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "chat_conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E8, US-34-36. Messages of the user, the assistant and tool results.");

            migrationBuilder.CreateTable(
                name: "ingredients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category_code = table.Column<string>(type: "character varying(32)", nullable: false),
                    unit_code = table.Column<string>(type: "character varying(16)", nullable: false),
                    flour_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredients", x => x.id);
                    table.ForeignKey(
                        name: "fk_ingredients_flours_flour_id",
                        column: x => x.flour_id,
                        principalTable: "flours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ingredients_ingredient_categories_category_code",
                        column: x => x.category_code,
                        principalTable: "ingredient_categories",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_ingredients_units_unit_code",
                        column: x => x.unit_code,
                        principalTable: "units",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_ingredients_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E3, US-10. Ingredient catalog of a user, with a unit of measure.");

            migrationBuilder.CreateTable(
                name: "starters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    flour_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hydration_percent = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    feeding_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    feeding_interval_hours = table.Column<int>(type: "integer", nullable: false),
                    rise_hours = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_starters", x => x.id);
                    table.CheckConstraint("ck_starters_feeding_interval_hours", "feeding_interval_hours > 0");
                    table.CheckConstraint("ck_starters_feeding_mode", "feeding_mode IN ('regular', 'weekly', 'fridge')");
                    table.CheckConstraint("ck_starters_hydration_percent", "hydration_percent > 0");
                    table.CheckConstraint("ck_starters_rise_hours", "rise_hours > 0");
                    table.ForeignKey(
                        name: "fk_starters_flours_flour_id",
                        column: x => x.flour_id,
                        principalTable: "flours",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_starters_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E6, US-27, US-30. Sourdough starters.");

            migrationBuilder.CreateTable(
                name: "bake_record_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    bake_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bake_record_photos", x => x.id);
                    table.ForeignKey(
                        name: "fk_bake_record_photos_bake_records_bake_record_id",
                        column: x => x.bake_record_id,
                        principalTable: "bake_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E7, US-51. Crumb photos; files live in object storage, the row keeps the key.");

            migrationBuilder.CreateTable(
                name: "ingredient_prices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                    per_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredient_prices", x => x.id);
                    table.CheckConstraint("ck_ingredient_prices_per_quantity", "per_quantity > 0");
                    table.CheckConstraint("ck_ingredient_prices_price", "price >= 0");
                    table.ForeignKey(
                        name: "fk_ingredient_prices_currencies_currency",
                        column: x => x.currency,
                        principalTable: "currencies",
                        principalColumn: "code");
                    table.ForeignKey(
                        name: "fk_ingredient_prices_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E3, US-12, US-15. Price history; the price on date D is the row with the latest effective_from <= D.");

            migrationBuilder.CreateTable(
                name: "starter_feedings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    starter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    flour_g = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: false),
                    water_g = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: false),
                    starter_kept_g = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    temperature_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    next_feeding_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expected_peak_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_starter_feedings", x => x.id);
                    table.CheckConstraint("ck_starter_feedings_flour_g", "flour_g >= 0");
                    table.CheckConstraint("ck_starter_feedings_next_after_fed", "next_feeding_at > fed_at");
                    table.CheckConstraint("ck_starter_feedings_peak_after_fed", "expected_peak_at > fed_at");
                    table.CheckConstraint("ck_starter_feedings_starter_kept_g", "starter_kept_g >= 0");
                    table.CheckConstraint("ck_starter_feedings_water_g", "water_g >= 0");
                    table.ForeignKey(
                        name: "fk_starter_feedings_starters_starter_id",
                        column: x => x.starter_id,
                        principalTable: "starters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E6, US-28. Feeding log of a starter.");

            migrationBuilder.CreateTable(
                name: "bake_record_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    bake_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    recipe_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    loaves = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bake_record_items", x => x.id);
                    table.CheckConstraint("ck_bake_record_items_cost", "cost >= 0");
                    table.CheckConstraint("ck_bake_record_items_loaves", "loaves > 0");
                    table.ForeignKey(
                        name: "fk_bake_record_items_bake_records_bake_record_id",
                        column: x => x.bake_record_id,
                        principalTable: "bake_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E7, US-31. Snapshot of what was baked (recipe title, loaves, cost).");

            migrationBuilder.CreateTable(
                name: "batch_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    loaves = table.Column<int>(type: "integer", nullable: false),
                    starter_amount_g = table.Column<decimal>(type: "numeric(10,1)", precision: 10, scale: 1, nullable: true),
                    kneading_cycles = table.Column<int>(type: "integer", nullable: true),
                    starter_reminder_minutes = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_items", x => x.id);
                    table.CheckConstraint("ck_batch_items_kneading_cycles", "kneading_cycles > 0");
                    table.CheckConstraint("ck_batch_items_loaves", "loaves > 0");
                    table.CheckConstraint("ck_batch_items_starter_amount_g", "starter_amount_g >= 0");
                    table.CheckConstraint("ck_batch_items_starter_reminder_minutes", "starter_reminder_minutes >= 0");
                    table.ForeignKey(
                        name: "fk_batch_items_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_batch_items_starters_starter_id",
                        column: x => x.starter_id,
                        principalTable: "starters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "E5, US-17, US-18. Recipe and number of loaves inside a batch.");

            migrationBuilder.CreateTable(
                name: "batch_stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    batch_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stage_type_code = table.Column<string>(type: "character varying(32)", nullable: false),
                    planned_min_minutes = table.Column<int>(type: "integer", nullable: false),
                    planned_max_minutes = table.Column<int>(type: "integer", nullable: false),
                    planned_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    planned_end_min_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    planned_end_max_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actual_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    actual_end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    extended_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    actual_temperature_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batch_stages", x => x.id);
                    table.CheckConstraint("ck_batch_stages_actual_order", "actual_end_at >= actual_start_at");
                    table.CheckConstraint("ck_batch_stages_done_has_times", "status <> 'done' OR (actual_start_at IS NOT NULL AND actual_end_at IS NOT NULL)");
                    table.CheckConstraint("ck_batch_stages_end_needs_start", "actual_end_at IS NULL OR actual_start_at IS NOT NULL");
                    table.CheckConstraint("ck_batch_stages_extended_minutes", "extended_minutes >= 0");
                    table.CheckConstraint("ck_batch_stages_planned_minutes", "planned_min_minutes >= 0 AND planned_max_minutes >= planned_min_minutes");
                    table.CheckConstraint("ck_batch_stages_planned_order", "planned_end_min_at >= planned_start_at AND planned_end_max_at >= planned_end_min_at");
                    table.CheckConstraint("ck_batch_stages_status", "status IN ('pending', 'running', 'done', 'skipped')");
                    table.CheckConstraint("ck_batch_stages_temperature", "actual_temperature_c BETWEEN -30 AND 350");
                    table.ForeignKey(
                        name: "fk_batch_stages_batch_items_batch_item_id",
                        column: x => x.batch_item_id,
                        principalTable: "batch_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_batch_stages_stage_types_stage_type_code",
                        column: x => x.stage_type_code,
                        principalTable: "stage_types",
                        principalColumn: "code");
                },
                comment: "E5, US-21, US-23, US-24. Stage log with planned and actual times (KPI-1).");

            migrationBuilder.CreateTable(
                name: "reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    starter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    batch_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    channel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reminders", x => x.id);
                    table.CheckConstraint("ck_reminders_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_reminders_channel", "channel IN ('push', 'email')");
                    table.CheckConstraint("ck_reminders_has_target", "num_nonnulls(starter_id, batch_item_id, batch_stage_id) >= 1");
                    table.CheckConstraint("ck_reminders_kind", "kind IN ('starter_feeding', 'before_kneading', 'stage_start')");
                    table.CheckConstraint("ck_reminders_sent_has_time", "status <> 'sent' OR sent_at IS NOT NULL");
                    table.CheckConstraint("ck_reminders_status", "status IN ('scheduled', 'sent', 'cancelled', 'failed')");
                    table.ForeignKey(
                        name: "fk_reminders_batch_items_batch_item_id",
                        column: x => x.batch_item_id,
                        principalTable: "batch_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reminders_batch_stages_batch_stage_id",
                        column: x => x.batch_stage_id,
                        principalTable: "batch_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reminders_starters_starter_id",
                        column: x => x.starter_id,
                        principalTable: "starters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_reminders_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E6, US-29. Scheduled notifications processed by the Notification service.");

            migrationBuilder.CreateTable(
                name: "content_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    reporter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_content_reports", x => x.id);
                    table.CheckConstraint("ck_content_reports_one_target", "num_nonnulls(published_recipe_id, comment_id) = 1");
                    table.CheckConstraint("ck_content_reports_resolution", "(status = 'open' AND resolved_at IS NULL AND resolved_by_id IS NULL) OR (status <> 'open' AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("ck_content_reports_status", "status IN ('open', 'resolved', 'rejected')");
                    table.ForeignKey(
                        name: "fk_content_reports_users_reporter_id",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_content_reports_users_resolved_by_id",
                        column: x => x.resolved_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                },
                comment: "LR-11. Reports on published recipes or comments, handled by an administrator.");

            migrationBuilder.CreateTable(
                name: "published_recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    unpublished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    removed_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_published_recipes", x => x.id);
                    table.CheckConstraint("ck_published_recipes_removed_has_reason", "status <> 'removed' OR removed_reason IS NOT NULL");
                    table.CheckConstraint("ck_published_recipes_status", "status IN ('published', 'unpublished', 'removed')");
                    table.CheckConstraint("ck_published_recipes_unpublished_at", "status <> 'unpublished' OR unpublished_at IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_published_recipes_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E9, US-39, US-40. Immutable published snapshot of a recipe (ingredients and stages in JSON).");

            migrationBuilder.CreateTable(
                name: "recipe_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    published_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    edited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_comments", x => x.id);
                    table.CheckConstraint("ck_recipe_comments_body_not_empty", "length(body) > 0");
                    table.ForeignKey(
                        name: "fk_recipe_comments_published_recipes_published_recipe_id",
                        column: x => x.published_recipe_id,
                        principalTable: "published_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E9, US-41. Comments on published recipes.");

            migrationBuilder.CreateTable(
                name: "recipe_reactions",
                columns: table => new
                {
                    published_recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reaction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_reactions", x => new { x.published_recipe_id, x.user_id });
                    table.CheckConstraint("ck_recipe_reactions_reaction", "reaction IN ('like', 'love', 'want_to_bake')");
                    table.ForeignKey(
                        name: "fk_recipe_reactions_published_recipes_published_recipe_id",
                        column: x => x.published_recipe_id,
                        principalTable: "published_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_reactions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E9, US-41. One reaction per user per published recipe.");

            migrationBuilder.CreateTable(
                name: "recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    copied_from_published_recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    yield_loaves = table.Column<int>(type: "integer", nullable: false),
                    loaf_weight_g = table.Column<decimal>(type: "numeric(8,1)", precision: 8, scale: 1, nullable: true),
                    variant_overrides = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipes", x => x.id);
                    table.CheckConstraint("ck_recipes_loaf_weight_g", "loaf_weight_g > 0");
                    table.CheckConstraint("ck_recipes_not_own_parent", "parent_recipe_id IS NULL OR parent_recipe_id <> id");
                    table.CheckConstraint("ck_recipes_overrides_only_for_variants", "variant_overrides IS NULL OR parent_recipe_id IS NOT NULL");
                    table.CheckConstraint("ck_recipes_title_not_blank", "length(btrim(title)) > 0");
                    table.CheckConstraint("ck_recipes_variant_inherits_scalars", "parent_recipe_id IS NULL OR (description IS NULL AND loaf_weight_g IS NULL AND notes IS NULL)");
                    table.CheckConstraint("ck_recipes_yield_loaves", "yield_loaves > 0");
                    table.ForeignKey(
                        name: "fk_recipes_published_recipes_copied_from_published_recipe_id",
                        column: x => x.copied_from_published_recipe_id,
                        principalTable: "published_recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_recipes_recipes_parent_recipe_id",
                        column: x => x.parent_recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_recipes_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E2, US-05-09. Recipes and variants; a variant stores only its differences from the parent.");

            migrationBuilder.CreateTable(
                name: "recipe_ingredients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    is_optional = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    replaces_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_ingredients", x => x.id);
                    table.CheckConstraint("ck_recipe_ingredients_quantity", "quantity >= 0");
                    table.CheckConstraint("ck_recipe_ingredients_removed_needs_target", "NOT is_removed OR replaces_item_id IS NOT NULL");
                    table.CheckConstraint("ck_recipe_ingredients_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "fk_recipe_ingredients_ingredients_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_recipe_ingredients_recipe_ingredients_replaces_item_id",
                        column: x => x.replaces_item_id,
                        principalTable: "recipe_ingredients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_ingredients_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "E2. Ingredient lines of a recipe; in a variant, lines replace (replaces_item_id), remove (is_removed) or add parent lines.");

            migrationBuilder.CreateTable(
                name: "recipe_stages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stage_type_code = table.Column<string>(type: "character varying(32)", nullable: false),
                    min_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    active_minutes = table.Column<int>(type: "integer", nullable: false),
                    proof_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    temperature_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    uses_oven = table.Column<bool>(type: "boolean", nullable: false),
                    uses_mixer = table.Column<bool>(type: "boolean", nullable: false),
                    is_optional = table.Column<bool>(type: "boolean", nullable: false),
                    instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    replaces_stage_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_removed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_stages", x => x.id);
                    table.CheckConstraint("ck_recipe_stages_active_minutes", "active_minutes >= 0");
                    table.CheckConstraint("ck_recipe_stages_active_within_max", "active_minutes <= max_duration_minutes");
                    table.CheckConstraint("ck_recipe_stages_max_duration", "max_duration_minutes >= min_duration_minutes");
                    table.CheckConstraint("ck_recipe_stages_min_duration", "min_duration_minutes >= 0");
                    table.CheckConstraint("ck_recipe_stages_name_not_blank", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_recipe_stages_proof_mode", "proof_mode IN ('warm', 'cold')");
                    table.CheckConstraint("ck_recipe_stages_removed_needs_target", "NOT is_removed OR replaces_stage_id IS NOT NULL");
                    table.CheckConstraint("ck_recipe_stages_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_recipe_stages_temperature_c", "temperature_c BETWEEN -30 AND 350");
                    table.ForeignKey(
                        name: "fk_recipe_stages_recipe_stages_replaces_stage_id",
                        column: x => x.replaces_stage_id,
                        principalTable: "recipe_stages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_stages_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_stages_stage_types_stage_type_code",
                        column: x => x.stage_type_code,
                        principalTable: "stage_types",
                        principalColumn: "code");
                },
                comment: "E2, US-05, US-09, US-16. Ordered stages with a min-max duration.");

            migrationBuilder.InsertData(
                table: "currencies",
                columns: new[] { "code", "minor_units", "name" },
                values: new object[,]
                {
                    { "EUR", (short)2, "Euro" },
                    { "GBP", (short)2, "Pound sterling" },
                    { "PLN", (short)2, "Polish zloty" },
                    { "USD", (short)2, "US dollar" }
                });

            migrationBuilder.InsertData(
                table: "flour_types",
                columns: new[] { "code", "name", "sort_order" },
                values: new object[,]
                {
                    { "barley", "Barley", 5 },
                    { "corn", "Corn", 7 },
                    { "oat", "Oat", 6 },
                    { "other", "Other", 8 },
                    { "rye", "Rye", 2 },
                    { "spelt", "Spelt", 3 },
                    { "wheat", "Wheat", 1 },
                    { "whole", "Whole grain (mixed)", 4 }
                });

            migrationBuilder.InsertData(
                table: "ingredient_categories",
                columns: new[] { "code", "is_flour", "is_liquid", "name", "sort_order" },
                values: new object[,]
                {
                    { "add_in", false, false, "Add-in (seeds, nuts, fruit)", 9 },
                    { "dairy", false, true, "Dairy", 8 },
                    { "fat", false, false, "Fat", 7 },
                    { "flour", true, false, "Flour", 1 },
                    { "other", false, false, "Other", 11 },
                    { "packaging", false, false, "Packaging", 10 },
                    { "salt", false, false, "Salt", 3 },
                    { "starter", false, false, "Starter", 4 },
                    { "sweetener", false, false, "Sweetener", 6 },
                    { "water", false, true, "Water", 2 },
                    { "yeast", false, false, "Yeast", 5 }
                });

            migrationBuilder.InsertData(
                table: "stage_types",
                columns: new[] { "code", "default_uses_mixer", "default_uses_oven", "is_fermentation", "name", "sort_order" },
                values: new object[,]
                {
                    { "autolyse", false, false, false, "Autolyse", 1 },
                    { "bake", false, true, false, "Baking", 8 },
                    { "bulk_fermentation", false, false, true, "Bulk fermentation", 3 },
                    { "cool", false, false, false, "Cooling", 9 },
                    { "fold", false, false, true, "Stretch and fold", 4 },
                    { "mix", true, false, false, "Mixing", 2 },
                    { "other", false, false, false, "Other", 10 },
                    { "preshape", false, false, false, "Pre-shaping", 5 },
                    { "proof", false, false, true, "Proofing", 7 },
                    { "shape", false, false, false, "Shaping", 6 }
                });

            migrationBuilder.InsertData(
                table: "units",
                columns: new[] { "code", "dimension", "factor_to_base", "name", "sort_order" },
                values: new object[,]
                {
                    { "g", "mass", 1m, "gram", 1 },
                    { "kg", "mass", 1000m, "kilogram", 2 },
                    { "l", "volume", 1000m, "litre", 4 },
                    { "ml", "volume", 1m, "millilitre", 3 },
                    { "pcs", "count", 1m, "piece", 5 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_bake_record_items_bake_record_id",
                table: "bake_record_items",
                column: "bake_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_bake_record_items_recipe_id",
                table: "bake_record_items",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_bake_record_photos_bake_record_id",
                table: "bake_record_photos",
                column: "bake_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_bake_records_currency",
                table: "bake_records",
                column: "currency");

            migrationBuilder.CreateIndex(
                name: "ix_bake_records_owner_id_baked_on",
                table: "bake_records",
                columns: new[] { "owner_id", "baked_on" });

            migrationBuilder.CreateIndex(
                name: "ux_bake_records_batch_id",
                table: "bake_records",
                column: "batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_batch_items_batch_id_sort_order",
                table: "batch_items",
                columns: new[] { "batch_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_batch_items_recipe_id",
                table: "batch_items",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_items_starter_id",
                table: "batch_items",
                column: "starter_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_stages_batch_item_id_sort_order",
                table: "batch_stages",
                columns: new[] { "batch_item_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_batch_stages_recipe_stage_id",
                table: "batch_stages",
                column: "recipe_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_batch_stages_stage_type_code",
                table: "batch_stages",
                column: "stage_type_code");

            migrationBuilder.CreateIndex(
                name: "ix_batches_currency",
                table: "batches",
                column: "currency");

            migrationBuilder.CreateIndex(
                name: "ix_batches_owner_id_planned_start_at",
                table: "batches",
                columns: new[] { "owner_id", "planned_start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_batches_owner_id_status",
                table: "batches",
                columns: new[] { "owner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_conversations_owner_id_updated_at",
                table: "chat_conversations",
                columns: new[] { "owner_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_conversation_id_created_at",
                table: "chat_messages",
                columns: new[] { "conversation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_comment_id",
                table: "content_reports",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_published_recipe_id",
                table: "content_reports",
                column: "published_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_reporter_id",
                table: "content_reports",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_resolved_by_id",
                table: "content_reports",
                column: "resolved_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_status",
                table: "content_reports",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_currencies_name",
                table: "currencies",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_flour_types_name",
                table: "flour_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_flours_flour_type_code",
                table: "flours",
                column: "flour_type_code");

            migrationBuilder.CreateIndex(
                name: "ux_flours_owner_id_name",
                table: "flours",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ingredient_categories_name",
                table: "ingredient_categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_prices_currency",
                table: "ingredient_prices",
                column: "currency");

            migrationBuilder.CreateIndex(
                name: "ux_ingredient_prices_ingredient_id_effective_from",
                table: "ingredient_prices",
                columns: new[] { "ingredient_id", "effective_from" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingredients_category_code",
                table: "ingredients",
                column: "category_code");

            migrationBuilder.CreateIndex(
                name: "ix_ingredients_flour_id",
                table: "ingredients",
                column: "flour_id");

            migrationBuilder.CreateIndex(
                name: "ix_ingredients_unit_code",
                table: "ingredients",
                column: "unit_code");

            migrationBuilder.CreateIndex(
                name: "ux_ingredients_owner_id_name",
                table: "ingredients",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kitchen_settings_currency",
                table: "kitchen_settings",
                column: "currency");

            migrationBuilder.CreateIndex(
                name: "ix_published_recipes_author_id",
                table: "published_recipes",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_recipes_source_recipe_id",
                table: "published_recipes",
                column: "source_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_recipes_status_published_at",
                table: "published_recipes",
                columns: new[] { "status", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_published_recipes_title",
                table: "published_recipes",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_user_id",
                table: "push_subscriptions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_push_subscriptions_endpoint",
                table: "push_subscriptions",
                column: "endpoint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recipe_comments_author_id",
                table: "recipe_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_comments_published_recipe_id_created_at",
                table: "recipe_comments",
                columns: new[] { "published_recipe_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_ingredient_id",
                table: "recipe_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_recipe_id_sort_order",
                table: "recipe_ingredients",
                columns: new[] { "recipe_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_replaces_item_id",
                table: "recipe_ingredients",
                column: "replaces_item_id");

            migrationBuilder.CreateIndex(
                name: "ux_recipe_ingredients_recipe_id_replaces_item_id",
                table: "recipe_ingredients",
                columns: new[] { "recipe_id", "replaces_item_id" },
                unique: true,
                filter: "replaces_item_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_reactions_user_id",
                table: "recipe_reactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_stages_recipe_id_sort_order",
                table: "recipe_stages",
                columns: new[] { "recipe_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_stages_replaces_stage_id",
                table: "recipe_stages",
                column: "replaces_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_stages_stage_type_code",
                table: "recipe_stages",
                column: "stage_type_code");

            migrationBuilder.CreateIndex(
                name: "ux_recipe_stages_recipe_id_replaces_stage_id",
                table: "recipe_stages",
                columns: new[] { "recipe_id", "replaces_stage_id" },
                unique: true,
                filter: "replaces_stage_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_copied_from_published_recipe_id",
                table: "recipes",
                column: "copied_from_published_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_owner_id_title",
                table: "recipes",
                columns: new[] { "owner_id", "title" });

            migrationBuilder.CreateIndex(
                name: "ix_recipes_parent_recipe_id",
                table: "recipes",
                column: "parent_recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_batch_item_id",
                table: "reminders",
                column: "batch_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_batch_stage_id",
                table: "reminders",
                column: "batch_stage_id");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_owner_id",
                table: "reminders",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_starter_id",
                table: "reminders",
                column: "starter_id");

            migrationBuilder.CreateIndex(
                name: "ix_reminders_status_due_at",
                table: "reminders",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ux_reminders_starter_id_scheduled_feeding",
                table: "reminders",
                column: "starter_id",
                unique: true,
                filter: "kind = 'starter_feeding' AND status = 'scheduled'");

            migrationBuilder.CreateIndex(
                name: "ux_stage_types_name",
                table: "stage_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_starter_feedings_starter_id_fed_at",
                table: "starter_feedings",
                columns: new[] { "starter_id", "fed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_starters_flour_id",
                table: "starters",
                column: "flour_id");

            migrationBuilder.CreateIndex(
                name: "ux_starters_owner_id_name",
                table: "starters",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_units_name",
                table: "units",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_normalized_email",
                table: "users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_bake_record_items_recipes_recipe_id",
                table: "bake_record_items",
                column: "recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_batch_items_recipes_recipe_id",
                table: "batch_items",
                column: "recipe_id",
                principalTable: "recipes",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_batch_stages_recipe_stages_recipe_stage_id",
                table: "batch_stages",
                column: "recipe_stage_id",
                principalTable: "recipe_stages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_content_reports_published_recipes_published_recipe_id",
                table: "content_reports",
                column: "published_recipe_id",
                principalTable: "published_recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_content_reports_recipe_comments_comment_id",
                table: "content_reports",
                column: "comment_id",
                principalTable: "recipe_comments",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_published_recipes_recipes_source_recipe_id",
                table: "published_recipes",
                column: "source_recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_published_recipes_recipes_source_recipe_id",
                table: "published_recipes");

            migrationBuilder.DropTable(
                name: "bake_record_items");

            migrationBuilder.DropTable(
                name: "bake_record_photos");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "content_reports");

            migrationBuilder.DropTable(
                name: "ingredient_prices");

            migrationBuilder.DropTable(
                name: "kitchen_settings");

            migrationBuilder.DropTable(
                name: "push_subscriptions");

            migrationBuilder.DropTable(
                name: "recipe_ingredients");

            migrationBuilder.DropTable(
                name: "recipe_reactions");

            migrationBuilder.DropTable(
                name: "reminders");

            migrationBuilder.DropTable(
                name: "starter_feedings");

            migrationBuilder.DropTable(
                name: "bake_records");

            migrationBuilder.DropTable(
                name: "chat_conversations");

            migrationBuilder.DropTable(
                name: "recipe_comments");

            migrationBuilder.DropTable(
                name: "ingredients");

            migrationBuilder.DropTable(
                name: "batch_stages");

            migrationBuilder.DropTable(
                name: "ingredient_categories");

            migrationBuilder.DropTable(
                name: "units");

            migrationBuilder.DropTable(
                name: "batch_items");

            migrationBuilder.DropTable(
                name: "recipe_stages");

            migrationBuilder.DropTable(
                name: "batches");

            migrationBuilder.DropTable(
                name: "starters");

            migrationBuilder.DropTable(
                name: "stage_types");

            migrationBuilder.DropTable(
                name: "currencies");

            migrationBuilder.DropTable(
                name: "flours");

            migrationBuilder.DropTable(
                name: "flour_types");

            migrationBuilder.DropTable(
                name: "recipes");

            migrationBuilder.DropTable(
                name: "published_recipes");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
