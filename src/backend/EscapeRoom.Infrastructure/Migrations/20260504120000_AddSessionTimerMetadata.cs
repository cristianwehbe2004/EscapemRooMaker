using System;
using EscapeRoom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EscapeRoom.Infrastructure.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260504120000_AddSessionTimerMetadata")]
    /// <inheritdoc />
    public partial class AddSessionTimerMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "sessions",
                type: "integer",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndsAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HostActorId",
                table: "sessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuickPlay",
                table: "sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAtUtc",
                table: "sessions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_EndsAtUtc",
                table: "sessions",
                column: "EndsAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sessions_EndsAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "EndsAtUtc",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "HostActorId",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "IsQuickPlay",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "LastActivityAtUtc",
                table: "sessions");
        }
    }
}
