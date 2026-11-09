using System;

namespace WwTool.Core.Models.Statistics
{
    public class FourStarHistoryItem
    {
        public int ResourceId { get; set; }
        public string IconPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
