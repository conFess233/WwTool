using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace WwTool.Common.Enums
{
    public enum CardPoolType
    {
        [Description("角色活动唤取")]
        CharacterEvent = 1,

        [Description("武器活动唤取")]
        WeaponEvent = 2,

        [Description("角色常驻唤取")]
        CharacterStandard = 3,

        [Description("武器常驻唤取")]
        WeaponStandard = 4,

        [Description("新手唤取")]
        Beginner = 5,

        [Description("新手自选唤取")]
        BeginnerChoice = 6,

        [Description("角色新旅唤取")]
        CharacterNoviceJourney = 7,

        [Description("武器新旅唤取")]
        WeaponNoviceJourney = 8
    }
}
