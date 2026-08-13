using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #18 — trail bằng chứng trên kho lưu trữ chỉ-ghi, đi qua đúng seam <see cref="Verifier.Verify"/>.
///
/// Lớp bằng chứng này nằm <b>ngoài</b> cơ sở dữ liệu: trong lúc lễ chạy, từng lô bản ghi được đẩy
/// thẳng lên kho mà chính máy chủ bốc thăm cũng không xoá được. Việc đọc kho (ký request, gọi mạng)
/// là của vỏ UI; ở đây các lô đã đọc đi vào lõi dưới dạng dữ liệu, nên test tất định, không cần mạng.
/// </summary>
public class TrailBangChungVerificationTests
{
    private static readonly DateTime BatDau = new(2026, 8, 15, 7, 30, 0, DateTimeKind.Utc);

    private const string MaTienTrinh = "a1b2c3d4";

    private static TransparencyReport Golden()
    {
        var ketQua = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);

        return ketQua.Report!;
    }

    /// <summary>
    /// Dựng một chuỗi lô đúng cách backend đẩy lên: mỗi lô trỏ về lô upload <b>thành công</b> liền
    /// trước. Số thứ tự lô nào không truyền vào thì coi như lô đó upload hỏng và bị bỏ — chuỗi vẫn
    /// liền, đúng như trên bucket thật.
    /// </summary>
    private static List<DoiTuongKho> Chuoi(params long[] soLo)
    {
        var doiTuong = new List<DoiTuongKho>();
        string? keyTruoc = null;
        string? shaTruoc = null;

        foreach (var so in soLo)
        {
            var luc = BatDau.AddSeconds(2 * so);
            var noiDung = DungLoTrail.NoiDung(
                so, MaTienTrinh, luc, [DungLoTrail.VeDaBoc($"HS{so:D3}", (int)so)], keyTruoc, shaTruoc);

            keyTruoc = DungLoTrail.Key(so, MaTienTrinh, luc);
            shaTruoc = DungLoTrail.Sha256Hex(noiDung);
            doiTuong.Add(new DoiTuongKho(keyTruoc, noiDung));
        }

        return doiTuong;
    }

    private static VerificationReport Kiem(KhoBangChung? kho) =>
        Verifier.Verify(new VerificationInput(Golden(), Kho: kho));

    private static CheckResult HangMuc(KhoBangChung? kho, string id) =>
        Kiem(kho).Items.Single(i => i.Id == id);

    private static CheckResult ChuoiMocXich(KhoBangChung? kho) => HangMuc(kho, CheckIds.TrailChuoiLo);

    private static CheckResult KhoangTrong(KhoBangChung? kho) => HangMuc(kho, CheckIds.TrailKhoangTrong);

    private static KhoBangChung DaDoc(CheDoDocKho cheDo, IReadOnlyList<DoiTuongKho> doiTuong) =>
        KhoBangChung.Doc(cheDo, doiTuong, "s3.thu-nghiem.vn/bang-chung-noxh");

    // ── AC1: liệt kê được các lô ở cả hai chế độ ────────────────────────────────────────────

    [Fact]
    public void AC1_DocBangKhoaChiDoc_LietKeDuLoVaNoiRoDaDocBangKhoa()
    {
        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.KhoaChiDoc, Chuoi(1, 2, 3)));

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Contains(ketQua.Metrics, m => m.Value == "3" && m.Label.Contains("lô", StringComparison.Ordinal));
        Assert.Contains(ketQua.Metrics, m => m.Value.Contains("khoá chỉ-đọc", StringComparison.Ordinal));
    }

    [Fact]
    public void AC1_SauLeDocAnDanh_VanLietKeDuLo_VaNoiRoDocAnDanh()
    {
        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.AnDanh, Chuoi(1, 2, 3)));

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Contains(ketQua.Metrics, m => m.Value.Contains("ẩn danh", StringComparison.Ordinal));
    }

    [Fact]
    public void AC1_KhoDocDuocNhungChuaCoLoNao_KhongKiemDuoc_ChuKhongPhaiKhongDat()
    {
        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.KhoaChiDoc, []));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
    }

    // ── AC2: chuỗi móc xích giữa các lô ─────────────────────────────────────────────────────

    [Fact]
    public void AC2_ThieuMotLoOGiua_NeuRoLoBiGiauVaLoToCao_KhongDat()
    {
        var day = Chuoi(1, 2, 3);
        var biGiau = day[1];
        day.RemoveAt(1);

        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.KhoaChiDoc, day));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains(biGiau.Key, ketQua.Explanation, StringComparison.Ordinal);
        Assert.Contains(day[1].Key, ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_NoiDungMotLoBiSua_MaBamMocXichKhongKhop_KhongDat()
    {
        var day = Chuoi(1, 2);
        day[0] = day[0] with { NoiDung = [.. day[0].NoiDung, (byte)'\n'] };

        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.KhoaChiDoc, day));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.Contains(day[0].Key, ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_ChiCoMotLo_ChuaCoMatXichNaoDeKiem_KhongKiemDuoc()
    {
        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.KhoaChiDoc, Chuoi(1)));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
    }

    // ── AC3: khoảng trống số thứ tự lô + giới hạn cắt cụt phần đuôi ─────────────────────────

    [Fact]
    public void AC3_KhoangTrongSoThuTuLo_CanhBaoVaNeuDichSoLoConThieu()
    {
        var ketQua = KhoangTrong(DaDoc(CheDoDocKho.KhoaChiDoc, Chuoi(1, 2, 5)));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("3", ketQua.Actual ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("4", ketQua.Actual ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_LienMachTuLoDauTien_VanNoiRoPhanDuoiBiCatCutSeKhongPhatHienDuoc()
    {
        var ketQua = KhoangTrong(DaDoc(CheDoDocKho.KhoaChiDoc, Chuoi(1, 2, 3)));

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Contains("cắt cụt", ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_ThieuLoDauTien_CungLaKhoangTrong_KhongKiemDuoc()
    {
        var ketQua = KhoangTrong(DaDoc(CheDoDocKho.KhoaChiDoc, Chuoi(2, 3)));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("1", ketQua.Actual ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_LietKeChuaHet_KhongKetLuanGiVeKhoangTrong()
    {
        var kho = KhoBangChung.Doc(CheDoDocKho.AnDanh, Chuoi(1, 2, 3), "kho thử", daLietKeHet: false);

        Assert.Equal(CheckStatus.KhongKiemDuoc, KhoangTrong(kho).Status);
    }

    // ── AC6: kho không truy cập được ⇒ KHÔNG KIỂM ĐƯỢC, không ảnh hưởng hạng mục khác ───────

    [Fact]
    public void AC6_ChuaDocKho_HaiHangMucTrailDeuKhongKiemDuoc()
    {
        Assert.Equal(CheckStatus.KhongKiemDuoc, ChuoiMocXich(null).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, KhoangTrong(null).Status);
    }

    [Fact]
    public void AC6_KhoTuChoiTruyCap_KhongKiemDuoc_VaNoiLaiLyDoChoNguoiKiem()
    {
        var kho = KhoBangChung.Hong(CheDoDocKho.KhoaChiDoc, "HTTP 403 AccessDenied", "s3.thu-nghiem.vn");

        var ketQua = ChuoiMocXich(kho);

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("403", ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC6_KhoHongHayKhongCoKho_CacHangMucKhacGiuNguyenKetLuan()
    {
        var khongCoKho = Kiem(null).Items.Where(i => !i.Id.StartsWith("trail-", StringComparison.Ordinal)).ToList();
        var khoHong = Kiem(KhoBangChung.Hong(CheDoDocKho.AnDanh, "trình duyệt chặn (CORS)"))
            .Items.Where(i => !i.Id.StartsWith("trail-", StringComparison.Ordinal)).ToList();
        var khoTot = Kiem(DaDoc(CheDoDocKho.KhoaChiDoc, Chuoi(1, 2, 3)))
            .Items.Where(i => !i.Id.StartsWith("trail-", StringComparison.Ordinal)).ToList();

        Assert.Equal(khongCoKho, khoHong);
        Assert.Equal(khongCoKho, khoTot);
    }

    // ── Đọc được nội dung lô: bản ghi trong lô là thứ vé sau đối chiếu với báo cáo ──────────

    [Fact]
    public void DocDuocBanGhiTrongLo_GiuNguyenLoaiVaPayloadDeDoiChieuVeSau()
    {
        var kho = DaDoc(CheDoDocKho.KhoaChiDoc, Chuoi(1, 2));

        Assert.All(kho.Lo, lo => Assert.Null(lo.Loi));
        Assert.All(kho.Lo, lo => Assert.Equal("TICKET_DRAWN", Assert.Single(lo.BanGhi).Loai));
        Assert.Contains("HS001", kho.Lo[0].BanGhi[0].PayloadJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Lô <b>có</b> trong danh sách kho nhưng tải nội dung không được thì mắt xích đó chưa kiểm được
    /// — vu cho ban tổ chức giấu bằng chứng vì mạng của chính người kiểm rớt là điều không được phép.
    /// </summary>
    [Fact]
    public void LoLietKeDuocNhungTaiKhongDuoc_KhongKiemDuoc_ChuKhongVuOanKhongDat()
    {
        var day = Chuoi(1, 2);
        day[0] = new DoiTuongKho(day[0].Key, [], "tải nội dung không được: HTTP 500");

        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.KhoaChiDoc, day));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("HTTP 500", ketQua.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    /// Danh sách còn dở thì "không thấy lô trước trong danh sách" chỉ là hệ quả của việc đọc thiếu,
    /// chưa phải bằng chứng có người lấy bớt lô.
    /// </summary>
    [Fact]
    public void ChuaLietKeHetMaThieuLoTruoc_KhongKiemDuoc_ChuKhongVuOanKhongDat()
    {
        var day = Chuoi(1, 2, 3);
        day.RemoveAt(1);

        var kho = KhoBangChung.Doc(CheDoDocKho.AnDanh, day, "kho thử", daLietKeHet: false);

        Assert.Equal(CheckStatus.KhongKiemDuoc, ChuoiMocXich(kho).Status);
    }

    [Fact]
    public void LoKhongDocDuoc_ChuoiMocXichDungLai_KhongKiemDuoc_ChuKhongVuOanKhongDat()
    {
        var day = Chuoi(1, 2);
        day[1] = day[1] with { NoiDung = "{ đây không phải JSONL }"u8.ToArray() };

        var ketQua = ChuoiMocXich(DaDoc(CheDoDocKho.KhoaChiDoc, day));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains(day[1].Key, ketQua.Explanation, StringComparison.Ordinal);
    }
}
