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
using System.Data;
using ServiceStack.OrmLite.Sqlite;
using ServiceStack.OrmLite;

namespace WebTest
{
    public class Database
    {
        public static IDbConnection OpenDbConnection(string connString)
        {
            OrmLiteConfig.DialectProvider = SqliteOrmLiteDialectProvider.Instance;
            return connString.OpenDbConnection();
        }
    }
}