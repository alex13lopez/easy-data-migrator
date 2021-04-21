
namespace EasyDataMigrator.modules
{    

    public class Query
    {
        public enum QueryType
        {
            Read,
            Execute
        }

        public enum QueryExecutionTime
        {
            BeforeMigration, // It will be executed Before starting the migration of tables
            AfterMigration,  // It will be executed After ending the migration of tables
            BeforeTableMigration,  // It will be executed Before each table migration
            AfterTableMigration, // It will be executed After each table migration
        }

        public string Sql { get; set; }

        public QueryType Type { get; set; }

        public QueryExecutionTime ExecutionTime { get; set; }
    }
}
