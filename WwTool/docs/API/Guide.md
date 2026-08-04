# 攻略站角色数据 API

本文记录鸣潮国际服官方攻略站的登录、玩家选择、角色拥有情况和角色养成详情接口。

- 攻略站入口：`https://wuwaguide.kurogames.com/zh-Hans`
- 主 API 域名：`https://guide-server.aki-game.net`
- 备用 API 域名：`https://guide-server-1.aki-game.net`
- 数据来源：用户提供的网络抓包、官方攻略站前端调用代码以及登录后的只读请求验证
- 验证日期：2026-08-04

> 这些接口不是公开、稳定的开发者 API，字段和行为可能随攻略站更新而改变。本文只给已观察字段赋予含义；未确认的枚举值保留原字段名，不推测映射。

## 数据边界

接口同时返回玩家实际数据和攻略推荐数据，两类数据不得混用。

| 分类 | 字段或对象 | 含义 |
| --- | --- | --- |
| 玩家实际数据 | `isAcquired`、`currentAmount`、`currentLevel`、`roleResonance.items[].isAcquired` | 当前所选 UID 的实际拥有或养成状态 |
| 玩家当前装备 | `echo.current`、`weapon.current` | 当前所选 UID 的实际装备 |
| 攻略推荐数据 | `recommendAmount`、`recommendLevel`、`echo.main`、`echo.spare`、`weapon.items`、`teammate.items` | 官方攻略方案，不代表玩家实际状态 |
| 社区或评价数据 | `likeCount`、`collectCount`、`grade` | 攻略互动或评价信息，不代表角色面板属性 |

除用户原始需求外，已确认还可获得武器、声骸、属性目标完成度、技能推荐等级、配队推荐、攻略点赞/收藏及评价信息。本文将这些字段纳入接口说明，但明确标注其数据性质。

## 配置约定

固定 SDK 参数和通用请求头优先复用 `ApiConfig.FixedParams` 与 `ApiConfig.CommonHeaders`，不得为 Guide 接口创建同义副本。

`ApiConfig.Urls` 只需要新增两个基础域名：

| 字段 | 默认值 |
| --- | --- |
| `GuideBaseUrl` | `https://guide-server.aki-game.net` |
| `GuideFallbackBaseUrl` | `https://guide-server-1.aki-game.net` |

本文中的接口路径应作为 Guide 服务内部协议常量，不为每个接口重复配置完整 URL。`cUid`、`cName`、`accessToken` 和 Guide Token 都是运行时数据，不属于 `ApiConfig`。

## 通用约定

### 请求头

| 字段 | 是否必需 | 示例值 | 说明 |
| --- | --- | --- | --- |
| `Content-Type` | POST 接口必需 | `application/json;charset=UTF-8` | JSON 请求体 |
| `x-token` | 登录后接口必需 | `<guide-token>` | `/user/login/sdk` 返回的 Guide Token |
| `x-language` | 建议 | `zh-Hans` | 返回语言，随当前 UI 语言传递 |
| `Accept-Language` | 建议 | `zh-Hans` | HTTP 语言偏好 |

攻略站前端还会发送浏览器、来源和跟踪类请求头。实测省略这些请求头仍能成功，因此不列为 API 必需字段，也不应持久化其值。

### 通用响应

```json
{
  "code": 200,
  "message": "ok",
  "data": {}
}
```

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `code` | int | 业务状态码；已验证成功值为 `200` |
| `message` | string | 业务提示；已验证成功值为 `ok` |
| `data` | object / array / null | 业务数据，不同接口结构不同 |

客户端必须同时检查 HTTP 状态码与 `code`。`code == 200` 不保证 `data` 一定非空。

## 认证流程

Guide 接口复用现有库洛登录结果，不直接保存或再次提交邮箱密码。

1. 使用现有 SDK 登录流程取得 `cUid`、`cName` 和 `accessToken`。
2. 调用 `POST /user/login/sdk` 换取 Guide Token。
3. 后续请求通过 `x-token` 携带 Guide Token。
4. Guide Token 可重复使用；本项目按库洛账号保存一份，不按 UID 重复保存。

### Guide SDK 登录

#### 请求地址

`POST /user/login/sdk`

#### 认证方式

请求体携带现有 SDK 登录上下文。

#### 请求体

| 字段 | 类型 | 来源 | 说明 |
| --- | --- | --- | --- |
| `cUid` | string | SDK 邮箱登录响应 | 库洛账号 ID |
| `cName` | string | SDK 邮箱登录响应 | SDK 用户名 |
| `accessToken` | string | SDK GetToken 响应 | SDK 访问令牌 |

#### 请求示例

```http
POST /user/login/sdk HTTP/1.1
Host: guide-server.aki-game.net
Content-Type: application/json;charset=UTF-8
x-language: zh-Hans

{
  "cUid": "<sdk-user-id>",
  "cName": "<sdk-user-name>",
  "accessToken": "<sdk-access-token>"
}
```

#### 响应体

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `data.token` | string | Guide Token，供后续请求的 `x-token` 使用 |

```json
{
  "code": 200,
  "message": "ok",
  "data": {
    "token": "<guide-token>"
  }
}
```

> `/user/login/game` 存在于攻略站前端，但未纳入当前 WwTool 认证流程。

### Guide Token 本地化

- 使用项目现有 Windows DPAPI `CurrentUser` 加密能力持久化，禁止明文保存。
- Token 与本地库洛账号关联；同一账号下的多个游戏 UID 共用一份 Token。
- 同步时优先使用本地 Token，不在每次请求前重新登录。
- 收到 HTTP `401`、`403` 或明确的 Token 失效业务码时，删除旧 Token，重新调用 `/user/login/sdk` 并覆盖本地值。
- 当前只确认 Token 可重复使用，未确认服务端有效期；不得虚构 `expiresAt` 或假定永久有效。
- 日志不得记录 Token、SDK Access Token、密码或完整原始响应。

## 查询账号资料

### 请求地址

`GET /user/profile`

### 认证方式

`x-token`

### 响应体

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `data.cUid` | string | 当前库洛账号 ID |
| `data.channelId` | int | 渠道 ID，具体枚举未确认 |
| `data.chosenPlayer` | object / null | 当前已选择的游戏角色 |

`data.chosenPlayer`

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `playerId` | int | 游戏 UID |
| `playerName` | string | 玩家昵称 |
| `serverId` | string | 服务器 ID |
| `serverName` | string | 服务器名称 |
| `level` | int | 玩家等级 |

## 查询账号下的玩家列表

### 请求地址

`GET /user/player/list`

### 认证方式

`x-token`

### 响应体

`data` 为玩家列表。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `playerId` | int / null | 游戏 UID；账号在该服务器没有角色时可能为空 |
| `playerName` | string / null | 玩家昵称 |
| `serverId` | string | 服务器 ID |
| `serverName` | string | 服务器名称 |
| `level` | int / null | 玩家等级 |

客户端必须用 WwTool 当前 UID 与非空 `playerId` 精确匹配，并同时取得该项的 `serverId`。不得默认选择数组第一项，也不得一次同步账号下的全部 UID。

## 选择玩家

### 请求地址

`POST /user/player/choose`

### 认证方式

`x-token`

### 请求体

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `playerId` | int | 从 `/user/player/list` 精确匹配到的 UID |
| `serverId` | string | 与该 UID 同一列表项中的服务器 ID |

```json
{
  "playerId": 100000001,
  "serverId": "<server-id>"
}
```

### 响应体

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `data.profile.cUid` | string | 当前库洛账号 ID |
| `data.profile.channelId` | int | 渠道 ID |
| `data.profile.chosenPlayer` | object | 选择后的玩家资料，结构与 `/user/profile` 相同 |

选择成功后才能查询该 UID 的角色实际数据。即使 `/user/profile` 显示的玩家与目标 UID 相同，也建议在同步流程中显式调用本接口，避免依赖攻略站会话中的旧选择状态。

## 查询角色拥有情况

### 请求地址

`GET /role/avatar/list`

### 认证方式

`x-token`

### 响应体

`data` 返回当前官方在线角色全集，而非只有已拥有角色。

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `roleGbId` | string | 稳定标识 | 角色业务 ID |
| `cardPictureUrl` | string | 静态资源 | 角色卡片图片 |
| `illustrationPictureUrl` | string | 静态资源 | 角色立绘 |
| `star` | int | 静态数据 | 星级 |
| `texts` | array | 本地化数据 | 角色多语言文本 |
| `element` | object | 静态数据 | 属性信息 |
| `rolePlays` | array | 静态数据 | 角色玩法或定位信息；具体枚举未确认 |
| `roleStatus` | int | 状态 | 攻略站角色状态；具体枚举未确认 |
| `sequence` | int | 内部字段 | 实测值类似 `10059`、`10048`，不是共鸣链数量，不得直接展示 |
| `isAcquired` | boolean | 玩家实际数据 | 是否拥有该角色 |
| `mayRoleGbId` | string / null | 关联标识 | 可能关联的角色 ID；确切语义未确认 |

`texts[]`

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `language` | string | 语言代码 |
| `name` | string | 角色名 |
| `skillDisplay` | string | 技能展示文本 |

`element` 与 `rolePlays[]`

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `gbId` | string | 业务 ID |
| `pictureUrl` | string | 图片地址 |
| `secondPictureUrl` | string / null | 第二图片地址 |

客户端应保存全部角色并保留 `isAcquired`。只对 `isAcquired == true` 的角色继续获取玩家详情；未拥有但可预览的角色可通过 `roleStatus` 单独展示，不得伪装为已拥有角色。

## 查询角色攻略列表

### 请求地址

`GET /introduction/list?roleGbId={roleGbId}`

### 查询参数

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `roleGbId` | string | `/role/avatar/list` 返回的角色业务 ID |

### 响应体

`data` 是该角色的攻略方案列表。

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | int | 攻略方案 ID |
| `role` | object | 角色基础信息 |
| `texts` | array | 攻略方案的多语言文本 |
| `likeCount` | int | 点赞数 |
| `isLiked` | boolean | 当前账号是否点赞 |
| `collectCount` | int | 收藏数 |
| `isCollected` | boolean | 当前账号是否收藏 |
| `teammateRecommends` | array | 配队预览，属于攻略推荐 |
| `modifiedAt` | int | 方案修改时间；时间单位尚未正式确认 |

`texts[]`

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `language` | string | 语言代码 |
| `introductionName` | string | 攻略名称 |
| `introductionSource` | string | 攻略来源 |
| `introductionDescription` | string | 攻略描述 |

方案选择规则：保持服务端返回顺序，先依次尝试包含当前 UI 语言文本的方案，再按服务端原顺序尝试其余方案；第一个返回非空详情的方案生效，并在本地记录其 `id` 和 `modifiedAt`。

## 查询角色养成详情

### 请求地址

`GET /introduction/info?roleGbId={roleGbId}&id={id}`

### 查询参数

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `roleGbId` | string | 角色业务 ID |
| `id` | int | `/introduction/list` 选出的攻略方案 ID |

### 响应体概览

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `id` | int | 攻略数据 | 攻略方案 ID |
| `role` | object | 静态数据 | 角色基础资料 |
| `baseTexts` | array | 攻略数据 | 角色与攻略说明文本 |
| `displayVideoUrl` | string / null | 攻略数据 | 展示视频 |
| `roleAttribute` | object | 混合 | 属性当前值与推荐目标 |
| `echo` | object | 混合 | 当前声骸与推荐声骸 |
| `echoTexts` | array | 攻略数据 | 声骸推荐说明 |
| `roleSkill` | object | 混合 | 当前技能等级与推荐等级 |
| `roleResonance` | object | 混合 | 共鸣链拥有情况与推荐说明 |
| `roleResonanceTexts` | array | 攻略数据 | 共鸣链推荐文本 |
| `weapon` | object | 混合 | 当前武器与推荐武器 |
| `weaponTexts` | array | 攻略数据 | 武器推荐说明 |
| `teammate` | object | 攻略数据 | 配队推荐 |
| `likeCount` / `collectCount` | int | 社区数据 | 点赞数与收藏数 |
| `isLiked` / `isCollected` | boolean | 账号数据 | 当前账号互动状态 |
| `grade` | string / null | 评价数据 | 攻略站评价；不是角色面板属性 |
| `calculationData` | object / null | 未确认 | 计算数据，当前样本可为空 |

实测该接口可能返回 `code == 200` 且 `data == null`。这种情况不代表角色没有养成数据，客户端应继续尝试后续方案；仅当该角色的所有方案均为空或失败时，才判定整次同步失败并保留旧快照。

### 属性 `roleAttribute`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `items` | array | 混合 | 属性列表 |
| `isFinished` | boolean | 进度结果 | 整体是否达到方案目标 |

`items[]`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `gbId` | string | 稳定标识 | 属性业务 ID |
| `pictureUrl` | string | 静态资源 | 图标 |
| `attachmentType` | string | 未确认 | 附加类型，保留原值 |
| `texts` | array | 本地化数据 | 属性名称 |
| `recommendAmount` | string | 攻略推荐 | 推荐值 |
| `operation` | int | 未确认 | 比较或运算类型，枚举未确认 |
| `currentAmount` | string | 玩家实际数据 | 当前值 |
| `isFinished` | boolean | 进度结果 | 是否达到推荐目标 |

### 声骸 `echo`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `current` | object | 玩家实际数据 | 当前装备声骸方案 |
| `main` | object | 攻略推荐 | 主推荐声骸方案 |
| `spare` | object | 攻略推荐 | 备选声骸方案 |
| `isFinished` | boolean | 进度结果 | 当前配置是否达到攻略目标 |

`current`、`main`、`spare` 使用相同的主要结构：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `echoProps` | object | 声骸基础资料，含 `gbId`、图片、COST、星级和本地化文本 |
| `echoSetEffects` | array | 声骸套装效果 |
| `echoAttributes` | array | 各 COST 位的属性与等级信息 |

`echoAttributes[]`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `cost` | int | 槽位数据 | COST |
| `currentLevel` | int / null | 玩家实际数据 | 当前装备对象返回实际等级；`main`、`spare` 推荐对象中可为 `null` |
| `attribute` | object | 属性数据 | 第一属性对象 |
| `attribute2` | object / null | 属性数据 | 第二属性对象 |
| `isFinishedMaxLevel` | boolean / null | 进度结果 | 当前装备对象表示是否达到最高等级；推荐对象中可为 `null` |
| `isFinished` | boolean / null | 进度结果 | 当前装备对象表示是否达到方案要求；推荐对象中可为 `null` |

### 技能 `roleSkill`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `fixedSkills` | array | 静态数据 | 固定技能资料 |
| `keynoteSkill` | object / null | 攻略推荐 | 重点技能 |
| `keynoteSkills` | array | 攻略推荐 | 重点技能列表 |
| `addPointTarget` | array | 混合 | 技能加点目标 |
| `addPointSequence` | array | 混合 | 技能加点顺序 |
| `isFinished` | boolean | 进度结果 | 技能是否达到方案目标 |

技能对象的公共字段包括 `gbId`、`pictureUrl`、`videoUrl`、`skillType` 和 `texts`。`addPointTarget[]` 额外包含：

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `recommendLevel` | int | 攻略推荐 | 推荐等级 |
| `currentLevel` | int | 玩家实际数据 | 当前技能等级 |

`addPointSequence[]` 还包含 `linkNextType`，其枚举含义未确认，不应自行映射。

### 共鸣链 `roleResonance`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `items` | array | 混合 | 各共鸣链节点 |
| `isFinished` | boolean | 进度结果 | 是否达到方案目标 |

`items[]`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `gbId` | string | 稳定标识 | 共鸣链节点 ID |
| `pictureUrl` | string | 静态资源 | 图标 |
| `resonanceSequence` | int | 静态数据 | 共鸣链序号 |
| `texts` | array | 本地化数据 | 名称和描述 |
| `status` | int | 未确认 | 攻略站状态枚举 |
| `isAcquired` | boolean | 玩家实际数据 | 当前 UID 是否已解锁该节点 |

客户端展示的共鸣链数量应统计 `items[].isAcquired == true` 的节点数，不得使用角色列表中的 `sequence` 字段。

### 武器 `weapon`

| 字段 | 类型 | 数据性质 | 说明 |
| --- | --- | --- | --- |
| `current` | object / null | 玩家实际数据 | 当前装备武器 |
| `items` | array | 攻略推荐 | 推荐武器列表 |
| `isFinished` | boolean | 进度结果 | 当前武器是否达到方案目标 |

武器公共字段包括 `gbId`、`pictureUrl`、`star`、`weaponType` 和 `texts`。推荐项还包含 `status`、`isAcquired` 与 `isFinished`；这些字段描述推荐项与当前玩家的关系，不能把 `items[0]` 当成当前装备。

### 配队 `teammate`

`teammate.items[]` 属于攻略推荐，可包含：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `main` | object | 推荐队友 |
| `spares` | array | 备选队友 |
| `weapon` | object | 推荐武器 |
| `echoProps` | object | 推荐声骸 |
| `echoSetEffect2` | object / null | 两件套推荐 |
| `echoSetEffect5` | object / null | 五件套推荐 |
| `echoAttributes` | array | 推荐声骸属性 |

## 推荐同步流程

1. 从本地读取并解密账号级 Guide Token；不存在或失效时调用 `/user/login/sdk`。
2. 调用 `/user/player/list`，用当前 WwTool UID 精确匹配玩家和服务器。
3. 调用 `/user/player/choose` 显式选择该 UID。
4. 调用一次 `/role/avatar/list`，保存全部角色及 `isAcquired`。
5. 对每个已拥有角色调用 `/introduction/list`，按当前 UI 语言选择方案。
6. 使用所选 `roleGbId` 和 `id` 调用 `/introduction/info`。
7. 所有核心详情成功后，在一个事务中替换该 UID 的完整快照并写入 `LastSyncedAtUtc`。

## WwTool 客户端实现约束

- 第一版只提供手动同步；页面打开时读取本地快照，不自动请求攻略站。
- 每个 UID 保存一份完整玩家快照；角色复杂详情以强类型对象序列化为 `DetailJson`，不保存 HTTP 响应信封或未知扩展字段。
- 数据库以 `roleGbId` 等稳定 ID 建立关系；角色与武器名称只从 `Local/Data/GameItemsResources.json` 通过 `GameDataService` 解析，不持久化 Guide 本地化名称；ID 缺失时显示字面值 `None`。
- 请求语言跟随 WwTool UI：`zh-Hans`、`en` 或 `ja`；本文示例使用 `zh-Hans`。
- 角色详情最大并发数为 2，并支持取消。
- 网络错误、超时和 HTTP `5xx` 可重试一次；HTTP `401`、`403`、`429` 不重试并停止本次同步。
- 主域名仅在网络错误、超时或 HTTP `5xx` 时切换一次备用域名；认证错误、业务错误和 `429` 不切换。一次请求最多尝试两个域名。
- 任一已拥有角色的核心详情失败、返回空数据或被取消时，不提交新快照，保留上一次成功数据。
- 已知核心字段反序列化失败应终止同步；未知扩展字段忽略，并且日志只记录字段名，不保存完整原始响应。
- API 返回顺序应作为快照的 `SourceOrder` 原样保存；展示层允许用户显式选择名称、星级或本地抽卡首次获取时间排序，但相同排序键与未匹配记录必须以 `SourceOrder` 稳定兜底。

## 验证状态

| 内容 | 状态 |
| --- | --- |
| `/user/login/sdk` 方法、请求体和响应体 | 抓包验证 |
| Guide Token 可重复使用 | 三次独立只读请求验证 |
| `/user/profile`、`/user/player/list` | 登录后请求验证 |
| `/user/player/choose` 方法、请求体和响应体 | 官方前端代码与抓包双重验证 |
| `/role/avatar/list` | 官方前端代码与登录后请求验证 |
| `/introduction/list`、`/introduction/info` | 官方前端代码与登录后请求验证 |
| `roleStatus`、`operation`、`status`、`linkNextType` 等枚举映射 | 待确认，文档未推测 |
| Guide Token 服务端有效期 | 待确认 |
| `modifiedAt` 时间单位 | 待确认 |
