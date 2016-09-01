using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Drawing;
using System.Web;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Data;
using ServiceStack;
using Microsoft.EntityFrameworkCore;

namespace DownSite
{
    public class Database : DbContext
    {

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string dbfile = Path.Combine(Paths.Data, "db3.sqlite3");
            //string dbfile = @"C:\Users\cody\Documents\DownSite\src\DownSite\data\db2.sqlite3;";
            optionsBuilder.UseSqlite("Filename= " + dbfile);// + ";BinaryGUID=False;");
        }

        public DbSet<Article> Article { get; set; }
        public DbSet<Image> Image { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<Tag> Tag { get; set; }
        public DbSet<Configuration> Configuration { get; set; }
        public DbSet<Menu> Menu { get; set; }
        public DbSet<Comment> Comment { get; set; }
        public DbSet<Settings> Settings { get; set; }

        //public static Database Db { get; private set; }

        public static void Init()
        {
            DirectoryInfo dir;
            if (!Directory.Exists(Paths.Data))
                //    Directory.Delete(Paths.Data, true);
                dir = Directory.CreateDirectory(Paths.Data);
            else
                dir = new DirectoryInfo(Paths.Data);

            //if(!dir.GetDirectories)
            dir.CreateSubdirectory("files");
            dir.CreateSubdirectory("cache");

            using (var context = new Database())
            {
                context.Database.Migrate();

                //context.SaveChanges();

                if (!context.Configuration.Any())
                {
                    //new DB
                    context.Configuration.Add(new Configuration() { Id = Guid.Empty, Version = 1 });
                    context.Settings.Add(new Settings() { Id = Guid.Empty, DisqusShortName = "", SiteName = "DownSite", ShowComments = true, AllowWriteComments = true, ShowLogin = false, ArticlesPerPage = 10, SiteDescription = "Test", SiteUrl = "" });

                    //context.SaveChanges();

                    Guid pic1 = Guid.NewGuid(), pic2 = Guid.NewGuid(), pic3 = Guid.NewGuid();
                    FileInfo tmp = new FileInfo(Path.Combine(".", "acf7eede5be5aa69.jpg"));
                    if (tmp.Exists)
                        DownSite.Image.Save(pic1, context, MimeTypes.ImageJpg, tmp.Name, tmp.OpenRead());
                    tmp = new FileInfo(Path.Combine(".", "e3939e928899550f.jpg"));
                    if (tmp.Exists)
                        DownSite.Image.Save(pic2, context, MimeTypes.ImageJpg, tmp.Name, tmp.OpenRead());
                    tmp = new FileInfo(Path.Combine(".", "d552c86d2ebd373c.webm"));
                    if (tmp.Exists)
                        DownSite.Image.Save(pic3, context, "video/webm", tmp.Name, tmp.OpenRead());

                    Guid person1;
                    context.User.Add(new User() { Id = person1 = Guid.NewGuid(), UserName = "admin", Password = Util.SHA1("downsite"), FirstName = "Firstname", LastName = "Lastname" });
                    context.User.Add(new User() { Id = Guid.NewGuid(), UserName = "cody1", FirstName = "cody1", LastName = "test" });
                    context.User.Add(new User() { Id = Guid.NewGuid(), UserName = "cody2", FirstName = "cody2", LastName = "test" });

                    string content = string.Format(@"-CONTENT-

![](/image/{0})
![video](/image/{1})
![youtube](cxBcHLylFbw)", pic1.ToString().Replace("-", "") + ".jpg", pic3.ToString().Replace("-", "") + ".webm");

                    Guid article;
                    context.Article.Add(new Article() { Id = article = Guid.NewGuid(), ShowInBlog = true, Content = content, AuthorId = person1, Created = DateTime.Now, Title = "page1", VersionGroup = Guid.NewGuid() });

                    context.Tag.Add(new Tag() { ArticleId = article, Name = "a" });
                    context.Tag.Add(new Tag() { ArticleId = article, Name = "b" });
                    context.Tag.Add(new Tag() { ArticleId = article, Name = "c" });


                    context.Article.Add(new Article()
                    {
                        Id = Guid.NewGuid(),
                        AuthorId = person1,
                        ShowInMenu = true,
                        Content = @"#MenuItem 1

<pre><code>blablalb
rhgb
regj
rejgn
</code></pre>",
                        Created = DateTime.Now,
                        Title = "MenuItem 1",
                        VersionGroup = Guid.NewGuid()
                    });
                    context.Article.Add(new Article() { Id = Guid.NewGuid(), AuthorId = person1, ShowInMenu = true, Content = "#MenuItem 2", Created = DateTime.Now, Title = "MenuItem 2", VersionGroup = Guid.NewGuid() });

                    for (int i = 0; i < 20; ++i)
                    {
                        Guid id;
                        context.Article.Add(new Article() { Id = id = Guid.NewGuid(), AuthorId = person1, ShowInBlog = true, Content = "blog" + i, Created = DateTime.Now, Title = "blog" + i, VersionGroup = Guid.NewGuid() });
                        context.Tag.Add(new Tag() { ArticleId = id, Name = "c" });
                    }

                    context.Comment.Add(new Comment() { Id = Guid.NewGuid(), ArticleId = article, Content = "blabla1", Created = DateTime.Now, Name = "anon" });
                    context.Comment.Add(new Comment() { Id = Guid.NewGuid(), ArticleId = article, Content = "blabla2", Created = DateTime.Now, Name = "anon" });

                    context.Menu.Add(new Menu() { Id = Guid.NewGuid(), Caption = "Blog", Link = "/blog/page1.html" });
                    
                }

                context.SaveChanges();
            }

            if (FileCache.CacheDirExists())
            {
                foreach (var f in FileCache.GetCacheDir().GetFiles("*.tmp"))
                {
                    try
                    {
                        f.Delete();
                    }
                    catch
                    {
                    }
                }
            }
        }

    }
}