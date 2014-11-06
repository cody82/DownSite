using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.OrmLite.Tests;
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
using HtmlAgilityPack;


namespace WebTest
{
    [Route("/article")]
    [Route("/article/Id/{Id}")]
    [Route("/article/Title/{Title}")]
    public class Article : IReturn<Article>
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public Guid VersionGroup { get; set; }

        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        [References(typeof(User))]
        public Guid AuthorId { get; set; }

        public string Title { get; set; }
        public string Content { get; set; }

        [Reference]
        public User Author { get; set; }

        [Reference]
        public List<Category> Category { get; set; }

        [Ignore]
        public string AuthorName { get; set; }

        public string CategoryString()
        {
            if (Category == null)
                return "";
            return Category.Select(x => x.Name).Join(", ");
        }

        public string ContentHtml()
        {
            return new CustomMarkdownSharp.Markdown().Transform(Content);
        }
    }

    public class Category
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Index(false), References(typeof(Article))]
        public Guid ArticleId { get; set; }

        [Index]
        public string Name { get; set; }
    }

    [Route("/Articles")]
    public class ArticleListRequest : IReturn<Article[]>
    {
    }

    [Route("/Blog/")]
    public class BlogRequest : IReturn<Article[]>
    {
    }

    public class Blog : Service
    {
        public object Get(BlogRequest request)
        {
            var blog = PersonRepository.db.LoadSelect<Article>().OrderBy(x => x.Created);

            foreach (var b in blog)
            {
                b.Content = Preview(b.Content);
                //var user = PersonRepository.db.Single<User>(x => x.Id == b.AuthorId);
                if (b.Author != null)
                    b.AuthorName = b.Author.UserName;
                else
                    b.AuthorName = "unknown author";
                b.Author = null;
            }

            return new HttpResult(blog.ToArray())
            {
                View = "Blog",
                Template = "Default",
            };
            //return blog.ToArray();
        }

        string Preview(string markdown)
        {
            string html = new CustomMarkdownSharp.Markdown().Transform(markdown);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(html);

            string text = new HtmlToText().ConvertHtml(html);


            if (text.Length > 50)
                text = text.Substring(0, 50);

            bool br_inserted = false;

            {
                var imgs = doc.DocumentNode.SelectNodes("//img");
                if (imgs != null)
                {
                    foreach (HtmlNode img in imgs)
                    {
                        string url = img.GetAttributeValue("src", null);
                        if (url != null && url.Length > 1 && url[0] == '/' && url[1] != '/')
                        {
                            if (url.StartsWith("/Image"))
                            {
                                if (!url.EndsWith("?thumb"))
                                    url += "?thumb";
                                if (!br_inserted)
                                {
                                    text += "<br/>";
                                    br_inserted = true;
                                }
                                text += string.Format(@"<img src=""{0}""/>", url);
                            }
                        }
                    }
                }
            }

            {
                var videos = doc.DocumentNode.SelectNodes("//video");
                if (videos != null)
                {
                    foreach (HtmlNode video in videos)
                    {
                        string url = video.GetAttributeValue("src", null);
                        if (url != null && url.Length > 1 && url[0] == '/' && url[1] != '/')
                        {
                            if (url.StartsWith("/Image"))
                            {
                                if (!url.EndsWith("?thumb"))
                                    url += "?thumb";
                                if (!br_inserted)
                                {
                                    text += "<br/>";
                                    br_inserted = true;
                                }
                                text += string.Format(@"<img src=""{0}""/>", url);
                            }
                        }
                    }
                }
            }

            return text;
        }
    }

    public class ArticleService : Service
    {
        public object Get(Article request)
        {
            Guid guid;
            if (Guid.TryParse(request.Title, out guid))
                request.Id = guid;

            var html = Request.AbsoluteUri.EndsWith("?html");
            if (html)
                return GetHtml(request);

            var a = PersonRepository.db.LoadSelect<Article>(x => x.Title == request.Title || x.Id == request.Id).SingleOrDefault();

            //return a;
            return new HttpResult(a)
            {
                View = "Article",
                Template = "Default",
            };
        }

        public Article[] Get(ArticleListRequest request)
        {
            var list = PersonRepository.db.LoadSelect<Article>();
            return list.ToArray();
        }

        [Authenticate]
        public object Delete(Article article)
        {
            if (article.Id == Guid.Empty)
                return new HttpResult(HttpStatusCode.NotFound, "no such article.");

            PersonRepository.db.Delete<Category>(x => x.ArticleId == article.Id);

            int count = PersonRepository.db.Delete<Article>(x => x.Id == article.Id);
            if (count == 0)
                return new HttpResult(HttpStatusCode.NotFound, "no such article.");

            return article;
        }

        public static string GetHtmlTest()
        {
            var obj = new ArticleService().GetHtml(new Article() { Id = new Guid("68b74829-bf4f-4c8e-8fd0-380ee6a0fa1c") });
            if (obj is string)
                return (string)obj;
            return "error";
        }

        //[AddHeader(ContentType = MimeTypes.Html)]
        public object GetHtml(Article request)
        {
            var preview = Request != null ? Request.AbsoluteUri.EndsWith("?preview") : false;

            //var article = PersonRepository.db.Single<Article>(x => x.Title == request.Title || x.Id == request.Id);
            var article = PersonRepository.db.LoadSelect<Article>(x => x.Title == request.Title || x.Id == request.Id).SingleOrDefault();
            if (article == null)
                return new HttpResult(HttpStatusCode.NotFound, "no such article.");

            //var author = PersonRepository.db.Single<User>(x => x.Id == article.AuthorId);
            //var category = string.Join(",", PersonRepository.db.Select<Category>(x => x.ArticleId == article.Id).OrderBy(x => x.Name).Select(x => x.Name));
            var category = article.Category != null ? string.Join(",", article.Category.OrderBy(x => x.Name).Select(x => x.Name)) : "";
            if (article.Author != null)
                article.AuthorName = article.Author.UserName;
            else
                article.AuthorName = "unknown author";

            //var parts = PersonRepository.db.Select<Part>(x => x.ArticleId == article.Id).OrderBy(x => x.Number).ToArray();

            string header = "<h1>" + article.Title + "</h1><p>" + (article.AuthorName ?? "unknown author") + ", " + article.Created + " [" + category + "]</p>";
            string html = "";
            string previewtext = "";

            if (!string.IsNullOrWhiteSpace(article.Content))
            {
                if (preview)
                    previewtext += new HtmlToText().ConvertHtml(article.Content);
                else
                    html += new CustomMarkdownSharp.Markdown().Transform(article.Content);
            }

            const int maxpreview = 100;
            if (!string.IsNullOrWhiteSpace(previewtext))
            {
                if(previewtext.Length > maxpreview)
                    previewtext = previewtext.Substring(0, maxpreview) + "...";
                previewtext += "</br>";
            }

            html = header + previewtext + html;

            return html;
        }

        [Authenticate]
        public object Put(Article article)
        {
            if (PersonRepository.db.Exists<Article>(x => x.Title == article.Title))
                return new HttpResult(HttpStatusCode.NotFound, "article with that title already exists.");
            article.Id = Guid.NewGuid();
            article.Created = article.Modified = DateTime.Now;

            var session = GetSession();
            if (session.IsAuthenticated)
                article.AuthorId = PersonRepository.db.Single<User>(x => x.UserName == session.UserAuthName).Id;

            PersonRepository.db.Insert<Article>(article);

            return article;
        }

        [Authenticate]
        public object Post(Article article)
        {
            var session = GetSession();
            if (session.IsAuthenticated)
                article.AuthorId = PersonRepository.db.Single<User>(x => x.UserName == session.UserAuthName).Id;

            var original = PersonRepository.db.LoadSelect<Article>(x => x.Id == article.Id).Single();
            article.Category = article.Category.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();


            article.Modified = DateTime.Now;

            if (original == null)
                throw new Exception("BUG");

            if(article.AuthorId == Guid.Empty)
                article.AuthorId = original.AuthorId;
            article.Created = original.Created;

            PersonRepository.db.Update<Article>(article);

            foreach (var c in article.Category.Where(x => original == null || original.Category == null || !original.Category.Any(y => y.Name == x.Name)))
            {
                c.ArticleId = article.Id;
                PersonRepository.db.Insert<Category>(c);
            }

            if (original.Category != null)
            {
                foreach (var c in original.Category.Where(x => !article.Category.Any(y => y.Name == x.Name)))
                {
                    c.ArticleId = article.Id;
                    PersonRepository.db.Delete<Category>(c);
                }
            }

            return article.Id;
        }
    }
}
