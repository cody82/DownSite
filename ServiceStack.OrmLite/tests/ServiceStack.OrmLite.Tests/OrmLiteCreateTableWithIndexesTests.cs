using NUnit.Framework;
using ServiceStack.Text;
using ServiceStack.Common.Tests.Models;

namespace ServiceStack.OrmLite.Tests
{
	[TestFixture]
	public class OrmLiteCreateTableWithIndexesTests 
		: OrmLiteTestBase
	{

		[Test]
		public void Can_create_ModelWithIndexFields_table()
		{
            using (var db = OpenDbConnection())
			{
				db.CreateTable<ModelWithIndexFields>(true);

				var sql = OrmLiteConfig.DialectProvider.ToCreateIndexStatements(typeof(ModelWithIndexFields)).Join();

			    var indexName = "idx_modelwithindexfields_name";
			    var uniqueName = "uidx_modelwithindexfields_uniquename";

			    if (Dialect == Dialect.Oracle)
			    {
			        indexName = OrmLiteConfig.DialectProvider.NamingStrategy.ApplyNameRestrictions(indexName);
                    uniqueName = OrmLiteConfig.DialectProvider.NamingStrategy.ApplyNameRestrictions(uniqueName);
			    }

                Assert.IsTrue(sql.Contains(indexName));
				Assert.IsTrue(sql.Contains(uniqueName));
			}
		}

		[Test]
		public void Can_create_ModelWithCompositeIndexFields_table()
		{
            if (Dialect == Dialect.PostgreSql) return; //Incompatible ColumnName in Attribute

            using (var db = OpenDbConnection())
			{
				db.CreateTable<ModelWithCompositeIndexFields>(true);

				var sql = OrmLiteConfig.DialectProvider.ToCreateIndexStatements(typeof(ModelWithCompositeIndexFields)).Join();

                var indexName = "idx_modelwithcompositeindexfields_name";
                var compositeName = "idx_modelwithcompositeindexfields_composite1_composite2";

                if (Dialect == Dialect.Oracle)
                {
                    indexName = OrmLiteConfig.DialectProvider.NamingStrategy.ApplyNameRestrictions(indexName);
                    compositeName = OrmLiteConfig.DialectProvider.NamingStrategy.ApplyNameRestrictions(compositeName);
                }

                Assert.IsTrue(sql.Contains(indexName));
				Assert.IsTrue(sql.Contains(compositeName));
			}
		}

        [Test]
        public void Can_create_ModelWithNamedCompositeIndex_table()
        {
            if (Dialect == Dialect.PostgreSql) return; //Incompatible ColumnName in Attribute

            using (var db = OpenDbConnection())
            {
                db.CreateTable<ModelWithNamedCompositeIndex>(true);

                var sql = OrmLiteConfig.DialectProvider.ToCreateIndexStatements(typeof(ModelWithNamedCompositeIndex)).Join();

                var indexName = "idx_modelwithnamedcompositeindex_name";
                var compositeName = "uidx_modelwithnamedcompositeindexfields_composite1_composite2";

                if (Dialect == Dialect.Oracle)
                {
                    indexName = OrmLiteConfig.DialectProvider.NamingStrategy.ApplyNameRestrictions(indexName);
                    compositeName = OrmLiteConfig.DialectProvider.NamingStrategy.ApplyNameRestrictions(compositeName);
                }

                Assert.IsTrue(sql.Contains(indexName));
                Assert.IsTrue(sql.Contains("custom_index_name"));
                Assert.IsFalse(sql.Contains(compositeName));
            }
        }

	}
}