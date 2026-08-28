using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSimBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    link_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "format_rhythms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    format_rhythm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    format_rhythm_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_format_rhythms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sim_carriers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_carrier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carrier_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    carrier_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    logo_img = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_carriers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sim_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    valuation_multiplier = table.Column<float>(type: "real", nullable: false, defaultValue: 1f),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    pass_word_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "phone_prefixes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_prefix_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prefix_value = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    sim_carrier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phone_prefixes", x => x.id);
                    table.ForeignKey(
                        name: "fk_phone_prefixes_sim_carriers_sim_carrier_id",
                        column: x => x.sim_carrier_id,
                        principalTable: "sim_carriers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_log_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    new_data = table.Column<string>(type: "text", nullable: false),
                    old_data = table.Column<string>(type: "text", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_user_accounts_actor_id",
                        column: x => x.actor_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    action_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_notifications_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    email = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    identity_number = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    avatar_img = table.Column<string>(type: "text", nullable: false),
                    verification_status = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_profiles_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_security_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_security_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    public_key = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    encrypted_private_key = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    key_hash = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    key_generated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_security_profile_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_security_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_security_profiles_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    available_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    locked_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    wallet_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    lock_version = table.Column<int>(type: "integer", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallets", x => x.id);
                    table.ForeignKey(
                        name: "fk_wallets_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sim_numbers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_number_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    sim_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    phone_prefix_id = table.Column<Guid>(type: "uuid", nullable: false),
                    format_rhythm_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_numbers", x => x.id);
                    table.ForeignKey(
                        name: "fk_sim_numbers_format_rhythms_format_rhythm_id",
                        column: x => x.format_rhythm_id,
                        principalTable: "format_rhythms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sim_numbers_phone_prefixes_phone_prefix_id",
                        column: x => x.phone_prefix_id,
                        principalTable: "phone_prefixes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "identity_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_verification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verification_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_identity_verifications_user_profiles_user_profile_id",
                        column: x => x.user_profile_id,
                        principalTable: "user_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wallet_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    wallet_transaction_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    destination_bank_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destination_bank_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    destination_account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destination_account_number_masked = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    destination_account_holder_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    balance_before = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallet_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_wallet_transactions_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auctions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    auction_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    auction_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    starting_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    current_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    reserve_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lock_version = table.Column<int>(type: "integer", nullable: false),
                    registration_start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    registration_end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_number_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auctions", x => x.id);
                    table.ForeignKey(
                        name: "fk_auctions_sim_numbers_sim_number_id",
                        column: x => x.sim_number_id,
                        principalTable: "sim_numbers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_auctions_user_accounts_author_id",
                        column: x => x.author_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sim_category_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_category_mapping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score_contribution = table.Column<float>(type: "real", nullable: false, defaultValue: 1f),
                    sim_number_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_category_mappings", x => x.id);
                    table.ForeignKey(
                        name: "fk_sim_category_mappings_sim_categories_sim_category_id",
                        column: x => x.sim_category_id,
                        principalTable: "sim_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sim_category_mappings_sim_numbers_sim_number_id",
                        column: x => x.sim_number_id,
                        principalTable: "sim_numbers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sim_ownerships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_ownership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ownership_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    acquistion_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_number_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_ownerships", x => x.id);
                    table.ForeignKey(
                        name: "fk_sim_ownerships_sim_numbers_sim_number_id",
                        column: x => x.sim_number_id,
                        principalTable: "sim_numbers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sim_ownerships_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auction_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    accepted_bid_count = table.Column<int>(type: "integer", nullable: false),
                    rejected_bid_count = table.Column<int>(type: "integer", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reject_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    blocked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    blocked_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_participants_auctions_auction_id",
                        column: x => x.auction_id,
                        principalTable: "auctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_auction_participants_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auction_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minimum_bid_increment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    maximum_bid_count_per_participant = table.Column<int>(type: "integer", nullable: false),
                    maximum_bid_count_per_minute = table.Column<int>(type: "integer", nullable: false),
                    minimum_bid_interval_seconds = table.Column<float>(type: "real", nullable: false),
                    extension_threshold_seconds = table.Column<float>(type: "real", nullable: false),
                    extension_duration_seconds = table.Column<float>(type: "real", nullable: false),
                    maximum_extension_count = table.Column<int>(type: "integer", nullable: false),
                    dutch_decrease_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    dutch_decrease_interval_seconds = table.Column<float>(type: "real", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_rules_auctions_auction_id",
                        column: x => x.auction_id,
                        principalTable: "auctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "trade_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_transaction_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    trade_transaction_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    escrow_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    signing_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    transfer_deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    buyer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seller_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_number_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trade_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_trade_transactions_auctions_auction_id",
                        column: x => x.auction_id,
                        principalTable: "auctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_transactions_sim_numbers_sim_number_id",
                        column: x => x.sim_number_id,
                        principalTable: "sim_numbers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_transactions_user_accounts_buyer_id",
                        column: x => x.buyer_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_trade_transactions_user_accounts_seller_id",
                        column: x => x.seller_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sim_verifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_verification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verification_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sim_ownership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_verifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_sim_verifications_sim_ownerships_sim_ownership_id",
                        column: x => x.sim_ownership_id,
                        principalTable: "sim_ownerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "auction_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    winner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    final_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    auction_result_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    determined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_results", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_results_auction_participants_winner_id",
                        column: x => x.winner_id,
                        principalTable: "auction_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_auction_results_auctions_auction_id",
                        column: x => x.auction_id,
                        principalTable: "auctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bids",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bid_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bid_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    placed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rejected_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bids", x => x.id);
                    table.ForeignKey(
                        name: "fk_bids_auction_participants_participant_id",
                        column: x => x.participant_id,
                        principalTable: "auction_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_bids_auctions_auction_id",
                        column: x => x.auction_id,
                        principalTable: "auctions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "fraud_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fraud_assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_score = table.Column<float>(type: "real", nullable: false),
                    risk_level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    fraud_signal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    assessment_decision = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assessment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fraud_assessments", x => x.id);
                    table.ForeignKey(
                        name: "fk_fraud_assessments_auction_participants_participant_id",
                        column: x => x.participant_id,
                        principalTable: "auction_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispute_tickets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trade_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    complainant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    resolved_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admin_resolution_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispute_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_dispute_tickets_trade_transactions_trade_transaction_id",
                        column: x => x.trade_transaction_id,
                        principalTable: "trade_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispute_tickets_user_accounts_complainant_id",
                        column: x => x.complainant_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_dispute_tickets_user_accounts_resolved_by_admin_id",
                        column: x => x.resolved_by_admin_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sim_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sim_transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sim_transfer_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cancelled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    trade_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sim_transfers", x => x.id);
                    table.ForeignKey(
                        name: "fk_sim_transfers_trade_transactions_trade_transaction_id",
                        column: x => x.trade_transaction_id,
                        principalTable: "trade_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfer_contracts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_contract_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    document_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    blockchain_tx_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    contract_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    trade_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_contracts", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfer_contracts_trade_transactions_trade_transaction_id",
                        column: x => x.trade_transaction_id,
                        principalTable: "trade_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispute_proofs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispute_proof_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dispute_ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dispute_proofs", x => x.id);
                    table.ForeignKey(
                        name: "fk_dispute_proofs_dispute_tickets_dispute_ticket_id",
                        column: x => x.dispute_ticket_id,
                        principalTable: "dispute_tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transfer_proofs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transfer_proof_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    proof_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    verified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sim_transfer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transfer_proofs", x => x.id);
                    table.ForeignKey(
                        name: "fk_transfer_proofs_sim_transfers_sim_transfer_id",
                        column: x => x.sim_transfer_id,
                        principalTable: "sim_transfers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "digital_signatures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    digital_signature_id = table.Column<Guid>(type: "uuid", nullable: false),
                    signature_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    public_key = table.Column<string>(type: "text", nullable: false),
                    cryptographic_signature = table.Column<string>(type: "text", nullable: false),
                    signed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    contrac_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_digital_signatures", x => x.id);
                    table.ForeignKey(
                        name: "fk_digital_signatures_transfer_contracts_contrac_id",
                        column: x => x.contrac_id,
                        principalTable: "transfer_contracts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_digital_signatures_user_accounts_user_id",
                        column: x => x.user_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auction_deposits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_deposit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deposit_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    deposit_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    deposit_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    appliedto_escrowed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    forfeited_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    forfeited_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auction_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_deposits", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_deposits_auction_participants_participant_id",
                        column: x => x.participant_id,
                        principalTable: "auction_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "escrows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    escrow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escrow_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    escrow_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    held_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    released_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    refunded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    frozen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    frozen_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    auction_deposit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    trade_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_deposit_id1 = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_escrows", x => x.id);
                    table.ForeignKey(
                        name: "fk_escrows_auction_deposits_auction_deposit_id",
                        column: x => x.auction_deposit_id,
                        principalTable: "auction_deposits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_escrows_auction_deposits_auction_deposit_id1",
                        column: x => x.auction_deposit_id1,
                        principalTable: "auction_deposits",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_escrows_trade_transactions_trade_transaction_id",
                        column: x => x.trade_transaction_id,
                        principalTable: "trade_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "auction_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    auction_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    auction_transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expired_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    balance_before = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    escrow_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auction_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_auction_transactions_escrows_escrow_id",
                        column: x => x.escrow_id,
                        principalTable: "escrows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_auction_transactions_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "wallets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auction_deposits_auction_transaction_id",
                table: "auction_deposits",
                column: "auction_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_deposits_deposit_code",
                table: "auction_deposits",
                column: "deposit_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_deposits_deposit_status",
                table: "auction_deposits",
                column: "deposit_status");

            migrationBuilder.CreateIndex(
                name: "ix_auction_deposits_participant_id",
                table: "auction_deposits",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_participants_auction_id_user_account_id",
                table: "auction_participants",
                columns: new[] { "auction_id", "user_account_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_participants_status",
                table: "auction_participants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_auction_participants_user_account_id",
                table: "auction_participants",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_results_auction_id",
                table: "auction_results",
                column: "auction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_results_winner_id",
                table: "auction_results",
                column: "winner_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_rules_auction_id",
                table: "auction_rules",
                column: "auction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auction_transactions_escrow_id",
                table: "auction_transactions",
                column: "escrow_id");

            migrationBuilder.CreateIndex(
                name: "ix_auction_transactions_transaction_status",
                table: "auction_transactions",
                column: "transaction_status");

            migrationBuilder.CreateIndex(
                name: "ix_auction_transactions_wallet_id",
                table: "auction_transactions",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ix_auctions_auction_code",
                table: "auctions",
                column: "auction_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_auctions_auction_status",
                table: "auctions",
                column: "auction_status");

            migrationBuilder.CreateIndex(
                name: "ix_auctions_author_id",
                table: "auctions",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_auctions_end_at",
                table: "auctions",
                column: "end_at");

            migrationBuilder.CreateIndex(
                name: "ix_auctions_sim_number_id",
                table: "auctions",
                column: "sim_number_id");

            migrationBuilder.CreateIndex(
                name: "ix_auctions_start_at",
                table: "auctions",
                column: "start_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_id",
                table: "audit_logs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_created_date",
                table: "audit_logs",
                column: "created_date");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_name_entity_id",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bids_auction_id",
                table: "bids",
                column: "auction_id");

            migrationBuilder.CreateIndex(
                name: "ix_bids_participant_id",
                table: "bids",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_bids_placed_at",
                table: "bids",
                column: "placed_at");

            migrationBuilder.CreateIndex(
                name: "ix_bids_status",
                table: "bids",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_digital_signatures_contrac_id",
                table: "digital_signatures",
                column: "contrac_id");

            migrationBuilder.CreateIndex(
                name: "ix_digital_signatures_user_id",
                table: "digital_signatures",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_proofs_dispute_ticket_id",
                table: "dispute_proofs",
                column: "dispute_ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_tickets_complainant_id",
                table: "dispute_tickets",
                column: "complainant_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_tickets_resolved_by_admin_id",
                table: "dispute_tickets",
                column: "resolved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_tickets_status",
                table: "dispute_tickets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_dispute_tickets_trade_transaction_id",
                table: "dispute_tickets",
                column: "trade_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_document_name",
                table: "documents",
                column: "document_name");

            migrationBuilder.CreateIndex(
                name: "ix_escrows_auction_deposit_id",
                table: "escrows",
                column: "auction_deposit_id");

            migrationBuilder.CreateIndex(
                name: "ix_escrows_auction_deposit_id1",
                table: "escrows",
                column: "auction_deposit_id1",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_escrows_escrow_status",
                table: "escrows",
                column: "escrow_status");

            migrationBuilder.CreateIndex(
                name: "ix_escrows_trade_transaction_id",
                table: "escrows",
                column: "trade_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_format_rhythms_format_rhythm_name",
                table: "format_rhythms",
                column: "format_rhythm_name");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_assessment_status",
                table: "fraud_assessments",
                column: "assessment_status");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_participant_id",
                table: "fraud_assessments",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fraud_assessments_risk_level",
                table: "fraud_assessments",
                column: "risk_level");

            migrationBuilder.CreateIndex(
                name: "ix_identity_verifications_user_profile_id",
                table: "identity_verifications",
                column: "user_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_is_read",
                table: "notifications",
                column: "is_read");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_account_id",
                table: "notifications",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_phone_prefixes_prefix_value",
                table: "phone_prefixes",
                column: "prefix_value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_phone_prefixes_sim_carrier_id",
                table: "phone_prefixes",
                column: "sim_carrier_id");

            migrationBuilder.CreateIndex(
                name: "ix_sim_carriers_carrier_code",
                table: "sim_carriers",
                column: "carrier_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sim_categories_category_code",
                table: "sim_categories",
                column: "category_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sim_category_mappings_sim_category_id",
                table: "sim_category_mappings",
                column: "sim_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_sim_category_mappings_sim_number_id_sim_category_id",
                table: "sim_category_mappings",
                columns: new[] { "sim_number_id", "sim_category_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sim_numbers_format_rhythm_id",
                table: "sim_numbers",
                column: "format_rhythm_id");

            migrationBuilder.CreateIndex(
                name: "ix_sim_numbers_phone_number",
                table: "sim_numbers",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sim_numbers_phone_prefix_id",
                table: "sim_numbers",
                column: "phone_prefix_id");

            migrationBuilder.CreateIndex(
                name: "ix_sim_numbers_sim_status",
                table: "sim_numbers",
                column: "sim_status");

            migrationBuilder.CreateIndex(
                name: "ix_sim_ownerships_ownership_status",
                table: "sim_ownerships",
                column: "ownership_status");

            migrationBuilder.CreateIndex(
                name: "ix_sim_ownerships_sim_number_id",
                table: "sim_ownerships",
                column: "sim_number_id");

            migrationBuilder.CreateIndex(
                name: "ix_sim_ownerships_user_account_id",
                table: "sim_ownerships",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "ix_sim_transfers_sim_transfer_status",
                table: "sim_transfers",
                column: "sim_transfer_status");

            migrationBuilder.CreateIndex(
                name: "ix_sim_transfers_trade_transaction_id",
                table: "sim_transfers",
                column: "trade_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sim_verifications_sim_ownership_id",
                table: "sim_verifications",
                column: "sim_ownership_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_transactions_auction_id",
                table: "trade_transactions",
                column: "auction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_transactions_buyer_id",
                table: "trade_transactions",
                column: "buyer_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_transactions_seller_id",
                table: "trade_transactions",
                column: "seller_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_transactions_sim_number_id",
                table: "trade_transactions",
                column: "sim_number_id");

            migrationBuilder.CreateIndex(
                name: "ix_trade_transactions_trade_transaction_code",
                table: "trade_transactions",
                column: "trade_transaction_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trade_transactions_trade_transaction_status",
                table: "trade_transactions",
                column: "trade_transaction_status");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_contracts_contract_status",
                table: "transfer_contracts",
                column: "contract_status");

            migrationBuilder.CreateIndex(
                name: "ix_transfer_contracts_trade_transaction_id",
                table: "transfer_contracts",
                column: "trade_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transfer_proofs_sim_transfer_id",
                table: "transfer_proofs",
                column: "sim_transfer_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_user_email",
                table: "user_accounts",
                column: "user_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_accounts_user_name",
                table: "user_accounts",
                column: "user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_identity_number",
                table: "user_profiles",
                column: "identity_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_phone_number",
                table: "user_profiles",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_profiles_user_account_id",
                table: "user_profiles",
                column: "user_account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_security_profiles_user_account_id",
                table: "user_security_profiles",
                column: "user_account_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_security_profiles_user_account_id_user_security_profil",
                table: "user_security_profiles",
                columns: new[] { "user_account_id", "user_security_profile_status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_transaction_status",
                table: "wallet_transactions",
                column: "transaction_status");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_wallet_id",
                table: "wallet_transactions",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "ix_wallets_user_account_id",
                table: "wallets",
                column: "user_account_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_auction_deposits_auction_transactions_auction_transaction_id",
                table: "auction_deposits",
                column: "auction_transaction_id",
                principalTable: "auction_transactions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_auction_deposits_auction_participants_participant_id",
                table: "auction_deposits");

            migrationBuilder.DropForeignKey(
                name: "fk_auction_deposits_auction_transactions_auction_transaction_id",
                table: "auction_deposits");

            migrationBuilder.DropTable(
                name: "auction_results");

            migrationBuilder.DropTable(
                name: "auction_rules");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "bids");

            migrationBuilder.DropTable(
                name: "digital_signatures");

            migrationBuilder.DropTable(
                name: "dispute_proofs");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "fraud_assessments");

            migrationBuilder.DropTable(
                name: "identity_verifications");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "sim_category_mappings");

            migrationBuilder.DropTable(
                name: "sim_verifications");

            migrationBuilder.DropTable(
                name: "transfer_proofs");

            migrationBuilder.DropTable(
                name: "user_security_profiles");

            migrationBuilder.DropTable(
                name: "wallet_transactions");

            migrationBuilder.DropTable(
                name: "transfer_contracts");

            migrationBuilder.DropTable(
                name: "dispute_tickets");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "sim_categories");

            migrationBuilder.DropTable(
                name: "sim_ownerships");

            migrationBuilder.DropTable(
                name: "sim_transfers");

            migrationBuilder.DropTable(
                name: "auction_participants");

            migrationBuilder.DropTable(
                name: "auction_transactions");

            migrationBuilder.DropTable(
                name: "escrows");

            migrationBuilder.DropTable(
                name: "wallets");

            migrationBuilder.DropTable(
                name: "auction_deposits");

            migrationBuilder.DropTable(
                name: "trade_transactions");

            migrationBuilder.DropTable(
                name: "auctions");

            migrationBuilder.DropTable(
                name: "sim_numbers");

            migrationBuilder.DropTable(
                name: "user_accounts");

            migrationBuilder.DropTable(
                name: "format_rhythms");

            migrationBuilder.DropTable(
                name: "phone_prefixes");

            migrationBuilder.DropTable(
                name: "sim_carriers");
        }
    }
}
