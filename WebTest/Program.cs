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


namespace WebTest
{
    [Route("/Person", "POST")]
    [Route("/Person/{Id}", "PUT")]
    public class Person
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
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

            var user = PersonRepository.db.Select<Person>(x => x.UserName == userName && x.Password == password);
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

            Plugins.Add(new RegistrationFeature());

            //container.Register<ICacheClient>(new MemoryCacheClient());
            //var userRep = new InMemoryAuthRepository();
            //container.Register<IUserAuthRepository>(userRep);

            //The IUserAuthRepository is used to store the user credentials etc.
            //Implement this interface to adjust it to your app's data storage
        }
    }

    //REST Resource DTO
    [Authenticate]
    [Route("/Persons")]
    [Route("/Persons/{Ids}")]
    [Route("/Persons/{Ids}", "DELETE")]
    public class Persons : IReturn<List<Person>>
    {
        public Guid[] Ids { get; set; }
        public Persons() { }
        public Persons(params Guid[] ids)
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

    public class FFMpeg
    {
        public static bool MakeThumbnail(string input, string output)
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
                        if (FFMpeg.MakeThumbnail(img.Item2.FullName, thumb))
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
        public object Get(Persons request)
        {
            return request.Ids.IsEmpty()
            ? Repository.GetAll()
            : Repository.GetByIds(request.Ids);
        }
        public object Post(Person todo)
        {
            return Repository.Store(todo);
        }
        public object Put(Person todo)
        {
            return Repository.Store(todo);
        }
        public void Delete(Persons request)
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
                db.CreateTable<Person>(true);
                db.CreateTable<Image>(true);
                db.CreateTable<Article>(true);
                db.CreateTable<Part>(true);
                if (Directory.Exists("files"))
                    Directory.Delete("files", true);

                Guid pic1 = Guid.NewGuid(), pic2 = Guid.NewGuid();
                Image.Save(pic1, db, MimeTypes.ImageJpg, "acf7eede5be5aa69.jpg", new FileInfo("acf7eede5be5aa69.jpg").OpenRead());
                Image.Save(pic2, db, MimeTypes.ImageJpg, "e3939e928899550f.jpg", new FileInfo("e3939e928899550f.jpg").OpenRead());

                Guid person1;
                db.Insert<Person>(new Person() { Id = person1 = Guid.NewGuid(), ImageId = pic1, UserName = "cody", Password = "cody", FirstName = "cody", LastName = "test", Age = 32 });
                db.Insert<Person>(new Person() { Id = Guid.NewGuid(), ImageId = pic2, FirstName = "cody1", LastName = "test", Age = 37 });
                db.Insert<Person>(new Person() { Id = Guid.NewGuid(), FirstName = "cody2", LastName = "test", Age = 34 });

                string content = string.Format(@"-CONTENT-

![](/Image/{0})

![youtube](cxBcHLylFbw)", pic1);

                Guid article;
                db.Insert<Article>(new Article() { Id = article = Guid.NewGuid(), Content = content, AuthorId = person1, Created = DateTime.Now, Title = "page1", VersionGroup = Guid.NewGuid() });
                    /*
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Html = "<h1>HelloThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg g</h1>", Number = 1 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, ImageId = pic1, Number = 2 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Html = "<h2>There rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg gThere rehgb wr gbwiru gwhr iguhwr giuwh rgiuwhrgiurhg g</h2>", Number = 3 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, ImageId = pic2, Number = 4 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Html = "<br/>", Number = 5 });
                    db.Insert<Part>(new Part() { Id = Guid.NewGuid(), ArticleId = article, Youtube = "cxBcHLylFbw", Number = 6 });
                    */
            }
            var p = db.Select<Person>().OrderBy(y => y.Age);
        }

        public List<Person> GetByIds(Guid[] ids)
        {
            List<Person> list = new List<Person>();
            foreach (var id in ids)
            {
                var p = db.Select<Person>().Where(x => x.Id == id).SingleOrDefault();
                if(p != null)
                    list.Add(p);
            }
            return list;
        }
        public List<Person> GetAll()
        {
            return db.Select<Person>();
        }
        public Person Store(Person todo)
        {
            if (todo.Id == Guid.Empty)
                todo.Id = Guid.NewGuid();
            db.Insert<Person>(todo);
            return todo;
        }
        public void DeleteByIds(params Guid[] ids)
        {
            db.DeleteByIds<Person>(ids);
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
