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
using System.ServiceModel.Syndication;


namespace WebTest
{
    public class Menu
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string Caption { get; set; }
        public string Link { get; set; }

        public static Menu[] Load()
        {
            return Database.Db.Select<Menu>().ToArray();
        }
    }

    public static class Settings
    {
        public const string Seperator = "!";
        public const int ArticlesPerPage = 3;
    }

    public class Configuration
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        public string SiteName { get; set; }
        public int Version { get; set; }

        public string SiteUrl { get; set; }
        public string SiteDescription { get; set; }

        public bool ShowComments { get; set; }
        public bool AllowWriteComments { get; set; }

        public static Configuration Load()
        {
            return Database.Db.SingleById<Configuration>(Guid.Empty);
        }
    }

    [Route("/user", "POST")]
    [Route("/user/{Id}", "PUT")]
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

    public class RequestParser
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
        string param = string.Empty;

        void Parse()
        {
            string filename = Path.GetFileNameWithoutExtension(requeststring);
            int i = filename.IndexOf(Settings.Seperator);
            if (i != -1)
            {
                param = filename.Substring(i);
                filename = filename.Substring(0, i);
            }
            else
            {
                param = string.Empty;
            }
            id = Guid.Parse(filename);
        }

        public Guid Id
        {
            get
            {
                return id;
            }
        }
        public string Param
        {
            get
            {
                return param;
            }
        }
    }

    [Route("/image/{RequestString}")]
    public class ImageRequest : IReturn<byte[]>
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
        string param = string.Empty;

        void Parse()
        {
            string filename = Path.GetFileNameWithoutExtension(requeststring);
            int i = filename.IndexOf(Settings.Seperator);
            if (i != -1)
            {
                param = filename.Substring(i);
                filename = filename.Substring(0, i);
            }
            else
            {
                param = string.Empty;
            }
            id = Guid.Parse(filename);
        }

        public Guid Id
        {
            get
            {
                return id;
            }
        }
        public string Param
        {
            get
            {
                return param;
            }
        }
    }

    public class Image
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public byte[] Data { get; set; }
        public string MimeType { get; set; }
        public string FileName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public static List<string> ConvertQueue = new List<string>();

        static Bitmap resizeImage(Bitmap imgToResize, Size size)
        {
            return new Bitmap(imgToResize, size);
        }

        static void ConvertImage(Image img)
        {
            foreach (int w in ImageService.ResizeWidths)
            {
                if (img.Width > w)
                {
                    int h3 = (int)((double)img.Height / (double)img.Width * (double)w);
                    if (h3 % 2 == 1)
                        h3 -= 1;
                    var file = UploadService.GetFileInfo(img.Id);
                    if (file == null)
                    {
                        Console.WriteLine("???");
                        continue;
                    }

                    string output = Path.Combine(FileCache.GetCacheDir().FullName, img.Id + WebTest.Settings.Seperator + w + "x0" + ".jpg");

                    if (File.Exists(output))
                        continue;

                    int h2 = h3;
                    int w2 = w;
                    Console.WriteLine("converting image to " + w2 + "x" + h2);
                    using(var bmp = new Bitmap(file.FullName))
                    {
                        using(var resized = resizeImage(bmp, new Size(w2,h2)))
                        {
                            resized.Save(output);
                        }
                    }
                }
            }
        }

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
                    if (file == null)
                    {
                        Console.WriteLine("???");
                        continue;
                    }

                    string output = Path.Combine(FileCache.GetCacheDir().FullName, img.Id + WebTest.Settings.Seperator + "0x"+h+".mp4");

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
            string extension = UploadService.MimeTypeExtension(mimetype);
            if (extension == null)
                extension = Path.GetExtension(filename);

            string path = UploadService.PutFile(id, extension, s);
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
            else if(mimetype.StartsWith("image"))
            {
                using (var bmp = new Bitmap(path))
                {
                    img.Width = bmp.Width;
                    img.Height = bmp.Height;
                    ConvertImage(img);
                }
            }
            db.Insert<Image>(img);
        }

        public static Tuple<Image, FileInfo> Load(Guid id)
        {
            var img = Database.Db.Select<Image>(x => x.Id == id).SingleOrDefault();
            if (img == null)
                return null;

            if (img.MimeType.StartsWith("video"))
                ConvertVideo(img);
            else
                ConvertImage(img);

            var s = UploadService.GetFileInfo(id);

            return Tuple.Create(img, s);
        }
    };

    [Route("/images", "GET")]
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
            var user = Database.Db.Select<User>(x => x.UserName == userName && x.Password == password);
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
    [Route("/users")]
    [Route("/users/{Ids}")]
    [Route("/users/{Ids}", "DELETE")]
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
    
    /*[FallbackRoute("/")]
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
    }*/

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
            FileInfo fi = new FileInfo(Path.Combine("data", "files", id.ToString().Replace("-", "")));
            if (!fi.Exists)
            {
                fi = new FileInfo(Path.Combine("data", "files", id.ToString().Replace("-","")));
                if (!fi.Exists)
                {
                    return null;
                }
            }
            return fi.OpenRead();
        }

        public static FileInfo GetFileInfo(Guid id)
        {
            FileInfo fi = GetFileInfo(id.ToString().Replace("-", ""));
            if (fi == null)
            {
                fi = GetFileInfo(id.ToString().Replace("-",""));
            }
            return fi;
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
                    return null;
            }
        }

        public static string PutFile(Guid id, string extension, Stream s)
        {
            if (extension.Length > 0 && !extension.StartsWith("."))
                extension = "." + extension;
            return PutFile(id.ToString().Replace("-", "") + extension, s);
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
            //var mimetypes = new string[] { MimeTypes.ImageJpg, "video/webm", MimeTypes.ImagePng, MimeTypes.ImageGif, "video/mp4" };
            if (file == null)
            {
                if(request.RequestStream == null)
                    return new HttpError(System.Net.HttpStatusCode.InternalServerError, "File missing.");

                //if (mimetypes.Contains(Request.ContentType))
                {
                    string filename = null;
                    if (Request.Headers.AllKeys.Contains("Content-Disposition"))
                    {
                        string content = Request.Headers["Content-Disposition"];
                        string search = "filename=\"";
                        content = content.Substring(content.IndexOf(search) + search.Length).Trim('"');
                        filename = content;
                    }

                    Image.Save(pic1, Database.Db, Request.ContentType, filename, request.RequestStream);

                    return new UploadResult() { Guid = pic1 };
                }
                //else
                //    return new HttpError(System.Net.HttpStatusCode.InternalServerError, string.Format("Unknown file type: {0}.", file.ContentType));
            }

            //if(!mimetypes.Contains(file.ContentType))
            //    return new HttpError(System.Net.HttpStatusCode.InternalServerError, string.Format("Unknown file type: {0}.", file.ContentType));

            Image.Save(pic1, Database.Db, Request.ContentType, file.FileName, file.InputStream);

            return new UploadResult(){ Guid= pic1 };
        }
    }

    public class ImageService : Service
    {
        public static readonly int[] ResizeHeights = { 480, 720 };
        public static readonly int[] ResizeWidths = { 640, 1024, 1920 };

        static HttpResult ResizeHelper(ImageRequest request, Image img, int[] sizes, Func<int, string> x)
        {
            foreach (var h in sizes)
            {
                string tmp = x(h);
                if (request.Param.Contains(tmp))
                {
                    string extension = null;
                    string mimetype = null;
                    string end = WebTest.Settings.Seperator + tmp;
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

        public object Delete(ImageRequest request)
        {
            Database.Db.Delete<Image>(x => x.Id == request.Id);

            FileInfo fi = UploadService.GetFileInfo(request.Id);
            if (fi != null)
            {
                fi.Delete();
            }

            DirectoryInfo di = FileCache.GetCacheDir();
            FileInfo[] cachefiles = di.GetFiles(request.Id.ToString().Replace("-", "") + "*");
            foreach (var f in cachefiles)
            {
                f.Delete();
            }
            return new HttpResult(System.Net.HttpStatusCode.OK, "File deleted.");
        }

        public object Get(ImageRequest request)
        {
            var img = Image.Load(request.Id);
            if (img != null)
            {
                bool thumb3 = request.Param.Contains("thumb");

                if(!thumb3)
                {
                    var res = ResizeHelper(request, img.Item1, ResizeHeights, x => "0x" + x);
                    if (res != null)
                        return res;
                    res = ResizeHelper(request, img.Item1, ResizeWidths, x => x + "x0");
                    if (res != null)
                        return res;
                }

                if (thumb3)
                {
                    string filename_without_extension = Path.GetFileNameWithoutExtension(img.Item2.Name);
                    string thumb = filename_without_extension + WebTest.Settings.Seperator + "thumb.jpg";
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
                            return new HttpResult(thumb_file, MimeTypes.ImageJpg) { };
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
                    var res = new HttpResult(img.Item2, string.IsNullOrWhiteSpace(img.Item1.MimeType) ? MimeTypes.ImageJpg : img.Item1.MimeType ) { };
                    if(img.Item1.MimeType == "application/octet-stream")
                    {
                        res.Options.Add("Content-Disposition", "attachment; filename=\""+img.Item1.FileName+"\"");
                    }
                    return res;
                }
            }
            return new HttpResult(System.Net.HttpStatusCode.NotFound, "No image with that ID.");
        }

        [AddHeader(ContentType = MimeTypes.Json)]
        public object Get(Images request)
        {
            return Database.Db.Select<Image>().ToArray();
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
        public List<User> GetByIds(Guid[] ids)
        {
            List<User> list = new List<User>();
            foreach (var id in ids)
            {
                var p = Database.Db.Select<User>().Where(x => x.Id == id).SingleOrDefault();
                if(p != null)
                    list.Add(p);
            }
            return list;
        }
        public List<User> GetAll()
        {
            return Database.Db.Select<User>();
        }
        public User Store(User todo)
        {
            if (todo.Id == Guid.Empty)
                todo.Id = Guid.NewGuid();
            Database.Db.Insert<User>(todo);
            return todo;
        }
        public void DeleteByIds(params Guid[] ids)
        {
            Database.Db.DeleteByIds<User>(ids);
        }
    }

    class Program
    {
        const string BaseUri = "http://*:1337/";
        //const string BaseUri = "http://localhost:1337/";

        static void Main(string[] args)
        {
            Database.Init();

            var appHost = new AppHost();
            appHost.Init();
            appHost.Start(BaseUri);

            string line;
            do
            {
                Console.WriteLine("Press return to generate the page");
                line = Console.ReadLine();
                if (line.Length == 0)
                {
                    Static.Generate();
                }
            }
            while (line.Length == 0);
        }
    }
}
