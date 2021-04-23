﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using EasyDataMigrator.Modules.Configuration;

namespace EasyDataMigrator.Modules.Core
{
    public class DbConnector
    {
        private readonly SqlConnection _sqlConnection;
        private SqlTransaction _sqlTransaction;

        public string ServerName { get; private set; }
        public string DataBaseName { get; private set; }             
        public SqlConnection SqlConnection { get => _sqlConnection; }
        public List<Query> CustomQueries { get; private set; }

        public DbConnector(string ConnectionStringKey, ref Variables varsCollection) //Conexión a BD SQL Server
        {
            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringKey].ConnectionString;
            _sqlConnection = new SqlConnection(connectionString);
            ServerName = _sqlConnection.DataSource;
            DataBaseName = _sqlConnection.Database;
            LoadQueries(ref varsCollection);
        }

        public DbConnector(string ConnectionStringKey) //Conexión a BD SQL Server
        {
            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringKey].ConnectionString;
            _sqlConnection = new SqlConnection(connectionString);
            ServerName = _sqlConnection.DataSource;
            DataBaseName = _sqlConnection.Database;
            LoadQueries();
        }
        
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

        public int ModifyDB(string sql, bool transactionedQuery = true) // Modify data operations, by default we DO require transaction since we are modifying data on de DB and something could go wrong
        {
            SqlCommand command;
            SqlDataAdapter adapter = new();
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
            adapter.UpdateCommand = command;           

            return adapter.UpdateCommand.ExecuteNonQuery();
        }

        public void Open() => _sqlConnection.Open();
        public void Close() => _sqlConnection.Close();
        public void BeginTransaction(string transName = "DefaultTransaction") => _sqlTransaction = _sqlConnection.BeginTransaction(transName);

        public void CommitTransaction() => _sqlTransaction.Commit();

        public void RollBackTransaction(string transName = "DefaultTransaction") => _sqlTransaction.Rollback(transName);

        public void BulkCopy(DataTable data,TableMap tableMap)
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

        private void LoadQueries(ref Variables varsCollection)
        {
            throw new NotImplementedException();
        }

        private void LoadQueries()
        {
            throw new NotImplementedException();
        }

    }
}
