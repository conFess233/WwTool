using System;
using System.Diagnostics;
using WwTool.Common;
using WwTool.Common.Utils;

using WwTool.Services.Interfaces;

namespace WwTool.UI.ViewModels
{
    /// <summary>
    /// 关于页面视图模型
    /// 展示应用版本号、作者信息，并提供外部链接跳转功能
    /// </summary>
    public class AboutViewModel : BindableBase
    {
        /// <summary>
        /// 应用程序版本号
        /// </summary>
        private string _version = "1.0.0";
        public string Version
        {
            get => _version;
            set { _version = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 应用程序作者名称
        /// </summary>
        private string _author = "";
        public string Author
        {
            get => _author;
            set { _author = value; RaisePropertyChanged(); }
        }

        /// <summary>
        /// 打开外部链接命令（如 GitHub 页面等）
        /// </summary>
        public DelegateCommand<string> OpenLinkCommand { get; private set; }

        /// <summary>
        /// 构造函数，通过配置服务注入版本和作者信息
        /// </summary>
        /// <param name="configService">配置服务接口</param>
        public AboutViewModel(IConfigService configService)
        {
            OpenLinkCommand = new DelegateCommand<string>(OpenLink);
            
            Version = $"v{configService.App.AppVersion}";
            Author = configService.App.AppAuther;
        }

        /// <summary>
        /// 使用系统默认浏览器打开指定的 URL
        /// </summary>
        /// <param name="url">要打开的链接地址</param>
        private void OpenLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
                // 忽略打开链接的错误
            }
        }
    }
}
