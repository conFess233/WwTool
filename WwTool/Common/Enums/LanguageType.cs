using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace WwTool.Common.Enums
{
    public enum LanguageType
    {
        [Description("简体中文")]
        ZhHans,

        [Description("繁体中文")]
        ZhHant,

        [Description("英语")]
        En,

        [Description("日语")]
        Ja,

        [Description("韩语")]
        Ko,

        [Description("德语")]
        De,

        [Description("西班牙语")]
        Es,

        [Description("法语")]
        Fr,

        [Description("泰语")]
        Th
    }
}
