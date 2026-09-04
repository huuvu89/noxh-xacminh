using Noxh.XacMinh.Core.Verification;

namespace Noxh.XacMinh.Web.HienThi;

/// <summary>
/// Năm thẻ trên màn hình kết quả. Ba thẻ đầu cho người dân, hai thẻ sau cần khoá hoặc script nên
/// dành cho tổ giám sát — tách ra để người dân không phải cuộn qua cảnh báo khoá bí mật.
/// </summary>
public enum TheKiem
{
    ChongPhieu,
    ChuoiNgauNhien,
    MocNeo,
    KhoBangChung,
    ToGiamSat,
}

/// <summary>Xếp hạng mục vào thẻ theo <see cref="CheckIds"/>, không theo tiêu đề hiển thị.</summary>
public static class NhomHangMuc
{
    public static readonly IReadOnlyList<TheKiem> ThuTu =
    [
        TheKiem.ChongPhieu,
        TheKiem.ChuoiNgauNhien,
        TheKiem.MocNeo,
        TheKiem.KhoBangChung,
        TheKiem.ToGiamSat,
    ];

    public static string Ma(this TheKiem the) => the switch
    {
        TheKiem.ChongPhieu => "chong-phieu",
        TheKiem.ChuoiNgauNhien => "chuoi-ngau-nhien",
        TheKiem.MocNeo => "moc-neo",
        TheKiem.KhoBangChung => "kho-bang-chung",
        _ => "to-giam-sat",
    };

    public static string Ten(this TheKiem the) => the switch
    {
        TheKiem.ChongPhieu => "Chồng phiếu",
        TheKiem.ChuoiNgauNhien => "Chuỗi ngẫu nhiên",
        TheKiem.MocNeo => "Mốc neo chuỗi khối",
        TheKiem.KhoBangChung => "Kho bằng chứng",
        _ => "Tổ giám sát",
    };

    /// <summary>Một câu cho người dân biết thẻ này trả lời câu hỏi gì.</summary>
    public static string MoTa(this TheKiem the) => the switch
    {
        TheKiem.ChongPhieu =>
            "Các lá vé đã niêm phong trước lễ, thứ tự bốc và bảng kết quả: những gì công bố có đúng "
            + "bản đã niêm phong hay không.",
        TheKiem.ChuoiNgauNhien =>
            "Nguồn ngẫu nhiên tạo ra thứ tự bốc: phần máy chủ đã cam kết trước, hạt giống gốc và dấu "
            + "thời gian niêm phong.",
        TheKiem.MocNeo =>
            "Mốc ngẫu nhiên lấy từ sổ cái công khai mà không ai điều khiển được. Cần bạn bấm tra cứu "
            + "một lần.",
        TheKiem.KhoBangChung =>
            "Bản ghi được đẩy lên kho chỉ-ghi ngay trong lễ, đối chiếu với báo cáo công bố sau. Cần "
            + "đọc kho hoặc nạp gói đã tải sẵn.",
        _ =>
            "Dành cho người giữ danh sách hồ sơ gốc và khoá chỉ mục mù. Người dân không cần dùng thẻ này.",
    };

    /// <summary>
    /// Thẻ của một hạng mục; <c>null</c> khi Id lạ. Id có thể mang hậu tố vòng (<c>deck-hash:A1</c>)
    /// nên chỉ xét phần trước dấu hai chấm.
    /// </summary>
    public static TheKiem? TheCua(string checkId)
    {
        var goc = checkId.Split(':', 2)[0];
        return goc switch
        {
            CheckIds.DeckHash or CheckIds.DeckRebuild or CheckIds.DrawLogChain
                or CheckIds.DrawTicketMatch or CheckIds.ResultsHash => TheKiem.ChongPhieu,
            CheckIds.RServerCommit or CheckIds.MasterSeed or CheckIds.FreezeTimestamp => TheKiem.ChuoiNgauNhien,
            CheckIds.MocNeo => TheKiem.MocNeo,
            CheckIds.TrailChuoiLo or CheckIds.TrailKhoangTrong or CheckIds.TrailLuotBoc
                or CheckIds.TrailCamKet or CheckIds.TrailDauChuoi => TheKiem.KhoBangChung,
            CheckIds.DanhSachHoSo => TheKiem.ToGiamSat,
            _ => null,
        };
    }

    /// <summary>Id lạ rơi về thẻ đầu — vẫn hiện, không bao giờ bị giấu.</summary>
    public static TheKiem TheHoacMacDinh(string checkId) => TheCua(checkId) ?? TheKiem.ChongPhieu;

    /// <summary>Trạng thái xấu nhất trong thẻ, cùng luật với kết luận chung; thẻ trống thì không có.</summary>
    public static CheckStatus? TrangThai(IEnumerable<CheckResult> hangMuc)
    {
        var danhSach = hangMuc.ToList();
        if (danhSach.Count == 0) return null;
        if (danhSach.Any(i => i.Status == CheckStatus.KhongDat)) return CheckStatus.KhongDat;
        if (danhSach.Any(i => i.Status == CheckStatus.KhongKiemDuoc)) return CheckStatus.KhongKiemDuoc;
        return CheckStatus.Dat;
    }
}
