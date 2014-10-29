using System;
using System.Data;
using System.IO;
using ServiceStack.OrmLite.Sqlite;

namespace ServiceStack.OrmLite.Tests
{
    public class Config
    {
        public static string SqliteMemoryDb = ":memory:";
        public static string SqliteFileDir = "~/App_Data/".MapAbsolutePath();
        //public static string SqliteFileDb = "~/App_Data/db.sqlite".MapAbsolutePath();
        public static string SqliteFileDb = "db.sqlite".MapAbsolutePath();
    }

    public class OrmLiteTestBase
    {
        protected virtual string ConnectionString { get; set; }

        protected virtual string GetFileConnectionString()
        {
            var connectionString = Config.SqliteFileDb;
            if (File.Exists(connectionString))
                File.Delete(connectionString);

            return connectionString;
        }

        protected void CreateNewDatabase()
        {
            if (ConnectionString.Contains(".sqlite"))
                ConnectionString = GetFileConnectionString();
        }

        public void TestFixtureSetUp()
        {
            OrmLiteConfig.DialectProvider = SqliteOrmLiteDialectProvider.Instance;
            //ConnectionString = ":memory:";
            ConnectionString = @"C:\Users\cody\Documents\FitnessService\FitnessService\db.sqlite3";
        }

        public void Log(string text)
        {
            Console.WriteLine(text);
        }

        public IDbConnection InMemoryDbConnection { get; set; }

        public IDbConnection OpenDbConnection(string connString = null)
        {
            connString = connString ?? ConnectionString;
            if (connString == ":memory:")
            {
                if (InMemoryDbConnection == null)
                {
                    var dbConn = connString.OpenDbConnection();
                    InMemoryDbConnection = new OrmLiteConnectionWrapper(dbConn)
                    {
                        DialectProvider = OrmLiteConfig.DialectProvider,
                        AutoDisposeConnection = false,
                    };
                }

                return InMemoryDbConnection;
            }

            return connString.OpenDbConnection();
        }
    }
}