using Prism.Mvvm;
using WwTool.Common.Enums;

namespace WwTool.Common.Models
{
    public class PoolTypeFilterItem : BindableBase
    {
        public CardPoolType PoolType { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
