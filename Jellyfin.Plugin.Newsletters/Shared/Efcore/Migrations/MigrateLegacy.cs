//using Jellyfin.Plugin.Newsletters.Shared.Efcore;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Infrastructure;
//using Microsoft.EntityFrameworkCore.Metadata;
//using Microsoft.EntityFrameworkCore.Migrations;
//using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
//
//namespace Jellyfin.Plugin.Newsletters.Shared.Efcore.Migrations
//{
//    [DbContext(typeof(NLPContext))]
//    [Migration("20251121_InitialCreate")]
//    partial class InitialCreate
//    {
//        protected override void BuildTargetModel(ModelBuilder modelBuilder)
//        {
//#pragma warning disable 612, 618
//            modelBuilder.Entity("Efcore.Newsletter", n =>
//                {
//                    n.Property<int>("Filename")
//                        .ValueGeneratedOnAdd()
//                        .HasColumnType("string")
//                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);
//
//                    n.Property<int>("Rating")
//                        .HasColumnType("int");
//
//                    n.Property<string>("Url")
//                        .HasColumnType("nvarchar(max)");
//
//                    n.HasKey("BlogId");
//
//                    n.ToTable("Blogs");
//                });
//
//            modelBuilder.Entity("Intro.Post", b =>
//                {
//                    b.Property<int>("PostId")
//                        .ValueGeneratedOnAdd()
//                        .HasColumnType("int")
//                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);
//
//                    b.Property<int>("BlogId")
//                        .HasColumnType("int");
//
//                    b.Property<string>("Content")
//                        .HasColumnType("nvarchar(max)");
//
//                    b.Property<string>("Title")
//                        .HasColumnType("nvarchar(max)");
//
//                    b.HasKey("PostId");
//
//                    b.HasIndex("BlogId");
//
//                    b.ToTable("Posts");
//                });
//
//            modelBuilder.Entity("Intro.Post", b =>
//                {
//                    b.HasOne("Intro.Blog", "Blog")
//                        .WithMany("Posts")
//                        .HasForeignKey("BlogId")
//                        .OnDelete(DeleteBehavior.Cascade)
//                        .IsRequired();
//                });
//#pragma warning restore 612, 618
//        }
//    }
//}