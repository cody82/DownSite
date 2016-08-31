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

namespace DownSite
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
}