using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VideoLibrary.Migrations
{
    /// <inheritdoc />
    public partial class TranscriptionVideoLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_transcriptions_video_id",
                table: "transcriptions",
                column: "video_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_transcriptions_videos_video_id",
                table: "transcriptions",
                column: "video_id",
                principalTable: "videos",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_transcriptions_videos_video_id",
                table: "transcriptions");

            migrationBuilder.DropIndex(
                name: "ix_transcriptions_video_id",
                table: "transcriptions");
        }
    }
}
