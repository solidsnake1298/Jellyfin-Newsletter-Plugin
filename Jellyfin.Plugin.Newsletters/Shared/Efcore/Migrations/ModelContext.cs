using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Newsletters.Shared.Efcore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Jellyfin.Plugin.Newsletters.Shared.Efcore.Migrations;

[DbContext(typeof(NLPContext))]
internal sealed class NLPContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
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