namespace WwTool.Core.Abstractions;

/// <summary>
/// 资料图片磁盘缓存中与界面框架无关的操作，供资料同步等后台服务使用。
/// </summary>
public interface ICatalogImageCache
{
    /// <summary>缓存内容变化（刷新或清空）后触发。可能在后台线程触发。</summary>
    event EventHandler? Changed;

    /// <summary>失败记录被重置、界面应重新请求图片时触发。可能在后台线程触发。</summary>
    event EventHandler? RetryRequested;

    /// <summary>按条件请求刷新已缓存的远端图片；<paramref name="resetFailures"/> 为真时清除失败抑制记录。</summary>
    Task RefreshCachedAsync(CancellationToken token, bool resetFailures = true);

    /// <summary>删除全部缓存图片与元数据。</summary>
    Task ClearAsync(CancellationToken token = default);
}
