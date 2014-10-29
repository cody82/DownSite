﻿using System;
using System.Collections.Generic;
using System.Data;

namespace ServiceStack.OrmLite
{
    public class OrmLiteSPStatement
    {
        private IDbCommand command { get; set; }

        public OrmLiteSPStatement(IDbCommand cmd)
        {
            command = cmd;
        }

        public List<T> ConvertToList<T>()
        {
            if (typeof(T).IsPrimitive || typeof(T) == typeof(string))
                throw new Exception("Type " + typeof(T).Name + " is a primitive type. Use ConvertScalarToList function.");

            IDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
                return reader.ConvertToList<T>();
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        public List<T> ConvertToScalarList<T>()
        {
            if (!((typeof(T).IsPrimitive) || (typeof(T) == typeof(string)) || (typeof(T) == typeof(String))))
                throw new Exception("Type " + typeof(T).Name + " is a non primitive type. Use ConvertToList function.");

            IDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
                return reader.Column<T>();
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        public T ConvertTo<T>()
        {
            if (typeof(T).IsPrimitive || typeof(T) == typeof(string))
                throw new Exception("Type " + typeof(T).Name + " is a primitive type. Use ConvertScalarTo function.");

            IDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
                return reader.ConvertTo<T>();
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        public T ConvertToScalar<T>()
        {
            if (!((typeof(T).IsPrimitive) || (typeof(T) == typeof(string)) || (typeof(T) == typeof(String))))
                throw new Exception("Type " + typeof(T).Name + " is a non primitive type. Use ConvertTo function.");

            IDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
                return reader.Scalar<T>();
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        public List<T> ConvertFirstColumnToList<T>()
        {
            if (!((typeof(T).IsPrimitive) || (typeof(T) == typeof(string)) || (typeof(T) == typeof(String))))
                throw new Exception("Type " + typeof(T).Name + " is a non primitive type. Only primitive type can be used.");

            IDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
                return reader.Column<T>();
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        public HashSet<T> ConvertFirstColumnToListDistinct<T>()
        {
            if (!((typeof(T).IsPrimitive) || (typeof(T) == typeof(string)) || (typeof(T) == typeof(String))))
                throw new Exception("Type " + typeof(T).Name + " is a non primitive type. Only primitive type can be used.");

            IDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
                return reader.ColumnDistinct<T>();
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        public int ExecuteNonQuery()
        {
            return command.ExecuteNonQuery();
        }

        public bool HasResult()
        {
            IDataReader reader = null;
            try
            {
                reader = command.ExecuteReader();
                if (reader.Read())
                    return true;
                else
                    return false;
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
    }
}
