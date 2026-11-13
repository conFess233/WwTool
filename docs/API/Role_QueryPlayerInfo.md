# 查询玩家信息

该接口用于根据 `oauthCode` 查询玩家在各大区的基础信息，例如角色 ID、角色名、等级、头像等。

## 请求地址

完整地址：`https://pc-launcher-sdk-api.kurogame.net/game/queryPlayerInfo`

## 请求方式

`POST`

## 认证方式

使用请求体中的 `oauthCode` 进行授权。

## 请求头

| 字段           | 值                            | 备注        |
| -------------- | --------------------------------- | ----------- |
| Content-Type | application/json; charset=utf-8 | JSON 请求体 |

## Cookie
| 字段           | 示例值                           | 备注        |
|---|---|---|
| Cookie | acw_tc=... | 多次请求相同接口时携带, 非必要，本项目交给HttpCilent自动处理 |

## 请求体
`json`

| 字段        | 类型   | 说明                       |
| ----------- | ------ |-------------------------- |
| oauthCode | string | 由 OAuth Code 生成接口返回 |

## 响应体
`json`
| 字段        | 类型   | 说明                     |
| ----------- | ------ | ------------------------ |
| code      | int    | 0 表示成功             |
| message   | string | 提示信息                 |
| data      | object | 按大区组织的玩家信息映射 |
| timestamp | long   | 时间戳                   |

`data` 中每个键对应一个大区，值是 JSON 字符串，需要二次解析。以 `Asia` 为例，解析后字段如下：

| 字段        | 类型   | 说明                 |
| ----------- | ------ | -------------------- |
| roleId    | string | 玩家 UID             |
| roleName  | string | 角色名               |
| level     | int    | 等级                 |
| sex       | int    | 性别，具体枚举待确认 |
| headPhoto | int    | 头像 ID              |

### 响应头 Cookie
| 字段           | 示例值                           | 备注        |
|---|---|---|
| Cookie | acw_tc=... | 多次请求相同接口时返回, 非必要，本项目交给HttpCilent自动处理 |


## 示例

### 请求

```http
POST /game/queryPlayerInfo HTTP/1.1
Host: pc-launcher-sdk-api.kurogame.net
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36
Content-Type: application/json; charset=utf-8

{"oauthCode":"111111111"}
```

### 响应

```json
{
    "code": 0,
    "message": "success",
    "data": {
        "Asia": "{\"roleId\":\"1111111\",\"roleName\":\"告白\",\"level\":80,\"sex\":0,\"headPhoto\":82000009}"
    },
    "timestamp": 1780235140205
}
```
