using WwTool.Common.Context;
using WwTool.Common.Exceptions;
using WwTool.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace WwTool.Services
{
    /// <summary>
    /// 初始化并升级本地数据库。
    /// </summary>
    public sealed class LocalDataService
    {
        private readonly ILoggerService _logger;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly DatabaseOptions _databaseOptions;
        private readonly SemaphoreSlim _initializeLock = new(1, 1);
        private bool _initialized;

        public LocalDataService(
            ILoggerService logger,
            IDbContextFactory<AppDbContext> contextFactory,
            DatabaseOptions databaseOptions)
        {
            _logger = logger;
            _contextFactory = contextFactory;
            _databaseOptions = databaseOptions;
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            await _initializeLock.WaitAsync(cancellationToken);
            try
            {
                if (_initialized)
                {
                    return;
                }

                EnsureWritableDirectory(Path.GetDirectoryName(_databaseOptions.DatabasePath)!);
                EnsureWritableDirectory(_databaseOptions.BackupDirectory);
                _logger.Info("Initializing and upgrading the local database...");
                await using AppDbContext db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                await DatabaseInitializer.InitializeAsync(db, cancellationToken);
                _initialized = true;
                _logger.Info("Local database is ready.");
            }
            catch (Exception ex)
            {
                throw new WwToolDatabaseException("Failed to initialize the local database.", ex);
            }
            finally
            {
                _initializeLock.Release();
            }
        }

        internal static void EnsureWritableDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
            string probePath = Path.Combine(directory, $".wwtool-write-{Guid.NewGuid():N}.tmp");
            try
            {
                using var stream = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.WriteByte(0);
                stream.Flush(true);
            }
            finally
            {
                if (File.Exists(probePath))
                {
                    File.Delete(probePath);
                }
            }
        }
    }
}
