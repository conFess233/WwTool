using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using WwTool.Common.Models.ApiRequest;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 抽卡 URL 解析工具类
    /// 用于将游戏客户端中的抽卡记录 URL 解析为用于查询的请求对象
    /// </summary>
    public static class GachaUrlParser
    {
        /// <summary>
        /// 解析 URL 并生成 GachaRequest 对象
        /// </summary>
        /// <param name="url">包含抽卡查询参数的完整 URL 字符串</param>
        /// <returns>封装了查询参数的 GachaRequest 实例</returns>
        public static GachaRequest Parse(string url)
        {
            string query =
                url.Substring(url.IndexOf('?'));

            var parameters = HttpUtility.ParseQueryString(query);

            return new GachaRequest
            {
                ServerId = parameters["svr_id"],

                PlayerId = parameters["player_id"],

                LanguageCode = parameters["lang"],

                CardPoolType = int.Parse(parameters["gacha_type"]),

                RecordId = parameters["record_id"],

                //CardPoolId =
                //    parameters["resources_id"]
            };
        }
    }
}
