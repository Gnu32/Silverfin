// ===========================
// =============Silverfin=====
// MongoDB Datamanager, based
// on SQLiteDataManager.cs
// by Major Rasputin
//
// This file is licensed under
// SIMPL 2012
// http://simplaza.net/hax/SIMPL

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;
using Aurora.DataManager.Migration;
using Aurora.Framework;
using OpenMetaverse;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoQuery = MongoDB.Driver.Builders.Query;
using log4net.Core;

namespace Aurora.DataManager.MongoDB
{
    public class MongoDBLoader : DataManagerBase
    {
        private MongoServer m_MongoServer;
        private MongoDatabase m_Database;
        private static string[] m_selectAll = { "*" };
           
        public override string Identifier
        {
            get { return "MongoDBConnector"; }
        }

        public override bool NoSQL
        {
            get { return true; }
        }

        public override void ConnectToDatabase(string connectionString, string migratorName, bool validateTables)
        {
            MongoUrl connectionUrl = new MongoUrl(connectionString);
            string database = connectionUrl.DatabaseName ?? Constants.DatabaseMain;

            m_MongoServer = MongoServer.Create(connectionUrl);
            m_Database = m_MongoServer[database];

            var migrationManager = new MigrationManager(this, migratorName, validateTables);
            migrationManager.DetermineOperation();
            migrationManager.ExecuteOperation();
        }

        public override List<string> Query(string[] wantedValue, string table, QueryFilter queryFilter, Dictionary<string, bool> sort, uint? start, uint? count)
        {
            // Retrieve the table (called collection in mongospeak) and create new query
            MongoCollection<BsonDocument> collection = m_Database[table];
            QueryDocument query = new QueryDocument();
            
            // If we get filters, walk through them and apply to the query document
            if (queryFilter != null && queryFilter.Count > 0)
                ApplyQueryFilter(queryFilter, ref query);

            // Execute our query with filters, this is done without any settings first
            MongoCursor<BsonDocument> results = collection.Find(query);

            // "*" doesn't work in Mongo, but that's ok because it automatically returns all fields.
            if (wantedValue != m_selectAll)
                results.SetFields(wantedValue);

            // Handle sorts
            if (sort != null && sort.Count > 1)
            {
                SortByDocument sortby = new SortByDocument();

                // Convert booleans to 1 and -1 for ASC and DESC respectively
                foreach (KeyValuePair<string, bool> sortrule in sort)
                    sortby.Add(sortrule.Key, sortrule.Value ? 1 : -1);

                results.SetSortOrder();
            }

            // Handle LIMIT's
            if (start.HasValue)
                results.SetSkip((int)start);

            if (count.HasValue)
                results.SetLimit((int)count);

            List<string> finalResults = new List<string>();
            foreach (BsonDocument row in results)
            {
                finalResults.Add(row.ToString());
            }

            return finalResults;
        }

            // MongoDB-specific function to convert queryfilters into MongoQuery stuff
            private static void ApplyQueryFilter(QueryFilter queryfilter, ref QueryDocument query)
            {
                if (queryfilter.Count > 0)
                {
                    BsonArray andfilters = new BsonArray();
                    BsonArray orfilters = new BsonArray();

                    #region Equality handlers

                    if (queryfilter.andFilters.Count > 0)
                        andfilters.Add( new BsonDocument(queryfilter.andFilters) );

                    if (queryfilter.orFilters.Count > 0)
                        orfilters.Add( new BsonDocument(queryfilter.orFilters) );

                    if (queryfilter.orMultiFilters.Count > 0)
                    {
                        // In MongoDB, we should use the "$in" query to check if elements are in a field
                        // Hence, we collect all the same-field queries into one document

                        BsonDocument ormultiquery = new BsonDocument();
                        foreach (KeyValuePair<string, List<object>> field in queryfilter.orMultiFilters){
                            BsonArray values = new BsonArray(field.Value);
                            ormultiquery.Add(field.Key, new BsonDocument("$in", values));
                        }

                        orfilters.Add(ormultiquery);
                    }

                    #endregion

                                        #region "Like" handlers

                if (queryfilter.andLikeFilters.Count > 0)
                    andfilters.Add(new BsonDocument(FilterValues2Regex(queryfilter.andLikeFilters)));

                if (queryfilter.orLikeFilters.Count > 0)
                    orfilters.Add(new BsonDocument(FilterValues2Regex(queryfilter.orLikeFilters)));

                #endregion

                                                                        #region Greater/Less than handlers

                foreach (var filter in queryfilter.andGreaterThanFilters)
                    andfilters.Add( new BsonDocument(filter.Key, new BsonDocument("$gt", filter.Value)) );

                foreach (var filter in queryfilter.orGreaterThanFilters)
                    orfilters.Add( new BsonDocument(filter.Key, new BsonDocument("$gt", filter.Value)) );

                foreach (var filter in queryfilter.andGreaterThanEqFilters)
                    andfilters.Add( new BsonDocument(filter.Key, new BsonDocument("$gte", filter.Value)) );

                foreach (var filter in queryfilter.andLessThanFilters)
                    andfilters.Add( new BsonDocument(filter.Key, new BsonDocument("$lt", filter.Value)) );

                foreach (var filter in queryfilter.orLessThanFilters)
                    orfilters.Add( new BsonDocument(filter.Key, new BsonDocument("$lt", filter.Value)) );

                foreach (var filter in queryfilter.andLessThanEqFilters)
                    andfilters.Add( new BsonDocument(filter.Key, new BsonDocument("$lte", filter.Value)) );

                #endregion

                    foreach (QueryFilter subFilter in queryfilter.subFilters)
                        ApplyQueryFilter(queryfilter, ref query);

                    if (andfilters.Count > 0) query.Add("$and", andfilters);
                    if (orfilters.Count > 0) query.Add("$or", orfilters);

                }
            }

            // Converts string values in a query filter into regexes
            private static Dictionary<string, BsonRegularExpression> FilterValues2Regex(Dictionary<string, string> queryfilter)
            {
                Dictionary<string, BsonRegularExpression> newqueryfilter = new Dictionary<string, BsonRegularExpression>();
                foreach (KeyValuePair<string, string> filter in queryfilter)
                {
                    string fixedvalue = filter.Value.Replace("%", "/");
                    BsonRegularExpression newvalue = new BsonRegularExpression(fixedvalue);

                    newqueryfilter.Add(filter.Key, newvalue);
                }

                return newqueryfilter;
            }
        
        public override List<string> QueryFullData(string whereClause, string table, string wantedValue)
        {
            throw new NotImplementedException();
        }

        public override IDataReader QueryData(string whereClause, string table, string wantedValue)
        {
            throw new NotImplementedException();
        }

        public override Dictionary<string, List<string>> QueryNames(string[] keyRow, object[] keyValue, string table, string wantedValue)
        {
            throw new NotImplementedException();
        }

        public override bool Insert(string table, object[] values)
        {
            throw new NotImplementedException();
        }

        public override bool InsertMultiple(string table, List<object[]> values)
        {
            throw new NotImplementedException();
        }

        public override bool Insert(string table, string[] keys, object[] values)
        {
            throw new NotImplementedException();
        }

        public override bool Delete(string table, string[] keys, object[] values)
        {
            MongoCollection<BsonDocument> collection = m_Database[table];
            var query = new QueryDocument( Zip(keys, values) );
            var result = collection.Remove(query);

            return (result != null && result.DocumentsAffected > 0) ? true : false;
        }

        public override bool Delete(string table, string whereclause)
        {
            throw new NotImplementedException();
        }

        public override bool DeleteByTime(string table, string key)
        {
            throw new NotImplementedException();
        }

        public override bool Insert(string table, object[] values, string updateKey, object updateValue)
        {
            throw new NotImplementedException();
        }

        public override bool Update(string table, object[] setValues, string[] setRows, string[] keyRows, object[] keyValues)
        {
            throw new NotImplementedException();
        }

        public override bool DirectUpdate(string table, object[] setValues, string[] setRows, string[] keyRows, object[] keyValues)
        {
            throw new NotImplementedException();
        }

        public override void CloseDatabase()
        {
            throw new NotImplementedException();
        }

        public override bool TableExists(string table)
        {
            return m_Database.CollectionExists(table);
        }

        public override void CreateTable(string table, ColumnDefinition[] columns)
        {
            if (TableExists(table)) throw new DataManagerException("MongoDB collection '" + table + "' already exists");

            // This is literally the table creation process.
            MongoCollection<BsonDocument> collection = m_Database[table];
            ApplySchema(collection, columns);
        }

        public override bool Replace(string table, string[] keys, object[] values)
        {
            if (!TableExists(table)) return false;

            MongoCollection<BsonDocument> collection = m_Database[table];

            var document = new BsonDocument( Zip(keys, values) );
            var result = collection.Save(document);
            
            return (result != null && result.DocumentsAffected > 0) ? true : false;
        }

        public override bool DirectReplace(string table, string[] keys, object[] values)
        {
            throw new NotImplementedException();
        }

        public override IGenericData Copy()
        {
            return new MongoDBLoader();
        }

        public override void DropTable(string tableName)
        {
            throw new NotImplementedException();
        }

        public override string FormatDateTimeString(int time)
        {
            throw new NotImplementedException();
        }

        public override string IsNull(string Field, string defaultValue)
        {
            throw new NotImplementedException();
        }

        public override string ConCat(string[] toConcat)
        {
            throw new NotImplementedException();
        }

        public override void UpdateTable(string table, ColumnDefinition[] columns, Dictionary<string, string> renameColumns)
        {
            throw new NotImplementedException();
        }

        public override string GetColumnTypeStringSymbol(ColumnTypes type)
        {
            throw new NotImplementedException();
        }

        public override void ForceRenameTable(string oldTableName, string newTableName)
        {
            throw new NotImplementedException();
        }

        protected override void CopyAllDataBetweenMatchingTables(string sourceTableName, string destinationTableName, ColumnDefinition[] columnDefinitions)
        {
            throw new NotImplementedException();
        }

        protected override List<ColumnDefinition> ExtractColumnsFromTable(string table)
        {
            MongoCollection<BsonDocument> collection = m_Database[table];
            var rows = collection.FindAll();
            var cols = new List<string>();

            foreach (var row in rows)
                cols.AddRange(row.Names);

            IEnumerable<string> columns = cols.Distinct<string>();

            //var cols = collection.
            throw new NotImplementedException();
        }

        // FIXME: This is extremely dirty, this shouldn't be nessecary with a NoSQL engine.
        // Whole DataManagerBase architecture needs to be rewritten to accomidate NoSQL
        private static void ApplySchema(MongoCollection collection, ColumnDefinition[] defs)
        {
            // If this table already has a schema, retrive it
            BsonDocument existingSchema = GetSchema(collection);

            // Setup directory for primary keys, query holding document, options and set options
            var schema = new BsonDocument();
            var final = new BsonDocument("_schema", schema);
            var options = IndexOptions.SetBackground(true).SetUnique(true);

            // Drop all existing indexes, then use previous ID
            collection.DropAllIndexes();
            if (existingSchema != null)
                final.Add("_id", existingSchema["_id"]);            

            // Iterate through columns; if primary, add to indexes. Register their names and types.
            foreach (ColumnDefinition column in defs)
            {
                if (column.IsPrimary)
                    collection.EnsureIndex(new IndexKeysDocument(column.Name, BsonBoolean.True), options);
                schema.Add(column.Name, column.Type.ToString());
            }

            collection.Save(final);
        }

        private static BsonDocument GetSchema(MongoCollection collection)
        {
            var search = collection.FindAllAs<BsonDocument>();
            search.SetFields(new[] { "_schema" });
            search.SetLimit(1);

            BsonDocument row = search.First();
            return row.Contains("_schema") ? row : null;
        }

        // MongoDB-specific helper function to deal with the weird list parameters
        private static Dictionary<object, object> Zip(IList<object> keys, IList<object> values)
        {
            if (keys.Count != values.Count) throw new ArgumentException("Keys and Values collection have inequal amount of elements");

            var dict = new Dictionary<object, object>();
            for (int i = 0; i < keys.Count; i++)
                dict.Add(keys[i], values[i].ToString());

            return dict;
        }
    }
}
