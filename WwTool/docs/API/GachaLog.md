## 抽卡记录

### 请求地址

`https://gmserver-api.aki-game2.net/gacha/record/query`

---

### 请求方式

`POST`

### 认证方式

`recordId`，需在游戏内打开一次抽卡记录后，在游戏根目录下的`Client\Saved\Logs\Client.log`中查找

#### 请求头(最简)

| 字段         | 类型   | 示例值                                       | 说明       | 备注 |
| ------------ | ------ | -------------------------------------------- | ---------- | ---- |
| Content-Type | string | application/json            | 请求体类型  |      |

#### 请求体

`json`

| 字段           | 类型     | 示例值                             | 说明                     | 备注                       |
| -------------- | -------- | ---------------------------------- | ------------------------ | -------------------------- |
| playerId     | string | 11111111                         | 玩家ID                   |                            |
| cardPoolId   | string | 111111111                        | 卡池ID？？不知道什么作用 | 可不加                     |
| cardPoolType | int    | 1                                | 卡池类型                 | [详情](#卡池类型)          |
| serverId     | string | 86d52186155b148b5c138ceb41be9650 | 服务器ID                 | [详情](WW_API.md#服务器id) |
| languageCode | string | zh-Hans                          | 语言                     |
| recordId     | string | 1111111111111                    | 抽卡记录ID               | 每个账号唯一               |


---

##### 卡池类型

| 卡池ID | 卡池类型       |
| ------ | -------------- |
| 1    | 角色活动唤取 |
| 2    | 武器活动唤取 |
| 3    | 角色常驻唤取 |
| 4    | 武器常驻唤取 |
| 5    | 新手唤取     |
| 6    | 新手自选唤取 |
| 7    | 角色新旅唤取 |
| 8    | 武器新旅唤取 |

---

### 响应
`json`
|字段|类型|示例值|说明|备注|
|---|---|---|---|---|
|code|int|0|状态码|成功: `0` <br> 失败: `-1`|
|message|string|success|状态信息|成功: `success` <br> 失败: `请求游戏获取日志异常!`|
|data|list|[]|抽卡记录列表|状态码为`0`，但返回空列表时代表六个月内没有此卡池的记录|

`data`

| 字段           | 类型     | 示例值                | 说明     | 备注                                                                      |
| -------------- | -------- | --------------------- | -------- | ------------------------------------------------------------------------- |
| cardPoolType | string | 角色精准调谐        | 卡池类型 |
| resourceId   | int    | 21010023            | 资源 ID  | [武器资源](./Resource/Weapons.md)<br>[角色资源](./Resource/Characters.md) |
| qualityLevel | int    | 3                   | 品质     | [详情](#品质)                                                             |
| resourceType | string | 武器                | 资源类型 |
| name         | string | 源能长刃·测壹       | 资源名称 |
| count        | int    | 1                   | 数量     |
| time         | string | 2026-05-15 13:24:03 | 获取时间 |

---

##### 品质

| 品质 | 说明         |
| ---- | ------------ |
| 3  | 三星（蓝） |
| 4  | 四星（紫） |
| 5  | 五星（金） |

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
