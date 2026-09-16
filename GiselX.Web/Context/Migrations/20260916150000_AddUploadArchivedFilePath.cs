using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations;

/// <summary>
/// Records where an uploaded workbook was archived. Only the parsed rows and a SHA-256 used to
/// survive an upload, so a disagreement about what the file contained could not be settled by
/// looking at the file. The bytes live on disk; this column is the pointer.
/// </summary>
[DbContext(typeof(GiselXDbContext))]
[Migration("20260916150000_AddUploadArchivedFilePath")]
public partial class AddUploadArchivedFilePath : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "StockUploadBatch", "SelloutUploadGroup" })
        {
            migrationBuilder.Sql($"""
                IF COL_LENGTH(N'dbo.{table}', N'ArchivedFilePath') IS NULL
                    ALTER TABLE dbo.{table} ADD ArchivedFilePath nvarchar(500) NULL;
                """);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Retained: dropping it would orphan files that are still on disk, with nothing left to say
        // which upload each one belonged to.
    }
}
