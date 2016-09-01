﻿using ServiceStack.DataAnnotations;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Data;
using System.IO;
using System.Drawing;
using ServiceStack.Web;
using System.Web;
using System.Threading;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace DownSite
{
    public class Comment
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string Content { get; set; }
        public DateTime Created { get; set; }
        public string Name { get; set; }

        [Ignore]
        public string Link
        {
            get
            {
                return "/article/" + ArticleId.ToString().Replace("-", "") + ".html";
            }
        }

        [References(typeof(Article))]
        public Guid ArticleId { get; set; }

        [Reference]
        public Article Article { get; set; }

        public static Comment[] LoadLatest(int n)
        {
            return Database.Db.Select<Comment>().OrderByDescending(x => x.Created).Take(n).ToArray();
        }
    }

    [Route("/comment")]
    public class CommentService : Controller
    {
        [HttpPut]
        public object Put(Comment c)
        {
            c.Created = DateTime.Now;
            c.Id = Guid.NewGuid();
            Database.Db.Insert<Comment>(c);
            return c;
        }
    }

    //[Route("/article")]
    //[Route("/article/{Id}")]
    //[Route("/article/{Id}.html")]
    public class Article// : IReturn<Article>
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

        public bool ShowInMenu { get; set; }
        public bool ShowInBlog { get; set; }

        [Reference]
        public User Author { get; set; }

        [Reference]
        public List<Tag> Category { get; set; }

        [Reference]
        public List<Comment> Comment { get; set; }

        [Ignore]
        public string AuthorName { get; set; }

        [Ignore]
        public string Html { get; set; }

        [Ignore]
        public string Link
        {
            get
            {
                return "/article/" + Id.ToString().Replace("-", "") +".html";
            }
        }

        public string CategoryString()
        {
            if (Category == null)
                return "";
            return Category.Select(x => x.Name).ToArray().Aggregate((a,b)=> a + ", " + b);
        }

        public string ContentHtml(bool staticpage = false)
        {
            if (Html != null)
                return Html;
            return new CustomMarkdownSharp.Markdown() { Static = staticpage}.Transform(Content);
        }
    }

    public class Tag
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Index(false), References(typeof(Article))]
        public Guid ArticleId { get; set; }

        [Index]
        public string Name { get; set; }
    }

    //[Route("/tags")]
    public class TagListRequest// : IReturn<Tag[]>
    {
    }

    [Route("/tags")]
    public class TagsService : Controller
    {
        //[DefaultView("Tags")]
        public object Get(TagListRequest request)
        {
            return LoadTags();
            /*return new HttpResult(c)
            {
                View = "Categories",
                Template = "Default",
            };*/
        }

        public static string[] LoadTags()
        {
            var c = Database.Db.Select<Tag>().Select(x => x.Name).Distinct().ToArray();
            return c;
        }
    }

    //[Route("/articles")]
    public class ArticleListRequest// : IReturn<Article[]>
    {
    }

    //[Route("/blog/{RequestString}")]
    //[Route("/blog/")]
    public class BlogRequest// : IReturn<Article[]>
    {
        public string RequestString
        {
            get
            {
                return requeststring;
            }
            set
            {
                requeststring = value;
                Parse();
            }
        }

        string requeststring;
        Guid id;
        string[] parts;

        void Parse()
        {
            if (requeststring == null)
                requeststring = string.Empty;
            string filename = Path.GetFileNameWithoutExtension(requeststring);
            parts = filename.Split(Constants.Seperator[0]);
            Guid.TryParse(parts[0], out id);
        }

        public Guid Id
        {
            get
            {
                return id;
            }
        }

        public string[] Parts
        {
            get
            {
                return parts;
            }
        }
    }

    public class BlogInfo
    {
        public Article[] Articles;
        public int TotalArticleCount;
        public int Page;
        public string Tag;
        public int PageCount
        {
            get
            {
                return CalcPageCount(TotalArticleCount);
            }
        }

        public static int CalcPageCount(int count)
        {
            var config = Settings.Load();

            if (count % config.ArticlesPerPage == 0)
                return Math.Max(1, count / config.ArticlesPerPage);

            return count / config.ArticlesPerPage + 1;
        }
    }

    [Route("Blog")]
    //[Route("")]
    public class Blog : Controller
    {
        [HttpGet]
        [Route("{requeststring}")]
        [Route("")]
        public IActionResult Get(string requeststring)
        {
            BlogRequest request = new BlogRequest() { RequestString = requeststring };

            string tag = null;
            int page = 1;
            if (request.Parts != null)
            {
                if(request.Parts.Length > 1 && request.Parts[1].StartsWith("tag_"))
                    tag = request.Parts[1].Substring(4);

                if (request.Parts.Length > 0 && request.Parts[0].StartsWith("page"))
                    page = int.Parse(request.Parts[0].Substring(4));
            }

            return Get(tag, page);
        }

        public static BlogInfo LoadBlog(string tag = null, int page = 1)
        {
            Article[] blog;
            
            if(string.IsNullOrWhiteSpace(tag))
                blog = Database.Db.LoadSelect<Article>(x => x.ShowInBlog).OrderByDescending(x => x.Created).ToArray();
            else
                blog = Database.Db.LoadSelect<Article>().OrderByDescending(x => x.Created).ToArray();

            if (!string.IsNullOrWhiteSpace(tag))
                blog = blog.Where(y => y.Category != null && y.Category.Any(x => x.Name.ToLower() == tag.ToLower())).ToArray();

            foreach (var b in blog)
            {
                b.Content = Preview(b.Content);
                //var user = Database.db.Single<User>(x => x.Id == b.AuthorId);
                if (b.Author != null)
                    b.AuthorName = b.Author.UserName;
                else
                    b.AuthorName = "unknown author";
                b.Author = null;
            }

            BlogInfo bloginfo = new BlogInfo()
            {
                TotalArticleCount = blog.Length,
                Page = page,
                Tag = tag
            };

            var config = Settings.Load();
            var itemsperpage = config.ArticlesPerPage;
            if (page > 0)
                page -= 1;


            blog = blog.Skip(page * itemsperpage).Take(itemsperpage).ToArray();
            bloginfo.Articles = blog;

            return bloginfo;
        }

        public IActionResult Get(string tag = null, int page = 1)
        {
            var blog = LoadBlog(tag, page);

            /*return new HttpResult(blog)
            {
                View = "Blog",
                Template = "Standard",
            };*/
            return View("Blog", blog);
        }

        public static string Preview(string markdown, bool staticpage = false)
        {
            string html = new CustomMarkdownSharp.Markdown() { Static = staticpage }.Transform(markdown);
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
                            if (url.StartsWith("/image/"))
                            {
                                url = url.Substring(7);
                                url = Path.GetFileNameWithoutExtension(url);
                                url = url.Split(Constants.Seperator[0])[0];
                                url += Constants.Seperator + "thumb.jpg";
                                url = "/image/" + url;
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
                            if (url.StartsWith("/image/"))
                            {
                                url = url.Substring(7);
                                url = Path.GetFileNameWithoutExtension(url);
                                url = url.Split(Constants.Seperator[0])[0];
                                url += Constants.Seperator + "thumb.jpg";
                                url = "/image/" + url;
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


            Regex regex = new Regex(@"!\[youtube\]\(([A-Za-z0-9\-]+)\)");
            Match match = regex.Match(markdown);
            if(match.Success)
            {
                do
                {
                    string youtube_id = match.Groups[1].Value;
                    text += string.Format(@"<img src=""{0}""/>", "https://img.youtube.com/vi/" + youtube_id + "/1.jpg");
                    match = match.NextMatch();
                }
                while (match.Success);
            }
            return text;
        }
    }

    [Route("articles")]
    public class ArticlesService : Controller
    {
        [HttpGet]
        public Article[] Get()
        {
            var list = Database.Db.LoadSelect<Article>().OrderByDescending(x => x.Created);
            return list.ToArray();
        }
    }

    [Route("article")]
    //[Route("/article/{Id}")]
    //[Route("/article/{Id}.html")]
    public class ArticleService : Controller
    {
        public static Article[] GetMenuArticles()
        {
            return Database.Db.Select<Article>(x => x.ShowInMenu).OrderBy(x => x.Title).ToArray();
        }

        [Route("{id}")]
        [Route("{id}.html")]
        [HttpGet]
        public IActionResult Get(string id)
        {
            Guid guid;
            if (Guid.TryParse(id, out guid))
            {
                //request.Id = guid;
            }

            //var html = Request.AbsoluteUri.EndsWith("?html");
            //if (html)
           //     return GetHtml(request);

            var a = Database.Db.LoadSelect<Article>().Single(x => x.Title == id || x.Id == guid);
            
            if (a.Author != null)
                a.AuthorName = a.Author.UserName;
            else
                a.AuthorName = "unknown author";

            return View("Article", a);
            //return a;
            /*return new HttpResult(a)
            {
                View = "Article",
                Template = "Standard",
            };*/
            //return a;
        }

        public Article[] Get(ArticleListRequest request)
        {
            return Get();
        }

        public static Article[] Get()
        {
            var list = Database.Db.LoadSelect<Article>().OrderByDescending(x => x.Created);
            return list.ToArray();
        }

        //[Authenticate]
        public object Delete(Article article)
        {
            if (article.Id == Guid.Empty)
                return new NotFoundResult();

            Database.Db.Delete<Tag>(x => x.ArticleId == article.Id);

            int count = Database.Db.Delete<Article>(x => x.Id == article.Id);
            if (count == 0)
                return new NotFoundResult();

            return article;
        }

        //[AddHeader(ContentType = MimeTypes.Html)]
        public object GetHtml(Article request, bool staticpage = false)
        {
            //var preview = Request != null ? Request.AbsoluteUri.EndsWith("?preview") : false;
            var preview = false;

            //var article = Database.db.Single<Article>(x => x.Title == request.Title || x.Id == request.Id);
            var article = Database.Db.LoadSelect<Article>(x => x.Title == request.Title || x.Id == request.Id).SingleOrDefault();
            if (article == null)
                return new NotFoundResult();

            //var author = Database.db.Single<User>(x => x.Id == article.AuthorId);
            //var category = string.Join(",", Database.db.Select<Category>(x => x.ArticleId == article.Id).OrderBy(x => x.Name).Select(x => x.Name));
            var category = article.Category != null ? string.Join(",", article.Category.OrderBy(x => x.Name).Select(x => x.Name)) : "";
            if (article.Author != null)
                article.AuthorName = article.Author.UserName;
            else
                article.AuthorName = "unknown author";

            //var parts = Database.db.Select<Part>(x => x.ArticleId == article.Id).OrderBy(x => x.Number).ToArray();

            string header = "<h1>" + article.Title + "</h1><p>" + (article.AuthorName ?? "unknown author") + ", " + article.Created + " [" + category + "]</p>";
            string html = "";
            string previewtext = "";

            if (!string.IsNullOrWhiteSpace(article.Content))
            {
                if (preview)
                    previewtext += new HtmlToText().ConvertHtml(article.Content);
                else
                    html += new CustomMarkdownSharp.Markdown() { Static = staticpage}.Transform(article.Content);
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

        //[Authenticate]
        [HttpPut]
        public IActionResult Put(Article article)
        {
            if (Database.Db.Exists<Article>(x => x.Title == article.Title))
                return new NotFoundResult();

            article.Id = Guid.NewGuid();
            article.Created = article.Modified = DateTime.Now;

            //var session = GetSession();
            //if (session.IsAuthenticated)
            //    article.AuthorId = Database.Db.Single<User>(x => x.UserName == session.UserAuthName).Id;

            Database.Db.Insert<Article>(article);

            return Ok(article);
        }

        //[Authenticate]
        [HttpPost]
        public object Post([FromBody]Article article)
        {
            //var session = GetSession();
            //if (session.IsAuthenticated)
            //    article.AuthorId = Database.Db.Single<User>(x => x.UserName == session.UserAuthName).Id;

            var original = Database.Db.LoadSelect<Article>().Single(x => x.Id == article.Id);
            article.Category = article.Category.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();


            article.Modified = DateTime.Now;

            if (original == null)
                throw new Exception("BUG");

            if(article.AuthorId == Guid.Empty)
                article.AuthorId = original.AuthorId;
            article.Created = original.Created;

            Database.Db.Update<Article>(article);

            foreach (var c in article.Category.Where(x => original == null || original.Category == null || !original.Category.Any(y => y.Name == x.Name)))
            {
                c.ArticleId = article.Id;
                Database.Db.Insert<Tag>(c);
            }

            if (original.Category != null)
            {
                foreach (var c in original.Category.Where(x => !article.Category.Any(y => y.Name == x.Name)))
                {
                    c.ArticleId = article.Id;
                    Database.Db.Delete<Tag>(c);
                }
            }

            return article.Id;
        }
    }
}
