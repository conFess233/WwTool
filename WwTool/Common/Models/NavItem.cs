using System;
using System.Collections.Generic;
using System.Text;

namespace WwTool.Common.Models
{
    public class NavItem : BindableBase
    {
        private string _icon = "DefaultImage";

        /// <summary>
        /// 菜单图标
        /// </summary>
        public string Icon
        {
            get { return _icon; }
            set { SetProperty(ref _icon, value); }
        }

        private string _title;

        /// <summary>
        /// 菜单名称
        /// </summary>
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private string _nameSpace;

        /// <summary>
        /// 菜单命名空间
        /// </summary>
        public string NameSpace
        {
            get { return _nameSpace; }
            set { SetProperty(ref _nameSpace, value); }
        }

        private bool _isBottomItem;
        public bool IsBottomItem
        {
            get => _isBottomItem;
            set => SetProperty(ref _isBottomItem, value);
        }
    }
}
