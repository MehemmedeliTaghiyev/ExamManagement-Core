using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exam.Migrations
{
    /// <inheritdoc />
    public partial class SecondDayFinished : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectAnswersCount",
                table: "StudentExams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FinalScore",
                table: "StudentExams",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "UnansweredCount",
                table: "StudentExams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WrongAnswersCount",
                table: "StudentExams",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CorrectAnswersCount",
                table: "StudentExams");

            migrationBuilder.DropColumn(
                name: "FinalScore",
                table: "StudentExams");

            migrationBuilder.DropColumn(
                name: "UnansweredCount",
                table: "StudentExams");

            migrationBuilder.DropColumn(
                name: "WrongAnswersCount",
                table: "StudentExams");
        }
    }
}
