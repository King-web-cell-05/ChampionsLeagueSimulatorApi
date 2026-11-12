
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChampionsLeagueSimulatorApi.Migrations
{
    public partial class AddUserOwnershipAndDefaultClubs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==========================================
            // 1. VALIDATE EXISTING DATA
            // ==========================================

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    -- Existing local data must match the original, verified dataset.
                    -- Fresh production databases may have no teams or competitions.
                    IF EXISTS (SELECT 1 FROM "Teams")
                       OR EXISTS (SELECT 1 FROM "Competitions") THEN
                    IF (SELECT COUNT(*) FROM "Teams") <> 37 THEN
                        RAISE EXCEPTION
                            'Expected exactly 37 existing clubs.';
                    END IF;

                    IF (SELECT COUNT(*) FROM "Competitions") <> 1 THEN
                        RAISE EXCEPTION
                            'Expected exactly 1 existing competition.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM "Teams"
                        WHERE "Id" =
                            '0159a80d-54c0-42ad-9d2d-7127e2baab6a'::uuid
                          AND LOWER(TRIM("Name")) = 'chelsea'
                    ) THEN
                        RAISE EXCEPTION
                            'Expected private Chelsea club was not found.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM "Users"
                        WHERE "Id" =
                            '6e3d1339-b5c8-4025-a23e-27a491c3d286'::uuid
                    ) THEN
                        RAISE EXCEPTION
                            'Code Kingz account was not found.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM "Users"
                        WHERE "Id" =
                            '7708c077-b28b-479e-9403-43bf45e5cb06'::uuid
                    ) THEN
                        RAISE EXCEPTION
                            'FootballSim Test User account was not found.';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM "Competitions"
                        WHERE "Id" =
                            'a4b84e46-1bd7-4a55-b596-ff1e0aaf6c8c'::uuid
                    ) THEN
                        RAISE EXCEPTION
                            'Expected Swagger test competition was not found.';
                    END IF;

                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM "Teams"
                        WHERE LENGTH("Name") > 150
                           OR LENGTH("Country") > 100
                    ) OR EXISTS (
                        SELECT 1 FROM "Competitions"
                        WHERE LENGTH("Name") > 150
                           OR LENGTH("Season") > 30
                    ) THEN
                        RAISE EXCEPTION
                            'Existing data exceeds new column limits.';
                    END IF;
                END
                $$;
                """);

            // ==========================================
            // 2. UPDATE TEAM COLUMN DEFINITIONS
            // ==========================================

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Teams",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // ==========================================
            // 3. ADD TEAM OWNERSHIP COLUMNS
            // ==========================================

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Teams",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Teams",
                type: "uuid",
                nullable: true);

            // ==========================================
            // 4. CLASSIFY THE 36 DEFAULT CLUBS
            // ==========================================

            migrationBuilder.Sql("""
                UPDATE "Teams"
                SET
                    "IsDefault" = TRUE,
                    "OwnerId" = NULL
                WHERE "Id" <>
                    '0159a80d-54c0-42ad-9d2d-7127e2baab6a'::uuid;
                """);

            // ==========================================
            // 5. ASSIGN PRIVATE CHELSEA TO CODE KINGZ
            // ==========================================

            migrationBuilder.Sql("""
                UPDATE "Teams"
                SET
                    "IsDefault" = FALSE,
                    "OwnerId" =
                        '6e3d1339-b5c8-4025-a23e-27a491c3d286'::uuid
                WHERE "Id" =
                    '0159a80d-54c0-42ad-9d2d-7127e2baab6a'::uuid;
                """);

            // ==========================================
            // 6. UPDATE COMPETITION COLUMNS
            // ==========================================

            migrationBuilder.AlterColumn<string>(
                name: "Season",
                table: "Competitions",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Competitions",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Competitions",
                type: "uuid",
                nullable: true);

            // ==========================================
            // 7. ASSIGN SWAGGER COMPETITION TO TEST USER
            // ==========================================

            migrationBuilder.Sql("""
                UPDATE "Competitions"
                SET "OwnerId" =
                    '7708c077-b28b-479e-9403-43bf45e5cb06'::uuid
                WHERE "Id" =
                    'a4b84e46-1bd7-4a55-b596-ff1e0aaf6c8c'::uuid;
                """);

            // ==========================================
            // 8. VERIFY OWNERSHIP BEFORE CONSTRAINTS
            // ==========================================

            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    -- The exact counts are only meaningful for the existing local dataset.
                    IF EXISTS (SELECT 1 FROM "Teams") THEN
                    IF (
                        SELECT COUNT(*) FROM "Teams"
                        WHERE "IsDefault" = TRUE
                          AND "OwnerId" IS NULL
                    ) <> 36 THEN
                        RAISE EXCEPTION
                            'Default club classification failed.';
                    END IF;

                    IF (
                        SELECT COUNT(*) FROM "Teams"
                        WHERE "IsDefault" = FALSE
                          AND "OwnerId" =
                              '6e3d1339-b5c8-4025-a23e-27a491c3d286'::uuid
                    ) <> 1 THEN
                        RAISE EXCEPTION
                            'Private Chelsea ownership assignment failed.';
                    END IF;

                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM "Teams"
                        WHERE NOT (
                            ("IsDefault" = TRUE AND "OwnerId" IS NULL)
                            OR
                            ("IsDefault" = FALSE AND "OwnerId" IS NOT NULL)
                        )
                    ) THEN
                        RAISE EXCEPTION
                            'Invalid team ownership detected.';
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM "Competitions"
                        WHERE "OwnerId" IS NULL
                    ) THEN
                        RAISE EXCEPTION
                            'Competition ownership assignment failed.';
                    END IF;
                END
                $$;
                """);

            // ==========================================
            // 9. CREATE OWNERSHIP INDEXES
            // ==========================================

            migrationBuilder.CreateIndex(
                name: "IX_Teams_IsDefault_OwnerId",
                table: "Teams",
                columns: new[] { "IsDefault", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OwnerId",
                table: "Teams",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_OwnerId",
                table: "Competitions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Competitions_OwnerId_CreatedDate",
                table: "Competitions",
                columns: new[] { "OwnerId", "CreatedDate" });

            // ==========================================
            // 10. ENFORCE VALID CLUB OWNERSHIP
            // ==========================================

            migrationBuilder.AddCheckConstraint(
                name: "CK_Teams_Ownership",
                table: "Teams",
                sql: """
                    ("IsDefault" = TRUE AND "OwnerId" IS NULL)
                    OR
                    ("IsDefault" = FALSE AND "OwnerId" IS NOT NULL)
                    """);

            // ==========================================
            // 11. ADD USER FOREIGN KEYS
            // ==========================================

            migrationBuilder.AddForeignKey(
                name: "FK_Competitions_Users_OwnerId",
                table: "Competitions",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Teams_Users_OwnerId",
                table: "Teams",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ==========================================
            // REMOVE OWNERSHIP FOREIGN KEYS
            // ==========================================

            migrationBuilder.DropForeignKey(
                name: "FK_Competitions_Users_OwnerId",
                table: "Competitions");

            migrationBuilder.DropForeignKey(
                name: "FK_Teams_Users_OwnerId",
                table: "Teams");

            // ==========================================
            // REMOVE OWNERSHIP INDEXES
            // ==========================================

            migrationBuilder.DropIndex(
                name: "IX_Teams_IsDefault_OwnerId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Teams_OwnerId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Competitions_OwnerId",
                table: "Competitions");

            migrationBuilder.DropIndex(
                name: "IX_Competitions_OwnerId_CreatedDate",
                table: "Competitions");

            // ==========================================
            // REMOVE OWNERSHIP CONSTRAINT
            // ==========================================

            migrationBuilder.DropCheckConstraint(
                name: "CK_Teams_Ownership",
                table: "Teams");

            // ==========================================
            // REMOVE OWNERSHIP COLUMNS
            // ==========================================

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Competitions");

            // ==========================================
            // RESTORE ORIGINAL COLUMN TYPES
            // ==========================================

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "Country",
                table: "Teams",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Season",
                table: "Competitions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Competitions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldMaxLength: 150);
        }
    }
}
