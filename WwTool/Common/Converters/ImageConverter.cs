using System;
using System.Runtime.Caching;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Windows.Data;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WwTool.Common.Converters
{
    /// <summary>
    /// 图片路径转换器
    /// </summary>
    public class ImageConverter : IValueConverter
    {
        // 图片缓存
        private static readonly MemoryCache cache = MemoryCache.Default;
        private static readonly HttpClient httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15)
        };

        private const string DefaultImg = "pack://application:,,,/UI/Resources/Images/Default.png";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            int decodePixelWidth = 64;
            if (parameter is int intWidth && intWidth > 0)
                decodePixelWidth = intWidth;
            else if (parameter is string stringWidth && int.TryParse(stringWidth, out int parsedWidth) && parsedWidth > 0)
                decodePixelWidth = parsedWidth;

            if (string.IsNullOrEmpty(path))
            {
                return LoadImage(DefaultImg, decodePixelWidth);
            }

            try
            {
                // 网络图片
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadImage(path, decodePixelWidth);
                }

                if (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadImage(path, decodePixelWidth);
                }

                // 本地文件
                if (Path.IsPathRooted(path))
                {
                    if (!File.Exists(path))
                        return LoadImage(DefaultImg, decodePixelWidth);

                    return LoadImage(path, decodePixelWidth);
                }


                // 项目资源（相对路径）
                return LoadImage("pack://application:,,,/" + path, decodePixelWidth);
            }
            catch
            {
                // 发生异常时使用默认图片
                return LoadImage(DefaultImg, decodePixelWidth);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        // 缓存
        private object LoadImage(string url, int decodePixelWidth)
        {
            string cacheKey = $"{url}|{decodePixelWidth}";
            if (cache.Get(cacheKey) is BitmapImage cachedBitmap)
            {
                return cachedBitmap;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.DecodePixelWidth = decodePixelWidth;

                using MemoryStream? remoteImageStream = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? new MemoryStream(httpClient.GetByteArrayAsync(url).GetAwaiter().GetResult())
                    : null;
                if (remoteImageStream is not null)
                {
                    bitmap.StreamSource = remoteImageStream;
                }
                else
                {
                    bitmap.UriSource = new Uri(url, UriKind.Absolute);
                }

                bitmap.EndInit();

                // 冻结对象
                bitmap.Freeze();

                // 加入缓存 (滑动过期 5 分钟)
                var policy = new CacheItemPolicy { SlidingExpiration = TimeSpan.FromMinutes(5) };
                cache.Set(cacheKey, bitmap, policy);
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("图片加载失败: " + ex.Message);
                return CreateDefaultImage(decodePixelWidth);
            }
        }

        private object CreateDefaultImage(int decodePixelWidth)
        {
            try
            {
                var defaultImg = new BitmapImage();
                defaultImg.BeginInit();
                defaultImg.UriSource = new Uri(DefaultImg, UriKind.Absolute);
                defaultImg.CacheOption = BitmapCacheOption.OnLoad;
                defaultImg.DecodePixelWidth = decodePixelWidth;
                defaultImg.EndInit();
                defaultImg.Freeze();
                return defaultImg;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("默认图片加载失败 - " + ex.Message);
                return DependencyProperty.UnsetValue;
            }
        }
    }
}
