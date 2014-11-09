using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Drawing;
using System.Web;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using ServiceStack.Text.Json;
using ServiceStack.Text;

namespace WebTest
{
    public interface IVideoThumbnailer
    {
        bool MakeThumbnail(string input, string output);
    }

    public class VideoStream
    {
        public int Index { get; set; }
        public string Codec_Type { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class VideoFormat
    {
        public string Filename { get; set; }
    }

    public class VideoInfo
    {
        public List<VideoStream> Streams { get; set; }
        public VideoFormat Format { get; set; }

        public Size Resolution
        {
            get
            {
                VideoStream s = Streams.First(x => x.Codec_Type == "video");
                return new Size(s.Width, s.Height);
            }
        }
    }

    public class VideoConverter
    {
        public static bool Resize(string input, string output, int width, int height)
        {
            string param = string.Format(@"-i ""{0}"" -y -vf scale={2}:{3} ""{1}""", input, output, width, height);
            ProcessStartInfo psi = new ProcessStartInfo("ffmpeg", param)
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
                if (File.Exists(output))
                    File.Delete(output);
                return false;
            }
        }
    }

    public class VideoProbe
    {
        static bool avprobe;
        static bool ffprobe;

        static VideoProbe()
        {
            avprobe = VideoThumbnailer.CheckProgram("avprobe");
            ffprobe = VideoThumbnailer.CheckProgram("ffprobe");
        }

        public static VideoInfo Probe(string filename)
        {
            if (ffprobe)
                return FfProbe(filename);
            else if (avprobe)
                return AvProbe(filename);
            return null;
        }


        protected static VideoInfo AvProbe(string filename)
        {
            return Probe("avprobe", filename);
        }

        protected static VideoInfo Probe(string program, string filename)
        {
            ProcessStartInfo psi = new ProcessStartInfo(program, "-v 0 -show_format -show_streams -of json \"" + filename + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            Process p = Process.Start(psi);

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode == 0)
            {
                var vi = JsonSerializer.DeserializeFromString<VideoInfo>(output);
                return vi;
            }
            else
                return null;
        }

        protected static VideoInfo FfProbe(string filename)
        {
            return Probe("ffprobe", filename);
        }
    }

    public class VideoThumbnailer
    {
        public static bool CheckProgram(string exe)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, "-version")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            try
            {
                var p = Process.Start(psi);
                p.WaitForExit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static bool _UseFFmpeg = false;
        static bool _FFmpeg_tested = false;

        static public bool UseFFmpeg
        {
            get
            {
                if (_FFmpeg_tested)
                    return _UseFFmpeg;

                Console.WriteLine("checking ffmpeg...");

                _FFmpeg_tested = true;

                return _UseFFmpeg = CheckProgram("ffmpeg");
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

                _Avconv_tested = true;

                return _UseAvconv = CheckProgram("avconv");
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
}