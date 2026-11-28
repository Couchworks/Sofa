using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Flow.Data.Migrations;

/// <inheritdoc />
public partial class _20261127220937_Questions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "questions",
            schema: "flow",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                run_id = table.Column<Guid>(type: "uuid", nullable: false),
                phase = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                asked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                answer = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                answered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_questions", x => x.id);
                table.ForeignKey(
                    name: "fk_questions_work_items_work_item_id",
                    column: x => x.work_item_id,
                    principalSchema: "flow",
                    principalTable: "work_items",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_questions_work_item_id_asked_at",
            schema: "flow",
            table: "questions",
            columns: new[] { "work_item_id", "asked_at" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "questions",
            schema: "flow");
    }
}
