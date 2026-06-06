using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WwTool.Common.Utils
{
    /// <summary>
    /// 极验行为验证本地服务
    /// </summary>
    public static class GeetestServer
    {
        // 库洛游戏端的极验 Captcha ID
        private const string CAPTCHA_ID = "1f4565ff7acc97b1a2fc97b921743aa4";

        /// <summary>
        /// 开启本地服务并唤起浏览器进行验证
        /// </summary>
        public static async Task<Dictionary<string, string>> SolveGeetestAsync(int port = 5000)
        {
            string url = $"http://localhost:{port}/";
            using var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();

            // 自动唤起系统浏览器
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            while (true)
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                try
                {
                    // 1. 浏览器访问主页，返回包含极验 SDK 的 HTML
                    if (request.Url.AbsolutePath == "/" && request.HttpMethod == "GET")
                    {
                        string html = GetGeetestHtml();
                        byte[] buffer = Encoding.UTF8.GetBytes(html);
                        
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength64 = buffer.Length;
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    }
                    // 2. 浏览器验证成功后，将参数 POST 回本地服务器
                    else if (request.Url.AbsolutePath == "/submit" && request.HttpMethod == "POST")
                    {
                        using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                        string body = await reader.ReadToEndAsync();

                        // 解析极验返回的验证参数
                        var resultParams = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

                        // 返回成功提示给浏览器
                        string okHtml = "<!DOCTYPE html><html lang='zh-CN'><body><h2 style='color:green;text-align:center;margin-top:20%'>验证成功！请关闭该网页并返回客户端。</h2></body></html>";
                        byte[] buffer = Encoding.UTF8.GetBytes(okHtml);
                        response.ContentType = "text/html; charset=utf-8";
                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        
                        return resultParams ?? new Dictionary<string, string>();
                    }
                    else
                    {
                        response.StatusCode = 404;
                    }
                }
                finally
                {
                    response.Close();
                }
            }
        }

        /// <summary>
        /// 生成极验 V4 版本的集成 HTML 模板
        /// </summary>
        private static string GetGeetestHtml()
        {
            return $@"
        <!DOCTYPE html>
        <html lang='zh-CN'>
        <head>
            <meta charset='UTF-8'>
            <meta name='referrer' content='no-referrer'>
            <title>库洛游戏 - 极验行为校验</title>
            <style>
                body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f5f7; display: flex; flex-direction: column; align-items: center; padding-top: 10%; }}
                .container {{ background: white; padding: 30px; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); text-align: center; max-width: 500px; }}
                .error-msg {{ color: #d93025; font-size: 14px; text-align: left; background: #fce8e6; padding: 15px; border-radius: 8px; display: none; }}
            </style>
            <script src='https://static.geetest.com/v4/gt4.js' onerror='showNetworkError()'></script>
        </head>
        <body>
            <div class='container'>
                <h2 style='margin-top:0'>账号保护：安全检测</h2>
                <p>您的登录触发了风控，请完成下方滑块验证以继续：</p>
                
                <div id='error-box' class='error-msg'>
                    <strong>⚠️ 严重错误：无法加载极验组件。</strong><br><br>
                    网络拦截了 <code>gt4.js</code>，请关闭您的去广告插件或浏览器防追踪护盾，然后刷新本页。
                </div>

                <div id='captcha' style='margin: 20px auto; min-height: 50px; display: flex; justify-content: center;'></div>
                <p id='status' style='color: #666; font-size: 14px;'>正在加载验证码组件...</p>
            </div>

            <script>
                function showNetworkError() {{
                    document.getElementById('status').style.display = 'none';
                    document.getElementById('error-box').style.display = 'block';
                }}

                window.onload = function() {{
                    if (typeof initGeetest4 === 'undefined') {{
                        showNetworkError();
                        return;
                    }}

                    initGeetest4({{
                        captchaId: '{CAPTCHA_ID}',
                        product: 'float'
                    }}, function (captchaObj) {{
                        captchaObj.onReady(function () {{
                            document.getElementById('status').style.display = 'none';
                        }});
                        
                        // 将极验组件挂载到页面的 div 中
                        captchaObj.appendTo('#captcha');

                        captchaObj.onSuccess(function () {{
                            document.getElementById('status').innerText = '验证通过，正在回传数据...';
                            document.getElementById('status').style.display = 'block';
                            document.getElementById('status').style.color = 'green';
                            
                            var result = captchaObj.getValidate();

                            fetch('/submit', {{
                                method: 'POST',
                                headers: {{ 'Content-Type': 'application/json' }},
                                body: JSON.stringify(result)
                            }}).then(res => res.text()).then(html => {{
                                document.open();
                                document.write(html);
                                document.close();
                            }});
                        }});

                        captchaObj.onError(function (err) {{
                            document.getElementById('status').innerText = '验证码加载失败: ' + err.msg;
                            document.getElementById('status').style.color = 'red';
                        }});
                    }});
                }};
            </script>
        </body>
        </html>";
        }
    }
}
