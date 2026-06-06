## 查询账号大区信息

该接口用于查询用户在游戏侧的大区与账号信息。

### 请求地址

`https://gar-service.aki-game.net/UserRegion/GetUserInfo`

### 请求方式

`GET`

### 认证方式

通过 URL Query 参数中的 `token` 和 `userId` 认证。

#### 请求参数 (Query)

| 字段      | 类型   | 说明                 | 备注                               |
| --------- | ------ | -------------------- | ---------------------------------- |
| loginType | int | 登录类型             | 默认值为 1                         |
| userId    | string | SDK 用户 ID（U开头） | 不是游戏内 UID                     |
| token     | string |访问令牌             |                                    |
| area      | string | 地区参数             | 国服为 Mcn                         |
| userName  | string | 用户名               | 可传空字符串，特殊字符需 UrlEncode |

### 响应体

`json`

| 字段         | 类型   | 说明         | 备注                 |
| ------------ | ------ | ------------ | -------------------- |
| Code         | int |状态码       | 0 和 49 均视为可接受 |
| UserId       | string | 用户 ID      |                      |
| SdkLoginCode | int |SDK 登录码   |                      |
| UserInfos    | array   | 角色账号列表 |                      |
