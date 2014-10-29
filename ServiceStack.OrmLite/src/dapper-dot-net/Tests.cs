﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace SqlMapper
{
    static class TestAssertions
    {

        public static void IsEquals<T>(this T obj, T other)
        {
            if (!obj.Equals(other))
            {
                throw new ApplicationException(string.Format("{0} should be equals to {1}", obj, other));
            }
        }

        public static void IsSequenceEqual<T>(this IEnumerable<T> obj, IEnumerable<T> other)
        {
            if (!obj.SequenceEqual(other))
            {
                throw new ApplicationException(string.Format("{0} should be equals to {1}", obj, other));
            }
        }

        public static void IsFalse(this bool b)
        {
            if (b)
            {
                throw new ApplicationException("Expected false");
            }
        }

        public static void IsNull(this object obj)
        {
            if (obj != null)
            {
                throw new ApplicationException("Expected null");
            }
        }

    }

    class Tests
    {
       
        SqlConnection connection = Program.GetOpenConnection();

        public void SelectListInt()
        {
            connection.Query<int>("select 1 union all select 2 union all select 3")
              .IsSequenceEqual(new[] { 1, 2, 3 });
        }

        public void PassInIntArray()
        {
            connection.Query<int>("select * from (select 1 as Id union all select 2 union all select 3) as X where Id in @Ids", new { Ids = new int[] { 1, 2, 3 }.AsEnumerable() })
             .IsSequenceEqual(new[] { 1, 2, 3 });
        }


        public void TestDoubleParam()
        {
			connection.Query<double>("select @d", new { d = 0.1d }).First()
                .IsEquals(0.1d);
        }

        public void TestBoolParam()
        {
			connection.Query<bool>("select @b", new { b = false }).First()
                .IsFalse();
        }

        public void TestStrings()
        {
			connection.Query<string>(@"select 'a' a union select 'b'")
                .IsSequenceEqual(new[] { "a", "b" });
        }

        public class Dog
        {
            public int? Age { get; set; }
            public Guid Id { get; set; }
            public string Name { get; set; }
            public float? Weight { get; set; }

            public int IgnoredProperty { get { return 1; } }
        }

        public void TestStrongType()
        {
            var guid = Guid.NewGuid();
			var dog = connection.Query<Dog>("select Age = @Age, Id = @Id", new { Age = (int?)null, Id = guid });
            
            dog.Count()
                .IsEquals(1);

            dog.First().Age
                .IsNull();

            dog.First().Id
                .IsEquals(guid);
        }

		//public void TestExpando()
		//{
		//    var rows = connection.Query("select 1 A, 2 B union all select 3, 4");

		//    ((int)rows[0].A)
		//        .IsEquals(1);

		//    ((int)rows[0].B)
		//        .IsEquals(2);

		//    ((int)rows[1].A)
		//        .IsEquals(3);

		//    ((int)rows[1].B)
		//        .IsEquals(4);
		//}

        public void TestStringList()
        {
			connection.Query<string>("select * from (select 'a' as x union all select 'b' union all select 'c') as T where x in @strings", new { strings = new[] { "a", "b", "c" } })
                .IsSequenceEqual(new[] {"a","b","c"});
        }

        public void TestExecuteCommand()
        {
			connection.Execute(@"
    set nocount on 
    create table #t(i int) 
    set nocount off 
    insert #t 
    select @a a union all select @b 
    set nocount on 
    drop table #t", new {a=1, b=2 }).IsEquals(2);
        }

        public void TestMassiveStrings()
        { 
            var str = new string('X', 20000);
			connection.Query<string>("select @a", new { a = str }).First()
                .IsEquals(str);
        }

    }
}
