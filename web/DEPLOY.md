# WwTool Web 端部署说明

后端是一个 ASP.NET Core 程序，前端构建产物打包在它的 `wwwroot` 里，部署时只需要运行这一个程序，再在前面放一层提供 HTTPS 的反向代理。

- 站点数据（站点账号、UID 绑定、抽卡记录与账号资料、登录 Cookie 的加密密钥）保存在 SQLite 与数据目录里，**只能运行一个实例**，并且需要一块不会丢失的磁盘。
- 背景图、视频、公告轮播图、资料图标都由浏览器直接从源站加载，服务器只提供页面本身、界面小图标和接口数据，流量很小。
- 服务器需要能访问库洛的接口（公告、启动器、攻略站）和 encore.moe。

下面给出两种方式：

| 方式 | 适合 | 费用 |
| --- | --- | --- |
| [A. 自己的电脑 + Cloudflare Tunnel](#a-自己的电脑--cloudflare-tunnel) | 不想租服务器、数据想留在自己手里 | 只需一个域名 |
| [B. Linux 服务器 + Caddy](#b-linux-服务器--caddy) | 需要一直在线 | 服务器费用（也有永久免费的小机器） |

> [!WARNING]
> 后端目前只在 Windows 上完整测试过。用方式 B 部署到 Linux 后，请把各页面都点一遍再对外公开。

## 1. 打包

需要 [.NET 10 SDK](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0) 与 Node.js 20.19 以上。在仓库根目录：

```powershell
cd web
npm ci
npm run build
cd ..
```

再按目标系统发布（自带运行时，服务器上不用安装 .NET）：

```powershell
# Windows
dotnet publish src/Web -c Release -r win-x64 --self-contained -o publish/web
# Linux（x64 服务器）
dotnet publish src/Web -c Release -r linux-x64 --self-contained -o publish/web
# Linux（ARM 服务器，如 Oracle 免费机）
dotnet publish src/Web -c Release -r linux-arm64 --self-contained -o publish/web
```

`publish/web` 就是要部署的全部文件，前端已经在其中的 `wwwroot` 里。

## 2. 配置

在程序目录新建 `appsettings.Production.json`（发布后的程序默认就是 Production 环境，会自动读取它）：

```json
{
  "Urls": "http://127.0.0.1:5180",
  "AllowedHosts": "wwtool.example.com",
  "WwTool": {
    "DataRoot": "data",
    "ReverseProxy": {
      "Enabled": true,
      "KnownNetworks": [],
      "ClientIpHeader": ""
    }
  }
}
```

| 键 | 说明 |
| --- | --- |
| `Urls` | 后端监听的地址。**只监听本机**（`127.0.0.1`），外部访问一律经过反向代理 |
| `AllowedHosts` | 你的域名，多个用分号分隔 |
| `WwTool:DataRoot` | 数据目录，相对路径按程序目录解析。建议放在程序目录之外（如 `D:\wwtool-data`、`/var/lib/wwtool`），更新程序时不会动到数据 |
| `WwTool:ReverseProxy:Enabled` | 放在反向代理后面时必须打开：后端会读取代理转发的访客 IP 与协议，限流才能按访客分开，会话 Cookie 才会带 `Secure`。直接对外暴露时保持关闭，转发头会被忽略，防止伪造 |
| `WwTool:ReverseProxy:KnownNetworks` | 信任的代理网段。默认只信任本机；代理在别的机器或 Docker 网络里时写它的网段，如 `["172.17.0.0/16"]` |
| `WwTool:ReverseProxy:ClientIpHeader` | 读取访客 IP 的请求头，默认 `X-Forwarded-For`。用 Cloudflare 时填 `CF-Connecting-IP` |

其他说明：

- 开发用的免密登录接口只在 Development 环境且配置了 `EnableDevTools` 时才注册，正式部署不会出现。
- `appsettings.json` 里的 `ContentSecurityPolicy` 已放行用到的外部来源（游戏登录的安全验证、库洛接口、外部图片与视频、GitHub 与 jsDelivr 上的资料），一般不用改。
- 环境变量也可以覆盖这些设置，层级用双下划线，如 `WwTool__DataRoot=/var/lib/wwtool`。

## A. 自己的电脑 + Cloudflare Tunnel

不需要公网 IP，也不用在路由器上开端口；HTTPS 由 Cloudflare 提供。需要一个托管在 Cloudflare 的域名，电脑需要一直开着。

1. **配置**：`appsettings.Production.json` 里把 `ClientIpHeader` 设为 `CF-Connecting-IP`，`KnownNetworks` 保持为空（cloudflared 在本机连后端）。
2. **开机自动运行后端**：以管理员身份打开 PowerShell（路径按实际修改）：

   ```powershell
   $action = New-ScheduledTaskAction -Execute 'C:\wwtool\WwTool.Web.exe' -WorkingDirectory 'C:\wwtool'
   $trigger = New-ScheduledTaskTrigger -AtStartup
   $settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit 0
   Register-ScheduledTask -TaskName 'WwTool Web' -Action $action -Trigger $trigger -Settings $settings -User 'SYSTEM' -RunLevel Highest
   Start-ScheduledTask -TaskName 'WwTool Web'
   ```

   之后在本机打开 `http://127.0.0.1:5180` 应能看到网站。
3. **建立隧道**：在 Cloudflare 控制台的 Zero Trust → Networks → Tunnels 新建一个 Cloudflared 隧道，按页面提示在电脑上安装 cloudflared 并执行它给出的 `cloudflared service install <令牌>`（会注册为开机自启的服务）。
4. **绑定域名**：在该隧道的 Public Hostname 里添加你的域名，Service 选 `HTTP`，地址填 `127.0.0.1:5180`。
5. 用浏览器打开 `https://你的域名` 检查。

## B. Linux 服务器 + Caddy

Caddy 会自动申请和续期 HTTPS 证书。以下以 Debian / Ubuntu 为例。

1. **上传与目录**：

   ```bash
   sudo useradd --system --home /opt/wwtool --shell /usr/sbin/nologin wwtool
   sudo mkdir -p /opt/wwtool /var/lib/wwtool
   # 把 publish/web 下的全部文件复制到 /opt/wwtool，并放好 appsettings.Production.json
   sudo chmod +x /opt/wwtool/WwTool.Web
   sudo chown -R wwtool:wwtool /opt/wwtool /var/lib/wwtool
   ```

   `appsettings.Production.json` 里 `DataRoot` 写 `/var/lib/wwtool`，`ReverseProxy:Enabled` 为 `true`，其余保持默认（Caddy 在本机）。
2. **systemd 服务**：新建 `/etc/systemd/system/wwtool.service`：

   ```ini
   [Unit]
   Description=WwTool Web
   After=network-online.target
   Wants=network-online.target

   [Service]
   User=wwtool
   WorkingDirectory=/opt/wwtool
   ExecStart=/opt/wwtool/WwTool.Web
   Restart=always
   RestartSec=5

   [Install]
   WantedBy=multi-user.target
   ```

   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now wwtool
   curl -I http://127.0.0.1:5180    # 应返回 200
   ```

3. **Caddy**：[安装 Caddy](https://caddyserver.com/docs/install) 后编辑 `/etc/caddy/Caddyfile`：

   ```
   wwtool.example.com {
       request_body {
           max_size 8MB
       }
       reverse_proxy 127.0.0.1:5180
   }
   ```

   ```bash
   sudo systemctl reload caddy
   ```

   抽卡记录上传最大 6 MB，所以请求体上限要大于 6 MB。域名的 DNS 需要先解析到这台服务器，并在防火墙放行 80 与 443 端口。

如果在 Caddy 前面再加一层 Cloudflare 代理（橙色云朵），把 `ClientIpHeader` 设为 `CF-Connecting-IP`。

## 更新

1. 按「打包」重新发布。
2. 停止后端（Windows：`Stop-ScheduledTask -TaskName 'WwTool Web'`；Linux：`sudo systemctl stop wwtool`）。
3. **先备份数据目录**，再用新文件覆盖程序目录（保留 `appsettings.Production.json`）。
4. 启动后端，打开网站检查。

## 备份

定期备份整个数据目录，最重要的是：

- `Local/Data/WebSite.db`：站点账号与 UID 绑定
- `Local/Data` 下的其他数据库：抽卡记录、账号资料与角色快照
- `Local/Data/keys/`：登录 Cookie 的加密密钥，丢失后所有人需要重新登录

`Local/Cache` 与 `Local/Logs` 可以不备份。

## 上线前检查

- [ ] `ReverseProxy:Enabled` 已打开，`AllowedHosts` 是你的域名
- [ ] 后端只监听 `127.0.0.1`，外部无法绕过代理直接访问 5180 端口
- [ ] 用 `https://` 打开网站：首页背景与公告、抽卡统计、角色详情、设置页的资料条目数都正常
- [ ] 用手机打开一遍：底部导航、公告详情与角色详情都能正常显示和关闭
- [ ] 注册一个站点账号并登录，刷新后仍保持登录
- [ ] 数据目录已加入备份
