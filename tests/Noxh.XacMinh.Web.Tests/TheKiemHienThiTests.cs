using System.Reflection;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Màn hình kết quả chia năm thẻ. Test vẽ component thật: hạng mục nằm đúng thẻ, thẻ mang nhãn
/// trạng thái xấu nhất, thẻ không chọn chỉ ẩn chứ không biến mất, và thẻ đang chọn sống theo phiên.
/// </summary>
public class TheKiemHienThiTests
{
    private static CheckResult HangMuc(string id, CheckStatus status, string ten = "Hạng mục") =>
        new(id, ten, status, "giải thích " + id);

    private static Task<string> Ve(TrinhVe trinh, params CheckResult[] items) =>
        trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = new VerificationReport(items),
        });

    // ── Ánh xạ Id → thẻ: hàng rào cho hạng mục cắm thêm sau ───────────────────────────────

    [Fact]
    public void MoiHangSoTrongCheckIds_DeuCoThe()
    {
        var ids = typeof(CheckIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(ids);
        Assert.All(ids, id => Assert.NotNull(NhomHangMuc.TheCua(id)));
    }

    [Theory]
    [InlineData("deck-hash:A1", TheKiem.ChongPhieu)]
    [InlineData("draw-ticket-match:B", TheKiem.ChongPhieu)]
    [InlineData("results-hash", TheKiem.ChongPhieu)]
    [InlineData("tsa-freeze", TheKiem.ChuoiNgauNhien)]
    [InlineData("master-seed", TheKiem.ChuoiNgauNhien)]
    [InlineData("moc-neo", TheKiem.MocNeo)]
    [InlineData("trail-luot-boc", TheKiem.KhoBangChung)]
    [InlineData("danh-sach-ho-so", TheKiem.ToGiamSat)]
    public void IdCoHauToVong_VanVeDungThe(string id, TheKiem the) =>
        Assert.Equal(the, NhomHangMuc.TheCua(id));

    [Fact]
    public void IdLa_KhongBiGiau_RoiVeTheDau()
    {
        Assert.Null(NhomHangMuc.TheCua("hang-muc-tuong-lai"));
        Assert.Equal(TheKiem.ChongPhieu, NhomHangMuc.TheHoacMacDinh("hang-muc-tuong-lai"));
    }

    // ── Vẽ: hạng mục nằm trong bảng của thẻ mình ──────────────────────────────────────────

    [Fact]
    public async Task HangMuc_NamTrongBangCuaTheMinh()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(
            trinh,
            HangMuc(CheckIds.DeckHash + ":A1", CheckStatus.Dat, "Mã băm chồng phiếu A1"),
            HangMuc(CheckIds.MocNeo, CheckStatus.KhongKiemDuoc, "Mốc neo Bitcoin"));

        var bangChongPhieu = Doan(html, "id=\"bang-chong-phieu\"", "id=\"bang-chuoi-ngau-nhien\"");
        var bangMocNeo = Doan(html, "id=\"bang-moc-neo\"", "id=\"bang-kho-bang-chung\"");

        Assert.Contains("Mã băm chồng phiếu A1", bangChongPhieu);
        Assert.DoesNotContain("Mốc neo Bitcoin", bangChongPhieu);
        Assert.Contains("Mốc neo Bitcoin", bangMocNeo);
    }

    [Fact]
    public async Task NamThe_DeuCoTen_VaCoMoTaChoNguoiDan()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, HangMuc(CheckIds.DeckHash, CheckStatus.Dat));

        foreach (var the in NhomHangMuc.ThuTu)
        {
            Assert.Contains(the.Ten(), html);
            Assert.Contains(the.MoTa(), html);
        }
    }

    // ── Nhãn trạng thái trên thẻ: xấu nhất thắng, bằng chữ chứ không chỉ màu ─────────────

    [Fact]
    public async Task The_MangNhanTrangThaiXauNhat()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(
            trinh,
            HangMuc(CheckIds.DeckHash + ":A1", CheckStatus.Dat),
            HangMuc(CheckIds.DeckHash + ":B", CheckStatus.KhongDat),
            HangMuc(CheckIds.MasterSeed, CheckStatus.Dat),
            HangMuc(CheckIds.MocNeo, CheckStatus.KhongKiemDuoc));

        var nutChongPhieu = Doan(html, "id=\"the-chong-phieu\"", "id=\"the-chuoi-ngau-nhien\"");
        var nutChuoi = Doan(html, "id=\"the-chuoi-ngau-nhien\"", "id=\"the-moc-neo\"");
        var nutMocNeo = Doan(html, "id=\"the-moc-neo\"", "id=\"the-kho-bang-chung\"");
        var nutKho = Doan(html, "id=\"the-kho-bang-chung\"", "id=\"the-to-giam-sat\"");

        Assert.Contains("KHÔNG ĐẠT", nutChongPhieu);
        Assert.Contains("khong-dat", nutChongPhieu);
        Assert.Contains("ĐẠT", nutChuoi);
        Assert.DoesNotContain("KHÔNG", nutChuoi);
        Assert.Contains("CHƯA ĐỦ DỮ LIỆU", nutMocNeo);
        // Thẻ trống không được đeo nhãn nào — không có gì để kết luận.
        Assert.DoesNotContain("ĐẠT", nutKho);
        Assert.DoesNotContain("DỮ LIỆU", nutKho);
    }

    [Fact]
    public void TrangThaiThe_CungLuatVoiKetLuanChung()
    {
        Assert.Null(NhomHangMuc.TrangThai([]));
        Assert.Equal(CheckStatus.Dat, NhomHangMuc.TrangThai([HangMuc("a", CheckStatus.Dat)]));
        Assert.Equal(
            CheckStatus.KhongKiemDuoc,
            NhomHangMuc.TrangThai([HangMuc("a", CheckStatus.Dat), HangMuc("b", CheckStatus.KhongKiemDuoc)]));
        Assert.Equal(
            CheckStatus.KhongDat,
            NhomHangMuc.TrangThai([HangMuc("a", CheckStatus.KhongKiemDuoc), HangMuc("b", CheckStatus.KhongDat)]));
    }

    // ── Thẻ không chọn chỉ ẩn: nội dung vẫn trong DOM, ô nhập không mất chữ ──────────────

    [Fact]
    public async Task MacDinhMoTheChongPhieu_CacTheKhacAnBangHidden()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, HangMuc(CheckIds.MocNeo, CheckStatus.KhongKiemDuoc, "Mốc neo Bitcoin"));

        Assert.Equal(TheKiem.ChongPhieu, trinh.TrangThai.TheDangChon);
        Assert.Contains("aria-selected=\"true\"", Doan(html, "id=\"the-chong-phieu\"", "id=\"the-chuoi-ngau-nhien\""));
        Assert.DoesNotContain("hidden", Doan(html, "<section role=\"tabpanel\" id=\"bang-chong-phieu\"", ">"));
        Assert.Contains("hidden", Doan(html, "<section role=\"tabpanel\" id=\"bang-moc-neo\"", ">"));
        Assert.Contains("Mốc neo Bitcoin", html);
    }

    [Fact]
    public async Task TheDangChon_SongTheoPhien_VeLaiVanGiu()
    {
        await using var trinh = new TrinhVe();
        trinh.TrangThai.TheDangChon = TheKiem.KhoBangChung;

        var html = await Ve(trinh, HangMuc(CheckIds.DeckHash, CheckStatus.Dat));

        Assert.Contains("aria-selected=\"true\"", Doan(html, "id=\"the-kho-bang-chung\"", "id=\"the-to-giam-sat\""));
        Assert.Contains("hidden", Doan(html, "<section role=\"tabpanel\" id=\"bang-chong-phieu\"", ">"));
        Assert.DoesNotContain("hidden", Doan(html, "<section role=\"tabpanel\" id=\"bang-kho-bang-chung\"", ">"));
    }

    [Fact]
    public async Task KetLuanChung_NamTrenThanhThe()
    {
        await using var trinh = new TrinhVe();

        var html = await Ve(trinh, HangMuc(CheckIds.DeckHash, CheckStatus.Dat));

        Assert.True(html.IndexOf("ket-luan", StringComparison.Ordinal) < html.IndexOf("role=\"tablist\"", StringComparison.Ordinal));
    }

    private static string Doan(string html, string tu, string den)
    {
        var batDau = html.IndexOf(tu, StringComparison.Ordinal);
        Assert.True(batDau >= 0, $"Không thấy '{tu}'.");
        var ketThuc = html.IndexOf(den, batDau + tu.Length, StringComparison.Ordinal);
        return ketThuc < 0 ? html[batDau..] : html[batDau..ketThuc];
    }
}
