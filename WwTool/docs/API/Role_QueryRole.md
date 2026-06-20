## 查询玩家角色详情

该接口用于根据 `oauthCode` 查询指定角色的详细信息（基础信息、摩托数据、活跃度等）。

### 请求地址

`https://pc-launcher-sdk-api.kurogame.net/game/queryRole`

### 请求方式

`POST`

### 认证方式

请求体携带 `oauthCode`

#### 请求头

| 字段         | 类型   | 示例值                          | 说明     | 备注 |
| ------------ | ------ | ------------------------------- | -------- | ---- |
| Content-Type | string | application/json; charset=utf-8 | 数据格式 |      |

#### 请求体

`json`

| 字段      | 类型   | 说明         | 备注              |
| --------- | ------ | ------------ | ----------------- |
| oauthCode | string | 启动器授权码 |                   |
| playerId  | long   | 玩家游戏 UID | 需传入整型 (long) |
| region    | string | 大区标识     |                   |

#### Cookie

| 字段   | 示例值                                               | 备注                                                         |
| ------ | ---------------------------------------------------- | ------------------------------------------------------------ |
| Cookie | 与[QueryPlayerInfo](Role_QueryPlayerInfo.md)接口相同 | 多次请求相同接口时携带, 非必要，本项目交给HttpCilent自动处理 |

#### 响应体

| 字段        | 类型   | 说明                             |
| ----------- | ------ | -------------------------------- |
| `code`      | int    | `0` 表示成功                     |
| `message`   | string | 提示信息                         |
| `data`      | object | 按大区返回的角色详情 JSON 字符串 |
| `timestamp` | long   | 时间戳                           |

`data[region]`

| 字段         | 说明                                                           |
| ------------ | -------------------------------------------------------------- |
| `Base`       | 基础角色信息，如名称、等级、世界等级、体力、活跃度、宝箱统计等 |
| `MotorData`  | 摩托等级、皮肤、贴纸等信息                                     |
| `MusicData`  | 车载音乐解锁信息                                               |
| `BattlePass` | 先约电台                                                       |

`Base`
| 字段 | 类型 | 示例值 | 说明 | 备注 |
| --------------------------- | ------- | -----------------------------------: | --------- | -------------- |
| Name | string | 告白 | 角色名称 | |
| Id | int | 11111111 | 角色 ID | |
| CreatTime | long | 1744640064349 | 创建时间 | 毫秒时间戳 |
| ActiveDays | int | 342 | 活跃天数 | |
| Level | int | 80 | 等级 | 角色等级 |
| WorldLevel | int | 8 | 索拉等级 | |
| RoleNum | int | 34 | 角色数量 | 已拥有角色数 |
| SoundBox | int | 11 | 声匣数量 | |
| Energy | int | 156 | 当前体力（结晶玻片） | |
| MaxEnergy | int | 240 | 体力上限 | |
| StoreEnergy | int | 173 | 储备体力（结晶单质） | |
| StoreEnergyRecoverTime | long | 0 | 储备体力恢复时间 | |
| MaxStoreEnergy | int | 480 | 储备体力上限 | |
| EnergyRecoverTime | long | 1780407097458 | 体力恢复时间 | 毫秒时间戳 |
| Liveness | int | 0 | 活跃度 | |
| LivenessMaxCount | int | 100 | 活跃度上限 | |
| LivenessUnlock | boolean | true | 活跃度功能是否解锁 | |
| ChapterId | int | 37 | 章节 ID | 当前章节进度 |
| WeeklyInstCount | int | 3 | 周本次数 | 剩余奖励次数，上限为3 |
| Boxes | object | `{"1":1241,"2":1143,"3":418,"4":78}` | 各类箱子数量统计 | 键是分类编号 |
| BasicBoxes | object | `{"1":798,"2":941,"3":400,"4":102}` | 箱子统计 | |
| PhantomBoxes | object | `{"1":197,"2":189,"3":188}` | 潮汐之遗 | |
| BirthMon | int | 1 | 生日月份 | |
| BirthDay | int | 1 | 生日日期 | |

`Boxes` `BasicBoxes` `PhantomBoxes(潮汐之遗)`
| 字段 | 数据类型 | 示例值 | 说明 | 备注 |
|---|---|---|---|---|
| Key | int | 1 | 分类编号 | 1/2/3/4 分别表示不同箱子类型 |
| Value | int | 1241 | 数量 | 各类箱子数量统计 |

`Boxes Keys`
| Key | 说明 |
| ---- | ---- |
| 1 | 朴素奇藏箱 |
| 2 | 基准奇藏箱 |
| 3 | 精密奇藏箱 |
| 4 | 辉光奇藏箱 |

`BasicBoxes Keys`
同上？

`PhantomBoxes Keys`
| Key | 说明 |
| ---- | ---- |
| 1 | 绿|
| 2 | 紫 |
| 3 | 金 |

`BattlePass（先约电台）`
| 字段 | 数据类型 | 示例值 | 说明 | 备注 |
| --------------------- | ------- | ----: | -------- | ---------- |
| Level | int | 70 | 电台等级 | |
| WeekExp | int | 0 | 本周经验 | 本周已获得经验 |
| WeekMaxExp | int | 12000 | 本周经验上限 | |
| IsUnlock | boolean | true | 是否解锁 | |
| IsOpen | boolean | true | 是否开启 | |
| Exp | int | 450 | 当前经验 | 当前等级内经验 |
| ExpLimit | int | 1000 | 升级所需经验上限 | 升一级所需经验 |

`MusicData`
| 字段 | 数据类型 | 示例值 | 说明 | 备注 |
| ----------------------------- | ------ | --: | ----- | -------- |
| Id | int | 1 | 专辑编号 | 专辑 ID |
| Count | int | 12 | 已收集数量 | 当前已获得 |
| TotalCount | int | 12 | 总数量 | 该专辑总可收集数 |

`MotorData`
| 字段 | 数据类型 | 示例值 | 说明 | 备注 |
| ------------------------------- | ------------- | ----------------------------------: | ------ | ------------ |
|Level | int | 20 | 摩托等级 | |
|Exp | int | 0 | 当前经验 | 当前经验值 |
|NextExp | int | 0 | 下一级经验 | 升级所需/下一等级经验 |
|Skins | List | `[{"SkinId":89200000,"Quality":5}]` | 皮肤列表 | 已拥有皮肤 |
|SkinId | int | 89200000 | 皮肤 ID | |
|Quality | int | 5 | 品质 | |
|Stickers | List | `[{Id:89101998,...}]` | 贴纸列表 | |
|Id | int | 89101998 | 贴纸 ID | 贴纸编号 |
|Quality | int | 4 | 贴纸品质 | 4 / 5 为主 |
|PartId | int | 1 | 部位编号 | 1/2/3 表示不同部位 |
|Decorations | List | `[{"Id":89300001,"Quality":5,...}]` | 装饰列表 | |
|Id | int | 89300001 | 装饰 ID | 装饰编号 |
|Quality | int | 5 | 装饰品质 | 装饰稀有度 |
|PartId | int | 1 | 部位编号 | 对应装饰部位 |
|Frames | List | `[{"Id":89400001,"Quality":1}]` | 车架列表 | |
|Id | int | 89400001 | 车架 ID | 车架编号 |
|Quality | int | 1 | 车架品质 | 1 / 5 等 |
|EquipSkin | object | `{"SkinId":89200000,"Quality":5}` | 当前装备皮肤 | 当前生效皮肤 |

### 示例

#### 请求

```json
{
  "oauthCode": "example-oauth-code",
  "playerId": 11111111,
  "region": "Asia"
}
```

#### 响应

```json
{
  "code": 0,
  "message": "success",
  "data": {
    "Asia": "{...Base, MotorData, MusicData, BattlePass 等字段的 JSON 字符串...}"
  }
}
```
