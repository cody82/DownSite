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
        public Guid AuthorId { get; set; }

        public string Title { get; set; }
        public string Content { get; set; }
    }

    public class Part
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public int Number { get; set; }
        public Guid ArticleId { get; set; }
        public Guid ImageId { get; set; }
        public Guid FileId { get; set; }
        public string Html { get; set; }
        public string Youtube { get; set; }
    }

    public class ImagePart
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public int Number { get; set; }
        public Guid ImageId { get; set; }
    }

    public class FilePart
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public int Number { get; set; }
        public Guid FileId { get; set; }
    }

    public class LinkPart
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public int Number { get; set; }
        public Guid ArticleId { get; set; }
    }
    /*
    [Route("/article/{Title}")]
    public class ArticleRequest : IReturn<string>
    {
        public string Title { get; set; }
    }*/

    [Route("/Articles")]
    public class ArticleListRequest : IReturn<Article[]>
    {
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

            var a = PersonRepository.db.Single<Article>(x => x.Title == request.Title || x.Id == request.Id);

            return a;
        }

        public Article[] Get(ArticleListRequest request)
        {
            var list = PersonRepository.db.Select<Article>();
            return list.ToArray();
        }

        public object Delete(Article article)
        {
            if (article.Id == Guid.Empty)
                return new HttpResult(HttpStatusCode.NotFound, "no such article.");

            int count = PersonRepository.db.Delete<Article>(x => x.Id == article.Id);
            if (count == 0)
                return new HttpResult(HttpStatusCode.NotFound, "no such article.");

            return article;
        }

        //[AddHeader(ContentType = MimeTypes.Html)]
        public object GetHtml(Article request)
        {
            var preview = Request.AbsoluteUri.EndsWith("?preview");

            var article = PersonRepository.db.Single<Article>(x => x.Title == request.Title || x.Id == request.Id);
            if (article == null)
                return new HttpResult(HttpStatusCode.NotFound, "no such article.");

            var author = PersonRepository.db.Single<User>(x => x.Id == article.AuthorId);

            var parts = PersonRepository.db.Select<Part>(x => x.ArticleId == article.Id).OrderBy(x => x.Number).ToArray();

            string header = "<h1>"+article.Title+"</h1><p>" + ((author != null) ? author.UserName : "unknown") + ", " + article.Created + "</p>";
            string html = "";
            string previewtext = "";

            if (!string.IsNullOrWhiteSpace(article.Content))
            {
                if (preview)
                    previewtext += new HtmlToText().ConvertHtml(article.Content);
                else
                    html += new CustomMarkdownSharp.Markdown().Transform(article.Content);
            }

            foreach (var p in parts)
            {
                if (!string.IsNullOrWhiteSpace(p.Html))
                {
                    if (preview)
                        previewtext += new HtmlToText().ConvertHtml(p.Html);
                    else
                        html += parts[0].Html;
                }
                else if (p.ImageId != Guid.Empty)
                {
                    html += string.Format("<img class=\"img-responsive img-rounded\" src=\"/Image/" + p.ImageId + "{0}\"></img>", preview ? "?thumb" : "");
                }
                else if (!string.IsNullOrWhiteSpace(p.Youtube))
                {
                    if (!preview)
                        html += string.Format(@"<div class=""video-container"">
<iframe width=""640"" height=""360"" src=""//www.youtube.com/embed/{0}"" frameborder=""0""> </iframe>
</div>", p.Youtube);
                    else
                        html += string.Format(@"<img src=""//img.youtube.com/vi/{0}/1.jpg""><img/>", p.Youtube);
                }
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

        public object Put(Article article)
        {
            article.Id = Guid.NewGuid();

            PersonRepository.db.Insert<Article>(article);

            return article;
        }

        public object Post(Article article)
        {
            var original = PersonRepository.db.Single<Article>(x => x.Title == article.Title);
            article.Id = original.Id;
            if(article.AuthorId == Guid.Empty)
                article.AuthorId = original.AuthorId;
            PersonRepository.db.Update<Article>(article);

            return article.Id;
        }
    }
}
