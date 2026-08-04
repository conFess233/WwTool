using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WwTool.Common.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddGuideSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GuideAccountCredentials",
                columns: table => new
                {
                    CUid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EncryptedGuideToken = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideAccountCredentials", x => x.CUid);
                });

            migrationBuilder.CreateTable(
                name: "GuidePlayerSnapshots",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CUid = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ServerId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    LastSyncedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuidePlayerSnapshots", x => x.Uid);
                    table.ForeignKey(
                        name: "FK_GuidePlayerSnapshots_GuideAccountCredentials_CUid",
                        column: x => x.CUid,
                        principalTable: "GuideAccountCredentials",
                        principalColumn: "CUid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GuidePlayerSnapshots_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuideEquippedWeaponSnapshots",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    OwnerRoleGbId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    WeaponGbId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PictureUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Star = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideEquippedWeaponSnapshots", x => new { x.Uid, x.OwnerRoleGbId });
                    table.ForeignKey(
                        name: "FK_GuideEquippedWeaponSnapshots_GuidePlayerSnapshots_Uid",
                        column: x => x.Uid,
                        principalTable: "GuidePlayerSnapshots",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuideRoleSnapshots",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RoleGbId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CardPictureUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IllustrationPictureUrl = table.Column<string>(type: "TEXT", nullable: true),
                    Star = table.Column<int>(type: "INTEGER", nullable: false),
                    RoleStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    IsAcquired = table.Column<bool>(type: "INTEGER", nullable: false),
                    MayRoleGbId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    StrategyId = table.Column<long>(type: "INTEGER", nullable: true),
                    StrategyModifiedAt = table.Column<long>(type: "INTEGER", nullable: true),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideRoleSnapshots", x => new { x.Uid, x.RoleGbId });
                    table.ForeignKey(
                        name: "FK_GuideRoleSnapshots_GuidePlayerSnapshots_Uid",
                        column: x => x.Uid,
                        principalTable: "GuidePlayerSnapshots",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GuideEquippedWeaponSnapshots_Uid_SourceOrder",
                table: "GuideEquippedWeaponSnapshots",
                columns: new[] { "Uid", "SourceOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GuidePlayerSnapshots_CUid",
                table: "GuidePlayerSnapshots",
                column: "CUid");

            migrationBuilder.CreateIndex(
                name: "IX_GuideRoleSnapshots_Uid_SourceOrder",
                table: "GuideRoleSnapshots",
                columns: new[] { "Uid", "SourceOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuideEquippedWeaponSnapshots");

            migrationBuilder.DropTable(
                name: "GuideRoleSnapshots");

            migrationBuilder.DropTable(
                name: "GuidePlayerSnapshots");

            migrationBuilder.DropTable(
                name: "GuideAccountCredentials");
        }
    }
}
