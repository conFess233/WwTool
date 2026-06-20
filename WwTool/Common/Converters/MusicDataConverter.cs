using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using WwTool.Common.Models.ApiResponse;

namespace WwTool.Common.Converters
{
    public class MusicDataConverter : JsonConverter<List<RoleMusicData>>
    {
        public override List<RoleMusicData>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                var list = new List<RoleMusicData>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    var item = JsonSerializer.Deserialize<RoleMusicData>(ref reader, options);
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
                return list;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                var list = new List<RoleMusicData>();
                using (var jsonDoc = JsonDocument.ParseValue(ref reader))
                {
                    // 检查对象是否包含 "Albums" 或 "albums" 数组属性
                    if (jsonDoc.RootElement.TryGetProperty("Albums", out var albumsProp) && albumsProp.ValueKind == JsonValueKind.Array)
                    {
                        var albumsList = JsonSerializer.Deserialize<List<RoleMusicData>>(albumsProp.GetRawText(), options);
                        return albumsList ?? new List<RoleMusicData>();
                    }
                    if (jsonDoc.RootElement.TryGetProperty("albums", out var albumsPropLower) && albumsPropLower.ValueKind == JsonValueKind.Array)
                    {
                        var albumsList = JsonSerializer.Deserialize<List<RoleMusicData>>(albumsPropLower.GetRawText(), options);
                        return albumsList ?? new List<RoleMusicData>();
                    }

                    // 如果对象为字典，则回退到解析键值对。
                    foreach (var property in jsonDoc.RootElement.EnumerateObject())
                    {
                        var value = property.Value;
                        if (value.ValueKind == JsonValueKind.Object)
                        {
                            var item = JsonSerializer.Deserialize<RoleMusicData>(value.GetRawText(), options);
                            if (item != null)
                            {
                                // 如果对象内部的 Id 为 0 或缺失，则根据键进行映射。
                                if (item.Id == 0 && int.TryParse(property.Name, out int parsedId))
                                {
                                    item.Id = parsedId;
                                }
                                list.Add(item);
                            }
                        }
                    }
                }
                return list;
            }

            throw new JsonException($"转换为 List<RoleMusicData> 时出现意外的标记类型 {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, List<RoleMusicData> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartArray();
            foreach (var item in value)
            {
                JsonSerializer.Serialize(writer, item, options);
            }
            writer.WriteEndArray();
        }
    }
}
