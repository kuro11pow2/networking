using System;
using System.Threading;
using System.Diagnostics;

namespace Common
{
    public enum LogLevel
    {
        OFF,
        RETURN,
        ERROR,
        WARN,
        INFO,
        DEBUG
    }

    public static class Log
    {
        public static LogLevel PrintLevel = LogLevel.DEBUG;
        private const int Width = 3;
        private const string _line = "--------------------------------------------------------------------------------------------------------------------";

        private static void Out(object obj)
        {
            Console.WriteLine(obj);
        }

        //[Conditional("DEBUG")]
        public static void Print(string msg, LogLevel level = LogLevel.DEBUG, string context = "")
        {
            if (level <= PrintLevel)
            {
                string printMsg;
                printMsg = FormattedString(
                    DateTime.Now.ToString("mm.ss.ffff"),
                    ThreadPool.ThreadCount,
                    ThreadPool.PendingWorkItemCount,
                    Thread.CurrentThread.ManagedThreadId,
                    context,
                    LogLevelTag(level),
                    $"{msg}\n{_line}");
                Out(printMsg);
            }
        }
        private static string LogLevelTag(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.RETURN:
                    return "RETURN";
                case LogLevel.ERROR:
                    return "ERROR";
                case LogLevel.WARN:
                    return "WARM";
                case LogLevel.INFO:
                    return "INFO";
                case LogLevel.DEBUG:
                    return "DEBUG";
                default:
                    return "WRONG LEVEL";
            }
        }

        //[Conditional("DEBUG")]
        public static void PrintLine()
        {
            Out(_line);
        }

        //[Conditional("DEBUG")]
        public static void PrintHeader()
        {
            if (PrintLevel > LogLevel.ERROR)
                Out(FormattedString(
                    "mm.ss.ffff",
                    "Avail",
                    "Pend",
                    "TID",
                    "Context",
                    "Type",
                    "Message") + $"\n{_line}");
        }

        public static Exception GetExceptionWithLog(string errMsg, string context="", Exception? innerException=null, LogLevel level = LogLevel.ERROR) 
        {
            Print(errMsg, level, context);
            if (innerException != null)
                return new Exception(errMsg, innerException);
            else
                return new Exception(errMsg);
        }

        private static string FormattedString(object time, object tall, object tpend, object tid, object contect, object type, object msg)
        {
            return $"| {time,-9} | {tall,-5} | {tpend,-4} | {tid,-4} | {contect,-34} | {type,-9} | {msg,-1}";
        }
    }
}
