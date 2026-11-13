## 自动登录

该接口用于使用 `autoToken` 快速自动登录，返回结果与邮箱密码登录相近，也会产出后续可换取访问令牌的 `code`。

### 请求地址

`https://sdkapi.kurogame-service.com/sdkcom/v2/login/auto.lg`

### 请求方式

`POST`

### 认证方式

`autoToken` 与 `sign` 参数签名认证。

#### 请求头

| 字段         | 类型   | 示例值                                       | 说明       | 备注 |
| ------------ | ------ | -------------------------------------------- | ---------- | ---- |
| Content-Type | string | application/x-www-form-urlencoded            | 表单提交   |      |

#### 请求体

`application/x-www-form-urlencoded`

| 字段          | 类型   | 说明           | 备注                                  |
| ------------- | ------ | -------------- | ------------------------------------- |
| token         | string |自动登录 token |    登录时返回的auto_token                                   |
| client_id     | string | 客户端标识     | 固定值                                |
| deviceNum     | string | 设备 ID        | 大写 UUID ,任意值即可，不检查但必须有 |
| sdkVersion    | string | SDK 版本       | 固定值                                |
| productId     | string |产品 ID        | 固定值                                |
| projectId     | string |  项目 ID        | 固定值                                |
| redirect_uri  | int |重定向 URI     | 固定参数                              |
| response_type | string |响应类型       | 固定参数                              |
| channelId     | int |  渠道 ID        | 固定参数                              |
| sign          | string | 参数签名       |                                       |

### 响应体

`json`

| 字段       | 类型   |说明         | 备注                      |
| ---------- | ------ | ------------ | ------------------------- |
| codes      | int | 状态码       | 0 表示成功                |
| code       | string |授权码       | 后续用于换取 Access Token |
| temp_token | string | 临时 token   |                           |
| autoToken  | string | 自动登录凭证 | 用于下次免密登录          |
| email      | string |  登录邮箱     |                           |

### 示例

#### 请求

```http
POST /sdkcom/v2/login/auto.lg HTTP/1.1
Host: sdkapi.kurogame-service.com
Content-Type: application/x-www-form-urlencoded

token=13.example_token.1111111111&client_id=7rxmydkibzzsf12om5asjnoo&deviceNum=11111111-1111-1111-1111-111111111111&sdkVersion=x.x.x&productId=123123&projectId=1233&redirect_uri=1&response_type=code&channelId=1111&sign=example_sign_hash

```

#### 响应
```json
{
    "codes": 0,
    "code": "example-auth-code",
    "temp_token": "example-temp-token",
    "autoToken": "13.example_token.1111111111",
    "email": "example@example.com"
}
