using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using ServiceStack;
using Funq;
using System.Data;
using System.IO;
using System.Drawing;
using ServiceStack.Web;
using System.Web;
using System.Threading;
using ServiceStack.Auth;
using ServiceStack.Caching;
using System.Diagnostics;
using System.Security.Cryptography;
using ServiceStack.Razor;
using ServiceStack.Logging;


namespace DownSite
{

    [Route("/generator")]
    public class GeneratorRequest
    {
    }

    [Authenticate]
    public class GeneratorService : Service
    {
        public static string Output;
        public static string Data;
        public static bool Delete;

        public object Get(GeneratorRequest request)
        {
            Static.Generate(Output, Data, Delete);
            return new HttpResult(HttpStatusCode.OK, "Page was generated");
        }
    }

    public static class Static
    {
        static IDbConnection db;


        private static void DirectoryCopy(
            string sourceDirName, string destDirName, bool copySubDirs)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            // If the source directory does not exist, throw an exception.
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory does not exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }


            // Get the file contents of the directory to copy.
            FileInfo[] files = dir.GetFiles();

            foreach (FileInfo file in files)
            {
                // Create the path to the new copy of the file.
                string temppath = Path.Combine(destDirName, file.Name);

                // Copy the file.
                file.CopyTo(temppath, true);
            }

            // If copySubDirs is true, copy the subdirectories.
            if (copySubDirs)
            {

                foreach (DirectoryInfo subdir in dirs)
                {
                    // Create the subdirectory.
                    string temppath = Path.Combine(destDirName, subdir.Name);

                    // Copy the subdirectories.
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        public static void Generate(string output, string data, bool delete)
        {
            Generate(new DirectoryInfo(output), new DirectoryInfo(data), delete);
        }

        public static void Generate(DirectoryInfo output, DirectoryInfo data, bool delete)
        {
            if (output.Exists && delete)
            {
                foreach(var d in output.GetDirectories())
                    d.Delete(true);
                foreach (var f in output.GetFiles())
                    f.Delete();
            }
            else
                output.Create();

            DirectoryCopy("js", Path.Combine(output.FullName, "js"), true);
            DirectoryCopy("css", Path.Combine(output.FullName, "css"), true);
            DirectoryCopy("fonts", Path.Combine(output.FullName, "fonts"), true);

            db = Database.OpenDbConnection(Path.Combine(data.FullName, "db.sqlite3"));

            var image_dir = output.CreateSubdirectory("image");
            var blog_dir = output.CreateSubdirectory("blog");

            foreach (FileInfo fi in new DirectoryInfo(Path.Combine(data.FullName, "files")).GetFiles())
            {
                File.Copy(fi.FullName, Path.Combine(image_dir.FullName, fi.Name.Replace("-","")));
            }
            foreach (FileInfo fi in new DirectoryInfo(Path.Combine(data.FullName, "cache")).GetFiles())
            {
                File.Copy(fi.FullName, Path.Combine(image_dir.FullName, fi.Name.Replace("-","")));
            }

            /*var images = db.Select<Image>();
            foreach (var img in images)
            {
                string path = Path.Combine(image_dir.FullName, img.Id.ToString().Replace("-","")// + "." + UploadService.MimeTypeExtension(img.MimeType));
                string source = UploadService.GetFileInfo(img.Id).FullName;

                File.Copy(source, path, true);
            }*/

            var article_dir = output.CreateSubdirectory("article");
            var articles = db.LoadSelect<Article>();
            foreach (var a in articles)
            {
                string path = Path.Combine(article_dir.FullName, a.Id.ToString().Replace("-","") + ".html");

                if (a.Author != null)
                    a.AuthorName = a.Author.UserName;
                using (StreamWriter sw = new StreamWriter(path, false))
                {
                    GenerateArticle(a, sw);
                }
            }

            GenerateBlog(articles.Where(x => x.ShowInBlog).ToArray(), blog_dir);

            var index = RazorFormat.Instance.GetContentPage("");
            string html = RazorFormat.Instance.RenderToHtml(index, null, "Standard");


            html = FixLinks(html);
            using (StreamWriter sw = new StreamWriter(Path.Combine(output.FullName, "index.html")))
            {
                sw.Write(html);
            }
        }
        static string FixLinks(string html, string root = "./")
        {
            html = html.Replace("\"/article/id/", "\"/article/");
            html = html.Replace("\"/Article/Id/", "\"/article/");
            html = html.Replace("\"/article/Id/", "\"/article/");
            html = html.Replace(" /image/", "../image/");

            html = html.Replace("\"/", "\"" + root);
            html = html.Replace("\"" + root + "\"", "\"" + root + "index.html\"");

            return html;
        }

        static void GenerateBlog(Article[] blog, DirectoryInfo output)
        {
            var page = RazorFormat.Instance.GetViewPage("Blog");

            var config = Settings.Load();

            var all_tags = db.Select<Tag>().ToArray();
            var tags = all_tags.Select(x => x.Name).Distinct().ToList();
            tags.Add(null);

            foreach (var a in blog)
            {
                a.Content = Blog.Preview(a.Content);
            }

            foreach (string tag in tags)
            {
                var articles = blog.Where(x => tag == null || all_tags.Any(y => y.ArticleId == x.Id && y.Name == tag)).OrderByDescending(x => x.Created).ToArray();
                
                BlogInfo bloginfo = new BlogInfo()
                {
                    Tag = tag,
                    TotalArticleCount = articles.Length
                };

                for (int i = 1; i <= bloginfo.PageCount; ++i)
                {
                    bloginfo.Page = i;
                    bloginfo.Articles = articles.Skip((i - 1) * config.ArticlesPerPage).Take(config.ArticlesPerPage).ToArray();
                    string html = RazorFormat.Instance.RenderToHtml(page, bloginfo, "Standard");

                    string content = FixLinks(html, "../");

                    string filename = "page" + i;
                    if (tag != null)
                    {
                        filename += "!tag_" + tag;
                    }
                    filename += ".html";

                    using (StreamWriter sw = new StreamWriter(Path.Combine(output.FullName, filename)))
                    {
                        sw.Write(content);
                    }
                }
            }


        }

        static void GenerateArticle(Article a, StreamWriter sw)
        {
            var page = RazorFormat.Instance.GetViewPage("Article");
            a.Html = new CustomMarkdownSharp.Markdown() { Static = true }.Transform(a.Content);
            string html = RazorFormat.Instance.RenderToHtml(page, a, "Standard");
            a.Html = null;
            string content = FixLinks(html, "../");

            sw.Write(content);
        }
    }
}
