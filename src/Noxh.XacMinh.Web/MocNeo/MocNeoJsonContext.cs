using System.Text.Json;
using System.Text.Json.Serialization;

namespace Noxh.XacMinh.Web.MocNeo;

/// <summary>Đúng những trường công cụ dùng của hai nguồn công khai — thừa trường thì bỏ qua, không lỗi.</summary>
internal sealed class EthTraLoi
{
    [JsonPropertyName("result")] public EthKhoi? Result { get; init; }

    [JsonPropertyName("error")] public EthLoi? Error { get; init; }
}

internal sealed class EthKhoi
{
    [JsonPropertyName("hash")] public string? Hash { get; init; }

    [JsonPropertyName("number")] public string? Number { get; init; }

    [JsonPropertyName("timestamp")] public string? Timestamp { get; init; }
}

internal sealed class EthLoi
{
    [JsonPropertyName("message")] public string? Message { get; init; }
}

internal sealed class BlockstreamKhoi
{
    [JsonPropertyName("height")] public long? Height { get; init; }

    [JsonPropertyName("timestamp")] public long? Timestamp { get; init; }
}

/// <summary>Sinh mã đọc JSON lúc biên dịch — bản publish có trimming, reflection sẽ hụt trường.</summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(EthTraLoi))]
[JsonSerializable(typeof(BlockstreamKhoi))]
internal partial class MocNeoJsonContext : JsonSerializerContext;
