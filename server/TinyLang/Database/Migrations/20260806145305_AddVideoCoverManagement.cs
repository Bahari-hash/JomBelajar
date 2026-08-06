using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TinyLang.Database.Migrations;

/// <inheritdoc />
public partial class AddVideoCoverManagement : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CoverMediaResourceId",
            table: "videos",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_videos_CoverMediaResourceId",
            table: "videos",
            column: "CoverMediaResourceId");

        migrationBuilder.AddForeignKey(
            name: "FK_videos_media_resources_CoverMediaResourceId",
            table: "videos",
            column: "CoverMediaResourceId",
            principalTable: "media_resources",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_videos_media_resources_CoverMediaResourceId",
            table: "videos");

        migrationBuilder.DropIndex(
            name: "IX_videos_CoverMediaResourceId",
            table: "videos");

        migrationBuilder.DropColumn(
            name: "CoverMediaResourceId",
            table: "videos");
    }
}
