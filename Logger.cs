using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RIFT_Downgrader
{
    public class Logger
    {
        public static string logFile { get; set; } = "";
        public static void Log(string text, LoggingType loggingType = LoggingType.Info)
        {
            //Remove username
            text = Regex.Replace(text, @"([A-Z]{1}\:\\[Uu]sers\\)([^\\]*\\)(.*)", "$1$3");
            File.AppendAllText(logFile, "\n" + GetLinePrefix(loggingType) + text);
        }
        public static void LogRaw(string text)
        {
            File.AppendAllText(logFile, text);
        }

        public static string GetLinePrefix(LoggingType loggingType)
        {
            DateTime t = DateTime.Now;
            return "[" + t.Day.ToString("d2") + "." + t.Month.ToString("d2") + "." + t.Year.ToString("d4") + "   " + t.Hour.ToString("d2") + ":" + t.Minute.ToString("d2") + ":" + t.Second.ToString("d2") + "." + t.Millisecond.ToString("d5") + "] " + (Enum.GetName(typeof(LoggingType), loggingType) + ":").PadRight(10);
        }

        public static void SetLogFile(string file)
        {
            logFile = file;
        }

    }

    public enum LoggingType
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Debug = 3
    }
}
