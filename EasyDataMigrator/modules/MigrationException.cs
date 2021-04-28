using System;

namespace EasyDataMigrator.Modules
{
    class MigrationException : Exception
    {
        public enum ExceptionSeverityLevel
        {
            WARNING,
            ERROR,
            CRITICAL
        }

        public ExceptionSeverityLevel SeverityLevel { get; set; }

        public MigrationException() : base() 
        {
            SeverityLevel = ExceptionSeverityLevel.ERROR;
        }

        public MigrationException(string message, ExceptionSeverityLevel severityLevel) : base(message) 
        {
            SeverityLevel = severityLevel;
        }
        public MigrationException(string message, ExceptionSeverityLevel severityLevel, Exception inner) : base(message, inner) 
        {
            SeverityLevel = severityLevel;
        }
    }
}
