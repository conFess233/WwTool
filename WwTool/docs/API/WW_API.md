## 通用数据

> 所有信息均通过解包/网络查找等方式获取

### URL

- [邮箱登录](Login_email.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/login/emailPwd.lg`
- [换取访问令牌](Login_GetToken.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/auth/getToken.lg`
- [获取Oauth Code](Login_Generate.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/user/oauth/code/generate.lg`
- [自动登录](Login_autoToken.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/login/auto.lg`
- [查询账号大区信息](Role_GetUserInfo.md) `https://gar-service.aki-game.net/UserRegion/GetUserInfo`
- [查询玩家角色详情](Role_QueryRole.md) `https://pc-launcher-sdk-api.kurogame.net/game/queryRole`

#### 服务器ID

| 服务器ID                         | 服务器名称   |
| -------------------------------- | ------------ |
| 86d52186155b148b5c138ceb41be9650 | 亚服 Asia    |
| 591d6af3a3090d8ea00d8f86cf6d7501 | 美服 America |
| 6eb2a235b30d05efd77bedb5cf60999e | 欧服 Europe  |
| 919752ae5ea09c1ced910dd668a63ffb | 港澳台 HMT   |
| 10cd7254d57e58ae560b15d51e34b4c8 | 东南亚 SEA   |

#### 语言

| 字符串  | 语言     |
| ------- | -------- |
| zh-Hans | 简体中文 |
| zh-Hant | 繁体中文 |
| en      | 英语     |
| ja      | 日语     |
| ko      | 韩语     |
| de      | 德语     |
| es      | 西班牙语 |
| fr      | 法语     |
| th      | 泰语     |

#### 通用请求头

> 这里直接使用从鸣潮启动器抓包来的请求头

| 字段            | 类型   | 示例值                                                                                                          | 说明             | 备注 |
| --------------- | ------ | --------------------------------------------------------------------------------------------------------------- | ---------------- | ---- |
| Accept-Encoding | string | gzip, deflate, br, zstd                                                                                         | 指定压缩算法     |      |
| Accept-Language | string | zh-Hans                                                                                                         | 服务器返回的语言 |      |
| User-Agent      | string | Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 | 客户端信息       |      |

#### 内部凭据

> 具体数据请自行搜集

| 字段             | 说明       | 备注                      |
| ---------------- | ---------- | ------------------------- |
| platform         | 平台       |                           |
| productId        | 产品 ID    |                           |
| productKey       | 产品 Key   |                           |
| projectId        | 项目 ID    |                           |
| sdkVersion       | SDK 版本   |                           |
| AppKey           | AppKey     | client_id用               |
| SecretKey        | SecretKey  | 签名加密, client_secret用 |
| H5GoogleClientID | 谷歌登录用 |                           |
