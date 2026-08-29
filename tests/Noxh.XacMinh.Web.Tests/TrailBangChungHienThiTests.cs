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
/// <summary>
/// Đường thứ hai vào trail: gói đã tải sẵn bằng script rồi nạp vào. Có vì trình duyệt chỉ đọc được
/// kho khi chính kho bật CORS, và vì bản offline vốn không có mạng. Điều phải canh ở vỏ: nạp gói
/// <b>không gọi mạng</b>, và màn hình không được nói nhập nhèm rằng chính nó đã đọc kho.
/// </summary>
public class NapGoiTrailHienThiTests
{
    private static Task<string> VeKhung(TrinhVe trinh) =>
        trinh.Ve<TrailBangChung>(new Dictionary<string, object?>());

    [Fact]
    public async Task NapGoi_KhongGoiMang_MaVanCoTrailDeKiem()
    {
        // TrinhVe cấp hàm đọc kho ném ngoại lệ: nạp gói mà lỡ gọi mạng là test đỏ.
        await using var trinh = new TrinhVe();

        trinh.Kho.NapGoi(DungGoiTrailWeb.Goi(DungLoTrailWeb.Chuoi(3)), "goi-trail.zip");

        Assert.Null(trinh.Kho.DaDoc!.Loi);
        Assert.Equal(3, trinh.Kho.DaDoc.Lo.Count);
    }

    [Fact]
    public async Task NapGoi_KhungNoiRoLaNapTuGoi_ChuKhongPhaiChinhNoDaDocKho()
    {
        await using var trinh = new TrinhVe();
        trinh.Kho.NapGoi(DungGoiTrailWeb.Goi(DungLoTrailWeb.Chuoi(3)), "goi-trail.zip");

        var html = await VeKhung(trinh);

        Assert.Contains("nạp 3 lô", html, StringComparison.Ordinal);
        Assert.Contains("gói «goi-trail.zip»", html, StringComparison.Ordinal);
        Assert.Contains("KHÔNG phải trình duyệt này đọc thẳng kho", html, StringComparison.Ordinal);
    }

    /// <summary>Mã băm gói phải là mã băm của ĐÚNG byte đã nạp — bản xuất kết quả in con số này ra.</summary>
    [Fact]
    public void NapGoi_GhiLaiMaBamDungByteCuaGoi()
    {
        var goi = DungGoiTrailWeb.Goi(DungLoTrailWeb.Chuoi(2));
        var trangThai = new TrangThaiKho((_, _, _) => throw new NotSupportedException());

        trangThai.NapGoi(goi, "goi-trail.zip");

        Assert.Equal("goi-trail.zip", trangThai.Goi!.TenFile);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(goi)).ToLowerInvariant(),
            trangThai.Goi.MaBamSha256);
    }

    [Fact]
    public void GoiHong_VanGiuMaBamGoi_ViDaThuKiemBangFileNayCungLaMotDuKien()
    {
        var trangThai = new TrangThaiKho((_, _, _) => throw new NotSupportedException());

        trangThai.NapGoi(System.Text.Encoding.UTF8.GetBytes("không phải zip"), "nham.zip");

        Assert.NotNull(trangThai.Goi);
        Assert.NotNull(trangThai.DaDoc!.Loi);
    }

    [Fact]
    public void Xoa_GoiVaTrailDeuBienMatKhoiBoNho()
    {
        var trangThai = new TrangThaiKho((_, _, _) => throw new NotSupportedException());
        trangThai.NapGoi(DungGoiTrailWeb.Goi(DungLoTrailWeb.Chuoi(1)), "goi-trail.zip");

        trangThai.Xoa();

        Assert.Null(trangThai.Goi);
        Assert.Null(trangThai.DaDoc);
    }

    /// <summary>
    /// Đọc thẳng kho sau khi đã nạp gói thì bản xuất không được khai là đã kiểm bằng gói: hai đường
    /// mang sức nặng khác nhau, lẫn vào nhau là bản xuất nói sai.
    /// </summary>
    [Fact]
    public async Task DocThangKhoSauKhiNapGoi_KhongCoKhaiGoiNua()
    {
        var trangThai = new TrangThaiKho((_, _, _) =>
            Task.FromResult(KhoBangChung.Doc(CheDoDocKho.AnDanh, DungLoTrailWeb.Chuoi(1), "kho thử")));
        trangThai.NapGoi(DungGoiTrailWeb.Goi(DungLoTrailWeb.Chuoi(1)), "goi-trail.zip");

        await trangThai.Doc(new ThongSoKho("https://s3.thu-nghiem.vn", "bang-chung", "hcm", "trail/"), null);

        Assert.Null(trangThai.Goi);
        Assert.Equal(CheDoDocKho.AnDanh, trangThai.DaDoc!.CheDo);
    }
}

/// <summary>
/// Dựng gói trail đúng khuôn script <c>cong-cu/tai-goi-trail.py</c> ghi ra — chỉ đủ cho phần vỏ:
/// một trang danh sách liệt kê hết và byte thô từng lô.
/// </summary>
internal static class DungGoiTrailWeb
{
    public static byte[] Goi(IReadOnlyList<DoiTuongKho> lo)
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>"
                  + "<ListBucketResult xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">"
                  + "<IsTruncated>false</IsTruncated>"
                  + string.Concat(lo.Select(l => $"<Contents><Key>{l.Key}</Key></Contents>"))
                  + "</ListBucketResult>";

        using var bo = new MemoryStream();

        using (var goi = new System.IO.Compression.ZipArchive(
                   bo, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            Them(goi, "listing/000.xml", System.Text.Encoding.UTF8.GetBytes(xml));

            foreach (var mot in lo) Them(goi, $"objects/{mot.Key}", mot.NoiDung);
        }

        return bo.ToArray();
    }

    private static void Them(System.IO.Compression.ZipArchive goi, string ten, byte[] noiDung)
    {
        using var dong = goi.CreateEntry(ten).Open();
        dong.Write(noiDung);
    }
}

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

    /// <summary>
    /// Một lô mang đúng một lượt bốc <b>không</b> có trong nhật ký công bố — ô phiếu bịa hẳn ra để
    /// không đụng ô thật nào của fixture.
    /// </summary>
    public static IReadOnlyList<DoiTuongKho> LoLuotBocLa(string maDuAn)
    {
        var ve = """
            {"kind":"TICKET_DRAWN","projectId":"DU-AN","occurredAt":"2026-08-15T07:30:05Z","payload":{"applicantId":"9fa3bc49-54fa-4a2d-a42f-193b03f957c2","deckId":"00000000-0000-0000-0000-0000000090a1","round":"C","position":9001,"ticketPayload":"KHONG_TRUNG"}}
            """.Replace("DU-AN", maDuAn, StringComparison.Ordinal);
        var dong = """
            {"kind":"BATCH_HEADER","version":"NOXH-TRAIL-v1","batchNo":1,"instanceId":"a1b2c3d4","createdAt":"2026-08-15T07:30:01Z","recordCount":1,"prevKey":null,"prevSha256":null}
            """ + "\n" + ve + "\n";

        return [new DoiTuongKho("trail/2026/08/15/073001000Z-b000001-a1b2c3d4.jsonl",
            System.Text.Encoding.UTF8.GetBytes(dong))];
    }

    private static string Json(string? giaTri) => giaTri is null ? "null" : $"\"{giaTri}\"";
}
