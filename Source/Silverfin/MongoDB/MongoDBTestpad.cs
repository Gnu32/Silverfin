using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace MongoDBTestpad
{
    class MongoDBTestpad
    {
        public static MongoServer m_MongoServer;
        public static MongoDatabase m_Database;

        static void Main(string[] args)
        {
            m_MongoServer = MongoServer.Create();

            //if (m_MongoServer.DatabaseExists("Testpad"))
            //    m_MongoServer.DropDatabase("Testpad");
            m_Database = m_MongoServer["Testpad"];

            var TestTable = m_Database["Test"];
            Dictionary<string, BsonBoolean> primaries = new Dictionary<string, BsonBoolean>();
            var options = IndexOptions.SetBackground(true).SetUnique(true);
            var schema = new BsonDocument();

            primaries.Add("primary1", BsonBoolean.True);
            primaries.Add("primary2", BsonBoolean.True);
            schema.Add("primary1", "Int");
            schema.Add("priaaaaamary2", "String");
            schema.Add("field3", "Int");
            schema.Add("sssss", "Cyrinx");

            if (primaries.Count > 0)
                foreach (var primary in primaries)
                    TestTable.EnsureIndex(new IndexKeysDocument(primary.Key, primary.Value), options);

            var final = new BsonDocument("_schema", schema);
            final.Add("_id", "4f1b4f96ec72a103746b132f");
            TestTable.Save(final);
            TestTable.Insert(schema);

            var res = TestTable.FindAllAs<BsonDocument>();
            res.Fields = Fields.Include(new[] { "_schema" });
            res.Limit = 1;

            BsonDocument search = res.First();
            var schema_row = search.Contains("_schema");
            

        }
    }
}
