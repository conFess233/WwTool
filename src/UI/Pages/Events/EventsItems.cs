using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using WwTool.Core.Services.Notices;

namespace WwTool.UI.Pages.Events;

/// <summary>一条公告在页面上的展示（列表行、时间线横条与详情共用）。</summary>
public sealed partial class NoticeItem(GameNotice notice) : ObservableObject
{
    public GameNotice Notice { get; } = notice;
    public string Id => Notice.Id;
    public string Title => Notice.Title;
    public string AutomationId => "events-notice-" + Notice.Id;

    public bool IsUpdate => Notice.Kind == NoticeKind.Update;
    public bool IsGacha => Notice.Kind == NoticeKind.Gacha;
    public bool IsEvent => Notice.Kind == NoticeKind.Event;
    public bool IsShop => Notice.Kind == NoticeKind.Shop;
    public bool IsInfo => Notice.Kind == NoticeKind.Info;

    /// <summary>5 星 UP，按界面语言的分隔符连接（<see cref="EventsViewModel"/> 设置）；没有时为空。</summary>
    [ObservableProperty] private string _featured = string.Empty;
    public bool HasFeatured => Notice.Featured.Count > 0;

    [ObservableProperty] private string _kindText = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isEnded;
    [ObservableProperty] private bool _isUpcoming;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private Bitmap? _thumbnail;

    /// <summary>缩略图已开始加载（只加载一次）。</summary>
    public bool ThumbnailRequested { get; set; }
}

/// <summary>「全部公告」视图的一组。</summary>
public sealed record NoticeGroup(string Title, IReadOnlyList<NoticeItem> Items);

/// <summary>时间线的一行：分组标题行（带数量），或一项卡池 / 活动 / 循环玩法的横条。</summary>
public sealed partial class TimelineRow : ObservableObject
{
    public TimelineRow(string header, int count)
    {
        IsHeader = true;
        Header = header;
        Count = count;
    }

    /// <param name="item">关联的公告；循环玩法没有公告，为 null。</param>
    public TimelineRow(NoticeItem? item, string title, string? subtitle, string lane, DateTimeOffset start, DateTimeOffset? end,
        double left, double right, string? tip = null)
    {
        Item = item;
        Title = title;
        Subtitle = subtitle;
        Lane = lane;
        Start = start;
        End = end;
        Left = left;
        Right = right;
        Tip = tip ?? title;
    }

    public bool IsHeader { get; }
    public bool IsBar => !IsHeader;
    public string? Header { get; }
    public int Count { get; }
    public NoticeItem? Item { get; }
    public string AutomationId => "events-bar-" + (Item?.Id ?? Lane);

    public string Title { get; } = string.Empty;
    public string? Subtitle { get; }
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
    public string Tip { get; } = string.Empty;
    public DateTimeOffset Start { get; }
    public DateTimeOffset? End { get; }

    /// <summary>可点击打开公告（循环玩法不能）。</summary>
    public bool IsClickable => Item is not null;

    /// <summary>横条颜色：character / weapon / reverb / event / recurring。</summary>
    public string Lane { get; } = string.Empty;
    public bool IsWeaponLane => Lane == "weapon";
    public bool IsReverbLane => Lane == "reverb";
    public bool IsEventLane => Lane == "event";
    public bool IsRecurringLane => Lane == "recurring";

    public double Left { get; }
    public double Right { get; }

    [ObservableProperty] private string _remainingText = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private bool _isEnded;
    [ObservableProperty] private bool _isUpcoming;
}

/// <summary>时间线顶部的日期刻度。</summary>
public sealed record TimelineTick(double Ratio, string Text, bool IsMajor);

/// <summary>版本日历图上的一块可点区域。</summary>
public sealed record CalendarHotspot(string NoticeId, NoticeItem? Item, double Left, double Top, double Right, double Bottom, string Tip)
{
    public string AutomationId => "events-spot-" + NoticeId;
    public bool IsPublished => Item is not null;
}

/// <summary>一个兑换码。</summary>
public sealed partial class RedeemCode(string code) : ObservableObject
{
    public string Code { get; } = code;
    [ObservableProperty] private bool _isCopied;
}
