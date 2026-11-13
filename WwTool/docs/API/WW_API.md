## 通用数据

> 所有信息均通过解包/网络查找等方式获取

### URL

- [邮箱登录](Login_email.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/login/emailPwd.lg`
- [换取访问令牌](Login_GetToken.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/auth/getToken.lg`
- [获取Oauth Code](Login_Generate.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/user/oauth/code/generate.lg`
- [自动登录](Login_autoToken.md) `https://sdkapi.kurogame-service.com/sdkcom/v2/login/auto.lg`
- [查询账号大区信息](Role_GetUserInfo.md) `https://gar-service.aki-game.net/UserRegion/GetUserInfo`
- [查询玩家角色详情](Role_QueryRole.md) `https://pc-launcher-sdk-api.kurogame.net/game/queryRole`
- [攻略站](Guide.md) `https://wuwaguide.kurogames.com`
- [启动器资讯](Launcher.md#启动器资讯) `https://prod-alicdn-gamestarter.kurogame.com/launcher/app/{gameId}_{launcherKey}/{appCode}/official/information/{language}.json`
- [启动器背景](Launcher.md#启动器背景) `https://prod-alicdn-gamestarter.kurogame.com/launcher/game/{gameId}_{launcherKey}/{appCode}/official/background/{backgroundId}/{language}.json`
- [游戏内公告](GameNotice.md)（活动 / 唤取 / 版本日历） `https://aki-gm-resources-back.aki-game.com/gamenotice/{appCode}/{服务器ID}/{language}.json`（国际服域名为 `.net`）
- [库街区帖子搜索](KuroBBS_Post.md#帖子搜索) `https://api.kurobbs.com/forum/search/v2/post`
- [库街区帖子详情](KuroBBS_Post.md#帖子详情)（前瞻兑换码） `https://api.kurobbs.com/forum/getPostDetail`

#### 服务器ID

| 服务器ID                         | 服务器名称   |
| -------------------------------- | ------------ |
| 86d52186155b148b5c138ceb41be9650 | 亚服 Asia    |
| 591d6af3a3090d8ea00d8f86cf6d7501 | 美服 America |
| 6eb2a235b30d05efd77bedb5cf60999e | 欧服 Europe  |
| 919752ae5ea09c1ced910dd668a63ffb | 港澳台 HMT   |
| 10cd7254d57e58ae560b15d51e34b4c8 | 东南亚 SEA   |
| 76402e5b20be2c39f095a152090afddc | 国服 CN      |

> 国际服 UID 的最高位对应服务器：5 美服、6 欧服、7 亚服、8 港澳台、9 东南亚；UID < 200000000 为国服。详见 [抽卡记录](GachaLog.md#按-uid-推断服务器)。

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
