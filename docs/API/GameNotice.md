## 游戏内公告

本文记录鸣潮游戏内「公告」界面使用的静态 JSON。它包含版本公告、活动、唤取（卡池）、周边等条目，每条都有起止时间、横幅图和 HTML 正文，另外还有游戏内「版本日历」图的数据。适合用来做活动日历、卡池时间线、当前版本信息。

- 数据来源：参考 [cyrahs/game-cal](https://github.com/cyrahs/game-cal)（`apps/api/src/games/ww.ts`）的用法。
- CDN（腾讯云 COS）上的静态文件，`GET` 即可，无需登录
- 与[启动器资讯](Launcher.md#启动器资讯)的区别：启动器资讯只有标题、`MM-dd` 日期和外链；本接口有精确到毫秒的起止时间和完整正文

### 请求地址

`{域名}/gamenotice/{appCode}/{服务器ID}/{language}.json`

| 区服   | 域名                                         | appCode | 服务器 ID                                       |
| ------ | -------------------------------------------- | ------- | ----------------------------------------------- |
| 国服   | `https://aki-gm-resources-back.aki-game.com` | G152    | `76402e5b20be2c39f095a152090afddc`              |
| 国际服 | `https://aki-gm-resources-back.aki-game.net` | G153    | 任一国际服 ID，见[服务器ID](WW_API.md#服务器id) |

| 区服   | 示例                                                                                                       |
| ------ | ---------------------------------------------------------------------------------------------------------- |
| 国服   | `https://aki-gm-resources-back.aki-game.com/gamenotice/G152/76402e5b20be2c39f095a152090afddc/zh-Hans.json` |
| 国际服 | `https://aki-gm-resources-back.aki-game.net/gamenotice/G153/86d52186155b148b5c138ceb41be9650/zh-Hans.json` |

> 路径中的服务器 ID 与[抽卡记录](GachaLog.md)、[服务器ID](WW_API.md#服务器id)用的是同一套。

#### 路径参数

| 参数       | 说明      | 备注                                                                           |
| ---------- | --------- | ------------------------------------------------------------------------------ |
| `appCode`  | 应用代号  | 与[启动器](Launcher.md#路径参数)相同：国服 `G152`，国际服 `G153`；填错返回 404 |
| `服务器ID` | 服务器 ID | 国际服 5 个服务器 ID 返回的内容**完全相同**（同一语言大小一致），任取一个即可  |
| `language` | 语言      | [详情](WW_API.md#语言)，以 `.json` 结尾；**区分大小写**（`zh-hans` 返回 404）  |

实测（2026-11-13）：

| 区服   | 可用语言                                                        | 不可用的组合                                      |
| ------ | --------------------------------------------------------------- | ------------------------------------------------- |
| 国服   | `zh-Hans` `zh-Hant` `en` `ja` `ko` `de` `es` `fr` `th` 均为 200 | 国服 ID 配 `G153`、或放在 `.net` 域名下均为 404   |
| 国际服 | 同上，均为 200                                                  | 国际服 ID 配 `G152`、或放在 `.com` 域名下均为 404 |

`aki-gm-resources.aki-game.com`、`aki-gm-resources-oversea.aki-game.net`（抽卡记录用的域名）下没有这个文件。

### 请求方式

`GET`

### 认证方式

无

#### 请求头

不需要特殊请求头。

#### 缓存

| 响应头         | 示例值                               | 说明                                                            |
| -------------- | ------------------------------------ | --------------------------------------------------------------- |
| ETag           | `"a59149fae3cacaf1582617afa00c729e"` | 支持 `If-None-Match`，未变化时返回 `304`                        |
| Last-Modified  | Fri, 13 Nov 2026 10:00:02 GMT        | 运营改公告后会变                                                |
| Content-Length | 150572                               | 国服 `zh-Hans` 约 150 KB；带 `Accept-Encoding: gzip` 时约 28 KB |

没有 `Cache-Control`，建议客户端自己做缓存并用 ETag 条件请求。

### 响应

`json`，直接是业务数据，**没有** `code` / `message` 外层。

| 字段      | 类型 | 说明           | 备注                                                |
| --------- | ---- | -------------- | --------------------------------------------------- |
| game      | list | 「游戏公告」页 | 版本说明、商城、签到、系统说明等，[条目](#公告条目) |
| activity  | list | 「活动」页     | 周边、社区资讯等，[条目](#公告条目)                 |
| recommend | list | 「推荐」页     | 唤取（卡池）、版本主活动等，[条目](#公告条目)       |
| calendar  | list | 版本日历       | 通常 1 条，[详情](#calendar)                        |

> 「哪个页签」只表示游戏里放在哪，不能当分类用；分类看条目的 `category` / `tag`。

#### 公告条目

| 字段          | 类型           | 示例值                                                                       | 说明              | 备注                                                                         |
| ------------- | -------------- | ---------------------------------------------------------------------------- | ----------------- | ---------------------------------------------------------------------------- |
| id            | string         | 50902                                                                        | 公告 ID           | 字符串形式的数字；国服与国际服同一公告 ID 不同                               |
| tabTitle      | string         | [盛放于昨日梦土的花朵]\n角色活动唤取                                         | 标题              | 可能含 `\n`（游戏内换行显示），展示前需替换                                  |
| startTimeMs   | long           | 1794427200000                                                                | 上架时间          | Unix 毫秒；[注意](#时间)                                                     |
| endTimeMs     | long           | 1796263199000                                                                | 下架时间          | Unix 毫秒；[注意](#时间)                                                     |
| operateTimeMs | long           | 1794441319000                                                                | 最后编辑时间      | Unix 毫秒                                                                    |
| category      | int            | 3                                                                            | 大类              | [取值](#category-与-tag)                                                     |
| tag           | int            | 7                                                                            | 小类 / 角标       | [取值](#category-与-tag)                                                     |
| permanent     | int            | 0                                                                            | 是否常驻公告      | `1` 常驻（如防沉迷、公平运营声明）                                           |
| tabBanner     | string         | https://aki-gm-resources-back.aki-game.com/notice/image/HhEpvxmuzOFuYpEh.png | 列表 / 顶部横幅图 | png / jpg                                                                    |
| foldBanner    | string/null    | https://aki-gm-resources-back.aki-game.com/notice/image/fAIaQxQQcRPFoVrh.jpg | 折叠状态横幅图    | 样本中只有唤取类有值，其余为 `null`                                          |
| content       | string         | `<p>…</p>`                                                                   | 正文              | HTML，带内联样式；版本说明可达 26 KB                                         |
| red           | int            | 1                                                                            | 是否显示红点      |                                                                              |
| redDotVersion | int            | 0                                                                            | 红点版本          | 推测：改大后重新亮红点                                                       |
| timeWatermark | int            | 0                                                                            | 未确认            | 样本中只有「已知问题及更新说明」为 `1`                                       |
| isToWebp      | bool/null      | true                                                                         | 图片是否可转 webp | 未确认                                                                       |
| platform      | list\<int\>    | [1,2,3,4,5,6,7,8]                                                            | 投放平台          | 未确认各值含义；防沉迷等只投 `[1..5]`                                        |
| channel       | list\<string\> | ["46"]                                                                       | 投放渠道          | 空列表表示所有渠道；国服同标题公告会按渠道拆成多条（如两条「反馈途径说明」） |
| whiteList     | list           | []                                                                           | 白名单            | 样本均为空                                                                   |

##### category 与 tag

样本（3.8 版本，国服 + 国际服）中出现的组合：

| category | tag | 内容                                             | 示例                                         |
| -------- | --- | ------------------------------------------------ | -------------------------------------------- |
| 1        | 1   | 公告：版本内容说明、已知问题、系统说明、社区资讯 | 「旧梦凋零的百合」\n3.8版本内容说明          |
| 2        | 3   | 活动                                             | [昔梦赠礼]七日签到活动                       |
| 2        | 4   | 商城 / 付费                                      | [商城礼包]限时折扣上架                       |
| 2        | 6   | 常驻玩法                                         | 「心影映花」全新常驻玩法                     |
| 3        | 7   | 唤取（卡池）                                     | 「安魂」武器活动唤取                         |
| 4        | 10  | 社区资讯（国际服）                               | Wuthering Waves Player Community Information |
| 4        | 11  | 周边                                             | 鸣潮丨「琳奈」1/7 比例手办预售将启           |

game-cal 的筛选规则可作参考：保留 `category` 为 `2`、`3` 的条目，外加 `4/10` 和「网页活动」类的 `1`；去掉 `permanent = 1`；再按标题关键词去掉商城礼包、已知问题、防沉迷等。

##### 时间

- `startTimeMs` / `endTimeMs` 是**公告**的上下架时间，不一定等于活动本身的时间。例如唤取公告在维护开始（04:00）就上架，但活动实际在「版本更新后」（维护结束）才开始。
- 活动真实时间写在正文里，通常在「✦活动时间✦」「✦唤取时间✦」「✦开放时间✦」这类小标题后，格式例如：
  - `2026年11月12日10:00 ~ 2026年12月3日09:59（服务器时间）`
  - `3.8版本更新后 ~ 2026年12月3日09:59（服务器时间）`
  - 只有月日（`12月3日09:59`），年份要按上架时间推断
- 「版本更新后」的具体时间要从当期**版本内容说明**（`category 1`）正文的「✦ 更新维护时间：2026年11月12日04:00 ~ 2026年11月12日11:00（UTC+8）」取结束时间。
- 国服正文时间为 UTC+8。国际服正文同样写「（服务器时间）」，各服务器时区不同，而国际服 5 个服务器共用同一份文件、毫秒值也与国服一致，换算方式**未确认**。
- 「挑战周期」类条目可能写成「持续时间为 3.8 版本 ~ 3.9 版本」，没有具体结束时间。

##### 唤取条目

- 标题含「唤取」即为卡池；含「角色」为角色池、含「武器」为武器池；「忆旅唤取」是复刻常驻类卡池。
- 正文第一段列出 UP 内容，例如：`活动期间， 5星角色「莉莉」，4星角色「釉瑚」、「秋水」、「秧秧」 唤取概率限时提升！`，可用 `5星(角色|武器)「…」` 提取 5 星 UP。
- 标题中的 `[盛放于昨日梦土的花朵]` 是卡池名，`「安魂」` 是武器名。

#### calendar

游戏内「版本日历」：一张整版的背景图，加上图上各区域对应的公告。

| 字段                                                                            | 类型   | 示例值                                                                       | 说明           | 备注                    |
| ------------------------------------------------------------------------------- | ------ | ---------------------------------------------------------------------------- | -------------- | ----------------------- |
| id                                                                              | string | 50901                                                                        | 日历 ID        |                         |
| startTimeMs                                                                     | long   | 1794427200000                                                                | 上架时间       | 与版本周期一致          |
| endTimeMs                                                                       | long   | 1797883199000                                                                | 下架时间       |                         |
| backgroundUrl                                                                   | string | https://aki-gm-resources-back.aki-game.com/notice/image/xEQkCoWICZCJkTQb.jpg | 日历图         | 文字和时间轴都画在图里  |
| timeline                                                                        | object | {}                                                                           | 时间轴         | [详情](#timeline)       |
| nodes                                                                           | list   | []                                                                           | 图上的可点区域 | [详情](#nodes)          |
| red / redDotVersion / isToWebp / platform / channel / whiteList / operateTimeMs |        |                                                                              |                | 同[公告条目](#公告条目) |

##### timeline

| 字段             | 类型   | 示例值              | 说明                     | 备注                         |
| ---------------- | ------ | ------------------- | ------------------------ | ---------------------------- |
| startDate        | string | 2026-11-12 04:00:00 | 版本开始                 | `yyyy-MM-dd HH:mm:ss`，UTC+8 |
| endDate          | string | 2026-12-22 03:59:59 | 版本结束                 | 同上                         |
| pointerStartDate | string | 2026-11-12 04:00:00 | 「当前时间」指针起点     |                              |
| x1               | string | 15.1875             | 时间轴起点在图上的横坐标 | 图片宽度的百分比，字符串形式 |
| x2               | string | 95.6999             | 时间轴终点在图上的横坐标 | 同上                         |

可用 `startDate`、`endDate` 作为**当前版本周期**。

##### nodes

| 字段                | 类型   | 示例值 | 说明             | 备注                                                                               |
| ------------------- | ------ | ------ | ---------------- | ---------------------------------------------------------------------------------- |
| relatedNoticeId     | int    | 50902  | 关联的公告 ID    | 对应[公告条目](#公告条目)的 `id`（那边是字符串）                                   |
| relatedNoticeStatus | int    | 1      | 关联公告状态     | `1` 已发布，能在三个列表里找到；`2` 尚未发布（下半版本的卡池和活动），列表中找不到 |
| x1 / y1 / x2 / y2   | string | 15.828 | 区域左上、右下角 | 图片宽高的百分比                                                                   |

样本中 `status = 2` 的节点都在时间轴右半段（下半版本），可据此知道「下半还有 N 个卡池 / 活动」，但标题和时间要等公告发布后才能拿到。

#### 行为说明

- 国服、国际服的条目内容基本一致，但 `id` 不同，个别条目只在一边出现（如国服的周边、签到工具）。
- 请求 `en` 等其他语言时，标题和正文都会翻译，`category` / `tag` / 时间不变。正文中的时间写法随语言变化，按中文写的解析规则只适用于 `zh-Hans`。
- 版本更新当天公告会大量变化；平时也会改（样本 `Last-Modified` 为维护后第二天）。

### 示例

#### 请求（国服）

```http
GET /gamenotice/G152/76402e5b20be2c39f095a152090afddc/zh-Hans.json HTTP/1.1
Host: aki-gm-resources-back.aki-game.com
Accept-Encoding: gzip
```

#### 响应（国服，节选）

```json
{
  "game": [
    {
      "id": "50944",
      "redDotVersion": 0,
      "startTimeMs": 1794445200000,
      "endTimeMs": 1797883199000,
      "operateTimeMs": 1794443743000,
      "isToWebp": true,
      "platform": [1, 2, 3, 4, 5, 6, 7, 8],
      "channel": [],
      "whiteList": [],
      "red": 1,
      "permanent": 0,
      "timeWatermark": 0,
      "tabTitle": "「旧梦凋零的百合」\n3.8版本内容说明",
      "tabBanner": "https://aki-gm-resources-back.aki-game.com/notice/image/puDzugTcDhERkIPt.png",
      "foldBanner": null,
      "tag": 1,
      "category": 1,
      "content": "<p>…【维护时间＆补偿说明】…✦ 更新维护时间： 2026年11月12日04:00 ~ 2026年11月12日11:00（UTC+8）…</p>"
    }
  ],
  "activity": [],
  "recommend": [
    {
      "id": "50902",
      "redDotVersion": 0,
      "startTimeMs": 1794427200000,
      "endTimeMs": 1796263199000,
      "operateTimeMs": 1794441319000,
      "isToWebp": true,
      "platform": [1, 2, 3, 4, 5, 6, 7, 8],
      "channel": [],
      "whiteList": [],
      "red": 1,
      "permanent": 0,
      "timeWatermark": 0,
      "tabTitle": "[盛放于昨日梦土的花朵]\n角色活动唤取",
      "tabBanner": "https://aki-gm-resources-back.aki-game.com/notice/image/HhEpvxmuzOFuYpEh.png",
      "foldBanner": "https://aki-gm-resources-back.aki-game.com/notice/image/fAIaQxQQcRPFoVrh.jpg",
      "tag": 7,
      "category": 3,
      "content": "<p>活动期间， 5星角色「莉莉」，4星角色「釉瑚」、「秋水」、「秧秧」 唤取概率限时提升！</p>…<p>✦活动时间✦</p><p>3.8版本更新后 ~ 2026年12月3日09:59（服务器时间）</p>…"
    }
  ],
  "calendar": [
    {
      "id": "50901",
      "startTimeMs": 1794427200000,
      "endTimeMs": 1797883199000,
      "backgroundUrl": "https://aki-gm-resources-back.aki-game.com/notice/image/xEQkCoWICZCJkTQb.jpg",
      "timeline": {
        "startDate": "2026-11-12 04:00:00",
        "endDate": "2026-12-22 03:59:59",
        "pointerStartDate": "2026-11-12 04:00:00",
        "x1": "15.1875",
        "x2": "95.6999"
      },
      "nodes": [
        {
          "relatedNoticeId": 50902,
          "relatedNoticeStatus": 1,
          "x1": "15.828",
          "y1": "9.358",
          "x2": "58.1885",
          "y2": "14.0116"
        },
        {
          "relatedNoticeId": 50905,
          "relatedNoticeStatus": 2,
          "x1": "59.1034",
          "y1": "9.4097",
          "x2": "95.6084",
          "y2": "14.0116"
        }
      ]
    }
  ]
}
```

`content` 为节选，实际是完整 HTML。

