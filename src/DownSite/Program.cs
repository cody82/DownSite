using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Data;
using System.IO;
using System.Drawing;
using System.Web;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
//using ServiceStack.Razor;
using System.ServiceModel.Syndication;
using System.Drawing.Imaging;
using System.Reflection;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;

namespace DownSite
{
    public static class MimeTypes
    {
        public const string ImageJpg = "image/jpeg";
        public const string ImagePng = "image/png";
        public const string ImageGif = "image/gif";
    }

    //[Route("/menu", "POST")]
    //[Route("/menu/{Id}", "PUT")]
    public class Menu
    {
        //[PrimaryKey]
        public Guid Id { get; set; }

        public string Caption { get; set; }
        public string Link { get; set; }

        public static Menu[] Load()
        {
            using (var context = new Database())
            {
                return context.Menu.ToArray();
            }
        }
    }

    //[Route("/menus")]
    //[Route("/menus/{Ids}")]
    //[Route("/menus/{Ids}", "DELETE")]
    public class MenuListRequest// : IReturn<List<Menu>>
    {
        public Guid[] Ids { get; set; }
        public MenuListRequest() { }
        public MenuListRequest(params Guid[] ids)
        {
            this.Ids = ids;
        }
    }

    public static class Constants
    {
        public const string Seperator = "!";
    }

    public class Configuration
    {
        //[PrimaryKey]
        public Guid Id { get; set; }

        public int Version { get; set; }

        public static Configuration Load()
        {
            using (var context = new Database())
            {
                var tmp = context.Configuration.Single();
                if (tmp == null)
                    throw new Exception("No config row");
                return tmp;
            }
        }
    }

    //[Route("/settings")]
    public class Settings
    {
        //[PrimaryKey]
        public Guid Id { get; set; }

        public string SiteName { get; set; }

        public string SiteUrl { get; set; }
        public string SiteDescription { get; set; }

        public bool ShowComments { get; set; }
        public bool AllowWriteComments { get; set; }

        public bool ShowLogin { get; set; }
        public int ArticlesPerPage { get; set; }

        public string DisqusShortName { get; set; }
        public bool Disqus { get; set; }

        public static Settings Load()
        {
            using (var context = new Database())
            {
                return context.Settings.Single();
            }
        }
    }

    public class SettingsService : Controller
    {
        public object Get(Settings s)
        {
            using (var context = new Database())
            {
                return context.Settings.Single();
            }
        }

        //[Authenticate]
        public object Post(Settings s)
        {
            using (var context = new Database())
            {
                context.Settings.Update(s);
                context.SaveChanges();
                return s;
            }
        }

        //[Authenticate]
        public object Put(Settings s)
        {
            return Post(s);
        }
    }

    //[Route("/user", "POST")]
    //[Route("/user/{Id}", "PUT")]
    public class User
    {
        //[PrimaryKey]
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        //[Index(true)]
        public string UserName { get; set; }
        public string Password { get; set; }
        public string PlainTextPassword { get; set; }
        public string Email { get; set; }


        //[References(typeof(Image))]
        public Guid ImageId { get; set; }
    }

    //[Route("/system")]
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

    public class SystemInfoService : Controller
    {
        public object Get(SystemInfoRequest request)
        {
            lock (Image.ConvertQueue)
            {
                return new SystemInfoResponse() { ConversionQueue = Image.ConvertQueue.Select(x => Path.GetFileName(x.Output)).ToList() };
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
            int i = filename.IndexOf(Constants.Seperator);
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

    //[Route("/image/{RequestString}")]
    public class ImageRequest// : IReturn<byte[]>
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
            int i = filename.IndexOf(Constants.Seperator);
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
        //[PrimaryKey]
        public Guid Id { get; set; }
        public byte[] Data { get; set; }
        public string MimeType { get; set; }
        public string FileName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public static BlockingCollection<ConvertInfo> ConvertQueue = new BlockingCollection<ConvertInfo>(1000);

        static Bitmap resizeImage(Bitmap imgToResize, Size size)
        {
            return new Bitmap(imgToResize, size);
        }

        static void Convert(Image img)
        {
            if(img.MimeType.StartsWith("image"))
            {
                ConvertImage(img);
            }
            else if(img.MimeType.StartsWith("video"))
            {
                ConvertVideo(img);
            }
        }

        static void Thumb(Image img)
        {
            var file = UploadService.GetFileInfo(img.Id);
            string filename_without_extension = Path.GetFileNameWithoutExtension(file.Name);
            string thumb = filename_without_extension + DownSite.Constants.Seperator + "thumb.jpg";
            var thumb_file = FileCache.GetFile(thumb);
            if (thumb_file != null)
            {
                return;
            }

            thumb_file = new FileInfo(Path.Combine(FileCache.GetCacheDir().FullName, thumb));

            var mimetypes = new string[] { MimeTypes.ImageJpg, MimeTypes.ImagePng, MimeTypes.ImageGif };

            if (!mimetypes.Contains(img.MimeType))
            {
                VideoThumbnailer.MakeThumbnail(file.FullName, thumb_file.FullName);
            }
            else
            {
                using (var fs = file.OpenRead())
                {
                    using (Bitmap bmp = new Bitmap(fs))
                    {
                        using (var thumb2 = bmp.GetThumbnailImage(80, 80, null, IntPtr.Zero))
                        {
                            ImageService.SaveJpeg(thumb2, thumb_file.FullName);
                        }
                    }
                }
            }
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

                    string output = Path.Combine(FileCache.GetCacheDir().FullName, img.Id + DownSite.Constants.Seperator + w + "x0" + ".jpg");

                    if (File.Exists(output))
                        continue;

                    int h2 = h3;
                    int w2 = w;
                    Console.WriteLine("converting image to " + w2 + "x" + h2);
                    using(var bmp = new Bitmap(file.FullName))
                    {
                        using(var resized = resizeImage(bmp, new Size(w2,h2)))
                        {
                            ImageService.SaveJpeg(resized, output);
                        }
                    }
                }
            }
        }

        public class ConvertInfo
        {
            public ConvertInfo(int w, int h, Image img, string output, FileInfo file)
            {
                Width = w;
                Height = h;
                Image = img;
                Output = output;
                File = file;
            }

            public int Width;
            public int Height;
            public Image Image;
            public string Output;
            public FileInfo File;
        }

        static Image()
        {
            new Thread(() =>
            {
                foreach (var c in ConvertQueue.GetConsumingEnumerable())
                {
                    if (File.Exists(c.Output))
                        continue;

                    Console.WriteLine("converting to " + c.Height + "p:" + c.Output);
                    if (VideoConverter.Resize(c.File.FullName, c.Output + ".tmp", c.Width, c.Height))
                    {
                        Console.WriteLine("conversion ready");
                        File.Move(c.Output + ".tmp", c.Output);
                    }
                    else
                        Console.WriteLine("conversion failed");

                }
            }).Start();
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

                    string output = Path.Combine(FileCache.GetCacheDir().FullName, img.Id + DownSite.Constants.Seperator + "0x"+h+".mp4");

                    if (File.Exists(output))
                        continue;

                    if (ConvertQueue.Any(x => x.Output == output))
                    {
                        Console.WriteLine("already in convert queue: " + output);
                        continue;
                    }

                    Console.WriteLine("add to convert queue: " + output);
                    ConvertQueue.Add(new ConvertInfo(w, h, img, output, file));
                }
            }
        }
        
        public static void Save(Guid id, Database db, string mimetype, string filename, Stream s)
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
            db.Image.Add(img);
            db.SaveChanges();
        }

        public static void GenerateCache()
        {
            using (var context = new Database())
            {
                var list = context.Image;
                foreach (var img in list)
                {
                    Convert(img);
                    Thumb(img);
                }
            }
        }

        public static Tuple<Image, FileInfo> Load(Guid id)
        {
            Image img;
            using (var context = new Database())
            {
                img = context.Image.SingleOrDefault(x => x.Id == id);
            }
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

    //[Route("/images", "GET")]
    public class Images// : IReturn<Image[]>
    {
    };

    /*
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
        
    }
    */
    

    //REST Resource DTO
    //[Authenticate]
    //[Route("/users")]
    //[Route("/users/{Ids}")]
    //[Route("/users/{Ids}", "DELETE")]
    public class Users// : IReturn<List<User>>
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

    //[Route("/upload")]
    public class Upload// : IReturn<UploadResult>, IRequiresRequestStream
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
        public static bool CacheDirExists()
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine(Paths.Data, "cache"));
            return di.Exists;
        }

        public static DirectoryInfo GetCacheDir()
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine(Paths.Data, "cache"));
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

    //[Authenticate]
    [Route("upload")]
    public class UploadService : Controller
    {
        public static DirectoryInfo GetFileDir()
        {
            DirectoryInfo di = new DirectoryInfo(Path.Combine(Paths.Data, "files"));
            if (!di.Exists)
                di.Create();
            return di;
        }

        public static FileStream GetFile(Guid id)
        {
            FileInfo fi = new FileInfo(Path.Combine(Paths.Data, "files", id.ToString().Replace("-", "")));
            if (!fi.Exists)
            {
                fi = new FileInfo(Path.Combine(Paths.Data, "files", id.ToString().Replace("-", "")));
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

            FileInfo fi = new FileInfo(Path.Combine(Paths.Data, "files", filename));
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
                s.CopyTo(fs);
            }
            return path;
        }
        
        [HttpPost]
        public object Post(ICollection<IFormFile> files)
        {
            var file = files.FirstOrDefault();

            Guid pic1 = Guid.NewGuid();
            //var mimetypes = new string[] { MimeTypes.ImageJpg, "video/webm", MimeTypes.ImagePng, MimeTypes.ImageGif, "video/mp4" };
            if (file == null)
            {
                //if(request.RequestStream == null)
                //    return new StatusCodeResult((int)HttpStatusCode.InternalServerError);// HttpError(System.Net.HttpStatusCode.InternalServerError, "File missing.");

                //if (mimetypes.Contains(Request.ContentType))
                {
                    string filename = null;
                    //if (Request.Headers.AllKeys.Contains("Content-Disposition"))
                    if (Request.Headers.Any(x=>x.Key =="Content-Disposition"))
                    {
                        string content = Request.Headers["Content-Disposition"];
                        string search = "filename=\"";
                        content = content.Substring(content.IndexOf(search) + search.Length).Trim('"');
                        filename = content;
                    }


                    using (var context = new Database())
                    {
                        Image.Save(pic1, context, Request.ContentType, filename, Request.Body);
                    }
                    return new UploadResult() { Guid = pic1 };
                }
                //else
                //    return new HttpError(System.Net.HttpStatusCode.InternalServerError, string.Format("Unknown file type: {0}.", file.ContentType));
            }

            //if(!mimetypes.Contains(file.ContentType))
            //    return new HttpError(System.Net.HttpStatusCode.InternalServerError, string.Format("Unknown file type: {0}.", file.ContentType));

            using (var context = new Database())
            {
                Image.Save(pic1, context, Request.ContentType, file.FileName, file.OpenReadStream());
            }
            return new UploadResult(){ Guid= pic1 };
        }
    }

    [Route("image/{RequestString}")]
    public class ImageService : Controller
    {
        public static readonly int[] ResizeHeights = { 480, 720 };
        public static readonly int[] ResizeWidths = { 640, 1024, 1920 };

        static IActionResult ResizeHelper(ImageRequest request, Image img, int[] sizes, Func<int, string> x)
        {
            foreach (var h in sizes)
            {
                string tmp = x(h);
                if (request.Param.Contains(tmp))
                {
                    string extension = null;
                    string mimetype = null;
                    string end = DownSite.Constants.Seperator + tmp;
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
                            //return new HttpResult(file, mimetype) { };
                            return StreamResult(file.FullName, mimetype);
                            //return new PhysicalFileResult(file.FullName, mimetype);
                        }
                    }
                }
            }

            return null;
        }

        //[Authenticate]
        public object Delete(ImageRequest request)
        {
            using (var context = new Database())
            {
                var del = context.Image.Single(x => x.Id == request.Id);
                context.Image.Remove(del);
                context.SaveChanges();
            }
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
            return Ok("File deleted.");
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        public const int JpegQuality = 85;

        public static void SaveJpeg(System.Drawing.Image img, string filename, int quality = JpegQuality)
        {
            ImageCodecInfo jgpEncoder = GetEncoder(ImageFormat.Jpeg);

            // Create an Encoder object based on the GUID
            // for the Quality parameter category.
            System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;

            EncoderParameters myEncoderParameters = new EncoderParameters(1);

            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, quality);

            myEncoderParameters.Param[0] = myEncoderParameter;

            img.Save(filename, jgpEncoder, myEncoderParameters);
        }

        static VideoStreamResult StreamResult(string filename, string mimetype)
        {
            return new VideoStreamResult(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), mimetype);
        }

        [HttpGet]
        public IActionResult Get(string RequestString)
        {
            ImageRequest request = new ImageRequest()
            {
                RequestString = RequestString
            };

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
                    string thumb = filename_without_extension + DownSite.Constants.Seperator + "thumb.jpg";
                    var thumb_file = FileCache.GetFile(thumb);
                    if (thumb_file != null)
                    {
                        return new PhysicalFileResult(thumb_file.FullName, MimeTypes.ImageJpg);
                        //return new HttpResult(thumb_file, MimeTypes.ImageJpg) { };
                    }

                    thumb_file = new FileInfo(Path.Combine(FileCache.GetCacheDir().FullName, thumb));

                    var mimetypes = new string[] { MimeTypes.ImageJpg, MimeTypes.ImagePng, MimeTypes.ImageGif };
                    
                    if (!mimetypes.Contains(img.Item1.MimeType))
                    {
                        if (VideoThumbnailer.MakeThumbnail(img.Item2.FullName, thumb_file.FullName))
                        {
                            //return StreamResult(thumb_file.FullName, MimeTypes.ImageJpg);
                            return new PhysicalFileResult(thumb_file.FullName, MimeTypes.ImageJpg);
                            //return new HttpResult(thumb_file, MimeTypes.ImageJpg) { };
                        }
                        else
                            return new NotFoundObjectResult(string.Format("No thumbnails for type {0}.", img.Item1.MimeType));
                    }
                    else
                    {
                        using (var fs = img.Item2.OpenRead())
                        {
                            using (Bitmap bmp = new Bitmap(fs))
                            {
                                using (var thumb2 = bmp.GetThumbnailImage(80, 80, null, IntPtr.Zero))
                                {
                                    SaveJpeg(thumb2, thumb_file.FullName);
                                    //return StreamResult(thumb_file.FullName, MimeTypes.ImageJpg);
                                    return new PhysicalFileResult(thumb_file.FullName, MimeTypes.ImageJpg);
                                    //return new HttpResult(thumb_file, MimeTypes.ImageJpg) { };
                                }
                            }
                        }
                    }
                }
                else
                {
                    return new PhysicalFileResult(img.Item2.FullName, string.IsNullOrWhiteSpace(img.Item1.MimeType) ? MimeTypes.ImageJpg : img.Item1.MimeType);
                    //var res = new HttpResult(img.Item2, string.IsNullOrWhiteSpace(img.Item1.MimeType) ? MimeTypes.ImageJpg : img.Item1.MimeType ) { };
                    var res = new PhysicalFileResult(img.Item2.FullName, string.IsNullOrWhiteSpace(img.Item1.MimeType) ? MimeTypes.ImageJpg : img.Item1.MimeType) { };
                    if (img.Item1.MimeType == "application/octet-stream")
                    {
                        res.FileDownloadName = img.Item1.FileName;
                        //res.Options.Add("Content-Disposition", "attachment; filename=\""+img.Item1.FileName+"\"");
                    }
                    return res;
                }
            }
            return new NotFoundObjectResult("No image with that ID.");
        }

    }

    [Route("images")]
    public class ImagesService : Controller
    {
        //[AddHeader(ContentType = MimeTypes.Json)]
        [HttpGet]
        public object Get(Images request)
        {
            using (var context = new Database())
            {
                return context.Image.ToArray();
            }
        }
    }

    public class MenuService : Controller
    {
        List<Menu> GetByIds(Guid[] ids)
        {
            List<Menu> list = new List<Menu>();

            using (var context = new Database())
            {
                foreach (var id in ids)
                {
                    var p = context.Menu.SingleOrDefault(x => x.Id == id);
                    if (p != null)
                        list.Add(p);
                }
            }
            return list;
        }
        List<Menu> GetAll()
        {
            using (var context = new Database())
            {
                return context.Menu.ToList();
            }
        }

        public object Get(MenuListRequest request)
        {
            return !request.Ids.Any()
            ? GetAll()
            : GetByIds(request.Ids);
        }
        //[Authenticate]
        public object Post(Menu todo)
        {
            if (todo.Id == Guid.Empty)
                todo.Id = Guid.NewGuid();

            using (var context = new Database())
            {
                context.Menu.Add(todo);
                context.SaveChanges();
            }
            return todo;
        }
        //[Authenticate]
        public object Put(Menu todo)
        {
            if (todo.Id == Guid.Empty)
                todo.Id = Guid.NewGuid();

            using (var context = new Database())
            {
                context.Menu.Add(todo);
                context.SaveChanges();
            }
            return todo;
        }
        //[Authenticate]
        public void Delete(MenuListRequest request)
        {
            using (var context = new Database())
            {
                throw new Exception("TODO");
                //var m = context.Menu.Single(x => x.Id == request.Ids);
            }
        }
    }
    public class PersonsService : Controller
    {
        public PersonRepository Repository { get; set; } //Injected by IOC
        public object Get(Users request)
        {
            return !request.Ids.Any()
            ? Repository.GetAll()
            : Repository.GetByIds(request.Ids);
        }
        //[Authenticate]
        public object Post(User todo)
        {
            if (todo.PlainTextPassword != null)
            {
                todo.Password = Util.SHA1(todo.PlainTextPassword);
                todo.PlainTextPassword = null;
            }
            return Repository.Store(todo);
        }
        //[Authenticate]
        public object Put(User todo)
        {
            if (todo.PlainTextPassword != null)
            {
                todo.Password = Util.SHA1(todo.PlainTextPassword);
                todo.PlainTextPassword = null;
            }
            return Repository.Store(todo);
        }
        //[Authenticate]
        public void Delete(Users request)
        {
            Repository.DeleteByIds(request.Ids);
        }
    }
    public static class Helper
    {

        public static RangeHeaderValue GetRanges(this HttpContext context, long contentSize)
        {
            RangeHeaderValue rangesResult = null;

            string rangeHeader = context.Request.Headers["Range"];

            if (!string.IsNullOrEmpty(rangeHeader))
            {
                // rangeHeader contains the value of the Range HTTP Header and can have values like:
                //      Range: bytes=0-1            * Get bytes 0 and 1, inclusive
                //      Range: bytes=0-500          * Get bytes 0 to 500 (the first 501 bytes), inclusive
                //      Range: bytes=400-1000       * Get bytes 500 to 1000 (501 bytes in total), inclusive
                //      Range: bytes=-200           * Get the last 200 bytes
                //      Range: bytes=500-           * Get all bytes from byte 500 to the end
                //
                // Can also have multiple ranges delimited by commas, as in:
                //      Range: bytes=0-500,600-1000 * Get bytes 0-500 (the first 501 bytes), inclusive plus bytes 600-1000 (401 bytes) inclusive

                // Remove "Ranges" and break up the ranges
                string[] ranges = rangeHeader.Replace("bytes=", string.Empty).Split(",".ToCharArray());

                rangesResult = new RangeHeaderValue();

                for (int i = 0; i < ranges.Length; i++)
                {
                    const int START = 0, END = 1;

                    long endByte, startByte;

                    long parsedValue;

                    string[] currentRange = ranges[i].Split("-".ToCharArray());

                    if (long.TryParse(currentRange[END], out parsedValue))
                        endByte = parsedValue;
                    else
                        endByte = contentSize - 1;


                    if (long.TryParse(currentRange[START], out parsedValue))
                        startByte = parsedValue;
                    else
                    {
                        // No beginning specified, get last n bytes of file
                        // We already parsed end, so subtract from total and
                        // make end the actual size of the file
                        startByte = contentSize - endByte;
                        endByte = contentSize - 1;
                    }

                    rangesResult.Ranges.Add(new RangeItemHeaderValue(startByte, endByte));
                }
            }

            return rangesResult;
        }
    }
    public class VideoStreamResult : FileStreamResult
    {
        // default buffer size as defined in BufferedStream type
        private const int BufferSize = 0x1000;
        private string MultipartBoundary = "<qwe123>";

        public VideoStreamResult(Stream fileStream, string contentType)
            : base(fileStream, contentType)
        {

        }

        public VideoStreamResult(Stream fileStream, MediaTypeHeaderValue contentType)
            : base(fileStream, contentType)
        {

        }

        private bool IsMultipartRequest(RangeHeaderValue range)
        {
            return range != null && range.Ranges != null && range.Ranges.Count > 1;
        }

        private bool IsRangeRequest(RangeHeaderValue range)
        {
            return range != null && range.Ranges != null && range.Ranges.Count > 0;
        }

        protected async Task WriteVideoAsync(HttpResponse response)
        {
            var bufferingFeature = response.HttpContext.Features.Get<IHttpBufferingFeature>();
            bufferingFeature?.DisableResponseBuffering();

            var length = FileStream.Length;

            var range = response.HttpContext.GetRanges(length);

            if (IsMultipartRequest(range))
            {
                response.ContentType = $"multipart/byteranges; boundary={MultipartBoundary}";
            }
            else
            {
                response.ContentType = ContentType.ToString();
            }

            response.Headers.Add("Accept-Ranges", "bytes");

            if (IsRangeRequest(range))
            {
                response.StatusCode = (int)HttpStatusCode.PartialContent;

                if (!IsMultipartRequest(range))
                {
                    response.Headers.Add("Content-Range", $"bytes {range.Ranges.First().From}-{range.Ranges.First().To}/{length}");
                }

                foreach (var rangeValue in range.Ranges)
                {
                    if (IsMultipartRequest(range)) // dunno if multipart works
                    {
                        await response.WriteAsync($"--{MultipartBoundary}");
                        await response.WriteAsync(Environment.NewLine);
                        await response.WriteAsync($"Content-type: {ContentType}");
                        await response.WriteAsync(Environment.NewLine);
                        await response.WriteAsync($"Content-Range: bytes {range.Ranges.First().From}-{range.Ranges.First().To}/{length}");
                        await response.WriteAsync(Environment.NewLine);
                    }

                    await WriteDataToResponseBody(rangeValue, response);

                    if (IsMultipartRequest(range))
                    {
                        await response.WriteAsync(Environment.NewLine);
                    }
                }

                if (IsMultipartRequest(range))
                {
                    await response.WriteAsync($"--{MultipartBoundary}--");
                    await response.WriteAsync(Environment.NewLine);
                }
            }
            else
            {
                await FileStream.CopyToAsync(response.Body);
            }
        }

        private async Task WriteDataToResponseBody(RangeItemHeaderValue rangeValue, HttpResponse response)
        {
            var startIndex = rangeValue.From ?? 0;
            var endIndex = rangeValue.To ?? 0;

            byte[] buffer = new byte[BufferSize];
            long totalToSend = endIndex - startIndex;
            int count = 0;

            long bytesRemaining = totalToSend + 1;
            response.ContentLength = bytesRemaining;

            FileStream.Seek(startIndex, SeekOrigin.Begin);

            while (bytesRemaining > 0)
            {
                try
                {
                    if (bytesRemaining <= buffer.Length)
                        count = FileStream.Read(buffer, 0, (int)bytesRemaining);
                    else
                        count = FileStream.Read(buffer, 0, buffer.Length);

                    if (count == 0)
                        return;

                    await response.Body.WriteAsync(buffer, 0, count);

                    bytesRemaining -= count;
                }
                catch (IndexOutOfRangeException)
                {
                    await response.Body.FlushAsync();
                    return;
                }
                finally
                {
                    await response.Body.FlushAsync();
                }
            }
        }

        public override async Task ExecuteResultAsync(ActionContext context)
        {
            await WriteVideoAsync(context.HttpContext.Response);
        }

    }

    public class PersonRepository
    {
        public List<User> GetByIds(Guid[] ids)
        {
            List<User> list = new List<User>();
            foreach (var id in ids)
            {
                throw new Exception("TODO");
                //var p = Database.Db.Select<User>().Where(x => x.Id == id).SingleOrDefault();
                //if(p != null)
                //    list.Add(p);
            }
            return list;
        }
        public List<User> GetAll()
        {
            throw new Exception("TODO");
            //return Database.Db.Select<User>();
        }
        public User Store(User todo)
        {
            if (todo.Id == Guid.Empty)
                todo.Id = Guid.NewGuid();

            throw new Exception("TODO");
            //Database.Db.Insert<User>(todo);
            return todo;
        }
        public void DeleteByIds(params Guid[] ids)
        {
            throw new Exception("TODO");
            //Database.Db.DeleteByIds<User>(ids);
        }
    }

    public static class Paths
    {
        public static string Data;
        public static string Output;
        public static string Web;
        public static string Watch;
    }

    static class Watcher
    {
        static FileSystemWatcher watcher;

        public static void Init(string path)
        {
            watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                IncludeSubdirectories = false,
                Filter = "*"
            };
            watcher.IncludeSubdirectories = false;
            watcher.Changed += watcher_Changed;
            watcher.Created += watcher_Created;
            watcher.Deleted += watcher_Deleted;
            watcher.EnableRaisingEvents = true;
        }

        static void watcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine(e.ChangeType + " " + e.FullPath);
        }

        static void watcher_Created(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine(e.ChangeType + " " + e.FullPath);
            AddDirectory(e.FullPath);
            Directory.Delete(e.FullPath, true);
        }

        static string MarkdownEscape(string text)
        {
            return text.Replace("_", "\\_");
        }

        private static void AddDirectory(string p)
        {
            var dir = new DirectoryInfo(p);

            using (var context = new Database())
            {
                var pics = dir.GetFiles("*.jpg").Select(x =>
            {
                Guid guid = Guid.NewGuid();
                using(var s = x.OpenRead())
                {
                        Image.Save(guid, context, MimeTypes.ImageJpg, x.Name, s);
                }
                return new {Guid = guid, Name = x.Name};
            });
            if (!pics.Any())
                return;

            string text = pics.Select(x => string.Format("{0}\r\n![responsive](/image/{1}.jpg)", MarkdownEscape(x.Name), x.Guid)).Aggregate((a, b) => a + "\r\n\r\n" + b);

            var article = new Article()
            {
                Id = Guid.NewGuid(),
                Title = dir.Name,
                Content = text,
            };
                
                context.Article.Add(article);
                context.SaveChanges();
            }
        }

        static void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine(e.ChangeType + " " + e.FullPath);
        }
    }

    class Program
    {
        //const string BaseUri = "http://*:1337/";
        const string BaseUri = "http://localhost:1337/";

        static void Main(string[] args)
        {
            string output = "output";
            string data = "data";
            //string web = new FileInfo(Assembly.GetEntryAssembly().Location).Directory.FullName;
            string web = "web";
            string watch = "watch";
            bool delete = false;

            int i = Array.IndexOf(args, "--output");
            if(i>=0)
            {
                output = args[i + 1];
            }

            i = Array.IndexOf(args, "--data");
            if (i >= 0)
            {
                data = args[i + 1];
            }

            i = Array.IndexOf(args, "--web");
            if (i >= 0)
            {
                web = args[i + 1];
            }

            i = Array.IndexOf(args, "--delete");
            if (i >= 0)
            {
                delete = bool.Parse(args[i + 1]);
            }

            i = Array.IndexOf(args, "--watch");
            if (i >= 0)
            {
                watch = args[i + 1];
            }

            data = new DirectoryInfo(data).FullName;
            web = new DirectoryInfo(web).FullName;
            output = new DirectoryInfo(output).FullName;
            watch = new DirectoryInfo(watch).FullName;

            Paths.Web = web;
            Paths.Data = data;
            Paths.Output = output;
            Paths.Watch = watch;
            GeneratorService.Delete = delete;

            Database.Init();

            bool gen_cache = !FileCache.CacheDirExists();

            Console.WriteLine("Data directory: {0}", data);
            Console.WriteLine("Web directory: {0}", web);
            Console.WriteLine("Output directory: {0}", output);

            if (Directory.Exists(watch))
            {
                Console.WriteLine("Watch directory: {0}", watch);
                Watcher.Init(watch);
            }

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();

            
            Console.WriteLine("Listening on " + BaseUri);


            if(gen_cache)
            {
                Console.WriteLine("Generating image cache...");
                Image.GenerateCache();
                Console.WriteLine("done.");
            }

            string line;
            do
            {
                Console.WriteLine("Press return to generate the page");
                line = Console.ReadLine();
                if (line.Length == 0)
                {
                    Static.Generate(output, data, delete);
                }
            }
            while (line.Length == 0);
        }
    }
}
