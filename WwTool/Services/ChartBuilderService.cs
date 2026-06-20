using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.Generic;
using WwTool.Common;
using WwTool.Common.Models;

namespace WwTool.Services
{
    public interface IChartBuilderService
    {
        ISeries[] BuildGoldHistorySeries(List<int> goldValues, string name, ISeries[] existingSeries = null);
        Axis[] BuildGoldHistoryXAxes(List<string> goldLabels, Axis[] existingAxes = null);
    }

    public class ChartBuilderService : IChartBuilderService
    {
        public ISeries[] BuildGoldHistorySeries(List<int> goldValues, string name, ISeries[] existingSeries = null)
        {
            if (existingSeries != null && existingSeries.Length > 0 && existingSeries[0] is ColumnSeries<int> col)
            {
                col.Values = goldValues;
                col.Name = name ?? "Pity";
                return existingSeries;
            }

            return new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Values = goldValues,
                    Name = name ?? "Pity",
                    MaxBarWidth = 40,
                }
            };
        }

        public Axis[] BuildGoldHistoryXAxes(List<string> goldLabels, Axis[] existingAxes = null)
        {
            if (existingAxes != null && existingAxes.Length > 0)
            {
                existingAxes[0].Labels = goldLabels;
                return existingAxes;
            }

            return new Axis[]
            {
                new Axis
                {
                    Labels = goldLabels,
                    LabelsRotation = 45,
                    TextSize = 12
                }
            };
        }
    }
}
