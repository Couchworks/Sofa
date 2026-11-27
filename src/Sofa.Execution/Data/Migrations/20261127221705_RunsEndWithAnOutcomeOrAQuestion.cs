using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sofa.Execution.Data.Migrations;

/// <inheritdoc />
public partial class _20261127221705_RunsEndWithAnOutcomeOrAQuestion : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "summary",
            schema: "execution",
            table: "runs");

        migrationBuilder.AddColumn<string>(
            name: "question",
            schema: "execution",
            table: "runs",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "outcome",
            schema: "execution",
            table: "runs",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "question_id",
            schema: "execution",
            table: "runs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "ck_runs_end_with_an_outcome_or_a_question",
            schema: "execution",
            table: "runs",
            sql: "outcome is null or question_id is null");

        migrationBuilder.AddCheckConstraint(
            name: "ck_runs_questions_have_their_text",
            schema: "execution",
            table: "runs",
            sql: "(question_id is null) = (question is null)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_runs_end_with_an_outcome_or_a_question",
            schema: "execution",
            table: "runs");

        migrationBuilder.DropCheckConstraint(
            name: "ck_runs_questions_have_their_text",
            schema: "execution",
            table: "runs");

        migrationBuilder.DropColumn(
            name: "outcome",
            schema: "execution",
            table: "runs");

        migrationBuilder.DropColumn(
            name: "question_id",
            schema: "execution",
            table: "runs");

        migrationBuilder.DropColumn(
            name: "question",
            schema: "execution",
            table: "runs");

        migrationBuilder.AddColumn<string>(
            name: "summary",
            schema: "execution",
            table: "runs",
            type: "character varying(4000)",
            maxLength: 4000,
            nullable: true);
    }
}
