using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using WwTool.Common.Enums;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// 日志服务
    /// </summary>
    public class LoggerService : ILoggerService, IDisposable
    {
        private struct LogEntry
        {
            public DateTime Time { get; set; }
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public Exception? Exception { get; set; }
        }

        private readonly IConfigService _configService;
        private string LogsFolder
        {
            get
            {
                string folder = _configService.App.LogFolderPath;
                if (string.IsNullOrWhiteSpace(folder))
                {
                    folder = Path.Combine("Local", "Logs");
                }
                return Path.IsPathRooted(folder) ? folder : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
            }
        }
        private readonly Channel<LogEntry> _logChannel;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _writeTask;

        public LoggerService(IConfigService configService)
        {
            _configService = configService;
            _logChannel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
            {
                SingleWriter = false,
                SingleReader = true
            });

            // 启动后台队列写入任务
            _writeTask = Task.Run(ProcessLogQueueAsync);

            // 异步触发日志清理
            Task.Run(CleanOldLogs);
        }

        public void Debug(string message, Exception? ex = null) => Log(LogLevel.Debug, message, ex);
        public void Info(string message, Exception? ex = null) => Log(LogLevel.Info, message, ex);
        public void Warn(string message, Exception? ex = null) => Log(LogLevel.Warn, message, ex);
        public void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);
        public void Fatal(string message, Exception? ex = null) => Log(LogLevel.Fatal, message, ex);

        /// <summary>
        /// 记录日志信息
        /// </summary>
        /// <param name="level">严重等级</param>
        /// <param name="message">消息</param>
        /// <param name="ex">错误信息</param>
        public void Log(LogLevel level, string message, Exception? ex = null)
        {
            if (!_configService.App.EnableFileLogging)
                return;

            var entry = new LogEntry
            {
                Time = DateTime.Now,
                Level = level,
                Message = message,
                Exception = ex
            };

            _logChannel.Writer.TryWrite(entry);
        }

        /// <summary>
        /// 日志写入队列
        /// </summary>
        /// <returns></returns>
        private async Task ProcessLogQueueAsync()
        {
            var reader = _logChannel.Reader;
            try
            {
                while (await reader.WaitToReadAsync(_cts.Token))
                {
                    while (reader.TryRead(out var entry))
                    {
                        try
                        {
                            await WriteEntryToFileAsync(entry);
                        }
                        catch
                        {
                            // 忽略日志系统内部错误，防止其导致主程序崩溃
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 关闭时消费完队列中剩余的所有日志
                while (reader.TryRead(out var entry))
                {
                    try
                    {
                        await WriteEntryToFileAsync(entry);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 异步写入日志到文件中
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private async Task WriteEntryToFileAsync(LogEntry entry)
        {
            Directory.CreateDirectory(LogsFolder);

            string dateStr = entry.Time.ToString("yyyyMMdd");
            string activeLogFileName = $"wwtool_{dateStr}.log";
            string activeLogPath = Path.Combine(LogsFolder, activeLogFileName);

            // 格式化日志内容
            var sb = new StringBuilder();
            sb.Append($"[{entry.Time:yyyy-MM-dd HH:mm:ss.fff}] ");
            sb.Append($"[{entry.Level.ToString().ToUpper()}] ");
            sb.Append(entry.Message);
            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append($"--- 异常详情 ---\n{entry.Exception}");
            }
            sb.AppendLine();

            string formattedMessage = sb.ToString();
            byte[] messageBytes = Encoding.UTF8.GetBytes(formattedMessage);

            // 体积超出最大限制时进行滚动备份
            long maxSizeBytes = _configService.App.LogMaxSizeBytes;
            if (File.Exists(activeLogPath))
            {
                var fileInfo = new FileInfo(activeLogPath);
                if (fileInfo.Length + messageBytes.Length > maxSizeBytes)
                {
                    string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string rotatedFileName = $"wwtool_{dateStr}_{timeStr}.log";
                    string rotatedPath = Path.Combine(LogsFolder, rotatedFileName);

                    try
                    {
                        File.Move(activeLogPath, rotatedPath);
                    }
                    catch
                    {
                        // 移动失败时，附加 GUID 防止重名
                        rotatedFileName = $"wwtool_{dateStr}_{timeStr}_{Guid.NewGuid().ToString().Substring(0, 4)}.log";
                        rotatedPath = Path.Combine(LogsFolder, rotatedFileName);
                        try { File.Move(activeLogPath, rotatedPath); } catch { }
                    }

                    // 触发定期清理
                    _ = Task.Run(CleanOldLogs);
                }
            }

            // 追加写入日志文件
            using (var fs = new FileStream(activeLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true))
            {
                await fs.WriteAsync(messageBytes, 0, messageBytes.Length);
                await fs.FlushAsync();
            }
        }

        /// <summary>
        /// 清理旧日志
        /// </summary>
        private void CleanOldLogs()
        {
            try
            {
                if (!Directory.Exists(LogsFolder))
                    return;

                var logFiles = Directory.GetFiles(LogsFolder, "wwtool_*.log")
                                        .Select(f => new FileInfo(f))
                                        .ToList();

                DateTime retentionDate = DateTime.Now.AddDays(-_configService.App.LogRetentionDays);

                // 清理超过天数限制 of 日志
                foreach (var file in logFiles)
                {
                    if (file.LastWriteTime < retentionDate)
                    {
                        try { file.Delete(); } catch { }
                    }
                }

                // 重新检索并按照最后写入时间升序排列
                logFiles = Directory.GetFiles(LogsFolder, "wwtool_*.log")
                                    .Select(f => new FileInfo(f))
                                    .OrderBy(f => f.LastWriteTime)
                                    .ToList();

                // 清理超出最大文件数量限制 of 日志
                int maxFileCount = _configService.App.LogMaxFileCount;
                if (logFiles.Count > maxFileCount)
                {
                    int filesToDeleteCount = logFiles.Count - maxFileCount;
                    for (int i = 0; i < filesToDeleteCount; i++)
                    {
                        try { logFiles[i].Delete(); } catch { }
                    }
                }
            }
            catch
            {
                // 忽略清理时的异常
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _logChannel.Writer.Complete();
                _cts.Cancel();
                _writeTask.Wait(1000);
            }
            catch { }
            finally
            {
                _cts.Dispose();
            }
        }
    }
}
