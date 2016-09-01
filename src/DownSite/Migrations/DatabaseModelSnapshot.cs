using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using DownSite;

namespace DownSite.Migrations
{
    [DbContext(typeof(Database))]
    partial class DatabaseModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.0.0-rtm-21431");

            modelBuilder.Entity("DownSite.Article", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid>("AuthorId");

                    b.Property<string>("Content");

                    b.Property<DateTime>("Created");

                    b.Property<DateTime>("Modified");

                    b.Property<bool>("ShowInBlog");

                    b.Property<bool>("ShowInMenu");

                    b.Property<string>("Title");

                    b.Property<Guid>("VersionGroup");

                    b.HasKey("Id");

                    b.HasIndex("AuthorId");

                    b.ToTable("Article");
                });

            modelBuilder.Entity("DownSite.Comment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid>("ArticleId");

                    b.Property<string>("Content");

                    b.Property<DateTime>("Created");

                    b.Property<string>("Name");

                    b.HasKey("Id");

                    b.HasIndex("ArticleId");

                    b.ToTable("Comment");
                });

            modelBuilder.Entity("DownSite.Configuration", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("Version");

                    b.HasKey("Id");

                    b.ToTable("Configuration");
                });

            modelBuilder.Entity("DownSite.Image", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<byte[]>("Data");

                    b.Property<string>("FileName");

                    b.Property<int>("Height");

                    b.Property<string>("MimeType");

                    b.Property<int>("Width");

                    b.HasKey("Id");

                    b.ToTable("Image");
                });

            modelBuilder.Entity("DownSite.Menu", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Caption");

                    b.Property<string>("Link");

                    b.HasKey("Id");

                    b.ToTable("Menu");
                });

            modelBuilder.Entity("DownSite.Settings", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<bool>("AllowWriteComments");

                    b.Property<int>("ArticlesPerPage");

                    b.Property<bool>("Disqus");

                    b.Property<string>("DisqusShortName");

                    b.Property<bool>("ShowComments");

                    b.Property<bool>("ShowLogin");

                    b.Property<string>("SiteDescription");

                    b.Property<string>("SiteName");

                    b.Property<string>("SiteUrl");

                    b.HasKey("Id");

                    b.ToTable("Settings");
                });

            modelBuilder.Entity("DownSite.Tag", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<Guid>("ArticleId");

                    b.Property<string>("Name");

                    b.HasKey("Id");

                    b.HasIndex("ArticleId");

                    b.ToTable("Tag");
                });

            modelBuilder.Entity("DownSite.User", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Email");

                    b.Property<string>("FirstName");

                    b.Property<Guid>("ImageId");

                    b.Property<string>("LastName");

                    b.Property<string>("Password");

                    b.Property<string>("PlainTextPassword");

                    b.Property<string>("UserName");

                    b.HasKey("Id");

                    b.ToTable("User");
                });

            modelBuilder.Entity("DownSite.Article", b =>
                {
                    b.HasOne("DownSite.User", "Author")
                        .WithMany()
                        .HasForeignKey("AuthorId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("DownSite.Comment", b =>
                {
                    b.HasOne("DownSite.Article", "Article")
                        .WithMany("Comment")
                        .HasForeignKey("ArticleId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("DownSite.Tag", b =>
                {
                    b.HasOne("DownSite.Article")
                        .WithMany("Category")
                        .HasForeignKey("ArticleId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}
