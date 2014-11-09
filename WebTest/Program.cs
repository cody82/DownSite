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
    public class Configuration
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string SiteName { get; set; }

        public static Configuration Load()
        {
            return PersonRepository.db.SingleById<Configuration>(Guid.Empty);
        }
    }

    [Route("/User", "POST")]
    [Route("/User/{Id}", "PUT")]
    public class User
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        [Index(true)]
        public string UserName { get; set; }
        public string Password { get; set; }
        public string PlainTextPassword { get; set; }
        [Index]
        public int Age { get; set; }

        [References(typeof(Image))]
        public Guid ImageId { get; set; }
    }

    [Route("/system")]
    public class SystemInfoRequest
    {
    }

    public class SystemInfoResponse
    {
        public SystemInfoResponse()
        {
            OS = Environment.OSVersion.ToString();
        }

        public List<string> ConversionQueue { get; set; }
        public string OS { get; set; }
    }

    public class SystemInfoService : Service
    {
        public object Get(SystemInfoRequest request)
        {
            lock (Image.ConvertQueue)
            {
                return new SystemInfoResponse() { ConversionQueue = Image.ConvertQueue.Select(x => Path.GetFileName(x)).ToList() };
            }
        }
    }

    [Route("/Image/{Id}", "GET")]
    public class Image : IReturn<byte[]>
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public byte[] Data { get; set; }
        public string MimeType { get; set; }
        public string FileName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public static List<string> ConvertQueue = new List<string>();

        static void ConvertVideo(Image img)
        {
            foreach (int h in ImageService.ResizeHeights)
            {
                if (img.Height > h)
                {
                    int w = (int)((double)img.Width / (double)img.Height * (double)h);
                    if (w % 2 == 1)
                        w -= 1;
                    var file = UploadService.GetFileInfo(img.Id);

                    string output = Path.Combine(FileCache.GetCacheDir().FullName, img.Id + "%0x"+h+".mp4");

                    if (File.Exists(output))
                        continue;

                    int h2 = h;
                    int w2 = w;
                    new Thread(() =>
                    {
                        lock (ConvertQueue)
                        {
                            if (ConvertQueue.Contains(output))
                                return;
                            ConvertQueue.Add(output);
                        }

                        Console.WriteLine("converting to " + h2 + "p...");
                        if (VideoConverter.Resize(file.FullName, output + ".tmp", w2, h2))
                        {
                            Console.WriteLine("conversion ready");
                            File.Move(output + ".tmp", output);
                        }
                        else
                            Console.WriteLine("conversion failed");

                        lock (ConvertQueue)
                        {
                            ConvertQueue.Remove(output);
                        }
                    }).Start();
                }
            }
        }
        
        public static void Save(Guid id, IDbConnection db, string mimetype, string filename, Stream s)
        {
            string path = UploadService.PutFile(id, mimetype, s);
            var img = new Image() { Id = id, MimeType = mimetype, FileName = filename };
            if (img.MimeType.StartsWith("video"))
            {
                var video = VideoProbe.Probe(path);
                if (video != null)
                {
                    var res = video.Resolution;
                    img.Width = res.Width;
                    img.Height = res.Height;

                    ConvertVideo(img);
                }
            }
            else
            {
                using (var bmp = new Bitmap(path))
                {
                    img.Width = bmp.Width;
                    img.Height = bmp.Height;
                }
            }
            db.Insert<Image>(img);
        }

        public static Tuple<Image, FileInfo> Load(Guid id)
        {
            var img = PersonRepository.db.Select<Image>(x => x.Id == id).SingleOrDefault();
            if (img == null)
                return null;

            if (img.MimeType.StartsWith("video"))
                ConvertVideo(img);

            var s = UploadService.GetFileInfo(id);

            return Tuple.Create(img, s);
        }
    };

    [Route("/Images", "GET")]
    public class Images : IReturn<Image[]>
    {
    };

    public class CustomCredentialsAuthProvider : CredentialsAuthProvider
    {
        public override bool TryAuthenticate(IServiceBase authService, string userName, string password)
        {
            //Add here your custom auth logic (database calls etc)
            //Return true if credentials are valid, otherwise false

            password = Util.SHA1(password);
            var user = PersonRepository.db.Select<User>(x => x.UserName == userName && x.Password == password);
            return user.Any();
        }

        /*public override IHttpResult OnAuthenticated(IServiceBase authService,
            IAuthSession session, IAuthTokens tokens, Dictionary<string, string> authInfo)
        {
            //Fill IAuthSession with data you want to retrieve in the app eg:
            session.FirstName = "some_firstname_from_db";

            session.IsAuthenticated = true;
            //session.UserAuthName = 
            //...

            return null;
        }*/
    }

    public class AppHost : AppHostHttpListenerBase
    {
        public AppHost() : base("Fitness", typeof(AppHost).Assembly) { }
        public override void Configure(Container container)
        {
            LogManager.LogFactory = new ConsoleLogFactory();

            RawHttpHandlers.Remove(RedirectDirectory);
            container.Register(new PersonRepository());
            this.Config.AllowFileExtensions.Add("ejs");
            this.Config.AllowFileExtensions.Add("webm");

            /*SetConfig(new HostConfig()
            {
                DefaultRedirectPath = "/blog/"
            });*/
            //this.Config.AllowFileExtensions.Remove
            Plugins.Add(new RazorFormat());
            Plugins.Add(new AuthFeature(() => new AuthUserSession(),
  new IAuthProvider[] { 
        //new BasicAuthProvider(), //Sign-in with Basic Auth
        //new CredentialsAuthProvider(), //HTML Form post of UserName/Password credentials
        new CustomCredentialsAuthProvider(), //HTML Form post of UserName/Password credentials
      }));
            
            //Plugins.Add(new RegistrationFeature());

            //container.Register<ICacheClient>(new MemoryCacheClient());
            //var userRep = new InMemoryAuthRepository();
            //container.Register<IUserAuthRepository>(userRep);

            //The IUserAuthRepository is used to store the user credentials etc.
            //Implement this interface to adjust it to your app's data storage
        }
    }

    //REST Resource DTO
    [Authenticate]
    [Route("/Users")]
    [Route("/Users/{Ids}")]
    [Route("/Users/{Ids}", "DELETE")]
    public class Users : IReturn<List<User>>
    {
        public Guid[] Ids { get; set; }
        public Users() { }
        public Users(params Guid[] ids)
        {
            this.Ids = ids;
        }
    }

    public class UploadResult
    {
        public Guid Guid { get; set; }
    }

    [Route("/upload")]
    public class Upload : IReturn<UploadResult>, IRequiresRequestStream
    {
        public Stream RequestStream { get; set; }
    }
    
    [FallbackRoute("/")]
    [Route("/page/{Name}")]
    [Route("/page/{Name}/{Id}")]
    public class PageRequest
    {
        public string Name { get; set; }
        public string Id { get; set; }
    }



    public class PageService : Service
    {
        public object Get(PageRequest request)
        {
            return Blog.Get();
        }
    }

    public class FileCache
    {
        public static DirectoryInfo GetCacheDir()
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine("data", "cache"));
            if (!di.Exists)
                di.Create();
            return di;
        }

        public static FileInfo GetFile(string filename)
        {
            var dir = GetCacheDir();

            var files = dir.GetFiles(filename);

            if (files.Any())
                return files.First();
            else
                return null;
        }
    }

    [Authenticate]
    public class UploadService : Service
    {
        public static DirectoryInfo GetFileDir()
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine("data", "files"));
            if (!di.Exists)
                di.Create();
            return di;
        }

        public static FileStream GetFile(Guid id)
        {
            FileInfo fi = new FileInfo(Path.Combine("data", "files", id.ToString()));
            if (!fi.Exists)
                return null;
            return fi.OpenRead();
        }

        public static FileInfo GetFileInfo(Guid id)
        {
            return GetFileInfo(id.ToString());
        }
        public static FileInfo GetFileInfo(string filename)
        {
            var dir = GetFileDir();
            var files = dir.GetFiles(filename + ".*");
            if (files.Any())
                return files.First();

            FileInfo fi = new FileInfo(Path.Combine("data", "files", filename));
            if (!fi.Exists)
                return null;
            return fi;
        }

        public static string MimeTypeExtension(string mimetype)
        {
            switch (mimetype)
            {
                case MimeTypes.ImageGif:
                    return "gif";
                case MimeTypes.ImageJpg:
                    return "jpg";
                case MimeTypes.ImagePng:
                    return "png";
                case "video/webm":
                    return "webm";
                case "video/mp4":
                    return "mp4";
                default:
                    throw new Exception("no extension for mime type " + mimetype);
            }
        }

        public static string PutFile(Guid id, string mimetype, Stream s)
        {
            return PutFile(id.ToString() + "." + MimeTypeExtension(mimetype), s);
        }
        public static string PutFile(string filename, Stream s)
        {
            var dir = GetFileDir();
            string path = Path.Combine(dir.FullName, filename);
            using (FileStream fs = new FileStream(path, FileMode.Create))
            {
                s.WriteTo(fs);
            }
            return path;
        }

        public object Post(Upload request)
        {
            var file = Request.Files.FirstOrDefault();

            Guid pic1 = Guid.NewGuid();
            var mimetypes = new string[] { MimeTypes.ImageJpg, "video/webm", MimeTypes.ImagePng, MimeTypes.ImageGif, "video/mp4" };
            if (file == null)
            {
                if(request.RequestStream == null)
                    return new HttpError(System.Net.HttpStatusCode.InternalServerError, "File missing.");

                if (mimetypes.Contains(Request.ContentType))
                {
                    string filename = null;
                    if (Request.Headers.AllKeys.Contains("Content-Disposition"))
                    {
                        string content = Request.Headers["Content-Disposition"];
                        string search = "filename=\"";
                        content = content.Substring(content.IndexOf(search) + search.Length).Trim('"');
                        filename = content;
                    }

                    Image.Save(pic1, PersonRepository.db, Request.ContentType, filename, request.RequestStream);

                    return new UploadResult() { Guid = pic1 };
                }
                else
                    return new HttpError(System.Net.HttpStatusCode.InternalServerError, string.Format("Unknown file type: {0}.", file.ContentType));
            }

            if(!mimetypes.Contains(file.ContentType))
                return new HttpError(System.Net.HttpStatusCode.InternalServerError, string.Format("Unknown file type: {0}.", file.ContentType));

            Image.Save(pic1, PersonRepository.db, Request.ContentType, file.FileName, file.InputStream);

            return new UploadResult(){ Guid= pic1 };
        }
    }


    public class ImageService : Service
    {
        public static readonly int[] ResizeHeights = { 480, 720 };
        public static readonly int[] ResizeWidths = { 640, 1024, 1920 };

        static HttpResult ResizeHelper(string url, Image img, int[] sizes, Func<int, string> x)
        {
            foreach (var h in sizes)
            {
                string tmp = x(h);
                if (url.EndsWith("?" + tmp))
                {
                    string extension = null;
                    string mimetype = null;
                    string end = "%" + tmp;
                    if (img.MimeType.StartsWith("video"))
                    {
                        extension = "mp4";
                        mimetype = "video/mp4";
                    }
                    else if (img.MimeType.StartsWith("image"))
                    {
                        extension = "jpg";
                        mimetype = "image/jpg";
                    }

                    if (extension != null)
                    {
                        var file = FileCache.GetFile(img.Id + end + "." + extension);
                        if (file != null)
                        {
                            return new HttpResult(file, mimetype) { };
                        }
                    }
                }
            }

            return null;
        }

        public object Get(Image request)
        {
            var img = Image.Load(request.Id);
            if (img != null)
            {
                var res = ResizeHelper(Request.AbsoluteUri, img.Item1, ResizeHeights, x => "0x" + x);
                if (res != null)
                    return res;
                res = ResizeHelper(Request.AbsoluteUri, img.Item1, ResizeWidths, x => x + "x0");
                if (res != null)
                    return res;

                if (Request.AbsoluteUri.EndsWith("?thumb"))
                {
                    string filename_without_extension = Path.GetFileNameWithoutExtension(img.Item2.Name);
                    string thumb = filename_without_extension + "%thumb.jpg";
                    var thumb_file = FileCache.GetFile(thumb);
                    if (thumb_file != null)
                    {
                        return new HttpResult(thumb_file, MimeTypes.ImageJpg) { };
                    }

                    thumb_file = new FileInfo(Path.Combine(FileCache.GetCacheDir().FullName, thumb));

                    var mimetypes = new string[] { MimeTypes.ImageJpg, MimeTypes.ImagePng, MimeTypes.ImageGif };

                    if (!mimetypes.Contains(img.Item1.MimeType))
                    {
                        if (VideoThumbnailer.MakeThumbnail(img.Item2.FullName, thumb_file.FullName))
                        {
                            return new HttpResult(new FileInfo(thumb), MimeTypes.ImageJpg) { };
                        }
                        else
                            return new HttpResult(System.Net.HttpStatusCode.NotFound, string.Format("No thumbnails for type {0}.", img.Item1.MimeType));
                    }
                    else
                    {
                        using (var fs = img.Item2.OpenRead())
                        {
                            using (Bitmap bmp = new Bitmap(fs))
                            {
                                using (var thumb2 = bmp.GetThumbnailImage(80, 80, null, IntPtr.Zero))
                                {
                                    thumb2.Save(thumb_file.FullName, System.Drawing.Imaging.ImageFormat.Jpeg);
                                    return new HttpResult(thumb_file, MimeTypes.ImageJpg) { };
                                }
                            }
                        }
                    }
                }
                else
                {
                    return new HttpResult(img.Item2, string.IsNullOrWhiteSpace(img.Item1.MimeType) ? MimeTypes.ImageJpg : img.Item1.MimeType) { };
                }
            }
            return new HttpResult(System.Net.HttpStatusCode.NotFound, "No image with that ID.");
        }

        [AddHeader(ContentType = MimeTypes.Json)]
        public object Get(Images request)
        {
            return PersonRepository.db.Select<Image>().ToArray();
        }
    }

    public class PersonsService : Service
    {
        public PersonRepository Repository { get; set; } //Injected by IOC
        public object Get(Users request)
        {
            return request.Ids.IsEmpty()
            ? Repository.GetAll()
            : Repository.GetByIds(request.Ids);
        }
        [Authenticate]
        public object Post(User todo)
        {
            if (todo.PlainTextPassword != null)
            {
                todo.Password = Util.SHA1(todo.PlainTextPassword);
                todo.PlainTextPassword = null;
            }
            return Repository.Store(todo);
        }
        [Authenticate]
        public object Put(User todo)
        {
            if (todo.PlainTextPassword != null)
            {
                todo.Password = Util.SHA1(todo.PlainTextPassword);
                todo.PlainTextPassword = null;
            }
            return Repository.Store(todo);
        }
        [Authenticate]
        public void Delete(Users request)
        {
            Repository.DeleteByIds(request.Ids);
        }
    }

    public class PersonRepository
    {
        public static IDbConnection db;

        public PersonRepository()
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

                db = Database.OpenDbConnection(dbfile);
                db.CreateTable<User>(true);
                db.CreateTable<Image>(true);
                db.CreateTable<Article>(true);
                db.CreateTable<Category>(true);
                db.CreateTable<Configuration>(true);

                db.Insert<Configuration>(new Configuration() { Id = Guid.Empty, SiteName = "WebTest" });

                db.ExecuteSql(@"CREATE UNIQUE INDEX category_unique on Category(ArticleId, Name);");


                Guid pic1 = Guid.NewGuid(), pic2 = Guid.NewGuid();
                Image.Save(pic1, db, MimeTypes.ImageJpg, "acf7eede5be5aa69.jpg", new FileInfo("acf7eede5be5aa69.jpg").OpenRead());
                Image.Save(pic2, db, MimeTypes.ImageJpg, "e3939e928899550f.jpg", new FileInfo("e3939e928899550f.jpg").OpenRead());

                Guid person1;
                db.Insert<User>(new User() { Id = person1 = Guid.NewGuid(), ImageId = pic1, UserName = "cody", Password = Util.SHA1("cody"), FirstName = "cody", LastName = "test", Age = 32 });
                db.Insert<User>(new User() { Id = Guid.NewGuid(), ImageId = pic2, FirstName = "cody1", LastName = "test", Age = 37 });
                db.Insert<User>(new User() { Id = Guid.NewGuid(), FirstName = "cody2", LastName = "test", Age = 34 });

                string content = string.Format(@"-CONTENT-

![](/Image/{0})

![youtube](cxBcHLylFbw)", pic1);

                Guid article;
                db.Insert<Article>(new Article() { Id = article = Guid.NewGuid(), ShowInBlog = true, Content = content, AuthorId = person1, Created = DateTime.Now, Title = "page1", VersionGroup = Guid.NewGuid() });

                db.Insert<Category>(new Category() { ArticleId = article, Name = "a" });
                db.Insert<Category>(new Category() { ArticleId = article, Name = "b" });
                db.Insert<Category>(new Category() { ArticleId = article, Name = "c" });


                db.Insert<Article>(new Article() { Id = Guid.NewGuid(), ShowInMenu = true, Content = "#MenuItem 1", Created = DateTime.Now, Title = "MenuItem 1", VersionGroup = Guid.NewGuid() });
                db.Insert<Article>(new Article() { Id = Guid.NewGuid(), ShowInMenu = true, Content = "#MenuItem 2", Created = DateTime.Now, Title = "MenuItem 2", VersionGroup = Guid.NewGuid() });

                var a = db.LoadSingleById<Article>(article);
                if (a.Category == null)
                    throw new Exception("BUG");
                a = db.LoadSelect<Article>(y => y.Id == article).First();
                if (a.Category == null)
                    throw new Exception("BUG");
            }
            else
                db = Database.OpenDbConnection(dbfile);

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

        public List<User> GetByIds(Guid[] ids)
        {
            List<User> list = new List<User>();
            foreach (var id in ids)
            {
                var p = db.Select<User>().Where(x => x.Id == id).SingleOrDefault();
                if(p != null)
                    list.Add(p);
            }
            return list;
        }
        public List<User> GetAll()
        {
            return db.Select<User>();
        }
        public User Store(User todo)
        {
            if (todo.Id == Guid.Empty)
                todo.Id = Guid.NewGuid();
            db.Insert<User>(todo);
            return todo;
        }
        public void DeleteByIds(params Guid[] ids)
        {
            db.DeleteByIds<User>(ids);
        }
    }

    class Program
    {
        const string BaseUri = "http://*:1337/";
        //const string BaseUri = "http://localhost:1337/";

        static void Main(string[] args)
        {
            var appHost = new AppHost();
            appHost.Init();
            appHost.Start(BaseUri);

            Static.Generate();

            Console.ReadLine();
        }
    }
}
