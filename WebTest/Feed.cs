using ServiceStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using ServiceStack.OrmLite;

namespace WebTest
{
    [Route("/feed/{FeedName}")]
    public class FeedRequest
    {
        public string FeedName { get; set; }
    }

    public class Feed : Service
    {
        public object Get(FeedRequest request)
        {
            var config = Configuration.Load();
            string site = config.SiteUrl;
            string id = "WebTestID";
            SyndicationFeed feed = new SyndicationFeed(config.SiteName ?? "Test", config.SiteDescription ?? "Test", new Uri(site), id, DateTime.Now);
            List<SyndicationItem> items = new List<SyndicationItem>();

            var blog = PersonRepository.db.Select<Article>(x => x.ShowInBlog).OrderByDescending(x => x.Created);
            foreach (var a in blog)
            {
                SyndicationItem item = new SyndicationItem(a.Title, a.Content, new Uri(site + a.Link), a.Id.ToString(), a.Created);
                items.Add(item);
            }

            feed.Items = items;
            Atom10FeedFormatter atomFormatter = new Atom10FeedFormatter(feed);
            StringBuilder builder = new StringBuilder();
            using (var writer = System.Xml.XmlWriter.Create(builder))
                atomFormatter.WriteTo(writer);

            return new HttpResult(builder.ToString(), "application/atom+xml");
        }
    }

}
