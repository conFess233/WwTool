# WwTool

> 鸣潮工具箱(?)

[English](WwTool/docs/README_en.md) | [日本語](WwTool/docs/README_ja.md) | 简体中文

## 简介

因为国际服想快速查看角色信息有点麻烦，又不想去用各种机器人，所以干脆做了个小工具，方便查看抽卡记录什么的。

## 功能

> 由于官方未开放国际服获取角色所有信息的接口，所以只能获取到部分信息。

1. 基本信息
   - 头像，昵称，UID，性别等
   - 角色等级，索拉等级，已解锁的角色数量，创建日期等
   - 活跃天数，活跃度，周本奖励次数，周本奖励次数上限等

2. 先约电台
   - 等级，经验，开通状态，高级电台状态等
   - 本周先约电讯经验

3. 大世界探索度
   - 声匣收集数量（不确定是哪的，目前推测应该是背包里的）
   - 已开启的宝箱数量
   - 已开启的潮汐之遗数量

4. 摩托数据
   - 摩托等级，经验，皮肤，饰品数据等
   - 车载音乐专辑解锁情况

> 以上功能均需登录账号后才能获取。目前仅支持邮箱账号密码登录。

5. 抽卡记录
   - 获取抽卡记录并进行简单分析整理。
   - 支持手动导入链接，以及选择游戏根目录后自动从游戏日志自动导入链接。
   - [点我查看教程](WwTool/docs/Help.md)

## 如何使用？

#### 开箱即用

1. 从 [Releases](https://github.com/conFess233/WwTool/releases) 下载并解压最新版 `WwTool.zip`。
2. 双击运行 `WwTool.exe`。

#### 自行编译

1. 克隆源码到本地：`git clone https://github.com/conFess233/WwTool.git`
2. 使用 Visual Studio 2022 或更高版本打开 `WwTool.slnx`，编译生成项目。
3. 运行输出目录中的 `WwTool.exe`。
   > 开发环境: .NET 10.0

## 运行环境

- 仅支持 Windows 10 及以上版本。
- 需要安装 .NET 10.0 桌面运行时 [下载 Microsoft .NET 10.0 Runtime - Windows x64](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.8/windowsdesktop-runtime-10.0.8-win-x64.exe)

## 整理的文档

- [鸣潮API文档](./WwTool/docs/API/WW_API.md)
- [角色资源](./WwTool/docs/Resource/Characters.md)
- [武器资源](./WwTool/docs/Resource/Weapons.md)
- [摩托资源](./WwTool/docs/Resource/Motorcycle.md)

## 预览

![WwTool](./WwTool/docs/Img/1.png)
![WwTool](./WwTool/docs/Img/2.png)
![WwTool](./WwTool/docs/Img/3.png)
![WwTool](./WwTool/docs/Img/4.png)
![WwTool](./WwTool/docs/Img/5.png)

## 简单的教程

1. **设置游戏路径**：
   - 切换到 **设置** 页面。
   - 在 **游戏安装目录** 处点击 **选择路径**，选中包含 `Wuthering Waves Game` 文件夹的《鸣潮》游戏目录（例如 `D:\Wuthering Waves`），然后点击 **保存设置**。
2. **同步游戏数据**（角色、探索度、摩托）：
   - 切换到 **首页**。
   - 点击 **添加账号**，输入库洛游戏账号的 **邮箱** 与 **密码** 进行登录。
   - _若触发风控，软件将自动打开本地浏览器窗口，在网页中完成滑块验证码即可。_
   - 登录成功后，在下拉菜单中选择您的 UID，点击 **获取云端数据** 进行数据同步。
3. **同步抽卡历史记录**：
   - 确保在游戏内**至少打开过一次**抽卡历史记录页面。
   - 切换到 **抽卡统计** 页面。
   - 点击 **自动导入链接**，软件会自动读取日志并填充 API 链接。
   - 解析出 UID 后，点击 **获取云端数据** 即可拉取并开始抽卡分析。

> 更加详细的步骤及常见问题排查，请参阅： [详细使用教程](WwTool/docs/Help.md)

## 问题反馈

欢迎通过 [Issue](https://github.com/conFess233/WwTool/issues) 提出建议或反馈问题，或提交PR。

> ~~改不改不一定哈，我比较懒~~

## 已知缺陷

- 目前仅支持邮箱账号密码登录。
- 目前仅支持国际服（抽卡统计功能除外）。
- 获取角色数据较少，且部分数据（如角色养成，声骸等）无法获取。
- 由于部分游戏物品我没有，所以懒得去找资源了，后续可能补充。

## 免责声明

本工具完全免费开源。仅供学习交流使用，请勿用于任何商业或非法用途。数据和UI资源均通过客户端逆向解包及公开网络资料收集整理获取，侵删。
