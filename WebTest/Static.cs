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


namespace WebTest
{
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
                file.CopyTo(temppath, false);
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

        public static void Generate()
        {
            Generate(new DirectoryInfo("output"));
        }

        public static void Generate(DirectoryInfo output)
        {
            if (output.Exists)
                output.Delete(true);
            output.Create();

            DirectoryCopy("js", Path.Combine(output.FullName, "js"), true);
            DirectoryCopy("css", Path.Combine(output.FullName, "css"), true);
            DirectoryCopy("fonts", Path.Combine(output.FullName, "fonts"), true);
            DirectoryCopy("data/", Path.Combine(output.FullName, "fonts"), true);

            db = Database.OpenDbConnection(Path.Combine("data", "db.sqlite3"));
            
            var image_dir = output.CreateSubdirectory("Image");

            foreach (FileInfo fi in new DirectoryInfo(Path.Combine("data", "files")).GetFiles())
            {
                File.Copy(fi.FullName, Path.Combine(image_dir.FullName, fi.Name.Replace("-","")));
            }
            foreach (FileInfo fi in new DirectoryInfo(Path.Combine("data", "cache")).GetFiles())
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

            var article_dir = output.CreateSubdirectory("Article");
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

            GenerateBlog(new BlogInfo() { Articles = articles.Where(x => x.ShowInBlog).ToArray(), TotalArticleCount = articles.Count() }, output);
        }

        static void GenerateBlog(BlogInfo blog, DirectoryInfo output)
        {
            var page = RazorFormat.Instance.GetViewPage("Blog");

            foreach (var a in blog.Articles)
            {
                a.Content = Blog.Preview(a.Content);
            }

            string html = RazorFormat.Instance.RenderToHtml(page, blog, "Standard");

            string content = html.Replace("\"/image/", "\"Image/");
            content = content.Replace("\"/article/id/", "\"article/");
            content = content.Replace("\"/Article/Id/", "\"article/");
            content = content.Replace("\"/article/Id/", "\"article/");
            content = content.Replace("\"/js/", "\"js/");
            content = content.Replace("\"/css/", "\"css/");
            content = content.Replace("\"//", "\"http://");
            content = content.Replace("\"/\"", "../index.html");

            using (StreamWriter sw = new StreamWriter(Path.Combine(output.FullName, "index.html")))
            {
                sw.Write(content);
            }
        }

        static void GenerateArticle(Article a, StreamWriter sw)
        {
            var page = RazorFormat.Instance.GetViewPage("Article");
            a.Html = new CustomMarkdownSharp.Markdown() { Static = true }.Transform(a.Content);
            string html = RazorFormat.Instance.RenderToHtml(page, a, "Standard");
            a.Html = null;

            string content = html.Replace("\"/image/", "\"../image/");
            content = content.Replace("\"/article/id/", "\"article/");
            content = content.Replace("\"/Article/Id/", "\"article/");
            content = content.Replace("\"/article/Id/", "\"article/");
            content = content.Replace("\"/js/", "\"../js/");
            content = content.Replace("\"/css/", "\"../css/");
            content = content.Replace("\"//", "\"http://");
            content = content.Replace("\"/\"", "../index.html");
            sw.Write(content);
        }
    }
}
