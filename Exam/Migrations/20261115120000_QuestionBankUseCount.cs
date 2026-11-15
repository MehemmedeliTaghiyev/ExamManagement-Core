using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exam.Migrations
{
    /// <inheritdoc />
    public partial class QuestionBankUseCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "QuestionBank",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UseCount",
                table: "QuestionBank",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "QuestionBank");

            migrationBuilder.DropColumn(
                name: "UseCount",
                table: "QuestionBank");
        }
    }
}
