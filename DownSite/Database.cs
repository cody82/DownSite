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
        public const int Version = 7;

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
                        throw new Exception("Not supported.");
                        // Databases version <5 must be recreated.
                    case 6:
                        Migrate006();
                        break;
                    case 7:
                        Migrate007();
                        break;
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

        static void Migrate006()
        {
            Db.ExecuteNonQuery("ALTER TABLE \"Settings\" ADD COLUMN \"DisqusShortName\" TEXT NOT NULL DEFAULT '';");
            Db.ExecuteNonQuery("ALTER TABLE \"Settings\" ADD COLUMN \"Disqus\" BOOLEAN NOT NULL DEFAULT FALSE;");
        }
        
        static void Migrate007()
        {
            //Drop all foreign keys
            var tables = GetTableNames();
            foreach (string table in tables)
            {
                Database.DropColumn(table, null);
            }
        }

        public static string[] GetColumnNames(string table)
        {
            return GetColumns(table).Select(x => x.Name).ToArray();
        }

        public static string[] GetTableNames()
        {
            var cmd = Db.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name != 'sqlite_sequence'";
            using (var reader = cmd.ExecuteReader())
            {
                var list = new List<string>();
                while (reader.Read())
                {
                    list.Add(reader.GetString(0));
                }
                return list.ToArray();
            }
        }
        
        public static Column[] GetColumns(string table)
        {
            var cmd = Db.CreateCommand();
            cmd.CommandText = "PRAGMA table_info('" + table + "')";
            using (var reader = cmd.ExecuteReader())
            {
                var list = new List<Column>();
                while (reader.Read())
                {
                    object def = reader.GetValue(4);
                    var c = new Column()
                    {
                        Name = reader.GetString(1),
                        Type = reader.GetString(2),
                        NotNull = reader.GetInt32(3),
                        Default = def is DBNull ? null : (string)def,
                        PrimaryKey = reader.GetInt32(5)
                    };
                    list.Add(c);
                }
                return list.ToArray();
            }
        }

        public class ForeignKey
        {
            public string OwnerTable { get; set; }
            public string TargetTable { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public string OnUpdate { get; set; }
            public string OnDelete { get; set; }
            public string Match { get; set; }
        }

        public class Index
        {
            public string Name { get; set; }
            public string Sql { get; set; }
            public string Column { get; set; }
            public int Unique { get; set; }
        }

        public class Column
        {
            public string Sql
            {
                get
                {
                    return string.Format("{0} {1}{2}{3}{4}", Name, Type, NotNull > 0 ? " NOT NULL" : "", (Default != "NULL" && Default != null) ? " DEFAULT " + Default : "", PrimaryKey > 0 ? " PRIMARY KEY" : "");
                }
            }
            public string Name { get; set; }
            public string Type { get; set; }
            public int NotNull { get; set; }
            public string Default { get; set; }
            public int PrimaryKey { get; set; }
        }

        public static Index[] GetIndexes(string table)
        {
            var cmd = Db.CreateCommand();
            cmd.CommandText = "SELECT name, sql FROM sqlite_master WHERE type = 'index' AND tbl_name = '" + table + "' AND sql != ''";
            var list = new List<Index>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var c = new Index()
                    {
                        Name = reader.GetString(0),
                        Sql = reader.GetString(1),
                    };
                    list.Add(c);
                }
            }

            foreach(var index in list)
            {
                using (cmd = Db.CreateCommand())
                {
                    cmd.CommandText = "PRAGMA index_info('" + index.Name + "')";
                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.Read();
                        index.Name = reader.GetString(2);
                    }
                }
            }

            using (cmd = Db.CreateCommand())
            {
                cmd.CommandText = "PRAGMA index_list('" + table + "')";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.GetString(1);
                        int unique = reader.GetInt32(2);

                        var index = list.SingleOrDefault(x => x.Name == name);
                        if (index != null)
                        {
                            index.Unique = unique;
                        }
                    }
                }
            }

            return list.ToArray();
        }
        
        public static ForeignKey[] GetAllForeignKeys()
        {
            var tables = GetTableNames();
            var fk = new List<ForeignKey>();
            foreach(string table in tables)
            {
                fk.AddRange(GetForeignKeys(table));
            }
            return fk.ToArray();
        }

        public static ForeignKey[] GetForeignKeys(string table)
        {
            var cmd = Db.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_key_list('" + table + "')";
            using (var reader = cmd.ExecuteReader())
            {
                var list = new List<ForeignKey>();
                while (reader.Read())
                {
                    var c = new ForeignKey()
                    {
                        OwnerTable = table,
                        TargetTable = reader.GetString(2),
                        From = reader.GetString(3),
                        To = reader.GetString(4),
                        OnUpdate = reader.GetString(5),
                        OnDelete = reader.GetString(6),
                        Match = reader.GetString(7)
                    };
                    list.Add(c);
                }
                return list.ToArray();
            }
        }

        public static void DropColumn(string table, string column)
        {
            var columns = GetColumns(table);
            var indexes = GetIndexes(table);
            string tmp_table = table + "_tmp";
            var newcolumns = columns.Where(x => x.Name != column).ToArray();
            //var newindexes = indexes.Where(x => x.Column != column).ToArray();

            Db.ExecuteNonQuery("ALTER TABLE '" + table + "' RENAME to '" + tmp_table +"'");

            CreateTable(table, newcolumns);

            CopyTableData(tmp_table, table, newcolumns);

            Db.ExecuteNonQuery("DROP TABLE '" + tmp_table + "'");

            foreach(var index in indexes)
            {
                CreateIndex(index);
            }
        }

        public static void CopyTableData(string from, string to, Column[] columns)
        {
            string tmp = string.Format("INSERT INTO {0} SELECT {1} FROM {2}", to, columns.Select(x => x.Name).Aggregate((a, b) => a + ", " + b), from);
            
            Db.ExecuteNonQuery(tmp);
        }

        public static void CreateTable(string name, Column[] columns)
        {
            string tmp = "CREATE TABLE " + name + "(";

            tmp += columns.Select(x => x.Sql).Aggregate((a, b) => a + ", " + b);

            tmp += ")";

            Db.ExecuteNonQuery(tmp);
        }


        public static void CreateIndex(Index index)
        {
            Db.ExecuteNonQuery(index.Sql);
        }



        public static IDbConnection OpenDbConnection(string connString)
        {
            OrmLiteConfig.DialectProvider = SqliteOrmLiteDialectProvider.Instance;
            var connection = connString.OpenDbConnection();
            connection.ExecuteNonQuery("PRAGMA foreign_keys = OFF");
            return connection;
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
                Db.Insert<Settings>(new Settings() { Id = Guid.Empty, DisqusShortName = "", SiteName = "DownSite", ShowComments = true, AllowWriteComments = true, ShowLogin = false, ArticlesPerPage = 10, SiteDescription = "Test", SiteUrl = "" });

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