using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using EasyDataMigrator.Modules.Configuration;

namespace EasyDataMigrator.Modules.Core
{
    /// <summary>
    /// The helper class to connect and perform operations in the DB.
    /// </summary>
    public class DbConnector
    {
        private readonly SqlConnection _sqlConnection;
        private SqlTransaction _sqlTransaction;
        private Query.QueryConnection _connectionType;       

        public string ServerName { get; private set; }
        public string DataBaseName { get; private set; }             
        public SqlConnection SqlConnection { get => _sqlConnection; }      
        public List<Query> Queries { get; private set; }

        private void SetConnectionType(string ConnectionStringKey)
        {
            switch (ConnectionStringKey)
            {
                case "OriginConnection":
                    _connectionType = Query.QueryConnection.OriginConnection;
                    break;
                case "DestinationConnection":
                    _connectionType = Query.QueryConnection.DestinationConnection;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(ConnectionStringKey, "Non valid connection string name");
            }
        }

        public DbConnector(string ConnectionStringKey) // Conexión a BD SQL Server
        {
            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringKey].ConnectionString;
            _sqlConnection = new SqlConnection(connectionString);
            ServerName = _sqlConnection.DataSource;
            DataBaseName = _sqlConnection.Database;            
            SetConnectionType(ConnectionStringKey);
            LoadQueries();
        }
        
        /// <summary>
        /// Function that executes the SQL command passed by parameter and returns and SqlDataReader
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="transactionedQuery"></param>
        /// <returns></returns>
        public SqlDataReader ReadDB(string sql, bool transactionedQuery = false) // Reading data operations, by default we do NOT require transaction since we are not modifying data on de DB
        {
            SqlCommand command;

            if (transactionedQuery && _sqlTransaction != null)
            {
                command = new SqlCommand(sql, _sqlConnection, _sqlTransaction);
            }
            else if (transactionedQuery && _sqlTransaction == null)
            {
                BeginTransaction();
                command = new SqlCommand(sql, _sqlConnection, _sqlTransaction);
            }
            else
            {
                command = new SqlCommand(sql, _sqlConnection);
            }

            command.CommandTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["MaxBulkModeTimeout"]); // Same max time as bulk mode, has no sense having less time than the max time the app is gonna wait

            return command.ExecuteReader();
        }

        /// <summary>
        /// A helper function to return the specified column index value of the first row.
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="column"></param>
        /// <param name="transactionedQuery"></param>
        /// <returns>Object value</returns>
        public object GetFirst(string sql, int column = 0, bool transactionedQuery = false)
        {
            using (SqlDataReader dataR = ReadDB(sql, transactionedQuery))
            {
                if (dataR.Read())
                {
                    return dataR.GetValue(column);
                }
            }

            return null;
        }

        /// <summary>
        /// A helper function to return the specified column name value of the first row.
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="column"></param>
        /// <param name="transactionedQuery"></param>
        /// <returns>Object value</returns>
        public object GetFirst(string sql, string columnName, bool transactionedQuery = false)
        {
            using (SqlDataReader dataR = ReadDB(sql, transactionedQuery))
            {
                if (dataR.Read())
                    return dataR.GetValue(columnName);
            }

            return null;
        }

        /// <summary>
        /// Function to execute the SQL command passed by parameter that modifies data in the DB.
        /// </summary>
        /// <param name="sql"></param>
        /// <param name="transactionedQuery"></param>
        /// <returns>Number of affected rows</returns>
        public int ModifyDB(string sql, bool transactionedQuery = true) // Modify data operations, by default we DO require transaction since we are modifying data on de DB and something could go wrong
        {
            SqlCommand command;
            int CommandTimeout;

            if (transactionedQuery && _sqlTransaction != null)
            {
                command = new SqlCommand(sql, _sqlConnection, _sqlTransaction);
            }
            else if (transactionedQuery && _sqlTransaction == null)
            {
                BeginTransaction();
                command = new SqlCommand(sql, _sqlConnection, _sqlTransaction);
            }
            else
            {
                command = new SqlCommand(sql, _sqlConnection);
            }

            if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MaxQueryTimeout"]))
                CommandTimeout = 60; // By default we asign 3 minutes of timeout if no value specified
            else
                CommandTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["MaxQueryTimeout"]);


            command.CommandTimeout = CommandTimeout;

            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// Function that opens the connection to DB.
        /// </summary>
        public void Open() 
        {
            try
            {
                _sqlConnection.Open();

            }
            catch (SqlException ex) when (ex.Number == 53)
            {
                string user = ex.Message.Remove(0, ex.Message.LastIndexOf(' ') + 1).Trim();
                user = user.Remove(user.Length - 1);
                string errMsg = $"Cannot find server {_sqlConnection.DataSource} make sure you have typed it correctly and the user {user} has access.";
                throw new MigrationException(errMsg, MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
            }
            catch (SqlException ex) when (ex.Number == 4060)
            {
                string user = ex.Message.Remove(0, ex.Message.LastIndexOf(' ') + 1).Trim();
                user = user.Remove(user.Length - 1);
                string errMsg = $"Cannot open database '{_sqlConnection.Database}' from server '{_sqlConnection.DataSource}'. Make sure you have typed it correctly and the user {user} has access.";
                throw new MigrationException(errMsg, MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
            }
        }

        /// <summary>
        /// Function that closes the connection to DB.
        /// </summary>
        public void Close() => _sqlConnection.Close();

        /// <summary>
        ///  Function to Begin a transaction in the DB.
        /// </summary>
        /// <param name="transName"></param>
        public void BeginTransaction(string transName = "DefaultTransaction") => _sqlTransaction = _sqlConnection.BeginTransaction(transName);

        /// <summary>
        ///  Function to Commit transactions in the DB.
        /// </summary>
        /// <param name="transName"></param>
        public void CommitTransaction()
        {
            _sqlTransaction.Commit();
            _sqlTransaction = null; // We void the object as we finished the transaction and next command will have to create a new one.
        }

        /// <summary>
        ///  Function to Rollback a transaction in the DB.
        /// </summary>
        /// <param name="transName"></param>
        public void RollBackTransaction(string transName = "DefaultTransaction")
        {
            if (_sqlTransaction.IsolationLevel == IsolationLevel.ReadUncommitted)
                _sqlTransaction.Rollback(transName);

            _sqlTransaction = null; // We void the object as we finished the transaction and next command will have to create a new one.
        }

        /// <summary>
        /// Function that will use SqlBulkCopy that will copy data from a DataTable to the DB. 
        /// Also known as "Bulk Insert Mode".
        /// </summary>
        /// <param name="data"></param>
        /// <param name="tableMap"></param>
        public void BulkCopy(DataTable data, TableMap tableMap)
        {
            using SqlBulkCopy bulkCopy = new(_sqlConnection, SqlBulkCopyOptions.KeepNulls, _sqlTransaction);
            int CommandTimeout;

            if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MaxBulkModeTimeout"]))
                CommandTimeout = 300; // By default we asign 5 minutes of timeout if no value specified
            else
                CommandTimeout = Convert.ToInt32(ConfigurationManager.AppSettings["MaxBulkModeTimeout"]);


            bulkCopy.DestinationTableName = tableMap.DestinationDataBase + ".dbo." + tableMap.ToTableName;
            bulkCopy.BulkCopyTimeout = CommandTimeout;

            tableMap.FieldMaps.ForEach(fieldMap => bulkCopy.ColumnMappings.Add(fieldMap.OriginField, fieldMap.DestinationField));

            try
            {
                bulkCopy.WriteToServer(data);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }            
        }

        /// <summary>
        /// Function that loads the queries of the connection so we can use them later.
        /// </summary>
        private void LoadQueries()
        {
            Queries = new List<Query>();            
            Queries queries = CustomQueriesConfig.GetConfig().Queries;

            foreach (Query query in queries)
            {
                if (query.Connection == _connectionType)
                    Queries.Add(query);
            }
        }
    }
}
