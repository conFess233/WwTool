using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Policy;
using System.Text;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace WwTool.Common.Converters
{
    /// <summary>
    /// 图片路径转换器
    /// </summary>
    public class ImageConverter : IValueConverter
    {
        // 图片缓存
        private static readonly ConcurrentDictionary<string, BitmapImage> cache = new();

        private const string DefaultImg = "pack://application:,,,/UI/Resources/Images/Default.png";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;

            if (string.IsNullOrEmpty(path))
            {
                return LoadImage(DefaultImg);
            }

            try
            {
                // 网络图片
                if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadImage(path);
                }

                if (path.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadImage(path);
                }

                // 本地文件
                if (Path.IsPathRooted(path))
                {
                    if (!File.Exists(path))
                        return LoadImage(DefaultImg);

                    return LoadImage(path);
                }


                // 项目资源（相对路径）
                return LoadImage("pack://application:,,,/" + path);
            }
            catch
            {
                // 发生异常时使用默认图片
                return LoadImage(DefaultImg);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        // 缓存
        private BitmapImage LoadImage(string url)
        {
            if (cache.TryGetValue(url, out var cachedBitmap))
            {
                return cachedBitmap;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.Absolute);

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;

                bitmap.DecodePixelWidth = 64;

                bitmap.EndInit();

                // 冻结对象
                bitmap.Freeze();

                // 加入缓存
                cache.TryAdd(url, bitmap);
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("图片加载失败: " + ex.Message);
                return CreateDefaultImage();
            }
        }

        private BitmapImage CreateDefaultImage()
        {
            try
            {
                var defaultImg = new BitmapImage();
                defaultImg.BeginInit();
                defaultImg.UriSource = new Uri(DefaultImg, UriKind.Absolute);
                defaultImg.CacheOption = BitmapCacheOption.OnLoad;
                defaultImg.DecodePixelWidth = 64;
                defaultImg.EndInit();
                defaultImg.Freeze();
                return defaultImg;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("默认图片加载失败 - " + ex.Message);
                return null;
            }
        }
    }
}
