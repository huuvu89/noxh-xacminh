using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #5 — hạng mục chuỗi băm nhật ký bốc, đi qua đúng seam <see cref="Verifier.Verify"/>.
/// Bản bị sửa/bị xoá dòng dựng từ chính fixture chuẩn vàng: đó mới là phép thử "tính lại", vì một
/// công cụ chỉ so nối chuỗi (prevHash bước sau == entryHash bước trước) vẫn báo ĐẠT cho những bản này.
/// </summary>
public class NhatKyBocVerificationTests
{
    /// <summary>Bước bị động vào — nằm giữa nhật ký để phép xoá/sửa kéo theo cả phần đuôi.</summary>
    private const int BuocThu = 3;

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static CheckResult HangMuc(TransparencyReport report) =>
        Assert.Single(Verifier.Verify(new VerificationInput(report)).Items, i => i.Id == CheckIds.DrawLogChain);

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonArray NhatKy(JsonObject root) => root["nhatKyBoc"]!.AsArray();

    /// <summary>Mô tả bước như báo cáo nêu ra — dùng để đối chiếu "lệch ở đâu".</summary>
    private static string MoTaBuoc(JsonObject buoc) =>
        $"vòng {buoc["round"]!.GetValue<string>()}, vị trí {buoc["position"]!.GetValue<int>()}";

    // ── AC1: tính lại mã băm từng bước, không chỉ so nối chuỗi ──────────────────────────

    [Fact]
    public void AC1_FixtureChuanVang_ChuoiBamDat()
    {
        var item = HangMuc(Golden());

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Equal(item.Expected, item.Actual);
        Assert.False(string.IsNullOrWhiteSpace(item.Preimage));
    }

    [Fact]
    public void AC1_SuaNoiDungVe_NhungGiuNguyenNoiChuoi_VanKhongDat()
    {
        // prevHash/entryHash công bố không bị đụng tới: chuỗi vẫn "nối" hoàn hảo. Chỉ phép tính lại
        // từ preimage mới bắt được — đây chính là chỗ tool cũ hụt.
        var sua = Parse(EditGolden(root =>
        {
            var buoc = NhatKy(root)[BuocThu]!.AsObject();
            buoc["payload"] = buoc["payload"]!.GetValue<string>() + "_DA_SUA";
        }));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC1_ThuTuDuyetLaTatDinh_KhongPhuThuocThuTuMangTrongFile()
    {
        var daoNguoc = Parse(EditGolden(root =>
        {
            var nhatKy = NhatKy(root);
            var buoc = nhatKy.Select(n => n!.DeepClone()).Reverse().ToList();
            nhatKy.Clear();
            foreach (var b in buoc) nhatKy.Add(b);
        }));

        Assert.Equal(CheckStatus.Dat, HangMuc(daoNguoc).Status);
    }

    // ── AC3: thiếu định danh hồ sơ / chồng phiếu → KHÔNG KIỂM ĐƯỢC kèm lý do ────────────

    [Theory]
    [InlineData("applicantId", "hồ sơ")]
    [InlineData("deckId", "chồng phiếu")]
    public void AC3_ThieuDinhDanh_KhongKiemDuoc_KemCauGiaiThichViSao(string truong, string tuKhoa)
    {
        var thieu = Parse(EditGolden(root => NhatKy(root)[BuocThu]!.AsObject()[truong] = null));

        var item = HangMuc(thieu);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains(tuKhoa, item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_ThieuDinhDanh_KhongBiHieuNhamLaGianLan()
    {
        var thieu = Parse(EditGolden(root => NhatKy(root)[BuocThu]!.AsObject()["deckId"] = null));

        Assert.NotEqual(CheckStatus.KhongDat, HangMuc(thieu).Status);
    }

    [Fact]
    public void AC3_VuaSaiVuaThieu_KetLuanLaKhongDat_ThieuDuLieuKhongDuocCheMatMatXichDaDut()
    {
        var lai = Parse(EditGolden(root =>
        {
            var nhatKy = NhatKy(root);
            var buoc = nhatKy[BuocThu]!.AsObject();
            buoc["payload"] = buoc["payload"]!.GetValue<string>() + "_DA_SUA";
            nhatKy[^1]!.AsObject()["deckId"] = null; // chỗ thiếu nằm SAU chỗ đã đứt
        }));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(lai).Status);
    }

    [Fact]
    public void AC3_ThieuDinhDanh_VanNeuRoDaKiemKhopDuocBaoNhieuBuocTruocDo()
    {
        var thieu = Parse(EditGolden(root => NhatKy(root)[BuocThu]!.AsObject()["deckId"] = null));

        var item = HangMuc(thieu);

        Assert.Contains(item.Metrics, m => m.Label.Contains("trước chỗ thiếu", StringComparison.Ordinal));
    }

    [Fact]
    public void AC3_BaoCaoKhongCoNhatKyBoc_KhongKiemDuoc_ChuKhongImLang()
    {
        var rong = Parse(EditGolden(root => root["nhatKyBoc"] = new JsonArray()));

        var item = HangMuc(rong);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Equal(CheckStatus.KhongKiemDuoc, Verifier.Verify(new VerificationInput(rong)).Overall);
    }

    [Fact]
    public void AC3_BuocChuaCongBoMaBam_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieu = Parse(EditGolden(root => NhatKy(root)[BuocThu]!.AsObject()["entryHash"] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    // ── AC4: sửa nội dung vé của một lượt bốc → KHÔNG ĐẠT, chỉ đúng bước lệch đầu tiên ──

    [Fact]
    public void AC4_SuaNoiDungVeMotLuotBoc_KhongDat_VaChiRaDungBuocLechDauTien()
    {
        var moTa = string.Empty;
        var sua = Parse(EditGolden(root =>
        {
            var buoc = NhatKy(root)[BuocThu]!.AsObject();
            buoc["payload"] = buoc["payload"]!.GetValue<string>() + "_DA_SUA";
            moTa = MoTaBuoc(buoc);
        }));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains(moTa, item.Explanation, StringComparison.Ordinal);
        Assert.NotEqual(item.Expected, item.Actual);
    }

    [Fact]
    public void AC4_KetLuanChungXuongKhongDat_KhiChuoiBamHong()
    {
        var sua = Parse(EditGolden(root =>
        {
            var buoc = NhatKy(root)[BuocThu]!.AsObject();
            buoc["payload"] = buoc["payload"]!.GetValue<string>() + "_DA_SUA";
        }));

        Assert.Equal(CheckStatus.KhongDat, Verifier.Verify(new VerificationInput(sua)).Overall);
    }

    // ── AC5: xoá một dòng nhật ký → KHÔNG ĐẠT ───────────────────────────────────────────

    [Fact]
    public void AC5_XoaMotDongNhatKy_KhongDat()
    {
        var xoa = Parse(EditGolden(root => NhatKy(root).RemoveAt(BuocThu)));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(xoa).Status);
    }

    [Fact]
    public void AC5_XoaMotDong_BuocLechDauTienLaBuocNgaySauDo()
    {
        var moTa = string.Empty;
        var xoa = Parse(EditGolden(root =>
        {
            var nhatKy = NhatKy(root);
            moTa = MoTaBuoc(nhatKy[BuocThu + 1]!.AsObject());
            nhatKy.RemoveAt(BuocThu);
        }));

        Assert.Contains(moTa, HangMuc(xoa).Explanation, StringComparison.Ordinal);
    }

    // ── AC6: báo cáo nêu rõ bước nào lệch, không chỉ nói "chuỗi hỏng" ───────────────────

    [Fact]
    public void AC6_SoLieuThoNeuRoBuocLechDauTien()
    {
        var moTa = string.Empty;
        var sua = Parse(EditGolden(root =>
        {
            var buoc = NhatKy(root)[BuocThu]!.AsObject();
            buoc["payload"] = buoc["payload"]!.GetValue<string>() + "_DA_SUA";
            moTa = MoTaBuoc(buoc);
        }));

        var item = HangMuc(sua);

        Assert.Contains(item.Metrics, m => m.Value == moTa);
    }

    [Fact]
    public void AC6_ChuoiConNguyen_ThiKhongCoBuocLechNaoDuocNeuRa()
    {
        var item = HangMuc(Golden());

        Assert.DoesNotContain(item.Metrics, m => m.Value.StartsWith("vòng ", StringComparison.Ordinal)
                                                 && m.Label.Contains("lệch", StringComparison.Ordinal));
    }

    [Fact]
    public void AC6_SoLieuThoCoSoLuotBocVaDauChuoi_DeDoiChieuVoiBienBan()
    {
        var golden = Golden();

        var item = HangMuc(golden);

        Assert.Contains(item.Metrics, m => m.Value == golden.DrawLog!.Count.ToString());
        Assert.Contains(item.Metrics, m => m.Value == golden.DrawLog![^1].EntryHash);
    }

    [Fact]
    public void AC6_CauGiaiThich_KhongDeLoHex_ViCheDoNguoiDanVeThangCauNay()
    {
        var item = HangMuc(Golden());

        Assert.False(string.IsNullOrWhiteSpace(item.Explanation));
        Assert.DoesNotContain(item.Expected!, item.Explanation, StringComparison.Ordinal);
    }
}
