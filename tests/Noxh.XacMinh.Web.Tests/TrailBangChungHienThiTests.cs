using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Noxh.XacMinh.Web.Components;
using Noxh.XacMinh.Web.HienThi;
using Xunit;

namespace Noxh.XacMinh.Web.Tests;

/// <summary>
/// Vé #18 — phần thuộc về vỏ giao diện: việc đọc kho bằng chứng (ký request, gọi mạng) nằm ở đây,
/// và nó chỉ được chạy khi người kiểm bấm. Khoá chỉ-đọc người kiểm dán vào chỉ sống trong bộ nhớ
/// tab, và xoá được ngay.
/// </summary>
public class TrailBangChungHienThiTests
{
    private static readonly ThongSoKho KhoThu = new("https://s3.thu-nghiem.vn", "bang-chung", "hcm", "trail/");

    private static Task<string> VeKhung(TrinhVe trinh) =>
        trinh.Ve<TrailBangChung>(new Dictionary<string, object?>());

    private static TrangThaiKho TrangThai(Func<KhoBangChung> traLoi) =>
        new((_, _, _) => Task.FromResult(traLoi()));

    private static KhoBangChung BaLo() =>
        KhoBangChung.Doc(CheDoDocKho.KhoaChiDoc, DungLoTrailWeb.Chuoi(3), KhoThu.MoTa);

    // ── AC1: đọc kho là việc của vỏ, và chỉ chạy khi người kiểm bấm ─────────────────────────

    [Fact]
    public async Task AC1_ChuaBamDoc_KhungKhongTuGoiMang_ChiMoiNut()
    {
        // TrinhVe cấp một hàm đọc kho ném ngoại lệ: chỉ cần vẽ mà lỡ gọi mạng là test đỏ.
        await using var trinh = new TrinhVe();

        var html = await VeKhung(trinh);

        Assert.Contains("Đọc trail bằng chứng", html, StringComparison.Ordinal);
        Assert.Contains("chưa đọc", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC1_DocXongBangKhoaChiDoc_KhungNoiRoDocDuocBaoNhieuLo()
    {
        var kho = TrangThai(BaLo);
        await kho.Doc(KhoThu, new KhoaKho("AKIDEXAMPLE", "bimat"));

        await using var trinh = new TrinhVe(kho: kho);

        var html = await VeKhung(trinh);

        Assert.Contains("3 lô", html, StringComparison.Ordinal);
        Assert.Contains(KhoThu.MoTa, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC1_DocXong_KetLuanTrailDiVaoBaoCaoKiem()
    {
        var kho = TrangThai(BaLo);
        await kho.Doc(KhoThu, null);

        await using var trinh = new TrinhVe(kho: kho);
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);

        var html = await trinh.Ve<KetQuaKiem>(new Dictionary<string, object?>
        {
            ["BaoCao"] = Verifier.Verify(new VerificationInput(nap.Report!, Kho: kho.DaDoc)),
            ["Nguon"] = "báo cáo.json",
        });

        Assert.Contains("chuỗi móc xích", html, StringComparison.Ordinal);
        Assert.Contains("cắt cụt", html, StringComparison.Ordinal);
    }

    // ── AC4: khoá chỉ-đọc — cảnh báo trước, và xoá được khỏi bộ nhớ ─────────────────────────

    [Fact]
    public async Task AC4_KhungNoiRoChiNhanKhoaChiDoc_TruocKhiMoODanKhoa()
    {
        await using var trinh = new TrinhVe();

        var html = await VeKhung(trinh);

        Assert.Contains("chỉ-đọc", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Khoá bí mật", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC4_DaHieuCanhBao_MoODanKhoaChiDoc()
    {
        var kho = TrangThai(BaLo);
        kho.HieuCanhBao();

        await using var trinh = new TrinhVe(kho: kho);

        var html = await VeKhung(trinh);

        Assert.Contains("Khoá bí mật", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC4_XoaKhoiBoNho_QuenCaLoDaDoc()
    {
        var kho = TrangThai(BaLo);
        await kho.Doc(KhoThu, new KhoaKho("AKIDEXAMPLE", "bimat"));
        Assert.NotNull(kho.DaDoc);

        kho.Xoa();

        Assert.Null(kho.DaDoc);
        Assert.Equal(BuocDocKho.ChuaDoc, kho.Buoc);
    }

    /// <summary>Khoá đi thẳng từ ô nhập vào hàm ký; trạng thái phiên không giữ lại khoá nào cả.</summary>
    [Fact]
    public async Task AC4_TrangThaiPhien_KhongGiuLaiKhoaNguoiKiemDanVao()
    {
        KhoaKho? khoaDaDung = null;
        var kho = new TrangThaiKho((_, khoa, _) =>
        {
            khoaDaDung = khoa;
            return Task.FromResult(BaLo());
        });

        await kho.Doc(KhoThu, new KhoaKho("AKIDEXAMPLE", "bimat"));

        Assert.Equal("AKIDEXAMPLE", khoaDaDung!.MaKhoa);
        Assert.DoesNotContain("bimat", System.Text.Json.JsonSerializer.Serialize(kho.DaDoc), StringComparison.Ordinal);
    }

    // ── AC6: đọc không được ⇒ nói lại lý do, không im lặng ──────────────────────────────────

    [Fact]
    public async Task AC6_KhoTuChoiTruyCap_KhungNoiLaiLyDoChoNguoiKiem()
    {
        var kho = TrangThai(() =>
            KhoBangChung.Hong(CheDoDocKho.KhoaChiDoc, "kho từ chối: AccessDenied", KhoThu.MoTa));
        await kho.Doc(KhoThu, new KhoaKho("AKIDEXAMPLE", "bimat"));

        await using var trinh = new TrinhVe(kho: kho);

        var html = await VeKhung(trinh);

        Assert.Contains("AccessDenied", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AC6_HamDocKhoNemNgoaiLe_VanThanhMotKetQuaDocKhongDuoc_KhongLamTrangTrang()
    {
        var kho = new TrangThaiKho((_, _, _) => throw new HttpRequestException("mạng đứt"));

        await kho.Doc(KhoThu, null);

        Assert.Equal(BuocDocKho.DaDoc, kho.Buoc);
        Assert.Contains("mạng đứt", kho.DaDoc!.Loi ?? string.Empty, StringComparison.Ordinal);
    }
}

/// <summary>Vài lô bằng chứng đủ dùng cho phần hiển thị — nội dung lô là việc của test bên lõi.</summary>
internal static class DungLoTrailWeb
{
    public static IReadOnlyList<DoiTuongKho> Chuoi(int soLuong)
    {
        var doiTuong = new List<DoiTuongKho>();
        string? keyTruoc = null;
        string? shaTruoc = null;

        for (var so = 1; so <= soLuong; so++)
        {
            var dong = $$"""
                {"kind":"BATCH_HEADER","version":"NOXH-TRAIL-v1","batchNo":{{so}},"instanceId":"a1b2c3d4","createdAt":"2026-08-15T07:30:0{{so}}Z","recordCount":0,"prevKey":{{Json(keyTruoc)}},"prevSha256":{{Json(shaTruoc)}}}
                """ + "\n";
            var noiDung = System.Text.Encoding.UTF8.GetBytes(dong);

            keyTruoc = $"trail/2026/08/15/07300{so}000Z-b{so:D6}-a1b2c3d4.jsonl";
            shaTruoc = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(noiDung)).ToLowerInvariant();
            doiTuong.Add(new DoiTuongKho(keyTruoc, noiDung));
        }

        return doiTuong;
    }

    private static string Json(string? giaTri) => giaTri is null ? "null" : $"\"{giaTri}\"";
}
