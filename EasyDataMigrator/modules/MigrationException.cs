using System;

namespace EasyDataMigrator.Modules
{
    /// <summary>
    /// Custom Exception class that helps to determine if the error that has happened is fatal or the program can continue its execution.
    /// </summary>
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
