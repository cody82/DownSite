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
using System.Diagnostics;
using System.Security.Cryptography;


namespace WebTest
{
    public class Util
    {
        public static string SHA1(string text)
        {
            var SHA1 = new SHA1CryptoServiceProvider();

            byte[] arrayData;
            byte[] arrayResult;
            string result = null;
            string temp = null;

            arrayData = Encoding.ASCII.GetBytes(text);
            arrayResult = SHA1.ComputeHash(arrayData);
            for (int i = 0; i < arrayResult.Length; i++)
            {
                temp = Convert.ToString(arrayResult[i], 16);
                if (temp.Length == 1)
                    temp = "0" + temp;
                result += temp;
            }
            return result.ToLower();
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

    [Route("/Image/{Id}", "GET")]
    public class Image : IReturn<byte[]>
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public byte[] Data { get; set; }
        public string MimeType { get; set; }
        public string FileName { get; set; }

        public static void Save(Guid id, IDbConnection db, string mimetype, string filename, Stream s)
        {
            UploadService.PutFile(id, s);
            db.Insert<Image>(new Image() { Id = id, MimeType = mimetype, FileName = filename });
        }

        public static Tuple<Image, FileInfo> Load(Guid id)
        {
            var img = PersonRepository.db.Select<Image>(x => x.Id == id).SingleOrDefault();
            if (img == null)
                return null;

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
            RawHttpHandlers.Remove(RedirectDirectory);
            container.Register(new PersonRepository());
            this.Config.AllowFileExtensions.Add("ejs");
            this.Config.AllowFileExtensions.Add("webm");


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

    [Authenticate]
    [Route("/check_auth")]
    public class CheckAuth
    {
    }

    public class CheckAuthService : Service
    {
        public object Get(CheckAuth request)
        {
            return new HttpResult(System.Net.HttpStatusCode.OK, "Authenticated!");
        }
    }


    [Route("/upload")]
    public class Upload : IReturn<UploadResult>, IRequiresRequestStream
    {
        public Stream RequestStream { get; set; }
    }

    [Authenticate]
    public class UploadService : Service
    {
        //public PersonRepository Repository { get; set; } //Injected by IOC
        /*public object Get(Upload request)
        {
            return null;
        }*/

        public static DirectoryInfo GetFileDir()
        {
            DirectoryInfo di = new DirectoryInfo("files");
            if (!di.Exists)
                di.Create();
            return di;
        }

        public static FileStream GetFile(Guid id)
        {
            FileInfo fi = new FileInfo(Path.Combine("files", id.ToString()));
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
            FileInfo fi = new FileInfo(Path.Combine("files", filename));
            if (!fi.Exists)
                return null;
            return fi;
        }
        public static void PutFile(Guid id, Stream s)
        {
            PutFile(id.ToString(), s);
        }
        public static void PutFile(string filename, Stream s)
        {
            var dir = GetFileDir();
            using (FileStream fs = new FileStream(Path.Combine(dir.FullName, filename), FileMode.Create))
            {
                s.WriteTo(fs);
            }
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

    public class VideoThumbnailer
    {
        static bool _UseFFmpeg = false;
        static bool _FFmpeg_tested = false;

        static public bool UseFFmpeg
        {
            get
            {
                if (_FFmpeg_tested)
                    return _UseFFmpeg;

                Console.WriteLine("checking ffmpeg...");

                ProcessStartInfo psi = new ProcessStartInfo("ffmpeg", "-version")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                try
                {
                    var p = Process.Start(psi);
                    p.WaitForExit();
                    _FFmpeg_tested = true;
                    Console.WriteLine("ffmpeg found");
                    return _UseFFmpeg = (p.ExitCode == 0);
                }
                catch
                {
                    Console.WriteLine("no ffmpeg");
                    _FFmpeg_tested = true;
                    return _UseFFmpeg = false;
                }
            }
        }

        static bool _Avconv_tested = false;
        static bool _UseAvconv = false;
        static public bool UseAvconv
        {
            get
            {
                if (_Avconv_tested)
                    return _UseAvconv;

                Console.WriteLine("checking avconv...");

                ProcessStartInfo psi = new ProcessStartInfo("avconv", "-version")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                try
                {
                    var p = Process.Start(psi);
                    p.WaitForExit();
                    _Avconv_tested = true;
                    Console.WriteLine("avconv found");
                    return _UseAvconv = (p.ExitCode == 0);
                }
                catch
                {
                    Console.WriteLine("no avconv");
                    _Avconv_tested = true;
                    return _UseAvconv = false;
                }
            }
        }

        public static bool MakeThumbnail(string input, string output)
        {
            if (UseFFmpeg)
                return MakeThumbnailFFmpeg(input, output);
            else if (UseAvconv)
                return MakeThumbnailAvconv(input, output);
            else
                return false;
        }

        static bool MakeThumbnailAvconv(string input, string output)
        {
            ProcessStartInfo psi = new ProcessStartInfo("avconv", string.Format(@"-i ""{0}"" -vframes 1 -s 80x80 ""{1}""", input, output))
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            try
            {
                var p = Process.Start(psi);
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        static bool MakeThumbnailFFmpeg(string input, string output)
        {
            // https://trac.ffmpeg.org/wiki/Scaling%20%28resizing%29%20with%20ffmpeg
            /*  Sometimes there is a need to scale the input image in such way it fits into a specified rectangle, i.e. if you have a placeholder (empty rectangle) in which you want to scale any given image. This is a little bit tricky, since you need to check the original aspect ratio, in order to decide which component to specify and to set the other component to -1 (to keep the aspect ratio). For example, if we would like to scale our input image into a rectangle with dimensions of 320x240, we could use something like this:

                ffmpeg -i input.jpg -vf scale="'if(gt(a,4/3),320,-1)':'if(gt(a,4/3),-1,240)'" output_320x240_boxed.png
            */
            ProcessStartInfo psi = new ProcessStartInfo("ffmpeg", string.Format(@"-i ""{0}"" -vframes 1 -vf scale=""'if(gt(a,1),80,-1)':'if(gt(a,1),-1,80)'"" ""{1}""", input, output))
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            try
            {
                var p = Process.Start(psi);
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ImageService : Service
    {
        public object Get(Image request)
        {
            var img = Image.Load(request.Id);
            if (img != null)
            {
                if (!Request.AbsoluteUri.EndsWith("?thumb"))
                {
                    return new HttpResult(img.Item2, string.IsNullOrWhiteSpace(img.Item1.MimeType) ? MimeTypes.ImageJpg : img.Item1.MimeType) { };
                }
                else
                {
                    string thumb = img.Item2.FullName + "-thumb.jpg";
                    if (File.Exists(thumb))
                    {
                        return new HttpResult(new FileInfo(thumb), MimeTypes.ImageJpg) { };
                    }

                    var mimetypes = new string[] { MimeTypes.ImageJpg, MimeTypes.ImagePng, MimeTypes.ImageGif};

                    if (!mimetypes.Contains(img.Item1.MimeType))
                    {
                        if (VideoThumbnailer.MakeThumbnail(img.Item2.FullName, thumb))
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
                                    thumb2.Save(thumb, System.Drawing.Imaging.ImageFormat.Jpeg);
                                    return new HttpResult(new FileInfo(thumb), MimeTypes.ImageJpg) { };
                                }
                            }
                        }
                    }
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
            var x = new OrmLiteTestBase();
            x.TestFixtureSetUp();
            OrmLiteConfig.DialectProvider = SqliteOrmLiteDialectProvider.Instance;
            //ConnectionString = ":memory:";

            string dbfile = @"db.sqlite3";
            bool init = !File.Exists(dbfile);
            db = x.OpenDbConnection(dbfile);
            if (init)
            {
                db.CreateTable<User>(true);
                db.CreateTable<Image>(true);
                db.CreateTable<Article>(true);
                db.CreateTable<Category>(true);

                db.ExecuteSql(@"CREATE UNIQUE INDEX category_unique on Category(ArticleId, Name);");

                if (Directory.Exists("files"))
                    Directory.Delete("files", true);

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
                db.Insert<Article>(new Article() { Id = article = Guid.NewGuid(), Content = content, AuthorId = person1, Created = DateTime.Now, Title = "page1", VersionGroup = Guid.NewGuid() });

                db.Insert<Category>(new Category() { ArticleId = article, Name = "a" });
                db.Insert<Category>(new Category() { ArticleId = article, Name = "b" });
                db.Insert<Category>(new Category() { ArticleId = article, Name = "c" });

                //var a = db.LoadSingleById<Article>(article);
                //if (a.Category == null)
                //    throw new Exception("BUG");
                var a = db.LoadSelect<Article>(y => y.Id == article).First();
                if (a.Category == null)
                    throw new Exception("BUG");
                    /*
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Html = "<h1>HelloThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg g</h1>", Number = 1 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, ImageId = pic1, Number = 2 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Html = "<h2>There rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg g</h2>", Number = 3 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, ImageId = pic2, Number = 4 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Html = "<br/>", Number = 5 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Youtube = "cxBcHLylFbw", Number = 6 });
                    */
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

            Console.ReadLine();
        }
    }
}
