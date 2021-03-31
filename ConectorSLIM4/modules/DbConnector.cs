
using System.Configuration;
using System.Data.SqlClient;

namespace ConectorSLIM4.modules
{
    public class DbConnector
    {
        private readonly SqlConnection _sqlConnection;
        private SqlTransaction _sqlTransaction;

        public DbConnector(string ConnectionStringKey) //Conexión a BD SQL Server
        {
            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringKey].ConnectionString;
            _sqlConnection = new SqlConnection(connectionString);            
        }

        public SqlDataReader ReadDB(string sql, bool transactionedQuery = false) // Reading data operations, by default we do NOT require transaction since we are not modifying data on de DB
        {
            SqlCommand command;
            SqlDataReader dataReader;

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

            dataReader = command.ExecuteReader();
            return dataReader;
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
                    
            adapter.UpdateCommand = command;
            adapter.UpdateCommand.ExecuteNonQuery();
        }

        public void OpenConnection() => _sqlConnection.Open();
        public void CloseConnection() => _sqlConnection.Close();
        public void BeginTransaction(string transName = "DefaultTransaction") => _sqlTransaction = _sqlConnection.BeginTransaction(transName);

        public void CommitTransaction() => _sqlTransaction.Commit();

        public void RollBackTransaction(string transName = "DefaultTransaction") => _sqlTransaction.Rollback(transName);
    }
}
