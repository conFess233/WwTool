using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace WwTool.Common.Models.ApiResponse
{
    public class GachaResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        [JsonPropertyName("data")]
        public T? Data { get; set; }
    }
}
