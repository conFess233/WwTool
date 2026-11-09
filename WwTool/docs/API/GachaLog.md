## 抽卡记录

### 请求地址

| 区服   | 地址                                                    |
| ------ | ------------------------------------------------------- |
| 国服   | `https://gmserver-api.aki-game2.com/gacha/record/query` |
| 国际服 | `https://gmserver-api.aki-game2.net/gacha/record/query` |

---

### 请求方式

`POST`

### 认证方式

`recordId`，需在游戏内打开一次抽卡记录后，在游戏根目录下的`Client\Saved\Logs\Client.log`中查找。

日志里的抽卡记录链接形如 `...?svr_id=<serverId>&player_id=<playerId>&lang=<languageCode>&record_id=<recordId>&gacha_type=<cardPoolType>...`（参数可能位于 `#` 之后）。

> `Client.log` 只保留**最后一次**在游戏内打开抽卡记录的账号的链接。多账号时必须校验 `player_id` 与要写入的账号 UID 一致，否则会把另一个账号的记录写进当前账号。

#### 请求头(最简)

| 字段         | 类型   | 示例值                         | 说明       | 备注 |
| ------------ | ------ | ------------------------------ | ---------- | ---- |
| Content-Type | string | application/json;charset=UTF-8 | 请求体类型 |      |

#### 请求体

`json`

| 字段         | 类型         | 示例值                           | 说明        | 备注                                                            |
| ------------ | ------------ | -------------------------------- | ----------- | --------------------------------------------------------------- |
| playerId     | string       | 11111111                         | 玩家 UID    | 即链接中的 `player_id`                                          |
| cardPoolId   | string       | 111111111                        | 卡池 ID     | 可不加                                                          |
| cardPoolType | string / int | 1                                | 卡池类型    | [详情](#卡池类型)；字符串或整数都被接受                         |
| serverId     | string       | 86d52186155b148b5c138ceb41be9650 | 服务器 ID   | [详情](WW_API.md#服务器id)，可按 [UID 推断](#按-uid-推断服务器) |
| languageCode | string       | zh-Hans                          | 语言        | 影响响应中 `name`、`cardPoolType`、`resourceType` 的文字        |
| recordId     | string       | 1111111111111                    | 抽卡记录 ID | 每个账号唯一；过期后返回 `code: -1`                             |

---

##### 卡池类型

| 卡池类型 | 名称                            | 本项目枚举               |
| -------- | ------------------------------- | ------------------------ |
| 1        | 角色活动唤取（角色精准调谐）    | `CharacterEvent`         |
| 2        | 武器活动唤取（武器精准调谐）    | `WeaponEvent`            |
| 3        | 角色常驻唤取（角色调谐·常驻池） | `CharacterStandard`      |
| 4        | 武器常驻唤取（武器调谐·常驻池） | `WeaponStandard`         |
| 5        | 新手唤取（新手调谐）            | `Beginner`               |
| 6        | 新手自选唤取                    | `BeginnerChoice`         |
| 7        | 新手自选唤取（感恩定向唤取）    | `BeginnerChoiceGiveback` |
| 8        | 角色新旅唤取                    | `CharacterNoviceJourney` |
| 9        | 武器新旅唤取                    | `WeaponNoviceJourney`    |
| 10       | 角色联动唤取                    | `CharacterCollaboration` |
| 11       | 武器联动唤取                    | `WeaponCollaboration`    |
| 12       | 角色忆旅唤取                    | `CharacterMemoryJourney` |
| 13       | 武器忆旅唤取                    | `WeaponMemoryJourney`    |

> 旧版文档把 7、8 写成「角色新旅 / 武器新旅」，是错误的：新旅为 8、9，7 是感恩定向唤取。

##### 按 UID 推断服务器

国际服按 UID 的最高位（`UID / 100000000`）对应服务器：

| 最高位 | 服务器       | serverId                         |
| ------ | ------------ | -------------------------------- |
| 5      | 美服 America | 591d6af3a3090d8ea00d8f86cf6d7501 |
| 6      | 欧服 Europe  | 6eb2a235b30d05efd77bedb5cf60999e |
| 7      | 亚服 Asia    | 86d52186155b148b5c138ceb41be9650 |
| 8      | 港澳台 HMT   | 919752ae5ea09c1ced910dd668a63ffb |
| 9      | 东南亚 SEA   | 10cd7254d57e58ae560b15d51e34b4c8 |

国服 serverId 为 `76402e5b20be2c39f095a152090afddc`。链接中的 `svr_id` 优先于推断结果。

---

### 响应

`json`
|字段|类型|示例值|说明|备注|
|---|---|---|---|---|
|code|int|0|状态码|成功: `0` <br> 失败: `-1`（链接无效或 `recordId` 过期）|
|message|string|success|状态信息|成功: `success` <br> 失败: `请求游戏获取日志异常!`|
|data|list|[]|抽卡记录列表|状态码为`0`，但返回空列表时代表六个月内没有此卡池的记录|

`data`

| 字段         | 类型   | 示例值              | 说明     | 备注                                                                                                             |
| ------------ | ------ | ------------------- | -------- | ---------------------------------------------------------------------------------------------------------------- |
| cardPoolType | string | 角色精准调谐        | 卡池名称 | 本地化文字，**不是**请求里的数字；历史记录可能出现旧名（如「角色精准调谐-2」「全频调谐」），以请求的卡池类型为准 |
| resourceId   | int    | 21010023            | 资源 ID  | [武器资源](./Resource/Weapons.md)<br>[角色资源](./Resource/Characters.md)                                        |
| qualityLevel | int    | 3                   | 品质     | [详情](#品质)                                                                                                    |
| resourceType | string | 武器                | 资源类型 | 本地化文字；不同数据源可能标记不同，去重时不应依赖                                                               |
| name         | string | 源能长刃·测壹       | 资源名称 | 本地化文字                                                                                                       |
| count        | int    | 1                   | 数量     |
| time         | string | 2026-05-15 13:24:03 | 获取时间 | 精确到秒，不带时区                                                                                               |

#### 行为说明

- **不分页**：一次请求返回该卡池近 6 个月的全部记录。
- **顺序**：从新到旧排列。同一次十连的 10 条记录 `time` 完全相同，它们在数组中的相对位置就是实际出货顺序，必须原样保留。
- **没有记录 ID、没有游标**：去重只能依靠记录内容加上同一秒内的出现序号；合法的完全相同记录（同一次十连里出了两把相同三星武器）必须保留。
- **6 个月以前的记录**不再返回，只能依靠本地保存；首次导入时最早一个五星之前的抽数和大小保底状态未知。
- **保底上限**：五星 80 抽硬保底。
- 连续请求多个卡池时，参考实现每个卡池之间间隔约 1 秒。

---

##### 品质

| 品质 | 说明       |
| ---- | ---------- |
| 3    | 三星（蓝） |
| 4    | 四星（紫） |
| 5    | 五星（金） |

### 示例

#### 请求

`json`

```json
{
  "playerId": "12345678",
  "languageCode": "zh-Hans",
  "cardPoolType": 1,
  "recordId": "dasdasj312313j123jk12jlk312jk3kj",
  "serverId": "86d52186155b148b5c138ceb41be9650"
}
```

#### 响应

`json`

```json
{
  "code": 0,
  "message": "success",
  "data": [
    {
      "cardPoolType": "角色精准调谐",
      "resourceId": 21030013,
      "qualityLevel": 3,
      "resourceType": "武器",
      "name": "暗夜佩枪·暗星",
      "count": 1,
      "time": "2026-05-02 13:43:56"
    },
    {
      "cardPoolType": "角色精准调谐",
      "resourceId": 1403,
      "qualityLevel": 4,
      "resourceType": "角色",
      "name": "秋水",
      "count": 1,
      "time": "2026-05-02 10:29:01"
    }
  ]
}
```

