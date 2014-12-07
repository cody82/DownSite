using ServiceStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.OrmLite;
using HtmlAgilityPack;
using System.IO;

namespace DownSite
{
    [Route("/feed/{FeedName}")]
    public class FeedRequest
    {
        public string FeedName { get; set; }
    }

    public class Feed : Service
    {
        public static string LoadAtom()
        {
            var config = Settings.Load();
            string site = config.SiteUrl;
            string id = "DownSiteID";
            SyndicationFeed feed = new SyndicationFeed(config.SiteName ?? "Test", config.SiteDescription ?? "Test", new Uri(site), id, DateTime.Now);
            List<SyndicationItem> items = new List<SyndicationItem>();

            var blog = Database.Db.Select<Article>(x => x.ShowInBlog).OrderByDescending(x => x.Created);
            foreach (var a in blog)
            {
                string html = new CustomMarkdownSharp.Markdown() { Static = true }.Transform(a.Content);
                HtmlDocument doc = new HtmlDocument();
                doc.LoadHtml(html);

                string text = new HtmlToText().ConvertHtml(html);

                SyndicationItem item = new SyndicationItem(a.Title, text, new Uri(site + a.Link), a.Id.ToString(), a.Created);
                items.Add(item);
            }

            feed.Items = items;
            Atom10FeedFormatter atomFormatter = new Atom10FeedFormatter(feed);
            MemoryStream mem = new MemoryStream();
            using (var writer = System.Xml.XmlWriter.Create(mem, new System.Xml.XmlWriterSettings() { Encoding = Encoding.UTF8, OmitXmlDeclaration = false }))
                atomFormatter.WriteTo(writer);

            string ret = Encoding.UTF8.GetString(mem.ToArray());
            return ret;
        }

        public object Get(FeedRequest request)
        {
            return new HttpResult(LoadAtom(), "application/atom+xml");
        }
    }

}
