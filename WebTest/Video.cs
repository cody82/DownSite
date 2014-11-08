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

namespace WebTest
{
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
}