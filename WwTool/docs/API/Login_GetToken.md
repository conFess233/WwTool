## 换取访问令牌

该接口用于将登录阶段返回的 `code` 换成正式的 `access_token`，后续的用户信息查询、启动器 OAuth 授权等接口都依赖这个 token。

### 请求地址

`https://sdkapi.kurogame-service.com/sdkcom/v2/auth/getToken.lg`

### 请求方式

`POST`

### 认证方式

`code`，`client_secret` 与 `sign` 参数授权换取。

#### 请求体

`application/x-www-form-urlencoded`

| 字段          | 类型   |说明       | 备注                        |
| ------------- | ------ |  ---------- | --------------------------- |
| client_id     | string |  客户端 ID  |                             |
| deviceNum     | string | 设备 ID    | 任意值即可，不检查但必须有  |
| client_secret | string |客户端密钥 |                             |
| code          | string |授权码     | 登录接口返回                |
| productId     | string | 产品 ID    | 获取 Token 时的 ID 是 A1725 |
| projectId     | string |项目 ID    |                             |
| grant_type    | string | 授权模式   | 固定值                      |
| redirect_uri  | string | 固定参数    | 固定值                                   |
| sign          | string |  参数签名   |                             |

#### 响应体

`json`

| 字段         | 类型   |说明     | 备注                 |
| ------------ | ------ | -------------------- | -------- | -------------------- |
| codes        | int |状态码   | 0 表示成功           |
| access_token | string | 访问令牌 | 后续核心业务接口使用 |
| expires_in   | int | 有效期   | 单位：秒             |


### 示例

#### 请求
`form`
```
POST /sdkcom/v2/auth/getToken.lg HTTP/1.1
Host: sdkapi.kurogame-service.com
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36
Accept-Language: zh-Hans
Accept-Encoding: gzip, deflate, br, zstd
Content-Type: application/x-www-form-urlencoded
Content-Length: 276

client_id=1111&........
```

#### 响应
`json`
```json
{
  "codes": 0,
  "error_description": "成功。",
  "timestamp": 1780449820180,
  "access_token": "111111111-1111-1111-1111-1111111111",
  "expires_in": 259200
}
```