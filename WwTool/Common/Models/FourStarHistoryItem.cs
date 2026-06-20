using System;

namespace WwTool.Common.Models
{
    public class FourStarHistoryItem
    {
        public int ResourceId { get; set; }
        public string IconPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
