using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddArticleReadingAudio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReadingAudioResourceId",
                table: "articles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_articles_ReadingAudioResourceId",
                table: "articles",
                column: "ReadingAudioResourceId");

            migrationBuilder.AddForeignKey(
                name: "FK_articles_audio_resources_ReadingAudioResourceId",
                table: "articles",
                column: "ReadingAudioResourceId",
                principalTable: "audio_resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_articles_audio_resources_ReadingAudioResourceId",
                table: "articles");

            migrationBuilder.DropIndex(
                name: "IX_articles_ReadingAudioResourceId",
                table: "articles");

            migrationBuilder.DropColumn(
                name: "ReadingAudioResourceId",
                table: "articles");
        }
    }
}
