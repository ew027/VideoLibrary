using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoLibrary.Migrations
{
    /// <inheritdoc />
    public partial class TagHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "left",
                table: "tags",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "level",
                table: "tags",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "parent_id",
                table: "tags",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "right",
                table: "tags",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_tags_parent_id",
                table: "tags",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_tags_left_right",
                table: "tags",
                columns: new[] { "left", "right" });

            migrationBuilder.AddForeignKey(
                name: "fk_tags_tags_parent_id",
                table: "tags",
                column: "parent_id",
                principalTable: "tags",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_tags_tags_parent_id",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ix_tags_left_right",
                table: "tags");

            migrationBuilder.DropIndex(
                name: "ix_tags_parent_id",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "left",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "level",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "parent_id",
                table: "tags");

            migrationBuilder.DropColumn(
                name: "right",
                table: "tags");
        }
    }
}
