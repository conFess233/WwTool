## 启动器资讯与背景

本文记录鸣潮 PC 启动器首页使用的两个静态 JSON：资讯（活动 / 公告 / 新闻列表与轮播图）和背景（背景视频、首帧图、标语图）。

- 数据来源：鸣潮启动器（WebView2）网络抓包
- 两个接口都是 CDN 上的静态文件，`GET` 即可，无需登录

### 路径参数

| 参数          | 说明                         | 备注                                             |
| ------------- | ---------------------------- | ------------------------------------------------ |
| `gameId`      | 启动器游戏 ID                | 国服 `10003`，国际服 `50004`                     |
| `launcherKey` | 启动器 Key                   | 与 `gameId` 用 `_` 连接，见[内部凭据](#内部凭据) |
| `appCode`     | 启动器应用代号               | 国服 `G152`，国际服 `G153`                       |
| `backgroundId`| 背景配置 ID                  | 仅背景接口使用，见[内部凭据](#内部凭据)          |
| `language`    | 语言                         | [详情](WW_API.md#语言)，以 `.json` 结尾          |

| 区服   | 域名                                          | gameId | appCode |
| ------ | --------------------------------------------- | ------ | ------- |
| 国服   | `https://prod-cn-alicdn-gamestarter.kurogame.com` | 10003  | G152    |
| 国际服 | `https://prod-alicdn-gamestarter.kurogame.com`    | 50004  | G153    |

#### 请求参数 (Query)

| 字段 | 类型 | 示例值     | 说明                   | 备注                           |
| ---- | ---- | ---------- | ---------------------- | ------------------------------ |
| \_t  | int  | 1794381002 | 当前 Unix 时间戳（秒） | 防 CDN 缓存，可省略或任意取值 |

#### 请求头

使用[通用请求头](WW_API.md#通用请求头)即可。启动器实际还会带 `Origin: null`、`Accept: application/json, text/plain, */*`，均非必需。

#### 内部凭据

> 具体数据请自行搜集（从启动器抓包可得）

| 字段                   | 说明                         | 备注                               |
| ---------------------- | ---------------------------- | ---------------------------------- |
| launcherKey（国服）    | 国服启动器 Key               | 32 位字母数字                      |
| launcherKey（国际服）  | 国际服启动器 Key             | 32 位字母数字                      |
| backgroundId（国服）   | 国服背景配置 ID              | 32 位十六进制；与国际服不同；来源未确认，可能随版本更换 |
| backgroundId（国际服） | 国际服背景配置 ID            | 32 位十六进制；来源未确认，可能随版本更换 |

---

## 启动器资讯

### 请求地址

`{域名}/launcher/app/{gameId}_{launcherKey}/{appCode}/official/information/{language}.json`

| 区服   | 示例                                                                                                   |
| ------ | ------------------------------------------------------------------------------------------------------ |
| 国服   | `https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/app/10003_<launcherKey>/G152/official/information/zh-Hans.json` |
| 国际服 | `https://prod-alicdn-gamestarter.kurogame.com/launcher/app/50004_<launcherKey>/G153/official/information/zh-Hans.json`    |

### 请求方式

`GET`

### 认证方式

无

### 响应

`json`

| 字段      | 类型   | 示例值 | 说明                 | 备注                      |
| --------- | ------ | ------ | -------------------- | ------------------------- |
| guidance  | object | {}     | 活动 / 公告 / 新闻   | [详情](#guidance)         |
| slideshow | list   | []     | 首页轮播图           | [详情](#slideshow)        |

响应直接是业务数据，**没有** `code` / `message` 外层。

#### guidance

| 字段     | 类型   | 示例值   | 说明         | 备注                         |
| -------- | ------ | -------- | ------------ | ---------------------------- |
| desc     | string | 暂无内容 | 空列表占位文字 | 本地化文字                 |
| activity | object | {}       | 活动         | [栏目结构](#栏目)            |
| notice   | object | {}       | 公告         | [栏目结构](#栏目)            |
| news     | object | {}       | 新闻         | [栏目结构](#栏目)            |

##### 栏目

| 字段           | 类型   | 示例值 | 说明       | 备注                                                         |
| -------------- | ------ | ------ | ---------- | ------------------------------------------------------------ |
| title          | string | 活动   | 栏目标题   | 本地化文字                                                   |
| sort           | int    | 1      | 排序       | 升序排列；样本为活动 1、公告 2、新闻 3                       |
| functionSwitch | int    | 1      | 栏目开关   | `1` 显示，`0` 关闭；关闭时 `contents` 为空列表（国际服样本的活动栏目即为 `0`） |
| contents       | list   | []     | 条目列表   | [详情](#contents)                                            |

##### contents

| 字段    | 类型   | 示例值                                              | 说明     | 备注                                                       |
| ------- | ------ | --------------------------------------------------- | -------- | ---------------------------------------------------------- |
| content | string | 【3.7版本】[角色/武器活动唤取・第二期]              | 标题     | 本地化文字，可能含多余空格                                 |
| jumpUrl | string | https://www.kurobbs.com/mc/post/1562066662997704704 | 跳转链接 | 国服多为库街区 / 官网，国际服多为 Facebook / 国际官网      |
| time    | string | 10-21                                               | 发布日期 | 格式 `MM-dd`，**不带年份**，跨年时需自行推断               |

#### slideshow

| 字段          | 类型   | 示例值                                                                                          | 说明         | 备注                                     |
| ------------- | ------ | ----------------------------------------------------------------------------------------------- | ------------ | ---------------------------------------- |
| url           | string | https://prod-alicdn-community.kurobbs.com/window/76c430b779854aae83514ea76ced76ca20261021.jpg | 图片地址     | jpg / png，不同条目可能在不同 CDN         |
| jumpUrl       | string | https://www.bilibili.com/video/BV1R8nF65Eme                                                     | 点击跳转链接 |                                          |
| md5           | string | 69c75e29936f8c79cbe69c327ec5431d                                                                | 图片 MD5     | 可用作缓存键或下载校验；同一张图国服与国际服 `url` 不同但 `md5` 可能相同 |
| carouselNotes | string | 锁暝pv                                                                                          | 运营备注     | 内部备注，不建议直接展示                 |

#### 行为说明

- 列表顺序即启动器展示顺序，样本中并非严格按 `time` 排序。
- 国际服请求 `zh-Hans` 时，`content` 同样返回简体中文，但跳转链接为国际服渠道。
- 每个栏目样本中 2–3 条，轮播 4–6 张。

### 示例

#### 请求

```http
GET /launcher/app/50004_<launcherKey>/G153/official/information/zh-Hans.json?_t=1794380999 HTTP/2
Host: prod-alicdn-gamestarter.kurogame.com
Accept: application/json, text/plain, */*
Accept-Encoding: gzip, deflate, br, zstd
Accept-Language: zh-CN,zh;q=0.9,en;q=0.8
```

#### 响应（国际服，节选）

```json
{
  "guidance": {
    "desc": "暂无内容",
    "activity": {
      "title": "活动",
      "sort": 1,
      "functionSwitch": 0,
      "contents": []
    },
    "notice": {
      "title": "公告",
      "sort": 2,
      "functionSwitch": 1,
      "contents": [
        {
          "content": "【3.7版本】[角色/武器活动唤取・第二期]",
          "jumpUrl": "https://www.facebook.com/share/p/1FwnLNQnoy/",
          "time": "10-21"
        }
      ]
    },
    "news": {
      "title": "新闻",
      "sort": 3,
      "functionSwitch": 1,
      "contents": [
        {
          "content": "档案公开 | 十重契——锁暝",
          "jumpUrl": "https://www.facebook.com/share/p/1GUV3iPg6h/",
          "time": "10-21"
        }
      ]
    }
  },
  "slideshow": [
    {
      "url": "https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/news/20261021/ccfaa4ea90fd4a0c81f37e607e2cf6a9.jpg",
      "jumpUrl": "https://www.facebook.com/share/v/19XjgcDJYM/",
      "md5": "e27749d8ef46cdb874bd33bf4618ad71",
      "carouselNotes": "锁暝pv"
    }
  ]
}
```

---

## 启动器背景

### 请求地址

`{域名}/launcher/game/{gameId}_{launcherKey}/{appCode}/official/background/{backgroundId}/{language}.json`

| 区服   | 示例                                                                                                                            |
| ------ | ------------------------------------------------------------------------------------------------------------------------------- |
| 国服   | `https://prod-cn-alicdn-gamestarter.kurogame.com/launcher/game/10003_<launcherKey>/G152/official/background/<backgroundId>/zh-Hans.json` |
| 国际服 | `https://prod-alicdn-gamestarter.kurogame.com/launcher/game/50004_<launcherKey>/G153/official/background/<backgroundId>/zh-Hans.json` |

> 注意资讯接口路径是 `/launcher/app/`，背景接口是 `/launcher/game/`，且多一级 `backgroundId`。

### 请求方式

`GET`

### 认证方式

无

### 响应

`json`

| 字段               | 类型   | 示例值                                                                                                                               | 说明         | 备注                                                       |
| ------------------ | ------ | ------------------------------------------------------------------------------------------------------------------------------------ | ------------ | ---------------------------------------------------------- |
| functionSwitch     | int    | 1                                                                                                                                    | 开关         | `1` 启用；`0` 时应回退到默认背景（未见样本）               |
| backgroundFile     | string | https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/background/20260930/68a8969b17524b7da001bc567a73c524.mp4 | 背景文件     | 类型由 `backgroundFileType` 决定                           |
| backgroundFileType | int    | 2                                                                                                                                    | 背景文件类型 | `2` 视频（mp4）；其他取值未确认（推测 `1` 为静态图片）     |
| firstFrameImage    | string | https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/background/20260930/544e04e45df24686af193a4a256fae8e.webp | 首帧图       | webp；视频加载完成前显示，也可直接作为静态背景使用         |
| slogan             | string | https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/background/20260930/1ed926d57c97482fb8691038c7196968.png | 标语图       | 透明 png，叠加在背景上                                     |

响应直接是业务数据，**没有** `code` / `message` 外层。资源路径中的日期段（如 `20260930`）为资源上传日期，随版本更新变化。

资源所在 CDN 因区服而异：

| 区服   | 资源地址前缀                                                                      | 备注                                                                 |
| ------ | --------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| 国服   | `https://pcdownload-huoshan.aki-game.com/launcher/game/10004/G152/bilibili/assets/background/` | 请求用 `10003` / `official`，资源路径却是 `10004` / `bilibili`，样本如此，原因未确认；直接使用返回的完整地址即可 |
| 国际服 | `https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/background/` |                                                                      |

### 示例

#### 请求（国服）

```http
GET /launcher/game/10003_<launcherKey>/G152/official/background/<backgroundId>/zh-Hans.json?_t=1794382011 HTTP/2
Host: prod-cn-alicdn-gamestarter.kurogame.com
Accept: application/json, text/plain, */*
Accept-Encoding: gzip, deflate, br, zstd
Accept-Language: zh-CN,zh;q=0.9,en;q=0.8
```

#### 响应（国服）

```json
{
  "functionSwitch": 1,
  "backgroundFile": "https://pcdownload-huoshan.aki-game.com/launcher/game/10004/G152/bilibili/assets/background/20260930/dc839ed8df284b1fb37d03ee34f188e3.mp4",
  "backgroundFileType": 2,
  "firstFrameImage": "https://pcdownload-huoshan.aki-game.com/launcher/game/10004/G152/bilibili/assets/background/20260930/875d3f84219443c88065b34fe2a35a60.webp",
  "slogan": "https://pcdownload-huoshan.aki-game.com/launcher/game/10004/G152/bilibili/assets/background/20260930/6d10bef9a08b485cac5dc946ee1145b9.png"
}
```

#### 请求（国际服）

```http
GET /launcher/game/50004_<launcherKey>/G153/official/background/<backgroundId>/zh-Hans.json?_t=1794381002 HTTP/2
Host: prod-alicdn-gamestarter.kurogame.com
Accept: application/json, text/plain, */*
Accept-Encoding: gzip, deflate, br, zstd
Accept-Language: zh-CN,zh;q=0.9,en;q=0.8
```

#### 响应（国际服）

```json
{
  "functionSwitch": 1,
  "backgroundFile": "https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/background/20260930/68a8969b17524b7da001bc567a73c524.mp4",
  "backgroundFileType": 2,
  "firstFrameImage": "https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/background/20260930/544e04e45df24686af193a4a256fae8e.webp",
  "slogan": "https://hw-pcdownload-qcloud.aki-game.net/launcher/game/50004/G153/official/assets/background/20260930/1ed926d57c97482fb8691038c7196968.png"
}
```
