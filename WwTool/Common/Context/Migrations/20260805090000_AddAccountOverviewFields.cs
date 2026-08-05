using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WwTool.Common.Context.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOverviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChapterId",
                table: "PlayerBaseInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "HasBattlePassData",
                table: "PlayerBaseInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasBasicBoxesData",
                table: "PlayerBaseInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasBoxesData",
                table: "PlayerBaseInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasMusicData",
                table: "PlayerBaseInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HasPhantomBoxesData",
                table: "PlayerBaseInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // 旧快照只有非空 JSON/音乐行能够证明字段曾被成功获取；空集合保持“未知”。
            migrationBuilder.Sql("""
                UPDATE PlayerBaseInfos
                SET HasBattlePassData = 1
                WHERE EXISTS (
                    SELECT 1 FROM PlayerBattlePasses
                    WHERE PlayerBattlePasses.Uid = PlayerBaseInfos.Uid
                      AND (
                          PlayerBattlePasses.Level <> 0 OR
                          PlayerBattlePasses.WeekExp <> 0 OR
                          PlayerBattlePasses.WeekMaxExp <> 0 OR
                          PlayerBattlePasses.IsUnlock <> 0 OR
                          PlayerBattlePasses.IsOpen <> 0 OR
                          PlayerBattlePasses.Exp <> 0 OR
                          PlayerBattlePasses.ExpLimit <> 0
                      )
                );

                UPDATE PlayerBaseInfos
                SET HasBoxesData = 1
                WHERE BoxesJson IS NOT NULL AND TRIM(BoxesJson) NOT IN ('', '{}');

                UPDATE PlayerBaseInfos
                SET HasBasicBoxesData = 1
                WHERE BasicBoxesJson IS NOT NULL AND TRIM(BasicBoxesJson) NOT IN ('', '{}');

                UPDATE PlayerBaseInfos
                SET HasPhantomBoxesData = 1
                WHERE PhantomBoxesJson IS NOT NULL AND TRIM(PhantomBoxesJson) NOT IN ('', '{}');

                UPDATE PlayerBaseInfos
                SET HasMusicData = 1
                WHERE EXISTS (
                    SELECT 1 FROM PlayerMusicData
                    WHERE PlayerMusicData.Uid = PlayerBaseInfos.Uid
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ChapterId", table: "PlayerBaseInfos");
            migrationBuilder.DropColumn(name: "HasBattlePassData", table: "PlayerBaseInfos");
            migrationBuilder.DropColumn(name: "HasBasicBoxesData", table: "PlayerBaseInfos");
            migrationBuilder.DropColumn(name: "HasBoxesData", table: "PlayerBaseInfos");
            migrationBuilder.DropColumn(name: "HasMusicData", table: "PlayerBaseInfos");
            migrationBuilder.DropColumn(name: "HasPhantomBoxesData", table: "PlayerBaseInfos");
        }
    }
}
