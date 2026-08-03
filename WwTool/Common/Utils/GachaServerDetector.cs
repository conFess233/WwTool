using System.Text.RegularExpressions;
using WwTool.Common.Enums;

namespace WwTool.Common.Utils
{
    public static class GachaServerDetector
    {
        public const string ChinaHost = "gmserver-api.aki-game2.com";
        public const string InternationalHost = "gmserver-api.aki-game2.net";

        private static readonly Regex UrlRegex = new(
            @"https://[^\s""'\r\n]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryDetect(string? input, out GachaServerRegion region)
        {
            region = GachaServerRegion.China;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            Match match = UrlRegex.Match(input);
            if (!match.Success
                || !Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            if (string.Equals(uri.Host, InternationalHost, StringComparison.OrdinalIgnoreCase))
            {
                region = GachaServerRegion.International;
                return true;
            }

            if (string.Equals(uri.Host, ChinaHost, StringComparison.OrdinalIgnoreCase))
            {
                region = GachaServerRegion.China;
                return true;
            }

            return false;
        }
    }
}
