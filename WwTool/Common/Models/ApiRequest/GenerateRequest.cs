using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;

namespace WwTool.Common.Models.ApiRequest
{
    /// <summary>
    /// 生成授权码请求参数
    /// </summary>
    public class GenerateRequest
    {
        /// <summary>
        /// 客户端标识
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// 访问令牌
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// 范围
        /// </summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// 产品 ID
        /// </summary>
        public string ProductId { get; set; } = string.Empty;

        /// <summary>
        /// 项目 ID
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// 重定向 URI
        /// </summary>
        public string RedirectUri { get; set; } = string.Empty;

        /// <summary>
        /// 设备 ID
        /// </summary>
        public string DeviceNum { get; set; } = string.Empty;

        /// <summary>
        /// 客户端密钥
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;


        /// <summary>
        /// 转换为字典以供 application/x-www-form-urlencoded 提交
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>
            {
                { "client_id", ClientId},
                { "deviceNum", DeviceNum },
                { "client_secret", ClientSecret },
                { "access_token", AccessToken },
                { "productId", ProductId },
                { "projectId", ProjectId },
                { "redirect_uri", RedirectUri },
                { "scope", Scope }

            };

            return dict;
        }
    }
}