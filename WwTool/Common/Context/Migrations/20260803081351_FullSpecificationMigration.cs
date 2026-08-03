using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WwTool.Common.Context.Migrations
{
    /// <inheritdoc />
    public partial class FullSpecificationMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GachaRecords_Uid_PoolType_Time",
                table: "GachaRecords");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAtUtc",
                table: "UserAccounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAtUtc",
                table: "PlayerMusicData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAtUtc",
                table: "PlayerMotorData",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAtUtc",
                table: "PlayerBattlePasses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSyncedAtUtc",
                table: "PlayerBaseInfos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApiPageIndex",
                table: "GachaRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DuplicateOccurrenceIndex",
                table: "GachaRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ImportBatchId",
                table: "GachaRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ImportedAtUtc",
                table: "GachaRecords",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "ResponseItemIndex",
                table: "GachaRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SourceOccurredAtUtc",
                table: "GachaRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceOrder",
                table: "GachaRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "SourceRecordId",
                table: "GachaRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StableFingerprint",
                table: "GachaRecords",
                type: "TEXT",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "GachaImportBatches",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PoolType = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RecordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstSourceOrder = table.Column<long>(type: "INTEGER", nullable: true),
                    LastSourceOrder = table.Column<long>(type: "INTEGER", nullable: true),
                    SourceCursor = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GachaImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GachaImportBatches_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SyncStates",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DataKind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ScopeKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSuccessfulSyncAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastCursor = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncStates", x => new { x.Uid, x.DataKind, x.ScopeKey });
                    table.ForeignKey(
                        name: "FK_SyncStates_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            // Legacy rows predate import batches and source metadata. Preserve their only
            // stable historical order (the auto-increment key) and assign one baseline
            // batch per account/pool before enforcing foreign keys and unique indexes.
            migrationBuilder.Sql(
                """
                INSERT INTO "GachaImportBatches"
                    ("Uid", "PoolType", "Source", "StartedAtUtc", "CompletedAtUtc", "RecordCount", "FirstSourceOrder", "LastSourceOrder")
                SELECT "Uid", "PoolType", 'legacy-baseline',
                       COALESCE(MIN("Time"), '1970-01-01T00:00:00+00:00'),
                       COALESCE(MAX("Time"), '1970-01-01T00:00:00+00:00'),
                       COUNT(*), MIN("Id"), MAX("Id")
                FROM "GachaRecords"
                GROUP BY "Uid", "PoolType";

                UPDATE "GachaRecords"
                SET "ImportBatchId" = (
                        SELECT b."Id" FROM "GachaImportBatches" b
                        WHERE b."Uid" = "GachaRecords"."Uid"
                          AND b."PoolType" = "GachaRecords"."PoolType"
                          AND b."Source" = 'legacy-baseline'
                    ),
                    "SourceOrder" = "Id",
                    "ResponseItemIndex" = "Id",
                    "StableFingerprint" = 'legacy-v1-' || "Id",
                    "SourceOccurredAtUtc" = "Time",
                    "ImportedAtUtc" = COALESCE("Time", '1970-01-01T00:00:00+00:00');
                """);

            migrationBuilder.CreateIndex(
                name: "IX_GachaRecords_ImportBatchId",
                table: "GachaRecords",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_GachaRecords_Uid_PoolType_SourceOccurredAtUtc",
                table: "GachaRecords",
                columns: new[] { "Uid", "PoolType", "SourceOccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_GachaRecords_Uid_PoolType_SourceOrder",
                table: "GachaRecords",
                columns: new[] { "Uid", "PoolType", "SourceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GachaRecords_Uid_PoolType_StableFingerprint",
                table: "GachaRecords",
                columns: new[] { "Uid", "PoolType", "StableFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GachaImportBatches_Uid",
                table: "GachaImportBatches",
                column: "Uid");

            migrationBuilder.AddForeignKey(
                name: "FK_GachaRecords_GachaImportBatches_ImportBatchId",
                table: "GachaRecords",
                column: "ImportBatchId",
                principalTable: "GachaImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GachaRecords_GachaImportBatches_ImportBatchId",
                table: "GachaRecords");

            migrationBuilder.DropTable(
                name: "GachaImportBatches");

            migrationBuilder.DropTable(
                name: "SyncStates");

            migrationBuilder.DropIndex(
                name: "IX_GachaRecords_ImportBatchId",
                table: "GachaRecords");

            migrationBuilder.DropIndex(
                name: "IX_GachaRecords_Uid_PoolType_SourceOccurredAtUtc",
                table: "GachaRecords");

            migrationBuilder.DropIndex(
                name: "IX_GachaRecords_Uid_PoolType_SourceOrder",
                table: "GachaRecords");

            migrationBuilder.DropIndex(
                name: "IX_GachaRecords_Uid_PoolType_StableFingerprint",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "LastSyncedAtUtc",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "LastSyncedAtUtc",
                table: "PlayerMusicData");

            migrationBuilder.DropColumn(
                name: "LastSyncedAtUtc",
                table: "PlayerMotorData");

            migrationBuilder.DropColumn(
                name: "LastSyncedAtUtc",
                table: "PlayerBattlePasses");

            migrationBuilder.DropColumn(
                name: "LastSyncedAtUtc",
                table: "PlayerBaseInfos");

            migrationBuilder.DropColumn(
                name: "ApiPageIndex",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "DuplicateOccurrenceIndex",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "ImportedAtUtc",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "ResponseItemIndex",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "SourceOccurredAtUtc",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "SourceOrder",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                table: "GachaRecords");

            migrationBuilder.DropColumn(
                name: "StableFingerprint",
                table: "GachaRecords");

            migrationBuilder.CreateIndex(
                name: "IX_GachaRecords_Uid_PoolType_Time",
                table: "GachaRecords",
                columns: new[] { "Uid", "PoolType", "Time" },
                descending: new[] { false, false, true });
        }
    }
}
