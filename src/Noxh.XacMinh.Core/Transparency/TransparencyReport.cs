using System.Text.Json.Serialization;

namespace Noxh.XacMinh.Core.Transparency;

/// <summary>
/// Báo cáo minh bạch đã công bố (<c>GET /projects/{id}/transparency</c>).
/// Chỉ khai báo những khối đã có hạng mục kiểm dùng tới — vé sau thêm khối của vé đó, để trường
/// thừa không âm thầm tạo cảm giác "đã kiểm rồi". Mọi trường đều nullable: thiếu dữ liệu là một
/// trạng thái hợp lệ (KHÔNG KIỂM ĐƯỢC), không phải lỗi nạp.
/// </summary>
public sealed class TransparencyReport
{
    [JsonPropertyName("projectId")] public string? ProjectId { get; init; }

    [JsonPropertyName("projectName")] public string? ProjectName { get; init; }

    [JsonPropertyName("completedAt")] public string? CompletedAt { get; init; }

    [JsonPropertyName("decks")] public IReadOnlyList<Deck>? Decks { get; init; }
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
