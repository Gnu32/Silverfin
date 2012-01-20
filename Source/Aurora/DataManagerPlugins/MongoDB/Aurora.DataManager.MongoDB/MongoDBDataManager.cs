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

        public override void ConnectToDatabase(string connectionString, string migratorName, bool validateTables)
        {
            MongoUrl connectionUrl = new MongoUrl(connectionString);

            // Note: No need for migrators just yet...
            m_MongoServer = MongoServer.Create(connectionUrl);

            if (connectionUrl.DatabaseName == null)
            {
                MainConsole.Instance.Log(Level.Debug, "[MongoDB]: No database name specified, assuming '" + Constants.DatabaseMain + "'");
                m_Database = m_MongoServer[Constants.DatabaseMain];
            }
            else
            {
                m_Database = m_MongoServer[connectionUrl.DatabaseName];
            }
            
        }

        public override List<string> Query(string[] wantedValue, string table, QueryFilter queryFilter, Dictionary<string, bool> sort, uint? start, uint? count)
        {
            // Retrieve the table (called collection in mongospeak) and create new query
            MongoCollection<BsonDocument> collection = m_Database[table];
            QueryDocument query = new QueryDocument();
            
            if (queryFilter != null && queryFilter.Count > 0)
                ApplyQueryFilter(queryFilter, ref query);

            MongoCursor<BsonDocument> results = collection.Find(query);

            // "*" doesn't work in Mongo, but that's ok because it automatically returns all fields.
            if (wantedValue != m_selectAll)
            {
                results.SetFields(wantedValue);
            }

            throw new NotImplementedException();
        }

        // MongoDB-specific function to convert queryfilters into MongoQuery stuff
        private static void ApplyQueryFilter(QueryFilter queryfilter, ref QueryDocument query)
        {
            if (queryfilter.Count > 0)
            {
                BsonDocument andfilters = new BsonDocument();
                BsonDocument orfilters = new BsonDocument();

                #region Equality handlers

                if (queryfilter.andFilters.Count > 0)
                    andfilters.Add(queryfilter.andFilters);

                if (queryfilter.orFilters.Count > 0)
                    orfilters.Add(queryfilter.orFilters);

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
                    andfilters.Add(filter.Key, new BsonDocument("$gt", filter.Value));

                foreach (var filter in queryfilter.orGreaterThanFilters)
                    orfilters.Add(filter.Key, new BsonDocument("$gt", filter.Value));

                foreach (var filter in queryfilter.andGreaterThanEqFilters)
                    andfilters.Add(filter.Key, new BsonDocument("$gte", filter.Value));

                foreach (var filter in queryfilter.andLessThanFilters)
                    andfilters.Add(filter.Key, new BsonDocument("$lt", filter.Value));

                foreach (var filter in queryfilter.orLessThanFilters)
                    orfilters.Add(filter.Key, new BsonDocument("$lt", filter.Value));

                foreach (var filter in queryfilter.andLessThanEqFilters)
                    andfilters.Add(filter.Key, new BsonDocument("$lte", filter.Value));

                #endregion

                foreach (QueryFilter subFilter in queryfilter.subFilters)
                    ApplyQueryFilter(queryfilter, ref query);

                if (andfilters.ElementCount > 0) query.Add("$and", andfilters);
                if (orfilters.ElementCount > 0) query.Add("$or", orfilters);

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
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        public override void CreateTable(string table, ColumnDefinition[] columns)
        {
            throw new NotImplementedException();
        }

        public override bool Replace(string table, string[] keys, object[] values)
        {
            throw new NotImplementedException();
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

        protected override List<ColumnDefinition> ExtractColumnsFromTable(string tableName)
        {
            throw new NotImplementedException();
        }
    }
}
