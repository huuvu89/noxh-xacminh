using System.Text.Json.Serialization;

namespace Noxh.XacMinh.Core.Transparency;

/// <summary>
/// Báo cáo minh bạch đã công bố (<c>GET /projects/{id}/transparency</c>).
/// Chỉ khai báo những khối đã có hạng mục kiểm dùng tới (vé sau thêm khối của vé đó), nhưng khối
/// nào đã khai thì khai đủ trường của khối đó. Mọi trường đều nullable: thiếu dữ liệu là một trạng
/// thái hợp lệ (KHÔNG KIỂM ĐƯỢC), không phải lỗi nạp.
/// </summary>
public sealed class TransparencyReport
{
    [JsonPropertyName("projectId")] public string? ProjectId { get; init; }

    [JsonPropertyName("projectName")] public string? ProjectName { get; init; }

    [JsonPropertyName("completedAt")] public string? CompletedAt { get; init; }

    [JsonPropertyName("nguonNgauNhien")] public IReadOnlyList<EntropySource>? EntropySources { get; init; }

    [JsonPropertyName("decks")] public IReadOnlyList<Deck>? Decks { get; init; }
}

/// <summary>
/// Nguồn ngẫu nhiên của một vòng (gate A/B/C). Vòng chưa đóng cổng thì các trường lộ ra sau khi
/// đóng (<c>rServer</c>, <c>blockHash</c>, <c>masterSeed</c>) còn trống — đó là dữ liệu thiếu hợp
/// lệ, không phải báo cáo hỏng.
/// </summary>
public sealed class EntropySource
{
    [JsonPropertyName("round")] public string? Round { get; init; }

    [JsonPropertyName("masterSeed")] public string? MasterSeed { get; init; }

    [JsonPropertyName("rServer")] public string? RServer { get; init; }

    [JsonPropertyName("rServerCommit")] public string? RServerCommit { get; init; }

    [JsonPropertyName("rSupervisor")] public string? RSupervisor { get; init; }

    [JsonPropertyName("blockHeight")] public long? BlockHeight { get; init; }

    [JsonPropertyName("blockHash")] public string? BlockHash { get; init; }

    [JsonPropertyName("anchorChain")] public string? AnchorChain { get; init; }

    [JsonPropertyName("inputHash")] public string? InputHash { get; init; }
}

/// <summary>Một chồng phiếu đã niêm phong; <c>Tickets</c> là nội dung vé sau khi mở.</summary>
public sealed class Deck
{
    [JsonPropertyName("round")] public string? Round { get; init; }

    [JsonPropertyName("deckId")] public string? DeckId { get; init; }

    [JsonPropertyName("deckHash")] public string? DeckHash { get; init; }

    [JsonPropertyName("size")] public int? Size { get; init; }

    [JsonPropertyName("sealedAt")] public string? SealedAt { get; init; }

    [JsonPropertyName("tickets")] public IReadOnlyList<string?>? Tickets { get; init; }
}
