## 邮箱密码登录

用于使用邮箱和密码获取访问令牌。

### 请求地址

`https://sdkapi.kurogame-service.com/sdkcom/v2/login/emailPwd.lg`

### 请求方式

`POST`

### 认证方式

请求体中包含签名字段 `sign`；当触发风控时，需要携带极验验证码参数：`geetestCaptchaOutput`、`geetestGenTime`、`geetestLotNumber`、`geetestPassToken`。

### 请求头

| 字段           | 示例值                              | 说明     |
| -------------- | ----------------------------------- | -------- |
| `Content-Type` | `application/x-www-form-urlencoded` | 表单提交 |

### 请求体

`application/x-www-form-urlencoded`

| 字段                   | 说明                       | 备注                                     |
| ---------------------- | -------------------------- | ---------------------------------------- |
| `__e__`                |意义不明，但是必须带       | 固定值                                   |
| email                | 登录邮箱                   |                                          |
| client_id            |  客户端标识                 | 固定值                                   |
| deviceNum            | 设备 ID，大写 UUIDv4       | 任意值即可，不检查但必须有               |
| password             | 加密后的密码串             | Base64 加密后再经过两轮邻位互换，步长为4 |
| platform             | 平台                       |                                          |
| productId            | 产品 ID                    | 固定值                                   |
| productKey           | 产品 Key                   | 固定值                                   |
| projectId            |项目 ID                    | 固定值                                   |
| redirect_uri         |固定参数                   | 固定值                                   |
| response_type        | 登录后换取授权码           | 固定值                                   |
| sdkVersion           | SDK 版本                   | 固定值                                   |
| channelId            | 登录渠道                   | 固定值                                   |
| sign                 | 对请求参数进行签名后的结果 | 对上面所有参数进行字典排序后的的字符串+secret_key进行MD5加密  |
| geetestCaptchaOutput | 触发风控后必填             |                                          |
| geetestGenTime       | 触发风控后必填             |                                          |
| geetestLotNumber     | 触发风控后必填             |                                          |
| geetestPassToken     | 触发风控后必填             |                                          |

## 响应体
`json`

| 字段                | 类型        | 说明                                     |
| ------------------- | ----------- | ---------------------------------------- |
| codes             | int      | `0` 表示成功<br>`41000` 表示需要行为校验 |
| error_description | string      | 接口提示信息                             |
| timestamp         | long      | 时间戳                                   |
| username          | string      | 用户名                                   |
| sdkuserid         | string      | SDK 用户 ID                              |
| id                | int      | 用户 ID                                  |
| loginType         | int      | 登录类型                                 |
| code              | string      | 后续换取访问令牌使用的授权码             |
| temp_token        | string      | 临时 token                               |
| idStat            | int      | 待确认                                   |
| userType          | int      | 用户类型                                 |
| cuid              | string      | 字符串形式用户 ID                        |
| showPaw           | boolean     | 待确认                                   |
| bindDevStat       | int/bool | 绑定设备状态                             |
| bindDevSwitch     | boolean     | 设备绑定开关                             |
| autoToken         | string      | 自动登录 token                           |
| thirdNickName     | string      | 昵称                                     |
| firstLgn          | int      | 是否首次登录，`0`表示否                 |
| email             | string      | 邮箱                                     |
| bind              | int      | 绑定状态                                 |

风控触发时仅返回 `codes`、`error_description` 和 `timestamp` 三个字段。

## 示例

### 请求

```http
POST /sdkcom/v2/login/emailPwd.lg HTTP/1.1
Host: sdkapi.kurogame-service.com
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36
Content-Type: application/x-www-form-urlencoded

__e__=1&email=example@example.com&client_id=11111&deviceNum=11111&password=123456789&platform=PC&productId=1111&productKey=11111&projectId=G153&redirect_uri=1&response_type=code&sdkVersion=x.x.x&channelId=11&sign=f88adc4ad22b206e5855744f7b4a87e2
```

### 响应

```json
{
  "codes": 0,
  "error_description": "success.",
  "timestamp": 1780232902618,
  "username": "U11111111A",
  "sdkuserid": "U11111111A",
  "id": 11111111,
  "loginType": 13,
  "code": "11111111-111111111-1111111",
  "temp_token": "111111111-11111111-11111111",
  "idStat": 0,
  "userType": 18,
  "cuid": "11111111",
  "showPaw": false,
  "bindDevStat": 0,
  "bindDevSwitch": false,
  "autoToken": "11.1111111111111111111111111111111111111111.111111111",
  "thirdNickName": "NickName",
  "firstLgn": 0,
  "email": "example@example.com",
  "bind": 1
}
```
