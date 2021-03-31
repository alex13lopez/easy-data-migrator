using System;
using System.Configuration;
using System.IO;

namespace ConectorSLIM4.modules
{
    public class Logger
    {
        private readonly System.DateTime _dateNow;

        public Logger() => _dateNow = System.DateTime.Now;

        public void Log(string logMessage)
        {
            string format = "-yyyyMMdd-hhmmsstt", formatLines = "hh:mm:ss";
            string fileName = ConfigurationManager.AppSettings["LogPath"] + "log" + _dateNow.ToString(format) + ".txt";

            try
            {
                File.AppendAllText(fileName, "[" + System.DateTime.Now.ToString(formatLines) + "] " + logMessage + Environment.NewLine /*"\n"*/);
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(ConfigurationManager.AppSettings["LogPath"]);
                Log(logMessage); // ;D
            }

        }
    }
}
