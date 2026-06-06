using System;
using WwTool.Common.Enums;

namespace WwTool.Services.Interfaces
{

    public interface ILoggerService
    {
        void Debug(string message, Exception? ex = null);
        void Info(string message, Exception? ex = null);
        void Warn(string message, Exception? ex = null);
        void Error(string message, Exception? ex = null);
        void Fatal(string message, Exception? ex = null);
        void Log(LogLevel level, string message, Exception? ex = null);
    }
}
