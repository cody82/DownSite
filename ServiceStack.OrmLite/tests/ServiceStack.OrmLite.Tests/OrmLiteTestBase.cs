using System;
using System.Data;
using System.Data.Common;
using System.IO;
using NUnit.Framework;
using ServiceStack.Logging;
using ServiceStack.OrmLite.Oracle;

namespace ServiceStack.OrmLite.Tests
{
    public class Config
    {
        public static string SqliteMemoryDb = ":memory:";
        public static string SqliteFileDir = "~/App_Data/".MapAbsolutePath();
        public static string SqliteFileDb = "~/App_Data/db.sqlite".MapAbsolutePath();
        public static string SqlServerDb = "~/App_Data/Database1.mdf".MapAbsolutePath();
        public static string SqlServerBuildDb = "Server={0};Database=test;User Id=test;Password=test;".Fmt(Environment.GetEnvironmentVariable("CI_HOST"));
        //public static string SqlServerBuildDb = "Data Source=localhost;Initial Catalog=TestDb;Integrated Security=SSPI;Connect Timeout=120;MultipleActiveResultSets=True";

        public static IOrmLiteDialectProvider DefaultProvider = SqlServerDialect.Provider;
        public static string DefaultConnection = SqlServerBuildDb;

        public static string GetDefaultConnection()
        {
            OrmLiteConfig.DialectProvider = DefaultProvider;
            return DefaultConnection;
        }

        public static IDbConnection OpenDbConnection()
        {
            return GetDefaultConnection().OpenDbConnection();
        }
    }

	public class OrmLiteTestBase
	{
	    protected virtual string ConnectionString { get; set; }

	    public OrmLiteTestBase() {}

	    public OrmLiteTestBase(Dialect dialect)
	    {
	        Dialect = dialect;
            Init();
        }

	    protected string GetConnectionString()
		{
			return GetFileConnectionString();
		}

	    public static OrmLiteConnectionFactory CreateSqlServerDbFactory()
	    {
            var dbFactory = new OrmLiteConnectionFactory(Config.SqlServerBuildDb, SqlServerDialect.Provider);
	        return dbFactory;
	    }

        public static OrmLiteConnectionFactory CreateSqliteMemoryDbFactory()
        {
            var dbFactory = new OrmLiteConnectionFactory(Config.SqliteMemoryDb, SqliteDialect.Provider);
            return dbFactory;
        }

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

        public Dialect Dialect = Dialect.Sqlite;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Init();
        }

        private void Init()
        {
	        LogManager.LogFactory = new ConsoleLogFactory(debugEnabled: false);

	        switch (Dialect)
	        {
	            case Dialect.Sqlite:
	                OrmLiteConfig.DialectProvider = SqliteDialect.Provider;
	                ConnectionString = GetFileConnectionString();
	                ConnectionString = ":memory:";
	                return;
	            case Dialect.SqlServer:
	                OrmLiteConfig.DialectProvider = SqlServerDialect.Provider;
	                ConnectionString = Config.SqlServerBuildDb;
	                return;
	            case Dialect.MySql:
	                OrmLiteConfig.DialectProvider = MySqlDialect.Provider;
	                ConnectionString = "Server=localhost;Database=test;UID=root;Password=test";
	                return;
	            case Dialect.PostgreSql:
	                OrmLiteConfig.DialectProvider = PostgreSqlDialect.Provider;
	                ConnectionString =
	                    "Server=localhost;Port=5432;User Id=test;Password=test;Database=test;Pooling=true;MinPoolSize=0;MaxPoolSize=200";
	                return;
	            case Dialect.SqlServerMdf:
	                OrmLiteConfig.DialectProvider = SqlServerDialect.Provider;
	                ConnectionString = "~/App_Data/Database1.mdf".MapAbsolutePath();
	                ConnectionString = Config.GetDefaultConnection();
	                return;
                case Dialect.Oracle:
                    OrmLiteConfig.DialectProvider = OracleDialect.Provider;
                    return;
                case Dialect.VistaDb:
                    OrmLiteConfig.DialectProvider = VistaDbDialect.Provider;
                    VistaDbDialect.Provider.UseLibraryFromGac = true;

                    var connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["myVDBConnection"];
                    var factory = DbProviderFactories.GetFactory(connectionString.ProviderName);
                    using (var db = factory.CreateConnection())
                    using (var cmd = db.CreateCommand())
                    {
                        var tmpFile = Path.GetTempPath().CombineWith(Guid.NewGuid().ToString("n") + ".vb5");
                        cmd.CommandText = @"CREATE DATABASE '|DataDirectory|{0}', PAGE SIZE 4, LCID 1033, CASE SENSITIVE FALSE;"
                            .Fmt(tmpFile);
                        cmd.ExecuteNonQuery();
                        ConnectionString = "Data Source={0};".Fmt(tmpFile);
                    }

                    return;
            }
	    }

	    public void Log(string text)
		{
			Console.WriteLine(text);
		}

        public IDbConnection InMemoryDbConnection { get; set; }

        public virtual IDbConnection OpenDbConnection(string connString = null)
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

        protected void SuppressIfOracle(string reason, params object[] args)
        {
            // Not Oracle if this base class used
        }
	}
}