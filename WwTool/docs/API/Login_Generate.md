## 生成启动器 OAuth Code

该接口用于基于 `access_token` 生成面向启动器接口的 `oauthCode`。获取到后才能继续调用玩家详情等接口。

### 请求地址

`https://sdkapi.kurogame-service.com/sdkcom/v2/user/oauth/code/generate.lg`

### 请求方式

`POST`

### 认证方式

使用 `access_token` 和 `client_secret` 授权。

#### 请求体

`application/x-www-form-urlencoded`

| 字段 | 类型   |说明 |备注
| --- | --- | --- |---|
| client_id|string |客户端 ID |
| deviceNum |string| 设备 ID |
| client_secret|string |客户端密钥 |
| access_token|string | 登录换得的访问令牌 |
| productId|string |产品 ID |
| projectId|string |  项目 ID |
| redirect_uri|string |固定参数 |
| scope|string |当前抓包显示用于启动器授权 |

### 响应体

`json`

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| codes | int | `0` 表示成功 |
| error_description | string | 提示信息 |
| timestamp | long | 时间戳 |
| oauthCode | string | 启动器接口使用的 OAuth Code |

## 示例

### 请求

```http
POST /sdkcom/v2/user/oauth/code/generate.lg HTTP/1.1
Host: sdkapi.kurogame-service.com
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36
Content-Type: application/x-www-form-urlencoded

client_id=1111&deviceNum=1111client_secret=111&access_token=111&productId=111&projectId=111&redirect_uri=1&scope=launcher
```

### 响应

```json
{
  "codes": 0,
  "error_description": "success.",
  "timestamp": 1780235139869,
  "oauthCode": "12122112"
}
```