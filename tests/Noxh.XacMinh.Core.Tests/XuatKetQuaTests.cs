using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.Core.XuatKetQua;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #20 — xuất kết quả kiểm ra file. Bản xuất là thứ được trích dẫn, gửi đi, lưu hồ sơ; nó phải
/// nói đủ và nói đúng những gì màn hình nói, kể cả những hạng mục không kết luận được.
/// </summary>
public class XuatKetQuaTests
{
    private const string HexDai = "3f2a91c4b7e8d0562a1b9c8d7e6f5a4b3c2d1e0f9a8b7c6d5e4f3a2b1c0d9e8f";

    private static readonly DateTimeOffset LucKiem = new(2026, 8, 13, 3, 12, 45, TimeSpan.Zero);

    private static ThongTinBanXuat ThongTin(params DauVaoDaKiem[] dauVao) => new(
        LucKiem,
        PhienBanCongCu.HienTai,
        dauVao.Length > 0 ? dauVao : [new DauVaoDaKiem("Báo cáo minh bạch (bao-cao.json)", HexDai)],
        "Dự án Chung cư X — bao-cao.json");

    private static VerificationReport BaoCaoBaTrangThai() => new(
    [
        new CheckResult(
            "hang-muc-dat", "Mã băm chồng phiếu A1", CheckStatus.Dat,
            "Nội dung vé công bố khớp mã băm đã niêm phong.",
            Expected: HexDai, Actual: HexDai, Preimage: "0\tVE-0001\n1\tVE-0002\n")
        {
            Metrics = [new CheckMetric("Số vé công bố", "892")],
        },
        new CheckResult(
            "hang-muc-khong-dat", "Mã băm chồng phiếu B", CheckStatus.KhongDat,
            "Mã băm tính lại KHÁC mã băm đã niêm phong.",
            Expected: HexDai, Actual: HexDai.Replace('3', '4')),
        new CheckResult(
            "hang-muc-khong-kiem-duoc", "Mã băm chồng phiếu C", CheckStatus.KhongKiemDuoc,
            "Chồng phiếu này chưa công bố nội dung vé, nên chưa có dữ liệu để đối chiếu."),
    ]);

    private static VerificationReport BaoCaoChuanVang()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return Verifier.Verify(new VerificationInput(nap.Report!));
    }

    // ── AC2: file chứa kết luận từng hạng mục kèm giá trị kỳ vọng và tính được ───────────

    [Fact]
    public void AC2_MoiHangMuc_CoTieuDe_KetLuan_VaCauGiaiThich()
    {
        var baoCao = BaoCaoBaTrangThai();

        var vanBan = BanXuatVanBan.Dung(baoCao, ThongTin());

        Assert.All(baoCao.Items, i =>
        {
            Assert.Contains(i.Title, vanBan, StringComparison.Ordinal);
            Assert.Contains($"{i.Title} — {i.Status.Nhan()}", vanBan, StringComparison.Ordinal);
            Assert.Contains(i.Explanation, vanBan, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AC2_MoiHangMuc_CoGiaTriKyVongVaTinhDuoc()
    {
        var baoCao = BaoCaoBaTrangThai();

        var vanBan = BanXuatVanBan.Dung(baoCao, ThongTin());

        Assert.All(baoCao.Items.Where(i => i.Expected is not null),
            i => Assert.Contains(i.Expected!, vanBan, StringComparison.Ordinal));
        Assert.All(baoCao.Items.Where(i => i.Actual is not null),
            i => Assert.Contains(i.Actual!, vanBan, StringComparison.Ordinal));
    }

    [Fact]
    public void AC2_HangMucChuaCoGiaTri_NoiRoLaChuaCo_KhongBoTrongDongDo()
    {
        var thieu = new VerificationReport([BaoCaoBaTrangThai().Items[2]]);

        var vanBan = BanXuatVanBan.Dung(thieu, ThongTin());

        Assert.Contains("Giá trị kỳ vọng: (không công bố)", vanBan, StringComparison.Ordinal);
        Assert.Contains("Giá trị tính được: (chưa tính được)", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_SoLieuThoVaPreimage_CungDiVaoFile_DeNguoiKhacTinhLai()
    {
        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin());

        Assert.Contains("Số vé công bố", vanBan, StringComparison.Ordinal);
        Assert.Contains("892", vanBan, StringComparison.Ordinal);
        // Preimage đi nguyên vẹn: cắt bớt là lấy mất đúng thứ người kiểm cần để băm lại.
        Assert.Contains("0\tVE-0001\n1\tVE-0002\n", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_BaoCaoChuanVangThat_KhongHangMucNaoBiRotLai()
    {
        var baoCao = BaoCaoChuanVang();

        var vanBan = BanXuatVanBan.Dung(baoCao, ThongTin());

        Assert.All(baoCao.Items, i => Assert.Contains($"{i.Title} — {i.Status.Nhan()}", vanBan, StringComparison.Ordinal));
        Assert.All(baoCao.Items.Where(i => i.Expected is not null),
            i => Assert.Contains(i.Expected!, vanBan, StringComparison.Ordinal));
    }

    [Fact]
    public void AC2_KetLuanChung_NamNgayDauFile()
    {
        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin());

        var ketLuan = vanBan.IndexOf(CheckStatus.KhongDat.Nhan(), StringComparison.Ordinal);
        var hangMucDau = vanBan.IndexOf("Mã băm chồng phiếu A1", StringComparison.Ordinal);

        Assert.InRange(ketLuan, 0, hangMucDau);
    }

    // ── AC3: file ghi phiên bản công cụ và mã băm dữ liệu đầu vào ────────────────────────

    [Fact]
    public void AC3_FileGhiPhienBanCongCuDaDung()
    {
        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin());

        Assert.Contains(PhienBanCongCu.HienTai, vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_PhienBanCongCu_CoCaSoPhienBanVaDauVetBanDung()
    {
        // Số phiên bản một mình không phân biệt được hai bản dựng khác nhau của cùng một số.
        Assert.Matches(@"^noxh-xacminh \d+\.\d+\.\d+.*\(bản dựng [0-9a-f]{16}\)$", PhienBanCongCu.HienTai);
    }

    [Fact]
    public void AC3_FileGhiMaBamCuaMoiDauVaoDaKiem()
    {
        var danhMuc = new DauVaoDaKiem("Danh mục căn (bản nhúng sẵn)", HexDai.Replace('3', '5'));
        var baoCao = new DauVaoDaKiem("Báo cáo minh bạch (bao-cao.json)", HexDai);

        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin(baoCao, danhMuc));

        Assert.Contains(baoCao.Ten, vanBan, StringComparison.Ordinal);
        Assert.Contains(baoCao.MaBamSha256, vanBan, StringComparison.Ordinal);
        Assert.Contains(danhMuc.Ten, vanBan, StringComparison.Ordinal);
        Assert.Contains(danhMuc.MaBamSha256, vanBan, StringComparison.Ordinal);
        Assert.Contains("SHA-256", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_FileGhiThoiDiemKiem_DangUtcKhongNhapNhang()
    {
        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin());

        Assert.Contains("2026-08-13T03:12:45Z", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_ThoiDiemKiemGioDiaPhuong_VanGhiTheoUtc()
    {
        var giaHan = new ThongTinBanXuat(
            new DateTimeOffset(2026, 8, 13, 10, 12, 45, TimeSpan.FromHours(7)),
            PhienBanCongCu.HienTai,
            [new DauVaoDaKiem("Báo cáo minh bạch (bao-cao.json)", HexDai)]);

        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), giaHan);

        Assert.Contains("2026-08-13T03:12:45Z", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_TenFile_MangTheoThoiDiemKiem_DeKhongDeLenNhau()
    {
        Assert.Equal("ket-qua-kiem-20260813T031245Z.md", BanXuatVanBan.TenFile(ThongTin()));
    }

    [Fact]
    public void AC3_NguonBaoCao_DiVaoFile()
    {
        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin());

        Assert.Contains("Dự án Chung cư X — bao-cao.json", vanBan, StringComparison.Ordinal);
    }

    // ── AC4: hạng mục CHƯA ĐỦ DỮ LIỆU xuất hiện đúng trạng thái đó, không bị lược mất ────

    [Fact]
    public void AC4_HangMucKhongKiemDuoc_CoMatTrongFile_DungTrangThaiDo()
    {
        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin());

        Assert.Contains("Mã băm chồng phiếu C — CHƯA ĐỦ DỮ LIỆU", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_KetLuanChungKhongKiemDuoc_KhongBiKeoThanhDat()
    {
        var chuaDu = new VerificationReport(
            [BaoCaoBaTrangThai().Items[0], BaoCaoBaTrangThai().Items[2]]);

        var vanBan = BanXuatVanBan.Dung(chuaDu, ThongTin());

        Assert.Equal(CheckStatus.KhongKiemDuoc, chuaDu.Overall);
        Assert.Contains("Kết luận chung: CHƯA ĐỦ DỮ LIỆU", vanBan, StringComparison.Ordinal);
        Assert.Contains("Thiếu dữ liệu không có nghĩa là đạt", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_BangTongHop_DemDuBaTrangThai()
    {
        var vanBan = BanXuatVanBan.Dung(BaoCaoBaTrangThai(), ThongTin());

        Assert.Contains("1 ĐẠT", vanBan, StringComparison.Ordinal);
        Assert.Contains("1 KHÔNG ĐẠT", vanBan, StringComparison.Ordinal);
        Assert.Contains("1 CHƯA ĐỦ DỮ LIỆU", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_BaoCaoChuanVangThat_DemDungSoHangMucKhongKiemDuoc()
    {
        var baoCao = BaoCaoChuanVang();
        var soChuaKiem = baoCao.Items.Count(i => i.Status == CheckStatus.KhongKiemDuoc);

        var vanBan = BanXuatVanBan.Dung(baoCao, ThongTin());

        Assert.True(soChuaKiem > 0, "Fixture chuẩn vàng không nạp trail/block nên phải có hạng mục chưa đủ dữ liệu.");
        Assert.Contains($"{soChuaKiem} CHƯA ĐỦ DỮ LIỆU", vanBan, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_BaoCaoRong_VanRaFileNoiRoLaChuaKiemDuocGi()
    {
        var vanBan = BanXuatVanBan.Dung(new VerificationReport([]), ThongTin());

        Assert.Contains("Kết luận chung: CHƯA ĐỦ DỮ LIỆU", vanBan, StringComparison.Ordinal);
    }

    // ── Dữ liệu lạ trong file người dùng thả vào không được phá cấu trúc bản xuất ────────

    [Fact]
    public void GiaTriChuaDauHuyen_VanNamGonTrongMotDoanMa()
    {
        Assert.Equal("`abc`", Markdown.Ma("abc"));
        Assert.Equal("``a`b``", Markdown.Ma("a`b"));
        Assert.Equal("```a``b```", Markdown.Ma("a``b"));
        // Dấu huyền ở mép phải có khoảng đệm, kẻo dính vào hàng rào thành hàng rào dài hơn.
        Assert.Equal("`` `abc ``", Markdown.Ma("`abc"));
    }

    [Fact]
    public void GiaTriNhieuDong_BiEpVeMotDong_KhongLamGayDanhSach()
    {
        Assert.Equal("`a b`", Markdown.Ma("a\nb"));
        Assert.Equal("`a b`", Markdown.Ma("a\r\nb"));
    }

    [Fact]
    public void PreimageChuaHangRaoMa_DuocRaoBangHangRaoDaiHon()
    {
        var khoi = Markdown.Khoi("dòng 1\n```\ndòng 3", "text");

        Assert.StartsWith("````text\n", khoi, StringComparison.Ordinal);
        Assert.EndsWith("\n````", khoi, StringComparison.Ordinal);
        Assert.Contains("dòng 1\n```\ndòng 3", khoi, StringComparison.Ordinal);
    }

    [Fact]
    public void TieuDeHangMucLa_KhongDuocTuBienThanhTieuDeMarkdownKhac()
    {
        var la = new VerificationReport([
            new CheckResult("la", "## Chèn tiêu đề\nvà xuống dòng", CheckStatus.Dat, "Giải thích."),
        ]);

        var vanBan = BanXuatVanBan.Dung(la, ThongTin());

        Assert.Contains("## 1. ## Chèn tiêu đề và xuống dòng — ĐẠT", vanBan, StringComparison.Ordinal);
        Assert.DoesNotContain("\n## Chèn", vanBan, StringComparison.Ordinal);
    }

    // ── Bản xuất phải tải được bằng đúng trang tĩnh, không cần công cụ ngoài ─────────────

    [Fact]
    public void BanXuatCuaBaoCaoThat_DuNhoDeTaiBangDuongDanDuLieu()
    {
        // Trang tải file bằng data: URL (không có JS, không có máy chủ). Trình duyệt còn giới hạn độ
        // dài, nên bản xuất của một dự án thật phải ở xa ngưỡng đó.
        var vanBan = BanXuatVanBan.Dung(BaoCaoChuanVang(), ThongTin());

        var soByteBase64 = ((System.Text.Encoding.UTF8.GetByteCount(vanBan) + 2) / 3) * 4;

        Assert.InRange(soByteBase64, 1, 4 * 1024 * 1024);
    }

    [Fact]
    public void HaiLanXuatCungDuLieu_RaCungMotVanBan()
    {
        var baoCao = BaoCaoChuanVang();

        Assert.Equal(
            BanXuatVanBan.Dung(baoCao, ThongTin()),
            BanXuatVanBan.Dung(BaoCaoChuanVang(), ThongTin()));
    }
}
