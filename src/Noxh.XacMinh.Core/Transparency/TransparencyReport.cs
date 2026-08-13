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

    /// <summary>
    /// Mã băm danh sách hồ sơ đã ghim. Endpoint minh bạch công khai hiện KHÔNG công bố trường này
    /// (nó nằm trong bản dành cho tổ giám sát), nhưng nó là một dòng của chuỗi đóng dấu mốc cam kết
    /// — khai sẵn để đối chiếu được ngay khi bản báo cáo có nó.
    /// </summary>
    [JsonPropertyName("listHash")] public string? ListHash { get; init; }

    [JsonPropertyName("nguonNgauNhien")] public IReadOnlyList<EntropySource>? EntropySources { get; init; }

    [JsonPropertyName("camKetNeo")] public AnchorCommitment? AnchorCommitment { get; init; }

    [JsonPropertyName("dauThoiGian")] public IReadOnlyList<TimestampToken?>? Timestamps { get; init; }

    [JsonPropertyName("decks")] public IReadOnlyList<Deck>? Decks { get; init; }

    [JsonPropertyName("nhatKyBoc")] public IReadOnlyList<DrawLogEntry>? DrawLog { get; init; }

    [JsonPropertyName("ketQua")] public ResultTable? Results { get; init; }
}

/// <summary>
/// Cam kết mốc neo: hai block đích được chốt <b>trước khi chúng tồn tại</b>, kèm thời điểm chốt.
/// Đây chính là bộ số mà dấu thời gian của bên thứ ba đóng lên.
/// </summary>
public sealed class AnchorCommitment
{
    [JsonPropertyName("ethTargetHeight")] public long? EthTargetHeight { get; init; }

    [JsonPropertyName("btcTargetHeight")] public long? BtcTargetHeight { get; init; }

    [JsonPropertyName("anchorFrozenAt")] public string? AnchorFrozenAt { get; init; }
}

/// <summary>
/// Một dấu thời gian RFC 3161. <see cref="Preimage"/> là chuỗi được đóng dấu, <see cref="Digest"/>
/// là dấu vân tay nằm trong token. <see cref="Scope"/> nói dấu này đóng lên mốc nào
/// (<c>FREEZE</c> = mốc cam kết, <c>STEPCHAIN:{vòng}</c>/<c>RESULTS:{vòng}</c> = mốc khác).
/// </summary>
public sealed class TimestampToken
{
    [JsonPropertyName("scope")] public string? Scope { get; init; }

    [JsonPropertyName("authority")] public string? Authority { get; init; }

    [JsonPropertyName("genTime")] public string? GenTime { get; init; }

    [JsonPropertyName("serialNumber")] public string? SerialNumber { get; init; }

    [JsonPropertyName("digest")] public string? Digest { get; init; }

    [JsonPropertyName("preimage")] public string? Preimage { get; init; }
}

/// <summary>Bảng kết quả chung cuộc kèm mã băm đã ghim (<c>resultsHash</c>) của chính bảng đó.</summary>
public sealed class ResultTable
{
    [JsonPropertyName("resultsHash")] public string? ResultsHash { get; init; }

    [JsonPropertyName("rows")] public IReadOnlyList<ResultRow?>? Rows { get; init; }
}

/// <summary>
/// Một dòng kết quả chung cuộc. <see cref="CancelledAt"/> công bố cho trung thực nhưng KHÔNG nằm
/// trong mã băm: huỷ kết quả là thay đổi hợp lệ sau lễ, không được phá cam kết đã ghim.
/// </summary>
public sealed class ResultRow
{
    [JsonPropertyName("applicantId")] public string? ApplicantId { get; init; }

    [JsonPropertyName("won")] public bool? Won { get; init; }

    [JsonPropertyName("tier")] public string? Tier { get; init; }

    [JsonPropertyName("typeCode")] public string? TypeCode { get; init; }

    [JsonPropertyName("unitCode")] public string? UnitCode { get; init; }

    [JsonPropertyName("waitlistRank")] public int? WaitlistRank { get; init; }

    [JsonPropertyName("cancelledAt")] public string? CancelledAt { get; init; }
}

/// <summary>
/// Một lượt bốc trong nhật ký. Hai định danh (<see cref="ApplicantId"/>, <see cref="DeckId"/>) là
/// thành phần bắt buộc của chuỗi đem băm: thiếu chúng thì chỉ so được bước sau có trỏ đúng bước
/// trước hay không, tức là chuỗi băm chỉ còn là trang trí.
/// </summary>
public sealed class DrawLogEntry
{
    [JsonPropertyName("round")] public string? Round { get; init; }

    [JsonPropertyName("applicantId")] public string? ApplicantId { get; init; }

    [JsonPropertyName("deckId")] public string? DeckId { get; init; }

    [JsonPropertyName("position")] public int? Position { get; init; }

    [JsonPropertyName("payload")] public string? Payload { get; init; }

    [JsonPropertyName("autoDrawn")] public bool? AutoDrawn { get; init; }

    [JsonPropertyName("prevHash")] public string? PrevHash { get; init; }

    [JsonPropertyName("entryHash")] public string? EntryHash { get; init; }
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

    /// <summary>Số vé trúng chồng phiếu khai lúc niêm phong — thành phần để dựng lại chồng phiếu.</summary>
    [JsonPropertyName("wonCount")] public int? WonCount { get; init; }

    [JsonPropertyName("sealedAt")] public string? SealedAt { get; init; }

    [JsonPropertyName("tickets")] public IReadOnlyList<string?>? Tickets { get; init; }
}
