using System.Collections.Specialized;
using System.Web;
using WwTool.Common.Models.ApiRequest;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 解析并校验抽卡记录链接。
    /// </summary>
    public static class GachaUrlParser
    {
        public static GachaRequest Parse(string url)
        {
            if (TryParse(url, out GachaRequest? request, out string? error))
            {
                return request!;
            }

            throw new FormatException(error);
        }

        public static bool TryParse(
            string? url,
            out GachaRequest? request,
            out string? error)
        {
            request = null;
            error = null;

            if (string.IsNullOrWhiteSpace(url))
            {
                error = "The gacha URL is empty.";
                return false;
            }

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "The gacha URL must be an absolute HTTP or HTTPS URL.";
                return false;
            }

            string query = uri.Query;
            if (string.IsNullOrEmpty(query) && uri.Fragment.Contains("?"))
            {
                int index = uri.Fragment.IndexOf('?');
                if (index >= 0)
                {
                    query = uri.Fragment.Substring(index);
                }
            }
            NameValueCollection parameters = HttpUtility.ParseQueryString(query);
            if (!TryGetRequired(parameters, "svr_id", out string serverId, out error)
                || !TryGetRequired(parameters, "player_id", out string playerId, out error)
                || !TryGetRequired(parameters, "lang", out string languageCode, out error)
                || !TryGetRequired(parameters, "record_id", out string recordId, out error)
                || !TryGetRequired(parameters, "gacha_type", out string cardPoolValue, out error))
            {
                return false;
            }

            if (!int.TryParse(cardPoolValue, out int cardPoolType) || cardPoolType < 0)
            {
                error = "Query parameter 'gacha_type' must be a non-negative integer.";
                return false;
            }

            request = new GachaRequest
            {
                ServerId = serverId,
                PlayerId = playerId,
                LanguageCode = languageCode,
                CardPoolType = cardPoolType,
                RecordId = recordId
            };
            return true;
        }

        private static bool TryGetRequired(
            NameValueCollection parameters,
            string key,
            out string value,
            out string? error)
        {
            value = parameters[key]?.Trim() ?? string.Empty;
            if (value.Length > 0)
            {
                error = null;
                return true;
            }

            error = $"Missing required query parameter '{key}'.";
            return false;
        }
    }
}
