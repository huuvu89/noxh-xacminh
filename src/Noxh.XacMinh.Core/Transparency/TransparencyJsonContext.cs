using System.Text.Json;
using System.Text.Json.Serialization;

namespace Noxh.XacMinh.Core.Transparency;

/// <summary>
/// Sinh mã đọc JSON lúc biên dịch thay vì dùng reflection: bản publish của Blazor WASM có trimming,
/// mà trimming cắt mất thuộc tính chỉ được reflection chạm tới thì công cụ sẽ im lặng báo "không
/// thấy khối chồng phiếu" đúng lúc đang chạy trên trang thật.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(TransparencyReport))]
internal partial class TransparencyJsonContext : JsonSerializerContext;
