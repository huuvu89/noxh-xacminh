using System.Globalization;

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

    /// <summary>Chồng phiếu vòng bốc thẳng theo loại căn: <c>B:{mã loại}</c>.</summary>
    public const string RoundBPrefix = "B:";

    /// <summary>Vòng căn dư gộp một chồng phiếu duy nhất (<c>LotteryDeck.Round</c>).</summary>
    public const string RoundC = "C";

    // ── Ticket payloads ─────────────────────────────────────────────────────
    public const string A1Win = "TRUNG_QUYEN_MUA";

    public const string A1Lose = "KHONG_TRUNG_UU_TIEN";

    /// <summary>Vé trúng một căn cụ thể — phần sau tiền tố là mã căn.</summary>
    public const string WinPrefix = "TRUNG:";

    /// <summary>Vé của người vào vòng ưu tiên nhưng hết suất: chờ máy gom phân căn dư.</summary>
    public const string A2Pending = "CHO_PHAN_LOAI_DU";

    /// <summary>Vé không trúng của vòng bốc thẳng theo loại căn.</summary>
    public const string BLose = "KHONG_TRUNG";

    /// <summary>Vé không trúng chung cuộc của vòng căn dư — backend dùng chung chuỗi với vòng bốc thẳng.</summary>
    public const string CLose = BLose;

    /// <summary>Phiếu dự khuyết trước khi đánh số — chỉ tồn tại lúc dựng chồng phiếu.</summary>
    public const string WaitlistPlaceholder = "DU_KHUYET";

    /// <summary>Phiếu dự khuyết đã đánh số — phần sau tiền tố là số dự khuyết.</summary>
    public const string WaitlistPrefix = "DU_KHUYET:";

    public static string Win(string unitCode) => WinPrefix + unitCode;

    public static string WaitlistTicket(int so) => WaitlistPrefix + so.ToString(CultureInfo.InvariantCulture);

    // ── Seed labels ─────────────────────────────────────────────────────────
    public const string A1Deck = "A1:deck";

    /// <summary>Nhãn dẫn xuất quỹ căn ưu tiên của một loại căn.</summary>
    public static string PriorityPool(string typeCode) => $"POOL:{typeCode}";

    /// <summary>Nhãn dẫn xuất chồng phiếu vòng phân căn ưu tiên của một loại căn.</summary>
    public static string A2Deck(string typeCode) => $"A2:deck:{typeCode}";

    /// <summary>Nhãn dẫn xuất quỹ căn còn dư của một loại căn (vòng bốc thẳng).</summary>
    public static string LeftoverUnits(string typeCode) => $"B:units:{typeCode}";

    /// <summary>Nhãn dẫn xuất chồng phiếu vòng bốc thẳng của một loại căn.</summary>
    public static string BDeck(string typeCode) => $"B:deck:{typeCode}";

    /// <summary>Nhãn dẫn xuất quỹ căn dư chung của vòng cuối (không đi theo loại căn).</summary>
    public const string CUnits = "C:units";

    /// <summary>Nhãn dẫn xuất chồng phiếu vòng căn dư.</summary>
    public const string CDeck = "C:deck";

    /// <summary>
    /// Nhãn dẫn xuất hoán vị số dự khuyết 1..wl. Tách khỏi <see cref="CDeck"/> chính là điều làm số
    /// dự khuyết độc lập với vị trí trong chồng phiếu — và do đó độc lập với thời điểm bấm.
    /// </summary>
    public const string CWaitlist = "C:waitlist";
}
