using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.DanhSach;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #16 — hạng mục 9: bảng danh sách hồ sơ tổ giám sát đang cầm có đúng là danh sách đã khoá và
/// đã được ghim dấu thời gian hay không. Đi qua đúng seam <see cref="Verifier.Verify"/>: bảng dán
/// và khoá chỉ mục mù vào lõi dưới dạng dữ liệu, lõi không đọc file cũng không gọi mạng.
/// </summary>
public class DanhSachHoSoVerificationTests
{
    private static readonly string KhoaDung = GoldenFixture.DanhSachKhoaHex();

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var ketQua = TransparencyJson.Parse(json);
        Assert.True(ketQua.Success, ketQua.ErrorMessage);

        return ketQua.Report!;
    }

    private static CheckResult HangMuc(TransparencyReport report, DanhSachDauVao? danhSach) =>
        Verifier.Verify(new VerificationInput(report, DanhSach: danhSach)).Items
            .Single(i => i.Id == CheckIds.DanhSachHoSo);

    /// <summary><paramref name="khoa"/> bỏ trống = khoá đúng của bảng chuẩn vàng.</summary>
    private static CheckResult KiemBang(string bang, string? khoa = null) =>
        HangMuc(Golden(), new DanhSachDauVao(bang, khoa ?? KhoaDung));

    private static string BangChuanVang() => GoldenFixture.DanhSach();

    /// <summary>Sửa đúng một ô của bảng — cách duy nhất trung thực để dựng bảng lệch.</summary>
    private static string SuaO(string bang, int dong, int cot, Func<string, string> sua)
    {
        var dongs = bang.Split('\n');
        var o = dongs[dong].Split('\t');
        o[cot] = sua(o[cot]);
        dongs[dong] = string.Join('\t', o);

        return string.Join('\n', dongs);
    }

    // ── AC1: dán bảng + khoá cho ra kết luận khớp hay không khớp ─────────────────────────

    [Fact]
    public void AC1_BangDaKhoaVaKhoaDung_Dat()
    {
        var ketQua = KiemBang(BangChuanVang());

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
        Assert.Equal(GoldenFixture.DanhSachListHash(), ketQua.Actual);
    }

    [Fact]
    public void AC1_ChuaDanGiCa_KhongKiemDuoc()
    {
        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(Golden(), null).Status);
    }

    [Fact]
    public void AC1_CoBangNhungChuaCoKhoa_KhongKiemDuoc_ChuKhongPhaiKhongDat()
    {
        Assert.Equal(CheckStatus.KhongKiemDuoc, KiemBang(BangChuanVang(), khoa: "  ").Status);
    }

    /// <summary>
    /// Mã băm danh sách nằm trong chuỗi ĐÃ ĐƯỢC ĐÓNG DẤU của mốc cam kết — báo cáo công khai không
    /// công bố trường <c>listHash</c> riêng. Không lấy được từ đó thì cả hạng mục vô nghĩa.
    /// </summary>
    [Fact]
    public void AC1_LayMaBamDaCongBoTuChuoiDongDauMocCamKet()
    {
        Assert.Equal(GoldenFixture.DanhSachListHash(), KiemBang(BangChuanVang()).Expected);
    }

    [Fact]
    public void AC1_BaoCaoCongBoThangListHash_CungDungDeDoiChieu()
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        root["dauThoiGian"] = new JsonArray();
        root["listHash"] = GoldenFixture.DanhSachListHash();

        var ketQua = HangMuc(Parse(root.ToJsonString()), new DanhSachDauVao(BangChuanVang(), KhoaDung));

        Assert.Equal(CheckStatus.Dat, ketQua.Status);
    }

    [Fact]
    public void AC1_KhongDauNaoCongBoMaBamDanhSach_KhongKiemDuoc()
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        root["dauThoiGian"] = new JsonArray();

        var ketQua = HangMuc(Parse(root.ToJsonString()), new DanhSachDauVao(BangChuanVang(), KhoaDung));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
    }

    /// <summary>Khoá sai không phải chuyện chuẩn hoá — mọi chỉ mục mù đều khác, không gợi ý gì cả.</summary>
    [Fact]
    public void AC1_KhoaSai_KhongDat()
    {
        var ketQua = KiemBang(BangChuanVang(), khoa: new string('0', 64));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.DoesNotContain("khớp nếu", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC1_ThieuMotHoSo_KhongDat()
    {
        var dongs = BangChuanVang().Split('\n').ToList();
        dongs.RemoveAt(7);

        Assert.Equal(CheckStatus.KhongDat, KiemBang(string.Join('\n', dongs)).Status);
    }

    // ── AC4: lệch đúng một khoảng trắng cuối họ tên ⇒ nói đúng nguyên nhân ────────────────

    [Fact]
    public void AC4_ThuaMotKhoangTrangCuoiHoTen_GoiYDungNguyenNhan()
    {
        var ketQua = KiemBang(SuaO(BangChuanVang(), 7, 1, ten => ten + " "));

        // Không phải ĐẠT (bảng đang cầm KHÁC bản đã khoá từng byte) nhưng cũng chưa đủ để nói dữ
        // liệu bị sửa: đây gần như chắc chắn là vết sao chép bảng.
        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("khớp nếu", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("khoảng trắng", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("họ tên", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC4_DauTiengVietGhiKieuToHopRoi_GoiYChuanHoaDau()
    {
        var bang = BangChuanVang().Normalize(System.Text.NormalizationForm.FormD);
        var ketQua = KiemBang(bang);

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("khớp nếu", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dấu tiếng Việt", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Nhóm ghi tắt hay ghi nhãn đầy đủ đều là cùng một nhóm — không được thành KHÔNG ĐẠT.</summary>
    [Fact]
    public void AC4_NhomGhiNhanDayDu_VanDat()
    {
        var bang = BangChuanVang()
            .Replace("\tU6\n", "\tU6 – Đối tượng thường\n", StringComparison.Ordinal)
            .Replace("\tU1\n", "\tNgười có công\n", StringComparison.Ordinal);

        Assert.Equal(CheckStatus.Dat, KiemBang(bang).Status);
    }

    // ── AC5: đổi một số định danh ⇒ KHÔNG khớp, KHÔNG gợi ý chuẩn hoá ─────────────────────

    [Fact]
    public void AC5_DoiMotSoDinhDanh_KhongDat_VaKhongGoiYChuanHoa()
    {
        var ketQua = KiemBang(SuaO(BangChuanVang(), 7, 2, so => so[..^1] + (so[^1] == '9' ? '8' : '9')));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.DoesNotContain("khớp nếu", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC5_DoiMotHoTen_KhongDat_VaKhongGoiYChuanHoa()
    {
        var ketQua = KiemBang(SuaO(BangChuanVang(), 7, 1, _ => "Nguyễn Văn Giả"));

        Assert.Equal(CheckStatus.KhongDat, ketQua.Status);
        Assert.DoesNotContain("khớp nếu", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    // ── AC6: số hồ sơ đọc được + cơ cấu theo nhóm để đối chiếu biên bản ───────────────────

    [Fact]
    public void AC6_HienSoHoSoDocDuoc_VaCoCauTheoNhom()
    {
        var ketQua = KiemBang(BangChuanVang());

        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("Số hồ sơ đọc được") && m.Value == "40");
        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("U2") && m.Value == "6");
        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("U6") && m.Value == "28");
        // Cơ cấu phải đọc được cả khi không mở chế độ chuyên sâu — đây là số để đối chiếu biên bản.
        Assert.Contains("40 hồ sơ", ketQua.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC6_SoLieuVanHienKhiKhongKhop()
    {
        var ketQua = KiemBang(BangChuanVang(), khoa: new string('0', 64));

        Assert.Contains(ketQua.Metrics, m => m.Label.Contains("Số hồ sơ đọc được") && m.Value == "40");
    }

    // ── Đọc bảng dán: định dạng và lỗi ────────────────────────────────────────────────────

    [Fact]
    public void BangKieuMarkdown_CungDocDuoc()
    {
        var bang = string.Join('\n', BangChuanVang()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => "| " + string.Join(" | ", d.Split('\t')) + " |"));

        Assert.Equal(CheckStatus.Dat, KiemBang(bang).Status);
    }

    [Fact]
    public void DongKhongDocDuocNhom_KhongKiemDuoc_VaChiRoDongNao()
    {
        var ketQua = KiemBang(SuaO(BangChuanVang(), 7, 3, _ => "nhóm lạ"));

        Assert.Equal(CheckStatus.KhongKiemDuoc, ketQua.Status);
        Assert.Contains("dòng 8", ketQua.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DongThieuCot_KhongKiemDuoc_ChuKhongPhaiKhongDat()
    {
        var dongs = BangChuanVang().Split('\n');
        dongs[7] = "HS007\tVũ Hữu Mai";

        Assert.Equal(CheckStatus.KhongKiemDuoc, KiemBang(string.Join('\n', dongs)).Status);
    }

    [Fact]
    public void BangRong_KhongKiemDuoc()
    {
        Assert.Equal(CheckStatus.KhongKiemDuoc, KiemBang("   \n  \n").Status);
    }

    /// <summary>
    /// Chuỗi đem băm mang toàn bộ họ tên + số định danh của cả danh sách: nó KHÔNG được đi vào kết
    /// quả kiểm, kể cả ở chế độ chuyên sâu — công cụ này để đối chiếu mã băm, không phải để bày dữ
    /// liệu cá nhân ra màn hình rồi lọt vào ảnh chụp hay báo lỗi.
    /// </summary>
    [Fact]
    public void KetQuaKiem_KhongMangTheoChuoiDemBam_VaKhongMangHoTen()
    {
        var ketQua = KiemBang(BangChuanVang());

        Assert.Null(ketQua.Preimage);
        foreach (var giaTri in new[] { ketQua.Explanation, ketQua.Expected, ketQua.Actual }
                     .Concat(ketQua.Metrics.Select(m => m.Value)))
            Assert.DoesNotContain("Vũ Hữu Mai", giaTri ?? string.Empty, StringComparison.Ordinal);
    }
}
