#pragma warning disable SA1205, SA1601 
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Newsletters.Shared.Efcore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Jellyfin.Plugin.Newsletters.Shared.Efcore.Migrations
{
    [DbContext(typeof(NLPContext))]
    [Migration("Initialize")]
    /// <summary>
    /// Creates the initial tables.
    /// Filename is the primary key for NewsletterData, ID for PreviousRun.
    /// NewsletterData tracks all files, and related data, processed by the plugin.
    /// PreviousRun tracks the last successful scan to provide a date to Jellyfin
    /// so that it only provides new files instead of the whole library.
    /// </summary>
    partial class InitialCreate
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.Entity(
                "Efcore.Newsletter", n =>
                {
                    n.Property<string>("Filename")
                        .HasColumnType("string");

                    n.Property<string>("Title")
                        .HasColumnType("string");

                    n.Property<string>("Album")
                        .HasColumnType("string");

                    n.Property<int>("Season")
                        .HasColumnType("int");

                    n.Property<int>("Episode")
                        .HasColumnType("int");

                    n.Property<string>("Overview")
                        .HasColumnType("string");

                    n.Property<string>("ItemID")
                        .HasColumnType("string");

                    n.Property<string>("PosterPath")
                        .HasColumnType("string");

                    n.Property<string>("Type")
                        .HasColumnType("string");

                    n.Property<int>("Emailed")
                        .HasColumnType("int");

                    n.HasKey("Filename");

                    n.HasIndex("Filename");

                    n.ToTable("NewsletterData");
                });

            modelBuilder.Entity(
                "Efcore.PreviousRun", p =>
                {
                    p.Property<int>("ID")
                        .HasColumnType("int");

                    p.Property<string>("LastRun")
                        .HasColumnType("string");

                    p.HasKey("ID");

                    p.ToTable("PreviousRun");
                });
#pragma warning restore 612, 618
        }
    }
}