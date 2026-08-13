namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>Lottery.Api/Features/Lottery/Engine/LotteryLabels.cs</c>) — chỉ
/// phần vé tái lập đang cần. Nội dung vé và nhãn dẫn xuất hạt giống là <b>chuỗi đi vào mã băm</b>:
/// gõ lại theo trí nhớ, sai một ký tự, là công cụ dựng ra chồng phiếu khác hẳn.
/// Vòng khác (B/C) mang nhãn của vòng đó sang khi có vé dựng lại vòng đó.
/// </summary>
public static class LotteryLabels
{
    /// <summary>Deck round của chồng phiếu vòng quyền mua (<c>LotteryDeck.Round</c>).</summary>
    public const string RoundA1 = "A1";

    /// <summary>Chồng phiếu vòng phân căn ưu tiên đi theo loại căn: <c>A2:{mã loại}</c>.</summary>
    public const string RoundA2Prefix = "A2:";

    // ── Ticket payloads ─────────────────────────────────────────────────────
    public const string A1Win = "TRUNG_QUYEN_MUA";

    public const string A1Lose = "KHONG_TRUNG_UU_TIEN";

    /// <summary>Vé trúng một căn cụ thể — phần sau tiền tố là mã căn.</summary>
    public const string WinPrefix = "TRUNG:";

    /// <summary>Vé của người vào vòng ưu tiên nhưng hết suất: chờ máy gom phân căn dư.</summary>
    public const string A2Pending = "CHO_PHAN_LOAI_DU";

    public static string Win(string unitCode) => WinPrefix + unitCode;

    // ── Seed labels ─────────────────────────────────────────────────────────
    public const string A1Deck = "A1:deck";

    /// <summary>Nhãn dẫn xuất quỹ căn ưu tiên của một loại căn.</summary>
    public static string PriorityPool(string typeCode) => $"POOL:{typeCode}";

    /// <summary>Nhãn dẫn xuất chồng phiếu vòng phân căn ưu tiên của một loại căn.</summary>
    public static string A2Deck(string typeCode) => $"A2:deck:{typeCode}";
}
