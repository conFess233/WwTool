using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
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
            public string? Exception { get; set; }
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
        private readonly Channel<LogEntry> _important = Channel.CreateBounded<LogEntry>(64);
        private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        private readonly CancellationTokenSource _cts = new();
        private int _disposed;
        private long _dropped;
        private int _cleanupRunning;
        private readonly Task _writeTask;

        public LoggerService(IConfigService configService)
        {
            _configService = configService;
            _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(512)
            {
                SingleWriter = false,
                SingleReader = true
            });

            // 启动后台队列写入任务
            _writeTask = Task.Run(ProcessLogQueueAsync);

            // 异步触发日志清理
            ScheduleCleanup();
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
                Message = message.Length > 8192 ? message[..8192] : message,
                Exception = ex?.ToString() is { } detail ? detail[..Math.Min(detail.Length, 8192)] : null
            };

            if (Volatile.Read(ref _disposed) != 0) return;
            var writer = level >= LogLevel.Warn ? _important.Writer : _logChannel.Writer;
            if (!writer.TryWrite(entry))
            {
                Interlocked.Increment(ref _dropped);
                if (level >= LogLevel.Warn) Trace.TraceError("重要日志缓冲已满：{0}", entry.Message);
            }
            _signal.Writer.TryWrite(true);
        }

        /// <summary>
        /// 日志写入队列
        /// </summary>
        /// <returns></returns>
        private async Task ProcessLogQueueAsync()
        {
            try
            {
                while (await _signal.Reader.WaitToReadAsync(_cts.Token).ConfigureAwait(false))
                {
                    while (_signal.Reader.TryRead(out _)) { }
                    var batch = new List<LogEntry>(128);
                    while (true)
                    {
                        while (batch.Count < 128 && _important.Reader.TryRead(out var important)) batch.Add(important);
                        while (batch.Count < 128 && _logChannel.Reader.TryRead(out var normal)) batch.Add(normal);
                        if (batch.Count == 0) break;
                        try
                        {
                            foreach (var group in batch.GroupBy(x => x.Time.Date))
                            {
                                await WriteBatchToFileAsync(group.ToList()).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException) { Trace.TraceError($"写入日志失败: {ex}"); }
                        batch.Clear();
                    }
                }
            }
            catch (OperationCanceledException) { Trace.TraceWarning("日志排空超时，停止后台写入。"); }
        }
        /// <summary>
        /// 异步写入日志到文件中
        /// </summary>
        /// <param name="entries">同一天的一批日志。</param>
        /// <returns></returns>
        private async Task WriteBatchToFileAsync(IReadOnlyList<LogEntry> entries)
        {
            Directory.CreateDirectory(LogsFolder);

            string dateStr = entries[0].Time.ToString("yyyyMMdd");
            string activeLogFileName = $"wwtool_{dateStr}.log";
            string activeLogPath = Path.Combine(LogsFolder, activeLogFileName);

            // 格式化日志内容
            var sb = new StringBuilder();
            foreach (LogEntry entry in entries)
            {
                sb.Append($"[{entry.Time:yyyy-MM-dd HH:mm:ss.fff}] [{entry.Level.ToString().ToUpperInvariant()}] ");
                sb.AppendLine(entry.Message);
                if (entry.Exception is not null) sb.AppendLine(entry.Exception);
            }
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
                    catch (Exception rotationException)
                    {
                        // 移动失败时，附加 GUID 防止重名
                        rotatedFileName = $"wwtool_{dateStr}_{timeStr}_{Guid.NewGuid().ToString().Substring(0, 4)}.log";
                        rotatedPath = Path.Combine(LogsFolder, rotatedFileName);
                        try { File.Move(activeLogPath, rotatedPath); }
                        catch (Exception fallbackException)
                        {
                            Trace.TraceError($"日志轮转失败: {rotationException}; fallback: {fallbackException}");
                        }
                    }

                    // 触发定期清理
                    ScheduleCleanup();
                }
            }

            // 追加写入日志文件
            using (var fs = new FileStream(activeLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true))
            {
                await fs.WriteAsync(messageBytes, _cts.Token).ConfigureAwait(false);
                await fs.FlushAsync(_cts.Token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 清理旧日志
        /// </summary>
        private void ScheduleCleanup()
        {
            if (Interlocked.CompareExchange(ref _cleanupRunning, 1, 0) != 0) return;
            _ = Task.Run(() =>
            {
                try { CleanOldLogs(); }
                finally { Volatile.Write(ref _cleanupRunning, 0); }
            });
        }

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
                        try { file.Delete(); }
                        catch (Exception deleteException) { Trace.TraceError($"删除过期日志失败: {deleteException}"); }
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
                        try { logFiles[i].Delete(); }
                        catch (Exception deleteException) { Trace.TraceError($"清理超量日志失败: {deleteException}"); }
                    }
                }
            }
            catch (Exception cleanupException)
            {
                Trace.TraceError($"清理日志失败: {cleanupException}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _logChannel.Writer.TryComplete();
            _important.Writer.TryComplete();
            _signal.Writer.TryWrite(true);
            _signal.Writer.TryComplete();
            if (!_writeTask.Wait(TimeSpan.FromSeconds(2))) _cts.Cancel();
            if (Interlocked.Read(ref _dropped) > 0) Trace.TraceWarning($"日志缓冲满，已丢弃 {_dropped} 条日志。");
            _ = _writeTask.ContinueWith(_ => _cts.Dispose(), TaskScheduler.Default);
        }
    }
}
