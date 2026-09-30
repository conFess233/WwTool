using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using WwTool.Common.Utils;
using WwTool.Services;
using WwTool.Services.Interfaces;

namespace WwTool.UI.ViewModels;

/// <summary>设置页资料同步与缓存操作，按类别显示版本和结果。</summary>
public sealed class CatalogSettingsViewModel : BindableBase
{
    private readonly CatalogSyncService sync;
    private readonly CatalogImageService images;
    private readonly ILoggerService logger;
    private CancellationTokenSource? cancellation;
    private bool clearing;
    private string message = "";
    public ObservableCollection<CatalogStatusRow> Rows { get; } = [];
    public DelegateCommand SyncCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public DelegateCommand ClearCommand { get; }
    public string Message { get => message; private set => SetProperty(ref message, value); }

    public CatalogSettingsViewModel(CatalogSyncService sync, CatalogImageService images, ILoggerService logger)
    {
        this.sync = sync;
        this.images = images;
        this.logger = logger;
        SyncCommand = new DelegateCommand(async () => await SynchronizeAsync(), () => !sync.IsBusy && !clearing && cancellation is null);
        CancelCommand = new DelegateCommand(() => cancellation?.Cancel(), () => cancellation is not null);
        ClearCommand = new DelegateCommand(async () => await ClearAsync(), () => !sync.IsBusy && !clearing && cancellation is null);
        sync.Changed += (_, _) => Application.Current.Dispatcher.InvokeAsync(Refresh);
        LanguageManager.Instance.PropertyChanged += (_, e) => { if (e.PropertyName == "Item[]") Refresh(); };
        Refresh();
    }

    private void Refresh()
    {
        Rows.Clear();
        var language = LanguageManager.Instance;
        foreach (var status in sync.Statuses)
            Rows.Add(new CatalogStatusRow(language["Catalog_" + status.Category],
                string.Format(language["Catalog_VersionCount"], status.Version, status.Count),
                language[sync.IsBusy ? "Catalog_Syncing" : status.StatusKey],
                string.Format(language["Catalog_LastSuccess"], status.LastSuccessUtc?.ToLocalTime().ToString("g") ?? "—")));
        SyncCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
    }

    private async Task SynchronizeAsync()
    {
        cancellation = new CancellationTokenSource();
        Message = "";
        Refresh();
        try { await sync.SyncAsync(true, cancellation.Token); }
        catch (Exception ex) { logger.Warn("手动同步资料失败。", ex); Message = LanguageManager.Instance["Catalog_Failed"]; }
        finally { cancellation.Dispose(); cancellation = null; Refresh(); }
    }

    private async Task ClearAsync()
    {
        clearing = true;
        Refresh();
        try { await images.ClearAsync(); Message = LanguageManager.Instance["Catalog_CacheCleared"]; }
        catch (Exception ex) { logger.Warn("清理图片缓存失败。", ex); Message = LanguageManager.Instance["Catalog_CacheClearFailed"]; }
        finally { clearing = false; Refresh(); }
    }
}

public sealed record CatalogStatusRow(string Name, string Version, string Status, string LastSuccess);
