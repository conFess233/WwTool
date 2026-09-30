namespace WwTool.Common.Models.Domain;

/// <summary>账号删除成功后通知相关页面使缓存失效。</summary>
public sealed class AccountDeletedEventArgs(string uid) : EventArgs
{
    public string Uid { get; } = uid;
}
