﻿using System;
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
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.OrmLite;
using ServiceStack;

namespace DownSite
{
    public class TableInfo
    {
        public int cid { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public bool notnull { get; set; }
        public bool pk { get; set; }

    }
    public class Database
    {
        public const int Version = 5;

        public static void Migrate(int from, int to)
        {
            if (from == to)
                return;
            if (from > to)
                throw new Exception("BUG");
            if(to - from > 1)
            {
                for(int i = from; i < to; ++i)
                {
                    Migrate(i, i + 1);
                }
                return;
            }

            Console.WriteLine("Migrating database to version " + to + "...");
            using(var t = Db.BeginTransaction())
            {
                switch(to)
                {
                    case 1:
                        break;
                    case 2:
                        Migrate002();
                        break;
                    case 3:
                        Migrate003();
                        break;
                    case 4:
                        Migrate004();
                        break;
                    case 5:
                        throw new Exception("Not supported");
                    default:
                        throw new Exception("BUG");
                }

                Db.Update<Configuration>(new { Version = to});
                t.Commit();
            }
        }

        static void Migrate002()
        {
            DropColumn("User", "Age");
        }

        static void Migrate003()
        {
            Db.ExecuteNonQuery("ALTER TABLE \"User\" ADD COLUMN \"Email\" VARCHAR;");
        }

        static void Migrate004()
        {
            Db.ExecuteNonQuery("ALTER TABLE \"Configuration\" ADD COLUMN \"ArticlesPerPage\" INTEGER NOT NULL DEFAULT 10;");
            Db.ExecuteNonQuery("ALTER TABLE \"Configuration\" ADD COLUMN \"ShowLogin\" BOOLEAN NOT NULL DEFAULT FALSE;");
            //Db.ExecuteNonQuery("UPDATE \"Configuration\" SET \"ShowLogin\" = FALSE, \"ArticlesPerPage\" = 10;");
        }

        public static string[] GetColumns(string table)
        {
            var cmd = Db.CreateCommand();
            cmd.CommandText = "PRAGMA table_info('"+table+"')";
            var reader = cmd.ExecuteReader();
            List<string> list = new List<string>();
            while(reader.Read())
            {
                string column = reader.GetString(1);
                list.Add(column);
            }
            return list.ToArray();
        }

        public static void DropColumn(string table, string column)
        {
            var cmd = Db.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = '"+table+"'";
            var reader = cmd.ExecuteReader();
            reader.Read();
            string sql = reader.GetString(0);
            reader.Dispose();
            sql = sql.Replace("\n", "");
            string[] lines = sql.Split(',');

            lines = lines.Where(x => !x.Contains("\"" + column + "\"") && !x.Contains("`" + column + "`")).ToArray();

            Db.ExecuteNonQuery("ALTER TABLE '" + table + "' RENAME to '" + table + "_tmp'");
            sql = lines.Join(",\n");
            Db.ExecuteNonQuery(sql);

            sql = string.Format("INSERT INTO '{0}' SELECT {1} FROM '{2}';", table, GetColumns(table).Join(","), table + "_tmp");
            Db.ExecuteNonQuery(sql);

            Db.ExecuteNonQuery("DROP TABLE \"" + table +"_tmp\";");
        }
        /*public static TableInfo[] GetTableInfo(string table)
        {

            var cmd = Db.CreateCommand();
            cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'User'";
            var reader = cmd.ExecuteReader();
            List<TableInfo> list = new List<TableInfo>();
            while(reader.Read())
            {
                string column = reader.GetString(1);
                var obj = reader.GetDto<TableInfo>();
            }

            return null;
        }*/


        public static IDbConnection OpenDbConnection(string connString)
        {
            OrmLiteConfig.DialectProvider = SqliteOrmLiteDialectProvider.Instance;
            return connString.OpenDbConnection();
        }


        public static IDbConnection Db { get; private set; }

        public static void Init()
        {
            string dbfile = Path.Combine("data", "db.sqlite3");
            bool init = !File.Exists(dbfile);
            if (init)
            {
                if (Directory.Exists("data"))
                    Directory.Delete("data", true);
                var dir = Directory.CreateDirectory("data");
                dir.CreateSubdirectory("files");
                dir.CreateSubdirectory("cache");

                Db = Database.OpenDbConnection(dbfile);
                Db.CreateTable<User>(true);
                Db.CreateTable<Image>(true);
                Db.CreateTable<Article>(true);
                Db.CreateTable<Tag>(true);
                Db.CreateTable<Settings>(true);
                Db.CreateTable<Configuration>(true);
                Db.CreateTable<Comment>(true);
                Db.CreateTable<Menu>(true);

                Db.Insert<Configuration>(new Configuration() { Id = Guid.Empty, Version = Version });
                Db.Insert<Settings>(new Settings() { Id = Guid.Empty, SiteName = "DownSite", ShowComments = true, AllowWriteComments = true, ShowLogin = false, ArticlesPerPage = 10, SiteDescription = "Test", SiteUrl = "" });

                Db.ExecuteSql(@"CREATE UNIQUE INDEX tag_unique on Tag(ArticleId, Name);");


                Guid pic1 = Guid.NewGuid(), pic2 = Guid.NewGuid(), pic3 = Guid.NewGuid();
                Image.Save(pic1, Db, MimeTypes.ImageJpg, "acf7eede5be5aa69.jpg", new FileInfo("acf7eede5be5aa69.jpg").OpenRead());
                Image.Save(pic2, Db, MimeTypes.ImageJpg, "e3939e928899550f.jpg", new FileInfo("e3939e928899550f.jpg").OpenRead());
                Image.Save(pic3, Db, "video/webm", "d552c86d2ebd373c.webm", new FileInfo("d552c86d2ebd373c.webm").OpenRead());

                Guid person1;
                Db.Insert<User>(new User() { Id = person1 = Guid.NewGuid(), UserName = "admin", Password = Util.SHA1("downsite"), FirstName = "Firstname", LastName = "Lastname" });
                Db.Insert<User>(new User() { Id = Guid.NewGuid(), UserName = "cody1", FirstName = "cody1", LastName = "test" });
                Db.Insert<User>(new User() { Id = Guid.NewGuid(), UserName = "cody2", FirstName = "cody2", LastName = "test" });

                string content = string.Format(@"-CONTENT-

![](/image/{0})
![video](/image/{1})
![youtube](cxBcHLylFbw)", pic1.ToString().Replace("-", "") + ".jpg", pic3.ToString().Replace("-", "") + ".webm");

                Guid article;
                Db.Insert<Article>(new Article() { Id = article = Guid.NewGuid(), ShowInBlog = true, Content = content, AuthorId = person1, Created = DateTime.Now, Title = "page1", VersionGroup = Guid.NewGuid() });

                Db.Insert<Tag>(new Tag() { ArticleId = article, Name = "a" });
                Db.Insert<Tag>(new Tag() { ArticleId = article, Name = "b" });
                Db.Insert<Tag>(new Tag() { ArticleId = article, Name = "c" });


                Db.Insert<Article>(new Article()
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
                Db.Insert<Article>(new Article() { Id = Guid.NewGuid(), AuthorId = person1, ShowInMenu = true, Content = "#MenuItem 2", Created = DateTime.Now, Title = "MenuItem 2", VersionGroup = Guid.NewGuid() });

                for (int i = 0; i < 20; ++i)
                {
                    Guid id;
                    Db.Insert<Article>(new Article() { Id = id = Guid.NewGuid(), AuthorId = person1, ShowInBlog = true, Content = "blog" + i, Created = DateTime.Now, Title = "blog" + i, VersionGroup = Guid.NewGuid() });
                    Db.Insert<Tag>(new Tag() { ArticleId = id, Name = "c" });
                }

                Db.Insert<Comment>(new Comment() { Id = Guid.NewGuid(), ArticleId = article, Content = "blabla1", Created = DateTime.Now, Name = "anon" });
                Db.Insert<Comment>(new Comment() { Id = Guid.NewGuid(), ArticleId = article, Content = "blabla2", Created = DateTime.Now, Name = "anon" });

                Db.Insert<Menu>(new Menu() { Id = Guid.NewGuid(), Caption = "Blog", Link = "/blog/page1.html" });

                var a = Db.LoadSingleById<Article>(article);
                if (a.Category == null)
                    throw new Exception("BUG");
                a = Db.LoadSelect<Article>(y => y.Id == article).First();
                if (a.Category == null)
                    throw new Exception("BUG");
            }
            else
            {
                Db = Database.OpenDbConnection(dbfile);

                int version = Configuration.Load().Version;
                if(version < Version)
                {
                    Migrate(version, Version);
                }
                else if(version > Version)
                {
                    throw new Exception(string.Format("Database version too high. ({0} vs. {1})", version, Version));
                }
            }
            foreach (var f in new DirectoryInfo(Path.Combine("data", "cache")).GetFiles("*.tmp"))
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