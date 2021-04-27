using System;
using System.Configuration;
using System.IO;

namespace EasyDataMigrator.Modules.Core
{
    public class Logger
    {
        private readonly System.DateTime _dateNow;

        public bool AlternateColors { get; set; }

        public enum LogType
        {
            INFO,
            WARNING,
            ERROR,
            CRITICAL
        }

        public Logger()
        {
            _dateNow = System.DateTime.Now;
            AlternateColors = false;
        }

        public void Log(string logMessage, LogType logType = LogType.INFO, string format = "_yyyyMMdd-hh.mm.ss.fff", string formatLines = "hh:mm:ss", string prefixName = "log")
        {
            string fileName = ConfigurationManager.AppSettings["LogPath"] + prefixName + _dateNow.ToString(format) + ".txt";

            try
            {
                File.AppendAllText(fileName, "[" + logType.ToString() + "] - [" + System.DateTime.Now.ToString(formatLines) + "] " + logMessage + Environment.NewLine);
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(ConfigurationManager.AppSettings["LogPath"]);
                Log(logMessage);
            }

        }

        public void Print(string message, LogType logType = LogType.INFO, string formatLines = "dd/MM/yy hh:mm:ss")
        {
            string messageInfo = "[" + System.DateTime.Now.ToString(formatLines) + "] ";

            switch (logType)
            {
                case LogType.INFO:
                    Console.ForegroundColor = !AlternateColors ? ConsoleColor.Blue : ConsoleColor.Green;
                    break;
                case LogType.WARNING:
                    Console.ForegroundColor = !AlternateColors ? ConsoleColor.Yellow : ConsoleColor.DarkYellow;
                    break;
                case LogType.ERROR:
                    Console.ForegroundColor = !AlternateColors ? ConsoleColor.Red : ConsoleColor.Magenta;
                    break;
                case LogType.CRITICAL:
                    Console.ForegroundColor = !AlternateColors ? ConsoleColor.DarkRed : ConsoleColor.DarkMagenta;
                    break;
                default:
                    break;
            }
            
            // We print the message
            Console.WriteLine("[" + logType.ToString() + "]" + " - " + messageInfo + " " + message);

            // And reset the font color to default
            Console.ResetColor();
        }

        public void PrintNLog(string message, LogType logType = LogType.INFO, string prefixName = "log")
        {
            Print(message, logType);
            Log(message, logType, "_yyyyMMdd-hh.mm.ss.fff", "hh:mm:ss", prefixName);
        }
    }
}
