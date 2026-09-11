using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustSimBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKycFieldsToIdentityVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_escrows_auction_deposits_auction_deposit_id1",
                table: "escrows");

            migrationBuilder.DropIndex(
                name: "ix_escrows_auction_deposit_id",
                table: "escrows");

            migrationBuilder.DropIndex(
                name: "ix_escrows_auction_deposit_id1",
                table: "escrows");

            migrationBuilder.DropColumn(
                name: "auction_deposit_id1",
                table: "escrows");

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "wallet_transactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_transaction_id",
                table: "wallet_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_code_url",
                table: "wallet_transactions",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transaction_code",
                table: "wallet_transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "back_id_card_url",
                table: "identity_verifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "face_selfie_url",
                table: "identity_verifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<float>(
                name: "face_similarity_score",
                table: "identity_verifications",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "front_id_card_url",
                table: "identity_verifications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ocr_raw_data",
                table: "identity_verifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "verification_method",
                table: "identity_verifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "AUTOMATED_AWS_REKOGNITION");

            migrationBuilder.AddColumn<Guid>(
                name: "verified_by_user_id",
                table: "identity_verifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    jwt_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by_ip = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    replaced_by_token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    reason_revoked = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalTable: "user_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_transaction_code",
                table: "wallet_transactions",
                column: "transaction_code");

            migrationBuilder.CreateIndex(
                name: "ix_wallet_transactions_wallet_transaction_type",
                table: "wallet_transactions",
                column: "wallet_transaction_type");

            migrationBuilder.CreateIndex(
                name: "ix_identity_verifications_verification_status",
                table: "identity_verifications",
                column: "verification_status");

            migrationBuilder.CreateIndex(
                name: "ix_identity_verifications_verified_by_user_id",
                table: "identity_verifications",
                column: "verified_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_escrows_auction_deposit_id",
                table: "escrows",
                column: "auction_deposit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_account_id",
                table: "refresh_tokens",
                column: "user_account_id");

            migrationBuilder.AddForeignKey(
                name: "fk_identity_verifications_user_accounts_verified_by_user_id",
                table: "identity_verifications",
                column: "verified_by_user_id",
                principalTable: "user_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_identity_verifications_user_accounts_verified_by_user_id",
                table: "identity_verifications");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "ix_wallet_transactions_transaction_code",
                table: "wallet_transactions");

            migrationBuilder.DropIndex(
                name: "ix_wallet_transactions_wallet_transaction_type",
                table: "wallet_transactions");

            migrationBuilder.DropIndex(
                name: "ix_identity_verifications_verification_status",
                table: "identity_verifications");

            migrationBuilder.DropIndex(
                name: "ix_identity_verifications_verified_by_user_id",
                table: "identity_verifications");

            migrationBuilder.DropIndex(
                name: "ix_escrows_auction_deposit_id",
                table: "escrows");

            migrationBuilder.DropColumn(
                name: "description",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "external_transaction_id",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "qr_code_url",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "transaction_code",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "back_id_card_url",
                table: "identity_verifications");

            migrationBuilder.DropColumn(
                name: "face_selfie_url",
                table: "identity_verifications");

            migrationBuilder.DropColumn(
                name: "face_similarity_score",
                table: "identity_verifications");

            migrationBuilder.DropColumn(
                name: "front_id_card_url",
                table: "identity_verifications");

            migrationBuilder.DropColumn(
                name: "ocr_raw_data",
                table: "identity_verifications");

            migrationBuilder.DropColumn(
                name: "verification_method",
                table: "identity_verifications");

            migrationBuilder.DropColumn(
                name: "verified_by_user_id",
                table: "identity_verifications");

            migrationBuilder.AddColumn<Guid>(
                name: "auction_deposit_id1",
                table: "escrows",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_escrows_auction_deposit_id",
                table: "escrows",
                column: "auction_deposit_id");

            migrationBuilder.CreateIndex(
                name: "ix_escrows_auction_deposit_id1",
                table: "escrows",
                column: "auction_deposit_id1",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_escrows_auction_deposits_auction_deposit_id1",
                table: "escrows",
                column: "auction_deposit_id1",
                principalTable: "auction_deposits",
                principalColumn: "id");
        }
    }
}
