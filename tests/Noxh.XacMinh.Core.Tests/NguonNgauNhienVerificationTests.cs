using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #4 — hai hạng mục kiểm nguồn ngẫu nhiên, đi qua đúng seam <see cref="Verifier.Verify"/>:
/// cam kết máy chủ (<c>SHA-256(R_server) == rServerCommit</c>) và hạt giống gốc
/// (<c>masterSeed == SHA-256(R_server ‖ R_supervisor ‖ blockHash)</c>).
/// Bản bị sửa dựng từ chính fixture chuẩn vàng, không phải file rời chép cứng.
/// </summary>
public class NguonNgauNhienVerificationTests
{
    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    // Kèm danh mục căn của dự án trong fixture: thiếu nó thì vòng phân căn ưu tiên ra KHÔNG KIỂM
    // ĐƯỢC và kéo cả kết luận chung theo, che mất thứ file này đang kiểm.
    private static VerificationReport Verify(TransparencyReport report) =>
        // Mốc neo cần block đọc từ nguồn công khai, danh sách hồ sơ cần bảng + khoá tổ giám sát dán
        // vào — thiếu cái nào thì kết luận chung không bao giờ ĐẠT, nên đưa vào đúng thứ vỏ UI đưa
        // vào, kẻo test ở đây khẳng định nhầm sang hạng mục khác.
        Verifier.Verify(new VerificationInput(
            report, DanhMucGolden.Doc(), QuanSatKhoiGolden.DocDuoc(), DanhSachGolden.Doc()));

    private static IReadOnlyList<CheckResult> HangMuc(VerificationReport report, string id) =>
        report.Items.Where(i => i.Id.StartsWith(id + ":", StringComparison.Ordinal)).ToList();

    private static CheckResult HangMucVong(VerificationReport report, string id, string vong) =>
        Assert.Single(report.Items, i => i.Id == $"{id}:{vong}");

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    /// <summary>Nguồn ngẫu nhiên của vòng thứ <paramref name="thu"/> trong JSON gốc.</summary>
    private static JsonObject Nguon(JsonObject root, int thu) =>
        root["nguonNgauNhien"]!.AsArray()[thu]!.AsObject();

    /// <summary>Lật đúng một byte của chuỗi hex — phép thử tối thiểu, không phải viết lại giá trị.</summary>
    private static string LatMotByte(string hex)
    {
        var byteDau = Convert.ToByte(hex[..2], 16);
        return (byteDau ^ 0x01).ToString("x2") + hex[2..];
    }

    // ── AC1: cả hai hạng mục chạy cho từng vòng, kết quả riêng từng vòng ─────────────────

    [Fact]
    public void AC1_MoiVong_CoCaHaiHangMucKetLuanRieng()
    {
        var golden = Golden();

        var report = Verify(golden);

        var vong = golden.EntropySources!.Select(s => s.Round).ToList();
        Assert.NotEmpty(vong);
        Assert.Equal(vong.Count, HangMuc(report, CheckIds.RServerCommit).Count);
        Assert.Equal(vong.Count, HangMuc(report, CheckIds.MasterSeed).Count);
        foreach (var v in vong)
        {
            Assert.Contains(report.Items, i => i.Id == $"{CheckIds.RServerCommit}:{v}");
            Assert.Contains(report.Items, i => i.Id == $"{CheckIds.MasterSeed}:{v}");
        }
    }

    [Fact]
    public void AC1_FixtureChuanVang_CaHaiHangMucDatOMoiVong()
    {
        var report = Verify(Golden());

        Assert.All(HangMuc(report, CheckIds.RServerCommit), i => Assert.Equal(CheckStatus.Dat, i.Status));
        Assert.All(HangMuc(report, CheckIds.MasterSeed), i => Assert.Equal(CheckStatus.Dat, i.Status));
        Assert.Equal(CheckStatus.Dat, report.Overall);
    }

    [Fact]
    public void AC1_HangMuc_MangGiaTriKyVongVaTinhDuoc_DeTuKiemLai()
    {
        var report = Verify(Golden());

        foreach (var item in HangMuc(report, CheckIds.RServerCommit).Concat(HangMuc(report, CheckIds.MasterSeed)))
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Expected));
            Assert.Equal(item.Expected, item.Actual);
            Assert.False(string.IsNullOrWhiteSpace(item.Preimage));
        }
    }

    // ── AC2: vòng thiếu dữ liệu → KHÔNG KIỂM ĐƯỢC, không thành KHÔNG ĐẠT ────────────────

    [Fact]
    public void AC2_VongChuaDongCong_ThieuRServer_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieu = Parse(EditGolden(root => Nguon(root, 1)["rServer"] = null));
        var vong = Golden().EntropySources![1].Round!;

        var report = Verify(thieu);

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMucVong(report, CheckIds.RServerCommit, vong).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMucVong(report, CheckIds.MasterSeed, vong).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, report.Overall);
    }

    [Fact]
    public void AC2_VongThieuDuLieu_KhongLamHongKetLuanCuaVongKhac()
    {
        var thieu = Parse(EditGolden(root => Nguon(root, 1)["rServer"] = null));
        var vongHong = Golden().EntropySources![1].Round!;

        var report = Verify(thieu);

        Assert.All(
            HangMuc(report, CheckIds.RServerCommit).Concat(HangMuc(report, CheckIds.MasterSeed))
                .Where(i => !i.Id.EndsWith(":" + vongHong, StringComparison.Ordinal)),
            i => Assert.Equal(CheckStatus.Dat, i.Status));
    }

    [Fact]
    public void AC2_BaoCaoKhongCoKhoiNguonNgauNhien_KhongKiemDuoc_ChuKhongImLang()
    {
        var rong = Parse(EditGolden(root => root["nguonNgauNhien"] = new JsonArray()));

        var report = Verify(rong);

        Assert.NotEmpty(HangMuc(report, CheckIds.RServerCommit).Concat(HangMuc(report, CheckIds.MasterSeed))
            .Concat(report.Items.Where(i => i.Id == CheckIds.RServerCommit || i.Id == CheckIds.MasterSeed)));
        Assert.Equal(CheckStatus.KhongKiemDuoc, report.Overall);
    }

    [Fact]
    public void AC2_ChuoiHexHong_KhongKiemDuoc_ChuKhongNemNgoaiLe()
    {
        var hong = Parse(EditGolden(root => Nguon(root, 0)["rServer"] = "khong-phai-hex"));
        var vong = Golden().EntropySources![0].Round!;

        var report = Verify(hong);

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMucVong(report, CheckIds.RServerCommit, vong).Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMucVong(report, CheckIds.MasterSeed, vong).Status);
    }

    // ── AC3: đổi một byte trong R_server → hạng mục cam kết KHÔNG ĐẠT ───────────────────

    [Fact]
    public void AC3_DoiMotByteRServer_HangMucCamKetKhongDat()
    {
        var vong = Golden().EntropySources![0].Round!;
        var sua = Parse(EditGolden(root =>
        {
            var nguon = Nguon(root, 0);
            nguon["rServer"] = LatMotByte(nguon["rServer"]!.GetValue<string>());
        }));

        var camKet = HangMucVong(Verify(sua), CheckIds.RServerCommit, vong);

        Assert.Equal(CheckStatus.KhongDat, camKet.Status);
        Assert.NotEqual(camKet.Expected, camKet.Actual);
    }

    [Fact]
    public void AC3_DoiMotByteRServer_KeoTheoHatGiongKhongDat_ViRServerLaDauVaoCuaCaHai()
    {
        var vong = Golden().EntropySources![0].Round!;
        var sua = Parse(EditGolden(root =>
        {
            var nguon = Nguon(root, 0);
            nguon["rServer"] = LatMotByte(nguon["rServer"]!.GetValue<string>());
        }));

        var report = Verify(sua);

        Assert.Equal(CheckStatus.KhongDat, HangMucVong(report, CheckIds.MasterSeed, vong).Status);
        Assert.Equal(CheckStatus.KhongDat, report.Overall);
    }

    // ── AC4: đổi mã băm block neo → hạt giống KHÔNG ĐẠT, cam kết vẫn ĐẠT ────────────────

    [Fact]
    public void AC4_DoiMaBamBlockNeo_HatGiongKhongDat_NhungCamKetVanDat()
    {
        var vong = Golden().EntropySources![0].Round!;
        var sua = Parse(EditGolden(root =>
        {
            var nguon = Nguon(root, 0);
            nguon["blockHash"] = LatMotByte(nguon["blockHash"]!.GetValue<string>());
        }));

        var report = Verify(sua);

        Assert.Equal(CheckStatus.KhongDat, HangMucVong(report, CheckIds.MasterSeed, vong).Status);
        Assert.Equal(CheckStatus.Dat, HangMucVong(report, CheckIds.RServerCommit, vong).Status);
    }

    [Fact]
    public void AC4_DoiMaBamBlockNeo_ChiHongDungVongDo()
    {
        var vongHong = Golden().EntropySources![0].Round!;
        var sua = Parse(EditGolden(root =>
        {
            var nguon = Nguon(root, 0);
            nguon["blockHash"] = LatMotByte(nguon["blockHash"]!.GetValue<string>());
        }));

        var report = Verify(sua);

        Assert.All(
            HangMuc(report, CheckIds.MasterSeed).Where(i => !i.Id.EndsWith(":" + vongHong, StringComparison.Ordinal)),
            i => Assert.Equal(CheckStatus.Dat, i.Status));
    }

    // ── AC5: lõi cấp đủ ba nguồn đầu vào của hạt giống cho chế độ chuyên sâu ────────────

    [Fact]
    public void AC5_HangMucHatGiong_CapDuBaNguonDauVao()
    {
        var golden = Golden();

        var report = Verify(golden);

        foreach (var nguon in golden.EntropySources!)
        {
            var item = HangMucVong(report, CheckIds.MasterSeed, nguon.Round!);
            var giaTri = item.Metrics.Select(m => m.Value).ToList();

            Assert.Contains(nguon.RServer, giaTri);
            Assert.Contains(nguon.RSupervisor, giaTri);
            Assert.Contains(nguon.BlockHash, giaTri);
        }
    }

    [Fact]
    public void AC5_HangMucHatGiong_PreimageLaBaNguonNoiTiepNhau()
    {
        var golden = Golden();
        var nguon = golden.EntropySources![0];

        var item = HangMucVong(Verify(golden), CheckIds.MasterSeed, nguon.Round!);

        Assert.Equal(nguon.RServer + nguon.RSupervisor + nguon.BlockHash, item.Preimage);
    }

    [Fact]
    public void AC5_MoiHangMuc_KhongDeLoHexTrongCauGiaiThich_ViCheDoNguoiDanVeThangCauNay()
    {
        var report = Verify(Golden());

        Assert.All(
            HangMuc(report, CheckIds.RServerCommit).Concat(HangMuc(report, CheckIds.MasterSeed)),
            i =>
            {
                Assert.False(string.IsNullOrWhiteSpace(i.Explanation));
                Assert.DoesNotContain(i.Expected!, i.Explanation);
            });
    }
}
