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
    };

    [Route("/Images", "GET")]
    public class Images : IReturn<Guid[]>
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
    public class Upload : IReturn<UploadResult>
    {
        
    }

    public class UploadService : Service
    {
        //public PersonRepository Repository { get; set; } //Injected by IOC
        /*public object Get(Upload request)
        {
            return null;
        }*/

        public object Post(Upload request)
        {
            var file = Request.Files.FirstOrDefault();

            if(file == null)
                return new HttpError(System.Net.HttpStatusCode.InternalServerError, "File missing.");

            var mimetypes = new string[] { MimeTypes.ImageJpg, "video/webm", MimeTypes.ImagePng, MimeTypes.ImageGif };
            if(!mimetypes.Contains(file.ContentType))
                return new HttpError(System.Net.HttpStatusCode.InternalServerError, string.Format("Unknown file type: {0}.", file.ContentType));

            Guid pic1;
            PersonRepository.db.Insert<Image>(new Image() { Id = pic1 = Guid.NewGuid(), Data = file.InputStream.ReadFully(), MimeType = file.ContentType });

            return new UploadResult(){ Guid= pic1};
        }
    }

    public class ImageService : Service
    {
        public object Get(Image request)
        {
            Console.WriteLine(Thread.CurrentThread.ManagedThreadId);
            var img = PersonRepository.db.Select<Image>(x => x.Id == request.Id).SingleOrDefault();
            if (img != null)
            {
                Response.AddHeader(HttpHeaders.CacheControl, "max-age=" + TimeSpan.FromMinutes(1).TotalSeconds);
                //Response.AddHeader(HttpHeaders.LastModified, DateTime.Now.ToSqliteDateString());
                if (!Request.AbsoluteUri.EndsWith("?thumb"))
                {
                    byte[] data = img.Data;
                    if (Request.Headers.AllKeys.Contains("Range"))
                    {
                        string range = Request.Headers["Range"];
                        Console.WriteLine(range);
                        var split = range.Split('=')[1].Split('-');
                        int start = int.Parse(split[0]);
                        Console.WriteLine(start);
                        data = new byte[img.Data.Length - start];
                        Console.WriteLine(data.Length);
                        Array.Copy(img.Data, start, data, 0, data.Length);
                        Console.WriteLine("ok");
                    }
                    return new HttpResult(data, string.IsNullOrWhiteSpace(img.MimeType) ? MimeTypes.ImageJpg : img.MimeType) { AllowsPartialResponse = false };
                }
                else
                {
                    using (Bitmap bmp = new Bitmap(new MemoryStream(img.Data)))
                    {
                        using (var thumb = bmp.GetThumbnailImage(80, 80, null, IntPtr.Zero))
                        {
                            var mem = new MemoryStream();
                            thumb.Save(mem, System.Drawing.Imaging.ImageFormat.Jpeg);
                            return new HttpResult(mem, MimeTypes.ImageJpg) { AllowsPartialResponse = false };
                        }
                    }
                }
            }
            return new HttpResult(System.Net.HttpStatusCode.NotFound, "No image with that ID.");
        }

        [AddHeader(ContentType = MimeTypes.Json)]
        public object Get(Images request)
        {
            return PersonRepository.db.Select<Image>().ToArray().Select(x => x.Id).ToArray();
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
            db = x.OpenDbConnection(@"db.sqlite3");

            db.CreateTable<Person>(true);
            db.CreateTable<Image>(true);
            db.CreateTable<Article>(true);
            db.CreateTable<Part>(true);
            //db.Close();

            Guid pic1, pic2;
            db.Insert<Image>(new Image() { Id = pic1 = Guid.NewGuid(), Data = new FileInfo("acf7eede5be5aa69.jpg").ReadFully(), MimeType = MimeTypes.ImageJpg });
            db.Insert<Image>(new Image() { Id = pic2 = Guid.NewGuid(), Data = new FileInfo("e3939e928899550f.jpg").ReadFully(), MimeType = MimeTypes.ImageJpg });

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
