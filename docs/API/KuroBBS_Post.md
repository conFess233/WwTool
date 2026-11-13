## 库街区帖子（前瞻兑换码）

本文记录库街区（kurobbs）的帖子搜索与帖子详情两个接口，用来从官方账号的「前瞻通讯」帖子里取兑换码和前瞻直播时间。游戏内公告里没有兑换码，需要从这里拿。

- 数据来源：参考 [cyrahs/game-cal](https://github.com/cyrahs/game-cal)（`apps/api/src/games/livestreamCodes.ts`）的用法。
- 公开帖子不需要登录
- 只有国服社区（库街区）；国际服官方渠道是 Facebook / X 等，没有对应接口

### 通用约定

| 项目        | 值                                                  |
| ----------- | --------------------------------------------------- |
| 域名        | `https://api.kurobbs.com`                           |
| 请求方式    | `POST`，表单（`application/x-www-form-urlencoded`） |
| 官方账号    | 鸣潮官方 `userId = 10012001`                        |
| 鸣潮 gameId | `3`                                                 |
| 帖子网页    | `https://www.kurobbs.com/mc/post/{postId}`          |

#### 请求头

| 字段         | 类型   | 示例值                            | 说明       | 备注                                                                |
| ------------ | ------ | --------------------------------- | ---------- | ------------------------------------------------------------------- |
| source       | string | android                           | 客户端来源 | **必需**；缺少时返回 `code: 102`「服务器外部错误」（HTTP 仍为 200） |
| devCode      | string | 任意字符串                        | 设备码     | 需要存在，服务端不校验                                              |
| version      | string | 2.2.0                             | 客户端版本 | 需要存在，服务端不校验                                              |
| Content-Type | string | application/x-www-form-urlencoded | 数据格式   |                                                                     |

#### 响应外层

| 字段    | 类型   | 示例值                               | 说明     | 备注                                             |
| ------- | ------ | ------------------------------------ | -------- | ------------------------------------------------ |
| code    | int    | 200                                  | 状态码   | `200` 成功；失败时 HTTP 状态码仍为 200，只看这里 |
| msg     | string | 请求成功                             | 提示信息 |                                                  |
| success | bool   | true                                 | 是否成功 |                                                  |
| traceId | string | 0abbfc25-7435-4407-9469-d2fd4fe0ce4c | 追踪 ID  |                                                  |
| data    | object |                                      | 业务数据 | 失败时无此字段                                   |

---

## 帖子搜索

### 请求地址

`https://api.kurobbs.com/forum/search/v2/post`

### 请求体

| 字段      | 类型   | 示例值   | 说明     | 备注                                           |
| --------- | ------ | -------- | -------- | ---------------------------------------------- |
| keyword   | string | 前瞻通讯 | 关键词   | 会被分词，标题含「前瞻」或「通讯」的都可能命中 |
| gameId    | int    | 3        | 游戏     | 鸣潮为 `3`                                     |
| pageIndex | int    | 1        | 页码     | 从 1 开始                                      |
| pageSize  | int    | 20       | 每页条数 |                                                |

### 响应 `data`

| 字段     | 类型 | 说明         | 备注                   |
| -------- | ---- | ------------ | ---------------------- |
| hasNext  | bool | 是否有下一页 |                        |
| postList | list | 帖子列表     | 按发帖时间**从新到旧** |

#### postList

只列出用到的字段：

| 字段            | 类型   | 示例值                                           | 说明     | 备注                                                                |
| --------------- | ------ | ------------------------------------------------ | -------- | ------------------------------------------------------------------- |
| postId          | string | 1568304036084461568                              | 帖子 ID  | 超出 JS 安全整数范围，按字符串处理                                  |
| userId          | string | 10012001                                         | 发帖人   | 用来筛出官方帖                                                      |
| postTitle       | string | 《鸣潮》3.8版本\<em\>前瞻通讯\</em\> \| 回顾影像 | 标题     | 命中的关键词被 `<em>…</em>` 包住，使用前需去掉                      |
| createTimestamp | string | 1793954419000                                    | 创建时间 | 字符串形式的 Unix 毫秒；是草稿 / 定时创建时间，可能早于实际发布时间 |
| isOfficial      | int    | 1                                                | 是否官方 |                                                                     |

其他字段（`browseCount`、`likeCount`、`userName`、`imgContent` 等）用于列表展示，这里不用。

#### 官方帖标题规律

| 标题                                                  | 用途                                                                        |
| ----------------------------------------------------- | --------------------------------------------------------------------------- |
| 《鸣潮》3.8版本前瞻通讯将于2026年11月6日19:00正式播出 | 直播预告，时间在标题里（较早的版本只写「x.x版本前瞻通讯预告」，时间在正文） |
| 《鸣潮》3.8版本前瞻通讯 \| 回顾影像                   | 直播结束后发布，**兑换码在楼主的评论里**                                    |
| 《鸣潮》版本前瞻 \| 3.8版本内容前瞻                   | 前瞻内容图文，无兑换码                                                      |

搜索结果里大量是玩家帖，必须按 `userId = 10012001` 过滤。

---

## 帖子详情

### 请求地址

`https://api.kurobbs.com/forum/getPostDetail`

### 请求体

| 字段            | 类型   | 示例值              | 说明         | 备注                            |
| --------------- | ------ | ------------------- | ------------ | ------------------------------- |
| postId          | string | 1568304036084461568 | 帖子 ID      |                                 |
| isOnlyPublisher | int    | 1                   | 只看楼主     | `1` 时 `comment` 只含楼主的评论 |
| showOrderType   | int    | 2                   | 评论排序     | game-cal 使用 `2`               |
| pageIndex       | int    | 1                   | 评论页码     |                                 |
| pageSize        | int    | 20                  | 评论每页条数 |                                 |

### 响应 `data`

| 字段       | 类型   | 说明     | 备注                |
| ---------- | ------ | -------- | ------------------- |
| postDetail | object | 帖子正文 | [详情](#postdetail) |
| comment    | list   | 评论     | [详情](#comment)    |
| gameId     | int    | 游戏     |                     |

#### postDetail

只列出用到的字段：

| 字段            | 类型   | 示例值                                                                                                              | 说明     | 备注                                               |
| --------------- | ------ | ------------------------------------------------------------------------------------------------------------------- | -------- | -------------------------------------------------- |
| postTitle       | string | 《鸣潮》3.8版本前瞻通讯 \| 回顾影像                                                                                 | 标题     | 无 `<em>`                                          |
| postTime        | string | 2026-11-06 19:40                                                                                                    | 发布时间 | `yyyy-MM-dd HH:mm`，UTC+8；比 `createTimestamp` 准 |
| createTimestamp | string | 1793954419000                                                                                                       | 创建时间 | 同搜索结果                                         |
| postContent     | list   | []                                                                                                                  | 正文块   | [内容块](#内容块)                                  |
| coverImages     | list   | [{"url": "https://prod-alicdn-community.kurobbs.com/forum/….jpg", "imgWidth": 1920, "imgHeight": 1080, "index": 0}] | 封面图   |                                                    |
| postUserId      | string | 10012001                                                                                                            | 发帖人   |                                                    |

#### comment

| 字段           | 类型   | 示例值              | 说明     | 备注                       |
| -------------- | ------ | ------------------- | -------- | -------------------------- |
| commentId      | string | 1568353184993259520 | 评论 ID  |                            |
| userId         | string | 10012001            | 评论人   |                            |
| isPublisher    | int    | 1                   | 是否楼主 |                            |
| commentContent | list   | []                  | 评论内容 | [内容块](#内容块)          |
| commentTime    | string | 11-06               | 评论时间 | 显示用文字，不带年份和时刻 |
| floor          | int    | 41                  | 楼层     |                            |

#### 内容块

`postContent` 与 `commentContent` 都是内容块列表：

| 字段                 | 类型   | 说明       | 备注                                                              |
| -------------------- | ------ | ---------- | ----------------------------------------------------------------- |
| contentType          | int    | 块类型     | `1` 文本，`2` 图片                                                |
| content              | string | 文本       | 文本块才有                                                        |
| url                  | string | 图片地址   | 图片块才有                                                        |
| imgWidth / imgHeight | int    | 图片尺寸   | 文本块为 `0`                                                      |
| children             | list   | 富文本片段 | 文本块的分段（`type`、`content`、`target`），拼起来等于 `content` |

把所有文本块的 `content` 按行拼接即为纯文本。

### 兑换码格式

「回顾影像」帖楼主评论的文本块，样本：

```text
《鸣潮》3.8版本前瞻通讯兑换码：【TRAINOFSOULS】、【BLOOMINGFLOWERS】、【SCHOOLLIFE】请查收，漂泊者们可前往游戏内兑换领取，兑换码有效期至2026年11月8日23:59。
```

- 兑换码：`【…】` 包住的 6–24 位大写字母数字（至少含一个字母）；没有括号时，取「兑换码」之后单独出现的 8–24 位大写字母数字。
- 有效期：「有效期至 / 失效 / 截止」附近的 `yyyy年M月d日HH:mm`（UTC+8），年份可能省略，按发布时间推断。
- 兑换码有效期通常只有直播后两天左右，过期后帖子仍在。

### 示例

#### 请求

```http
POST /forum/getPostDetail HTTP/1.1
Host: api.kurobbs.com
source: android
devCode: any
version: 2.2.0
Content-Type: application/x-www-form-urlencoded

postId=1568304036084461568&showOrderType=2&isOnlyPublisher=1&pageIndex=1&pageSize=20
```

#### 响应（节选）

```json
{
  "code": 200,
  "msg": "请求成功",
  "success": true,
  "data": {
    "gameId": 3,
    "postDetail": {
      "postTitle": "《鸣潮》3.8版本前瞻通讯 | 回顾影像",
      "postTime": "2026-11-06 19:40",
      "createTimestamp": "1793954419000",
      "postContent": [
        {
          "contentType": 1,
          "content": "《鸣潮》3.8版本前瞻通讯完整回顾影像已送达，欢迎漂泊者前往观看。",
          "imgWidth": 0,
          "imgHeight": 0
        },
        {
          "contentType": 1,
          "content": "《鸣潮》3.8版本「旧梦凋零的百合」将于11月12日正式开启！",
          "imgWidth": 0,
          "imgHeight": 0
        }
      ],
      "coverImages": [
        {
          "index": 0,
          "imgWidth": 1920,
          "imgHeight": 1080,
          "url": "https://prod-alicdn-community.kurobbs.com/forum/52e523471c6440f18a3a9c1435c9bd3020261106.jpg"
        }
      ]
    },
    "comment": [
      {
        "userId": "10012001",
        "isPublisher": 1,
        "commentContent": [
          {
            "contentType": 2,
            "imgWidth": 3840,
            "imgHeight": 2160,
            "url": "https://prod-alicdn-community.kurobbs.com/forum/9ffa6003ac564d4a8697826a760b72df20261106.jpeg"
          },
          {
            "contentType": 1,
            "content": "《鸣潮》3.8版本前瞻通讯兑换码：【TRAINOFSOULS】、【BLOOMINGFLOWERS】、【SCHOOLLIFE】请查收，漂泊者们可前往游戏内兑换领取，兑换码有效期至2026年11月8日23:59。",
            "imgWidth": 0,
            "imgHeight": 0
          }
        ]
      }
    ]
  }
}
```

