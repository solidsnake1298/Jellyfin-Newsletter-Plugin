using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jellyfin.Plugin.Newsletters.Shared.Efcore.Migrations
{
    /// <summary>
    /// Creates the initial tables.
    /// Filename is the primary key for NewsletterData, ID for PreviousRun.
    /// NewsletterData tracks all files, and related data, processed by the plugin.
    /// PreviousRun tracks the last successful scan to provide a date to Jellyfin
    /// so that it only provides new files instead of the whole library.
    /// </summary>
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsletterData",
                columns: table => new
                {
                    Filename = table.Column<string>(nullable: false),
                    Title = table.Column<string>(nullable: true),
                    Album = table.Column<string>(nullable: true),
                    Season = table.Column<int>(nullable: true),
                    Episode = table.Column<int>(nullable: true),
                    Overview = table.Column<string>(nullable: true),
                    ItemID = table.Column<string>(nullable: true),
                    PosterPath = table.Column<string>(nullable: true),
                    Type = table.Column<string>(nullable: true),
                    Emailed = table.Column<int>(nullable: true)
                },
                constraints: table => { table.PrimaryKey("PK_NewsletterData", x => x.Filename); });

            migrationBuilder.CreateTable(
                name: "PreviousRun",
                columns: table => new
                {
                    ID = table.Column<int>(nullable: false),
                    LastRun = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreviousRun", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterData_Filename",
                table: "NewsletterData",
                column: "Filename");
        }

//        protected override void Down(MigrationBuilder migrationBuilder)
//        {
//            migrationBuilder.DropTable(
//                name: "Posts");

//            migrationBuilder.DropTable(
//                name: "Blogs");
//        }
    }
}