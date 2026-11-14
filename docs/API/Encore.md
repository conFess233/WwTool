## encore.moe（逆境深塔 / 冥歌海墟 / 终焉矩阵 / 角色详情）

[encore.moe](https://encore.moe) 是第三方的鸣潮资料站，数据来自游戏解包。本文记录两部分：活动页要用的三个玩法（逆境深塔、冥歌海墟、终焉矩阵），每个玩法有一个列出各期、一个返回某一期详情的接口；以及角色详情弹窗用的[角色详情](#角色详情)接口。

- 官方文档：OpenAPI 3 规范，`https://api-v2.encore.moe/openapi.json`（`info.version` 为 `2.0.0`）
- 公开接口，`GET` 即可，无需登录
- 第三方站点，不是库洛官方数据；字段命名直接沿用游戏配置表，同一份响应里大小写风格并不统一（见各节）

### 请求地址

| 服务器 | 基址                            | 说明                                       |
| ------ | ------------------------------- | ------------------------------------------ |
| 主     | `https://api-v2.encore.moe/api` | 网站默认使用                               |
| 备用   | `https://api.encore.moe`        | 旧后端，路径**没有** `/api` 前缀，内容相同 |

`GET https://api-v2.encore.moe/api` 会返回服务器列表和支持的语言：

```json
{
  "apiList": [
    { "url": "https://api.encore.moe", "P": 2 },
    { "url": "https://api-v2.encore.moe", "P": 1 }
  ],
  "language": [
    "en",
    "zh-Hans",
    "zh-Hant",
    "ja",
    "ko",
    "de",
    "es",
    "fr",
    "id",
    "pt",
    "ru",
    "th",
    "vi"
  ]
}
```

网站在主服务器返回 5xx 时自动改用备用服务器，客户端可以照做。

| 接口                  | 路径                       | 示例                                               |
| --------------------- | -------------------------- | -------------------------------------------------- |
| [深塔各期](#深塔各期) | `/{lang}/toa`              | `https://api-v2.encore.moe/api/zh-Hans/toa`        |
| [深塔详情](#深塔详情) | `/{lang}/toa/{seasonId}`   | `https://api-v2.encore.moe/api/zh-Hans/toa/42`     |
| [海墟各期](#海墟各期) | `/{lang}/whiwa`            | `https://api-v2.encore.moe/api/zh-Hans/whiwa`      |
| [海墟详情](#海墟详情) | `/{lang}/whiwa/{seasonId}` | `https://api-v2.encore.moe/api/zh-Hans/whiwa/23`   |
| [矩阵各期](#矩阵各期) | `/{lang}/dpmatrix`         | `https://api-v2.encore.moe/api/zh-Hans/dpmatrix`   |
| [矩阵详情](#矩阵详情) | `/{lang}/dpmatrix/{id}`    | `https://api-v2.encore.moe/api/zh-Hans/dpmatrix/9` |
| [角色详情](#角色详情) | `/{lang}/character/{id}`   | `https://api-v2.encore.moe/api/zh-Hans/character/1413` |

#### 路径参数

| 参数       | 说明 | 备注                                                                                      |
| ---------- | ---- | ----------------------------------------------------------------------------------------- |
| `lang`     | 语言 | 上面 13 种之一，写法与[游戏语言](WW_API.md#语言)相同（`zh-Hans`、`en`、`ja` …）           |
| `seasonId` | 期数 | 深塔、海墟用，取各期列表里的 `id` / `Season`                                              |
| `id`       | 期数 | 矩阵用，取各期列表里的 `Season`（不是 `SeasonId`，也不是 `LevelIds`）；填关卡 ID 返回 500 |
| `id`       | 角色 ID | 角色详情用，与游戏资料、抽卡记录里的角色 ID 相同（如 `1413` 清宵）                        |

网站请求时会在末尾加 `?v={数据版本}`，用来看测试服数据；正式数据不加，客户端也不用加。

### 请求方式

`GET`

### 认证方式

无。不需要特殊请求头。

#### 缓存

| 响应头                               | 值                                | 说明                                                    |
| ------------------------------------ | --------------------------------- | ------------------------------------------------------- |
| Content-Type                         | `application/json; charset=utf-8` |                                                         |
| cf-cache-status                      | `DYNAMIC`                         | 经 Cloudflare，但每次都回源                             |
| ETag / Last-Modified / Cache-Control | 无                                | 带 `If-Modified-Since` 也返回 `200`，**不支持条件请求** |

所以客户端要自己判断数据是否过期：

- 各期列表很小（1.6 ~ 4.3 KB），可以每次打开都请求一次。
- 详情较大，见下表。带 `Accept-Encoding: gzip` 后小很多，比如海墟第 23 期从 932 KB 降到约 26 KB。
- 某一期的详情在那一期开始后基本不变，但 encore.moe 在新版本解包后可能修正数据。建议按「期数 + 抓取时间」缓存，过一段时间重新请求。

| 接口         | 原始大小（zh-Hans） |
| ------------ | ------------------- |
| `toa/42`     | 约 239 KB           |
| `whiwa/23`   | 约 932 KB           |
| `dpmatrix/9` | 约 89 KB            |
| `character/1413` | 约 410 KB       |

#### 错误

| 情况                  | 状态码 | 响应                                                                                                |
| --------------------- | ------ | --------------------------------------------------------------------------------------------------- |
| 矩阵 `id` 不存在      | 500    | `{"error":true,"url":"…","statusCode":500,"statusMessage":"Server Error","message":"Server Error"}` |
| 备用服务器加了 `/api` | 404    |                                                                                                     |

### 通用约定

- 响应直接是业务数据，**没有** `code` / `message` 外层。
- 图片字段是完整 URL，指向 `https://api-v2.encore.moe/resource/Data/Game/Aki/UI/UIResources/…/*.webp`。网站可切换为 `.png`，把扩展名换掉即可。
- 文本可能含游戏富文本：`<br>` 换行、`<span style="color:#ffd12f;">…</span>` 着色，有时带 `class="font-bold"`。
- 各期列表里的 `start` / `finish` 是**只有日期**的字符串 `yyyy-MM-dd`。实际在当天服务器时间 04:00 刷新（国服为 UTC+8），与活动页时间线「循环」组用的规则一致。
- `current` 由服务器按当天日期算出，所以在刷新日 00:00 ~ 04:00 之间可能与游戏内差一期。客户端最好按 `start` 加 04:00 自己判断。
- 列表里会提前出现**还没开始的下一期**（解包得到，标 `current: false`），它的详情也能请求到，但正式上线前可能有改动。

#### 元素 ID

`elements[].id`、`ElementId` 等字段共用这套 ID：

| ID  | 属性 |
| --- | ---- |
| 0   | 物理 |
| 1   | 冷凝 |
| 2   | 热熔 |
| 3   | 导电 |
| 4   | 气动 |
| 5   | 衍射 |
| 6   | 湮灭 |

---

## 逆境深塔

### 深塔各期

`GET /{lang}/toa`

```json
{
  "seasons": [
    { "id": 0, "name": "Standard", "areas": 3 },
    {
      "id": 42,
      "name": "Season 42",
      "areas": 3,
      "start": "2026-11-09",
      "finish": "2026-12-07",
      "current": true
    },
    {
      "id": 43,
      "name": "Season 43",
      "areas": 3,
      "start": "2026-12-07",
      "finish": "2027-01-04",
      "current": false
    }
  ]
}
```

| 字段      | 类型   | 说明                                     |
| --------- | ------ | ---------------------------------------- |
| `id`      | int    | 期数；`0` 是常驻的「稳定」难度，没有日期 |
| `name`    | string | `Season N`，**不随语言变化**             |
| `areas`   | int    | 区域数，目前都是 3                       |
| `start`   | string | 开始日期                                 |
| `finish`  | string | 结束日期，等于下一期的 `start`           |
| `current` | bool   | 是否为当前期                             |

早期每期 14 天（第 1 ~ 18 期），之后每期 28 天。第 42 期从 2026-11-09 开始。

### 深塔详情

`GET /{lang}/toa/{seasonId}`

响应是四层嵌套的对象，键都是字符串形式的数字：

```text
{ "{seasonId}": { "{areaNum}": { "{floor}": { "{cost}": [ 层 ] } } } }
```

- `areaNum`：1 残响之塔、2 深境之塔、3 回音之塔（名字取 `areaName`，会随语言变化）。
- `floor`：1 ~ 4 层。
- `cost`：这层每位角色消耗的疲劳值。残响之塔和回音之塔是 1 ~ 4，深境之塔每层都是 5。
- 最内层的数组目前都只有 1 个元素。

#### 层

| 字段         | 类型   | 说明                      |
| ------------ | ------ | ------------------------- |
| `id`         | int    | 层的配置 ID               |
| `instanceId` | int    | 副本 ID                   |
| `season`     | int    | 期数                      |
| `areaNum`    | int    | 区域                      |
| `floor`      | int    | 层数                      |
| `cost`       | int    | 疲劳消耗                  |
| `difficulty` | int    | 难度，当期为 3            |
| `areaName`   | string | 区域名，如 `深境之塔`     |
| `monsters`   | list   | [敌人](#深塔敌人)         |
| `buffs`      | list   | 本层增益 / 敌方强化，见下 |
| `targets`    | list   | 星级条件，见下            |
| `bgPath`     | string | 区域背景图                |
| `itemBgPath` | string | 区域卡片图                |

`buffs[]`：

| 字段   | 类型   | 说明               |
| ------ | ------ | ------------------ |
| `id`   | int    |                    |
| `name` | string | 目前都是空字符串   |
| `desc` | string | 效果说明（纯文本） |
| `icon` | string | 图标               |

`targets[]`（3 项，对应 1 ~ 3 星）：

| 字段      | 类型   | 说明                                                 |
| --------- | ------ | ---------------------------------------------------- |
| `id`      | int    |                                                      |
| `score`   | int    | 达成后的分数                                         |
| `desc`    | string | 带富文本的说明，如 `通关剩余时间<span …>60</span>秒` |
| `rawDesc` | string | 模板，如 `通关剩余时间{0}秒`                         |
| `params`  | list   | 模板参数，如 `[60]`                                  |

#### 深塔敌人

| 字段              | 类型   | 说明                                                |
| ----------------- | ------ | --------------------------------------------------- |
| `id`              | int    | 敌人 ID                                             |
| `name`            | string | 名称                                                |
| `rarity`          | object | `{id, desc}`，如 `{"id":2,"desc":"巨浪级(COST 3)"}` |
| `icon`            | string | 头像                                                |
| `elements`        | list   | `{id, name, icon}`，[元素 ID](#元素-id)             |
| `whiteGreenProps` | list   | 属性列表，见下                                      |

`whiteGreenProps[]` 的每项是 `{attributeType, key, name, value, isPercent, icon}`，常用的 `key`：

| key                                                     | 说明                           |
| ------------------------------------------------------- | ------------------------------ |
| `Lv`                                                    | 等级                           |
| `LifeMax`                                               | 生命                           |
| `Atk` / `Def`                                           | 攻击 / 防御                    |
| `DamageResistancePhys`                                  | 物理抗性（`isPercent: true`）  |
| `DamageResistanceElement1` ~ `DamageResistanceElement6` | 冷凝 ~ 湮灭抗性，顺序同元素 ID |

其余项（`name` 以 `dnt/` 开头的）是内部数值，不用展示。

---

## 冥歌海墟

### 海墟各期

`GET /{lang}/whiwa`

响应直接是数组，字段名首字母大写，和深塔不一样：

```json
[
  { "Season": 0, "Name": "Season 0" },
  {
    "Season": 23,
    "Name": "Season 23",
    "start": "2026-10-26",
    "finish": "2026-11-23",
    "current": true
  },
  {
    "Season": 24,
    "Name": "Season 24",
    "start": "2026-11-23",
    "finish": "2026-12-21",
    "current": false
  }
]
```

| 字段      | 类型   | 说明                     |
| --------- | ------ | ------------------------ |
| `Season`  | int    | 期数；`0` 没有日期       |
| `Name`    | string | `Season N`，不随语言变化 |
| `start`   | string | 开始日期                 |
| `finish`  | string | 结束日期                 |
| `current` | bool   | 是否为当前期             |

一般每期 28 天，第 23 期从 2026-10-26 开始（与 3.8 官方日历一致）。个别相邻两期是 27 天和 29 天（如第 2 期、第 15 期为 27 天，第 9 期、第 22 期为 29 天），因此第 16 ~ 22 期的日期比从 10-26 按 28 天倒推的早一天（如第 16 期写 2026-04-12，倒推是 04-13）。无法确认哪边对，只能说 encore.moe 的历史日期可能差一天，当前期与官方一致。

### 海墟详情

`GET /{lang}/whiwa/{seasonId}`

| 字段          | 类型   | 说明                                             |
| ------------- | ------ | ------------------------------------------------ |
| `season`      | int    | 期数（这里又是小写）                             |
| `name`        | string | `Season N`                                       |
| `stageGroups` | list   | 区域分组，每组带自己的 `levels`                  |
| `levels`      | list   | 全部关卡，**与 `stageGroups[].levels` 内容重复** |
| `buffItems`   | list   | 本期可选的信物，见下                             |

响应大，主要是因为关卡在两处各出现一次，而且每个敌人都带完整的属性表。只用 `stageGroups` 就够了。

`stageGroups[]`：

| 字段       | 类型   | 说明                                |
| ---------- | ------ | ----------------------------------- |
| `id`       | int    |                                     |
| `name`     | string | 如 `再生海域-海隙`、`再生海域-湍渊` |
| `outIndex` | int    | 排序                                |
| `levels`   | list   | [关卡](#海墟关卡)                   |

第 23 期有两组：「海隙」5 关（险滩、涡流、急潮、狂澜、海魇），「湍渊」1 关（无尽湍渊，`endLess: true`）。

#### 海墟关卡

| 字段              | 类型   | 说明                                                           |
| ----------------- | ------ | -------------------------------------------------------------- |
| `id`              | int    | 关卡 ID                                                        |
| `season`          | int    | 期数                                                           |
| `title`           | string | 关卡名                                                         |
| `desc`            | string | 本关机制说明，**带富文本**（`<br>`、`<span style="color:…">`） |
| `targetScore`     | list   | 评级分数线，如 `[1500, 2000, 2500]`                            |
| `scoreStage`      | list   | 对应的评级，如 `["B", "A", "S"]`                               |
| `passScore`       | int    | 通关分                                                         |
| `orderIndex`      | int    | 排序                                                           |
| `endLess`         | bool   | 是否为无尽关                                                   |
| `instIds`         | list   | 副本 ID，与 `stages` 一一对应                                  |
| `preLevel`        | list   | 前置关卡                                                       |
| `levelPassReward` | int    | 通关奖励 ID                                                    |
| `stages`          | list   | 每关 2 段，见下                                                |

`stages[]`：

| 字段          | 类型   | 说明                                                                             |
| ------------- | ------ | -------------------------------------------------------------------------------- |
| `instId`      | int    | 副本 ID                                                                          |
| `stageInfoId` | int    |                                                                                  |
| `dungeonDesc` | string | 这一段的附加规则，带富文本，如「进入挑战时，协奏能量回复至100%」                 |
| `monsters`    | list   | 敌人：`{id, name, icon, element, elements, whiteGreenProps}`，`element` 是主属性 |
| `buffs`       | list   | 目前为空                                                                         |

海墟敌人的 `elements[]` 比深塔多一个 `color` 字段（如 `7892A1FF`，RGBA）。

`buffItems[]`（信物）：

| 字段         | 类型   | 说明                                                                    |
| ------------ | ------ | ----------------------------------------------------------------------- |
| `id`         | int    |                                                                         |
| `itemId`     | int    | 道具 ID                                                                 |
| `orderIndex` | int    | 排序                                                                    |
| `buffIds`    | list   | 增益 ID（字符串）                                                       |
| `item`       | object | `{name, icon, iconMiddle, iconSmall, qualityId}`，如「狂欢者—船长印章」 |

信物只有名字和图标，**没有效果说明**。

---

## 终焉矩阵

接口路径叫 `dpmatrix`（OpenAPI 里写作 Doubled Pawns Matrix），网站上显示为 Endstate Matrix。

### 矩阵各期

`GET /{lang}/dpmatrix`

```json
[
  {
    "Season": 9,
    "Name": "S2第四阶段",
    "CycleName": "S2第四阶段",
    "SeasonId": 2,
    "SeasonName": "险境强袭",
    "EndVersion": "3.8",
    "LevelIds": [17, 18],
    "LevelNames": ["稳态协议", "奇点扩张"]
  }
]
```

| 字段         | 类型   | 说明                                                           |
| ------------ | ------ | -------------------------------------------------------------- |
| `Season`     | int    | 期数（阶段），**详情接口的 `id` 用它**                         |
| `Name`       | string | 阶段名，如 `S2第四阶段`；随语言变化（en 为 `S1 Phase 1` 这类） |
| `CycleName`  | string | 同 `Name`                                                      |
| `SeasonId`   | int    | 所属赛季；`0` 是赛季制之前的 Cycle 1、2                        |
| `SeasonName` | string | 赛季名，如 `险境强袭`                                          |
| `EndVersion` | string | 赛季结束的版本号，如 `3.8`                                     |
| `LevelIds`   | list   | 两个模式的关卡 ID                                              |
| `LevelNames` | list   | 两个模式的名字：`稳态协议`、`奇点扩张`                         |

**没有开始 / 结束日期，也没有 `current`**，网站只是把列表倒序显示。要判断当前是哪一期，可以：

- 取列表最后一项（实测时为第 9 期「S2第四阶段」，`EndVersion` 3.8，与当前版本一致）。
- 或用 `EndVersion` 对照[游戏内公告](GameNotice.md)里的版本号排除已结束的赛季。

阶段的具体切换日期需要另找来源。

### 矩阵详情

`GET /{lang}/dpmatrix/{id}`

顶层字段与列表项相同（`Season`、`Name`、`SeasonId`、`SeasonName`、`EndVersion`、`LevelIds`），另外有：

| 字段     | 类型 | 说明                                |
| -------- | ---- | ----------------------------------- |
| `Levels` | list | 两个模式，[见下](#矩阵模式)         |
| `Roles`  | list | 本期加成角色，[见下](#矩阵加成角色) |

#### 矩阵模式

| 字段                | 类型   | 说明                                                                    |
| ------------------- | ------ | ----------------------------------------------------------------------- |
| `Id`                | int    | 关卡 ID                                                                 |
| `Name`              | string | `稳态协议` 或 `奇点扩张`                                                |
| `Diff`              | int    | `0` 稳态协议，`1` 奇点扩张                                              |
| `TeamLimit`         | int    | 队伍数上限；稳态协议 3，奇点扩张 0（不限）                              |
| `MaxLoopCount`      | int    | 奇点扩张为 99                                                           |
| `NewTowerBuffs`     | list   | 可选增益 `{Id, Icon, Name, Desc}`，如「通用强化」，`Desc` 带 `<br>`     |
| `NewTowerBuffCount` | int    | 可选数量                                                                |
| `ScoreLevelRule`    | list   | 评级分数线 `{Key, Value, Icon, …}`；第 9 期为 `0 / 4800 / 7200 / 10000` |
| `Waves`             | list   | 敌人波次，见下                                                          |

`Waves[]`：稳态协议 4 波；奇点扩张 3 轮 × 5 波，每轮敌人等级 +10（100 / 110 / 120）。

| 字段                   | 类型   | 说明                                                |
| ---------------------- | ------ | --------------------------------------------------- |
| `Round` / `Wave`       | int    | 轮次 / 波次                                         |
| `Name`                 | string | 敌人名，如 `飞廉之猩`                               |
| `MonsterId`            | int    |                                                     |
| `MonsterLevel`         | int    | 等级                                                |
| `KillScore`            | int    | 击杀得分，「矩阵奇藏」为 1000，其余 0               |
| `ElementId`            | int    | [元素 ID](#元素-id)                                 |
| `Icon`                 | string | 头像                                                |
| `Tags`                 | list   | `{Id, Name, Desc, Path, Color}`，如 `气动抗性`      |
| `ShowBuffIds`          | list   | 敌人机制：`SkillTitle`、`SkillDesc`、`SkillIcon` 等 |
| `RecommendTeamFeature` | list   | 推荐配队 `{Id, Icon, Name}`，如 `谐度破坏体系`      |
| `MonsterPortrait`      | string | 游戏内资源路径（**不是** URL）                      |

字段名里的拼写照抄原数据，如 `DamegaRate`。

#### 矩阵加成角色

| 字段               | 类型   | 说明                                                                            |
| ------------------ | ------ | ------------------------------------------------------------------------------- |
| `Id`               | int    | 角色 ID                                                                         |
| `RoleInfo`         | object | `{Name, QualityId, ElementId, WeaponType, RoleHeadIcon, RoleHeadIconCircle, …}` |
| `EnhanceSkillDesc` | list   | `[{Key, Value}]`，`Value` 是加成说明，带富文本                                  |
| `AddBuffs`         | list   | 内部增益 ID                                                                     |

第 9 期有 13 名加成角色，例如：

- 洛瑟菈：「角色拥有额外疲劳值（限S2第4阶段）」
- 今汐：「角色最终伤害提升25%」

---

## 角色详情

`GET /{lang}/character/{id}`

返回一名角色的全部解包资料，约 410 KB，其中大半是 3D 模型和动作列表（`Model3D`、`Model3DEquips`），工具箱不用。角色资料在版本更新时才会变，可以长期缓存：工具箱打开详情时先显示缓存，缓存超过 12 小时才在后台刷新（每次刷新要下载、比较、写入整份数据，会产生十几 MB 的临时内存）。

很多文字字段是 `{Title, Content}` 对象，取 `Content`；`Title` 多为占位文字 `No Description Available`。

### 基本资料

| 字段                                    | 类型   | 说明                                                                       |
| --------------------------------------- | ------ | -------------------------------------------------------------------------- |
| `Id`                                    | int    | 角色 ID                                                                    |
| `Name` / `NickName` / `Introduction`    | object | 名称、昵称、简介（取 `Content`，简介含 `
`）                              |
| `QualityId`                             | int    | 星级（4 / 5）                                                              |
| `ElementId` / `ElementName`             | int / string | [元素 ID](#元素-id) 与名称                                           |
| `WeaponTypeName`                        | string | 武器类型，如 `迅刀`                                                        |
| `Tags`                                  | list   | 定位标签 `{TagName, TagDesc, TagNameColor, TagIcon}`，颜色为 `RRGGBB`       |
| `FormationRoleCard`                     | string | 编队立绘（半身像，带透明背景的 webp，约 700×960），网站默认显示这张        |
| `RolePortrait` / `Card` / `RoleHeadIcon*` | string | 活动立绘、方形头像等                                                     |
| `Skins`                                 | list   | 皮肤 `{Id, Name, FormationRoleCard, PreviewRoleCard, …}`；网站取 `Id` 最小的作为默认 |

### 档案 `favorRole`

`Birthday`、`Sex`、`Country`（出身）、`Influence`（所属）、`CVNameCn` / `CVNameJp` / `CVNameEn` / `CVNameKo`（四语声优），都是 `{Title, Content}`。另有 `Info`、`TalentName`、`TalentDoc`、`TalentCertification` 等长文本。

### 基础属性 `Properties`

`[{Name, Icon, BaseValue, GrowthValues}]`，`GrowthValues` 为 `[{growthId, level, value}]`：

- 每个等级一项，突破等级（20、40、50、60、70、80）出现两次，后一项是突破后的值。
- `value` 多数是数字（可带小数，如攻击 `462.5`，游戏面板显示时截去小数），暴击等为文字，如 `"5%"`。
- `Icon` 是虚幻引擎对象路径（`…/T_Icon.T_Icon_UI`），没有对应的图片文件。

### 技能 `Skills`

10 项左右，顺序为常态攻击、共鸣技能、共鸣解放、固有技能……

| 字段              | 类型   | 说明                                                                      |
| ----------------- | ------ | ------------------------------------------------------------------------- |
| `SkillType`       | string | 常态攻击、共鸣技能、共鸣解放、共鸣回路、变奏技能、延奏技能、谐度破坏、固有技能 |
| `SkillName`       | string | 技能名                                                                    |
| `SkillDescribe`   | string | 富文本描述（数值已填好，没有 `{0}` 占位符）                               |
| `Icon`            | string | 技能图标 URL                                                              |
| `SkillMedia`      | string | 技能演示视频（mp4）                                                       |
| `SkillAttributes` | list   | 倍率表 `{attributeId, attributeName, values}`，`values` 为 1–20 级的文字，如 `"15.16%*2"`；玩家可升到 10 级 |
| `SkillTree` / `Consumes` / `DamageList` | list | 技能树节点、升级材料、伤害明细，工具箱不用              |

### 共鸣链 `ResonantChain`

6 项 `{GroupIndex, NodeName, AttributesDescription, NodeIcon, …}`。`GroupIndex` 为 1–6；`AttributesDescription` 是数值已填好的富文本；`NodeIcon` 是以 `/Game/` 开头的路径，拼在 `https://api-v2.encore.moe/resource/Data` 后面即为图片地址。

### 故事与语音

| 字段      | 说明                                                                                                          |
| --------- | ------------------------------------------------------------------------------------------------------------- |
| `Stories` | 角色故事 `{Sort, Title, Content}`，`Content` 含 `<br>` 和游戏内链接标签 `<te href=…>`（只取文字）              |
| `Words`   | 语音 `{Sort, Title, Content, VoiceZh, VoiceJa, VoiceEn, VoiceKo}`；`Content` 是台词，`Voice*` 是 mp3 地址       |

### 资源路径

以 `/Game/` 开头的字段（`NodeIcon`、`Tags[].TagIcon`、`FormationSpineAtlas` 等）不是完整 URL。网站的处理：拼到 `https://api-v2.encore.moe/resource/Data` 后面，并把路径里的 `UiResources` 改成 `UIResources`。只有带图片扩展名（`.webp`、`.png`）的才是文件。
