﻿using ServiceStack.OrmLite.VistaDB;

namespace ServiceStack.OrmLite
{
    public static class VistaDbDialect
    {
        public static VistaDbDialectProvider Provider { get { return VistaDbDialectProvider.Instance; } }
    }
}
