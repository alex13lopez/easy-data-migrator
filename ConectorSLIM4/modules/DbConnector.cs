using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace ConectorSLIM4.modules
{
    public class DbConnector
    {
        private readonly SqlConnection _sqlConnection;
        private SqlTransaction _sqlTransaction;

        public string ServerName { get; private set; }
        public string DataBaseName { get; private set; }     
        
        public SqlConnection SqlConnection { get => _sqlConnection; }

        public DbConnector(string ConnectionStringKey) //Conexión a BD SQL Server
        {
            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringKey].ConnectionString;
            _sqlConnection = new SqlConnection(connectionString);
            ServerName = _sqlConnection.DataSource;
            DataBaseName = _sqlConnection.Database;
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

            return command.ExecuteReader();
        }

        public void ModifyDB(string sql, bool transactionedQuery = true) // Modify data operations, by default we DO require transaction since we are modifying data on de DB and something could go wrong
        {
            SqlCommand command;
            SqlDataAdapter adapter = new();

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
            command.CommandTimeout = 360; // 6 minutes
            adapter.UpdateCommand = command;           
            adapter.UpdateCommand.ExecuteNonQuery();
        }

        public void Open() => _sqlConnection.Open();
        public void Close() => _sqlConnection.Close();
        public void BeginTransaction(string transName = "DefaultTransaction") => _sqlTransaction = _sqlConnection.BeginTransaction(transName);

        public void CommitTransaction() => _sqlTransaction.Commit();

        public void RollBackTransaction(string transName = "DefaultTransaction") => _sqlTransaction.Rollback(transName);

        public void BulkCopy(DataTable data,TableMap tableMap)
        {            
            using (SqlBulkCopy bulkCopy = new(_sqlConnection, SqlBulkCopyOptions.KeepNulls, _sqlTransaction))
            {
                bulkCopy.DestinationTableName = tableMap.DestinationDataBase + ".dbo." + tableMap.ToTableName;
                bulkCopy.BulkCopyTimeout = 360; // 6 minutes

                tableMap.FieldMaps.ForEach(fieldMap => bulkCopy.ColumnMappings.Add(fieldMap.OriginField, fieldMap.DestinationField));

                //var t = bulkCopy.ColumnMappings;
                //Console.WriteLine(t);
                
                try
                {
                    bulkCopy.WriteToServer(data);                   
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
