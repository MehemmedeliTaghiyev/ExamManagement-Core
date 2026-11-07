using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exam.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FigureUrl",
                table: "Questions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceQnum",
                table: "Questions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OptionText",
                table: "QuestionOptions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateTable(
                name: "QuestionBank",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Qnum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InputKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CorrectText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DifficultyLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    Audience = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MainTopic = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    SituationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SituationTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FigureUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBank", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestionBankOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QuestionBankId = table.Column<int>(type: "int", nullable: false),
                    OptionText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsCorrect = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBankOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBankOptions_QuestionBank_QuestionBankId",
                        column: x => x.QuestionBankId,
                        principalTable: "QuestionBank",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBank_Qnum",
                table: "QuestionBank",
                column: "Qnum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBankOptions_QuestionBankId",
                table: "QuestionBankOptions",
                column: "QuestionBankId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionBankOptions");

            migrationBuilder.DropTable(
                name: "QuestionBank");

            migrationBuilder.DropColumn(
                name: "FigureUrl",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "SourceQnum",
                table: "Questions");

            migrationBuilder.AlterColumn<string>(
                name: "OptionText",
                table: "QuestionOptions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
