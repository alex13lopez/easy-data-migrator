using System;
using System.Configuration;
using System.IO;

namespace EasyDataMigrator.modules
{
    public class Logger
    {
        private readonly System.DateTime _dateNow;

        public enum LogType
        {
            INFO,
            WARNING,
            ERROR,
            CRITICAL
        }

        public Logger() => _dateNow = System.DateTime.Now;

        public void Log(string logMessage, LogType logType = LogType.INFO, string format = "-yyyyMMdd-hhmmsstt", string formatLines = "hh:mm:ss")
        {
            string fileName = ConfigurationManager.AppSettings["LogPath"] + "log" + _dateNow.ToString(format) + ".txt";

            try
            {
                File.AppendAllText(fileName, "[" + logType.ToString() + "] -- [" + System.DateTime.Now.ToString(formatLines) + "] " + logMessage + Environment.NewLine);
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(ConfigurationManager.AppSettings["LogPath"]);
                Log(logMessage);
            }

        }

        public void Print(string message, LogType logType = LogType.INFO, string formatLines = "yyyyMMdd - hh:mm:ss")
        {
            string messageInfo = "[" + System.DateTime.Now.ToString(formatLines) + "] ";

            switch (logType)
            {
                case LogType.INFO:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case LogType.WARNING:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;
                case LogType.ERROR:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LogType.CRITICAL:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                default:
                    break;
            }
            
            // We print the message
            Console.WriteLine("[" + logType.ToString() + "]" + " -- " + messageInfo + " " + message);

            // And reset the font color to default
            Console.ResetColor();
        }

        public void PrintNLog(string message, LogType logType = LogType.INFO)
        {
            Print(message, logType);
            Log(message, logType);
        }
    }
}
