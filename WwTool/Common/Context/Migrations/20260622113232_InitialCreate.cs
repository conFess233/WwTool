using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WwTool.Common.Context.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedOauthCode = table.Column<string>(type: "TEXT", nullable: true),
                    Region = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Sex = table.Column<int>(type: "INTEGER", nullable: false),
                    HeadPhoto = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Uid);
                });

            migrationBuilder.CreateTable(
                name: "GachaRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Uid = table.Column<string>(type: "TEXT", nullable: false),
                    PoolType = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    ResourceType = table.Column<string>(type: "TEXT", nullable: true),
                    QualityLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Time = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GachaRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GachaRecords_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerBaseInfos",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", nullable: false),
                    RoleName = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    WorldLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveDays = table.Column<int>(type: "INTEGER", nullable: false),
                    RoleNum = table.Column<int>(type: "INTEGER", nullable: false),
                    SoundBox = table.Column<int>(type: "INTEGER", nullable: false),
                    Energy = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxEnergy = table.Column<int>(type: "INTEGER", nullable: false),
                    StoreEnergy = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxStoreEnergy = table.Column<int>(type: "INTEGER", nullable: false),
                    Liveness = table.Column<int>(type: "INTEGER", nullable: false),
                    LivenessMaxCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LivenessUnlock = table.Column<bool>(type: "INTEGER", nullable: false),
                    WeeklyInstCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatTime = table.Column<long>(type: "INTEGER", nullable: false),
                    BirthMon = table.Column<int>(type: "INTEGER", nullable: false),
                    BirthDay = table.Column<int>(type: "INTEGER", nullable: false),
                    StoreEnergyRecoverTime = table.Column<long>(type: "INTEGER", nullable: false),
                    EnergyRecoverTime = table.Column<long>(type: "INTEGER", nullable: false),
                    BoxesJson = table.Column<string>(type: "TEXT", nullable: false),
                    BasicBoxesJson = table.Column<string>(type: "TEXT", nullable: false),
                    PhantomBoxesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerBaseInfos", x => x.Uid);
                    table.ForeignKey(
                        name: "FK_PlayerBaseInfos_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerBattlePasses",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekExp = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekMaxExp = table.Column<int>(type: "INTEGER", nullable: false),
                    IsUnlock = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOpen = table.Column<bool>(type: "INTEGER", nullable: false),
                    Exp = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpLimit = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerBattlePasses", x => x.Uid);
                    table.ForeignKey(
                        name: "FK_PlayerBattlePasses_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMotorData",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    Exp = table.Column<int>(type: "INTEGER", nullable: false),
                    NextExp = table.Column<int>(type: "INTEGER", nullable: false),
                    SkinsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StickersJson = table.Column<string>(type: "TEXT", nullable: false),
                    DecorationsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FramesJson = table.Column<string>(type: "TEXT", nullable: false),
                    EquipSkinId = table.Column<int>(type: "INTEGER", nullable: false),
                    EquipSkinQuality = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMotorData", x => x.Uid);
                    table.ForeignKey(
                        name: "FK_PlayerMotorData_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerMusicData",
                columns: table => new
                {
                    Uid = table.Column<string>(type: "TEXT", nullable: false),
                    AlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMusicData", x => new { x.Uid, x.AlbumId });
                    table.ForeignKey(
                        name: "FK_PlayerMusicData_UserAccounts_Uid",
                        column: x => x.Uid,
                        principalTable: "UserAccounts",
                        principalColumn: "Uid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GachaRecords_Uid_PoolType_Time",
                table: "GachaRecords",
                columns: new[] { "Uid", "PoolType", "Time" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GachaRecords");

            migrationBuilder.DropTable(
                name: "PlayerBaseInfos");

            migrationBuilder.DropTable(
                name: "PlayerBattlePasses");

            migrationBuilder.DropTable(
                name: "PlayerMotorData");

            migrationBuilder.DropTable(
                name: "PlayerMusicData");

            migrationBuilder.DropTable(
                name: "UserAccounts");
        }
    }
}
