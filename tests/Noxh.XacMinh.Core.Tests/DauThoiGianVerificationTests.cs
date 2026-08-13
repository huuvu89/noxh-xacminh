using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #8 — dấu thời gian độc lập của mốc cam kết, đi qua đúng seam <see cref="Verifier.Verify"/>.
/// Hạng mục này trả lời một câu duy nhất: dấu do bên thứ ba cấp có đóng lên <b>đúng bộ số đang được
/// công bố</b> hay không. Không có dấu nào thì phải nói to, vì khi đó lập luận "cam kết có trước
/// block đích" chỉ còn là lời khẳng định của ban tổ chức.
/// </summary>
public class DauThoiGianVerificationTests
{
    /// <summary>Fixture chuẩn vàng có 16 dấu, trong đó 2 dấu của mốc cam kết (freetsa + digicert).</summary>
    private const int SoDauMocCamKet = 2;

    private const int SoDauMocKhac = 14;

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static CheckResult HangMuc(TransparencyReport report) =>
        Assert.Single(Verifier.Verify(new VerificationInput(report)).Items, i => i.Id == CheckIds.FreezeTimestamp);

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonArray Dau(JsonObject root) => root["dauThoiGian"]!.AsArray();

    private static JsonObject CamKetNeo(JsonObject root) => root["camKetNeo"]!.AsObject();

    private static IEnumerable<JsonObject> DauMocCamKet(JsonObject root) =>
        Dau(root).Select(d => d!.AsObject()).Where(d => d["scope"]!.GetValue<string>() == "FREEZE");

    private static JsonObject DauMocKhac(JsonObject root) =>
        Dau(root).Select(d => d!.AsObject()).First(d => d["scope"]!.GetValue<string>() != "FREEZE");

    private static JsonObject NguonNgauNhien(JsonObject root, string vong) =>
        root["nguonNgauNhien"]!.AsArray().Select(n => n!.AsObject())
            .First(n => n["round"]!.GetValue<string>() == vong);

    /// <summary>Dấu vân tay tính bằng thư viện, không qua lõi — để test không tự chứng minh chính nó.</summary>
    private static string VanTay(string preimage) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preimage))).ToLowerInvariant();

    /// <summary>Đổi một dòng của chuỗi đóng dấu rồi đóng lại dấu vân tay — mô phỏng một lần chốt khác.</summary>
    private static void DongDauLai(JsonObject dau, string khoa, string giaTri)
    {
        var dong = dau["preimage"]!.GetValue<string>().Split('\n')
            .Select(d => d.StartsWith(khoa + "=", StringComparison.Ordinal) ? $"{khoa}={giaTri}" : d);
        var preimage = string.Join('\n', dong);

        dau["preimage"] = preimage;
        dau["digest"] = VanTay(preimage);
    }

    // ── AC1: băm lại chuỗi đóng dấu và so với dấu vân tay của TỪNG dấu ───────────────────

    [Fact]
    public void AC1_FixtureChuanVang_BamLaiChuoiDongDau_KhopDauVanTay_Dat()
    {
        var item = HangMuc(Golden());

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Equal(item.Expected, item.Actual);
    }

    [Fact]
    public void AC1_SuaDauVanTayCongBo_ChuyenKhongDat()
    {
        var sua = Parse(EditGolden(root => DauMocCamKet(root).First()["digest"] = new string('0', 64)));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.NotEqual(item.Expected, item.Actual);
    }

    [Fact]
    public void AC1_BamLaiTungDau_ChiDauThuHaiLechVanTay_VanChuyenKhongDat()
    {
        // Gộp kết luận theo "có ít nhất một dấu khớp" sẽ bỏ lọt dấu thứ hai bị sửa.
        var sua = Parse(EditGolden(root => DauMocCamKet(root).Last()["digest"] = new string('0', 64)));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC1_SuaChuoiDongDau_VanTayKhongConKhop_ChuyenKhongDat()
    {
        var sua = Parse(EditGolden(root =>
            DauMocCamKet(root).First()["preimage"] = "NOXH-FREEZE-v1\nprojectId=khac\n"));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC1_DauKhongCongBoVanTay_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieu = Parse(EditGolden(root => DauMocCamKet(root).First()["digest"] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    [Fact]
    public void AC1_VanTayKhongPhaiChuoiMaBamHopLe_KhongKiemDuoc()
    {
        var la = Parse(EditGolden(root => DauMocCamKet(root).First()["digest"] = "khong-phai-hex"));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(la).Status);
    }

    [Fact]
    public void AC1_DauKhongCongBoChuoiDongDau_KhongKiemDuoc()
    {
        var thieu = Parse(EditGolden(root => DauMocCamKet(root).First()["preimage"] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    // ── AC2 + AC5: bóc từng trường trong chuỗi đóng dấu, đối chiếu dữ liệu công bố ────────

    [Theory]
    [InlineData("ethTargetHeight")]
    [InlineData("btcTargetHeight")]
    public void AC5_SuaMocBlockDichTrongCamKetNeo_ChuyenKhongDat_VaNoiRoTruong(string truong)
    {
        var sua = Parse(EditGolden(root => CamKetNeo(root)[truong] = 999_999));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains(truong, item.Explanation, StringComparison.Ordinal);
        Assert.Equal(CheckStatus.KhongDat, Verifier.Verify(new VerificationInput(sua)).Overall);
    }

    [Fact]
    public void AC5_SuaThoiDiemChotMocNeo_ChuyenKhongDat_VaNoiRoTruong()
    {
        var sua = Parse(EditGolden(root => CamKetNeo(root)["anchorFrozenAt"] = "2026-08-12T20:26:51.0000000Z"));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("anchorFrozenAt", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_SuaCamKetNgauNhienMayChuMotVong_ChuyenKhongDat_VaNoiRoVongNao()
    {
        var sua = Parse(EditGolden(root =>
            NguonNgauNhien(root, "B")["rServerCommit"] = new string('a', 64)));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("rServerCommitB", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_SuaPhanNgauNhienGiamSat_ChuyenKhongDat_VaNoiRoTruong()
    {
        var sua = Parse(EditGolden(root =>
        {
            foreach (var vong in new[] { "A", "B", "C" })
                NguonNgauNhien(root, vong)["rSupervisor"] = new string('b', 64);
        }));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("rSup", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_SuaDinhDanhDuAn_ChuyenKhongDat_VaNoiRoTruong()
    {
        var sua = Parse(EditGolden(root => root["projectId"] = "ffffffff-0000-0000-0000-0000000000ff"));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("projectId", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_LietKeDuDiemLech_ChuKhongDungOTruongDauTien()
    {
        var sua = Parse(EditGolden(root =>
        {
            CamKetNeo(root)["ethTargetHeight"] = 999_999;
            CamKetNeo(root)["btcTargetHeight"] = 999_998;
        }));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains(item.Metrics, m => m.Label == "Số điểm lệch" && m.Value == "2");
    }

    [Fact]
    public void AC2_ThoiDiemChotMocNeo_LechChuSoLeGiay_VanDat()
    {
        // Trong dấu là 3 chữ số lẻ giây, báo cáo công bố 7 — đối chiếu là so GIÁ TRỊ thời điểm,
        // không phải so chuỗi ký tự; so chuỗi thì mọi báo cáo thật đều KHÔNG ĐẠT oan.
        var sua = Parse(EditGolden(root => CamKetNeo(root)["anchorFrozenAt"] = "2026-08-12T20:26:50.8539999Z"));

        Assert.Equal(CheckStatus.Dat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC2_ThoiDiemChotMocNeo_DoiSangMuiGio_VanDat_ViSoTheoMocUtc()
    {
        var sua = Parse(EditGolden(root => CamKetNeo(root)["anchorFrozenAt"] = "2026-08-13T03:26:50.853+07:00"));

        Assert.Equal(CheckStatus.Dat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC2_BaoCaoThieuKhoiCamKetNeo_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieu = Parse(EditGolden(root => root.Remove("camKetNeo")));

        var item = HangMuc(thieu);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("ethTargetHeight", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_BaoCaoThieuNguonNgauNhien_KhongKiemDuoc()
    {
        var thieu = Parse(EditGolden(root => root.Remove("nguonNgauNhien")));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    [Fact]
    public void AC2_MoiVongCongBoMotPhanNgauNhienGiamSatKhacNhau_KhongKiemDuoc()
    {
        // Báo cáo tự mâu thuẫn: không biết phần nào ứng với dấu, nên không được kết luận thay.
        var lech = Parse(EditGolden(root => NguonNgauNhien(root, "C")["rSupervisor"] = new string('c', 64)));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(lech).Status);
    }

    [Fact]
    public void AC2_MaBamDanhSachHoSoTrongDau_HienRaChoNguoiKiem_DuBaoCaoCongKhaiKhongCongBo()
    {
        var item = HangMuc(Golden());

        Assert.Contains(item.Metrics, m => m.Label.Contains("danh sách hồ sơ", StringComparison.Ordinal));
    }

    [Fact]
    public void AC2_BaoCaoCoCongBoMaBamDanhSach_DoiChieuLuon_LechThiKhongDat()
    {
        var sua = Parse(EditGolden(root => root["listHash"] = new string('d', 64)));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("listHash", item.Explanation, StringComparison.Ordinal);
    }

    // ── AC3: không có dấu nào ⇒ cảnh báo mức cao, không ĐẠT và không im lặng ─────────────

    [Fact]
    public void AC3_KhoiDauThoiGianRong_KhongKiemDuoc_VaKeoKetLuanChungXuong()
    {
        var rong = Parse(EditGolden(root => root["dauThoiGian"] = new JsonArray()));

        var item = HangMuc(rong);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.NotEqual(CheckStatus.Dat, Verifier.Verify(new VerificationInput(rong)).Overall);
    }

    [Fact]
    public void AC3_BaoCaoKhongCoKhoiDauThoiGian_VanRaHangMucCanhBao_ChuKhongImLang()
    {
        var thieu = Parse(EditGolden(root => root.Remove("dauThoiGian")));

        var item = HangMuc(thieu);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("KHÔNG có bằng chứng thời gian độc lập", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_ChiCoDauCuaMocKhac_VanCanhBaoMocCamKetKhongCoDau()
    {
        var chiMocKhac = Parse(EditGolden(root =>
        {
            var giu = Dau(root).Select(d => d!.DeepClone())
                .Where(d => d["scope"]!.GetValue<string>() != "FREEZE").ToList();
            var mang = Dau(root);
            mang.Clear();
            foreach (var d in giu) mang.Add(d);
        }));

        var item = HangMuc(chiMocKhac);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains(item.Metrics, m => m.Label.Contains("mốc cam kết", StringComparison.Ordinal) && m.Value == "0");
    }

    // ── AC4: không gộp lẫn dấu của mốc cam kết với dấu của các mốc khác ──────────────────

    [Fact]
    public void AC4_ChiKetLuanTrenDauCuaMocCamKet_MotHangMucDuyNhat()
    {
        var item = HangMuc(Golden());

        Assert.Contains(item.Metrics, m => m.Label.Contains("mốc cam kết", StringComparison.Ordinal)
                                           && m.Value == SoDauMocCamKet.ToString());
    }

    [Fact]
    public void AC4_SuaDauCuaMocKhac_HangMucNayVanDat()
    {
        var sua = Parse(EditGolden(root => DauMocKhac(root)["digest"] = new string('0', 64)));

        Assert.Equal(CheckStatus.Dat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC4_SoLieuTho_NeuSoDauCuaMocKhac_DeNguoiKiemBietChungKhongBiBoQua()
    {
        var item = HangMuc(Golden());

        Assert.Contains(item.Metrics, m => m.Label.Contains("mốc khác", StringComparison.Ordinal)
                                           && m.Value == SoDauMocKhac.ToString());
    }

    // ── AC6: chế độ chuyên sâu có nguyên văn chuỗi đóng dấu và thời điểm cấp dấu ─────────

    [Fact]
    public void AC6_CheDoChuyenSau_HienNguyenVanChuoiDongDau()
    {
        var item = HangMuc(Golden());
        var trongBaoCao = Golden().Timestamps!.First(t => t!.Scope == "FREEZE")!.Preimage;

        Assert.Equal(trongBaoCao, item.Preimage);
    }

    [Fact]
    public void AC6_CheDoChuyenSau_NeuThoiDiemCapCuaTungDau()
    {
        var item = HangMuc(Golden());

        foreach (var dau in Golden().Timestamps!.Where(t => t!.Scope == "FREEZE"))
            Assert.Contains(item.Metrics, m => m.Value == dau!.GenTime);
    }

    [Fact]
    public void AC6_CheDoChuyenSau_NeuSoHieuDau_DeDoiChieuVoiTokenTho()
    {
        var item = HangMuc(Golden());
        var soHieu = Golden().Timestamps!.First(t => t!.Scope == "FREEZE")!.SerialNumber;

        Assert.Contains(item.Metrics, m => m.Value == soHieu);
    }

    [Fact]
    public void AC6_ChuoiDongDauVanCoMatKhiKhongDat_DeNguoiKiemTuBamLaiTay()
    {
        var sua = Parse(EditGolden(root => CamKetNeo(root)["ethTargetHeight"] = 999_999));

        Assert.False(string.IsNullOrWhiteSpace(HangMuc(sua).Preimage));
    }

    // ── Chốt lại (refreeze): dấu của lần chốt trước không phải bằng chứng dữ liệu bị sửa ─

    [Fact]
    public void ChotLai_ConDauCuaLanChotTruoc_VanDat_VaNeuRoSoDauKhongKhop()
    {
        var chotLai = Parse(EditGolden(root =>
        {
            var cu = DauMocCamKet(root).First().DeepClone().AsObject();
            DongDauLai(cu, "ethTargetHeight", "840001");
            cu["genTime"] = "2026-08-12T20:26:40.0000000Z";
            Dau(root).Add(cu);
        }));

        var item = HangMuc(chotLai);

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Contains("chốt lại", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ChotLai_KhongDauNaoKhopDuLieuDangCongBo_KhongDat()
    {
        var sua = Parse(EditGolden(root =>
        {
            foreach (var dau in DauMocCamKet(root).ToList()) DongDauLai(dau, "ethTargetHeight", "840001");
        }));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    // ── Dữ liệu lạ: KHÔNG KIỂM ĐƯỢC, không ném ngoại lệ ─────────────────────────────────

    [Fact]
    public void PhanTuDauThoiGianRong_KhongNemNgoaiLe()
    {
        var rong = Parse(EditGolden(root => Dau(root)[0] = null));

        Assert.Equal(CheckStatus.Dat, HangMuc(rong).Status);
    }

    [Fact]
    public void ChuoiDongDauKhongPhaiDinhDangMocCamKet_KhongKiemDuoc()
    {
        var la = Parse(EditGolden(root =>
        {
            var dau = DauMocCamKet(root).First();
            var preimage = "NOXH-FREEZE-v9" + dau["preimage"]!.GetValue<string>()["NOXH-FREEZE-v1".Length..];
            dau["preimage"] = preimage;
            dau["digest"] = VanTay(preimage);
        }));

        var item = HangMuc(la);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
    }

    [Fact]
    public void ChuoiDongDauCoDongLa_KhongKiemDuoc_ChuKhongImLangBoQua()
    {
        var la = Parse(EditGolden(root =>
        {
            var dau = DauMocCamKet(root).First();
            var preimage = dau["preimage"]!.GetValue<string>() + "truongLa=1\n";
            dau["preimage"] = preimage;
            dau["digest"] = VanTay(preimage);
        }));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(la).Status);
    }

    [Fact]
    public void ChuoiDongDauThieuMotDong_KhongKiemDuoc()
    {
        var thieu = Parse(EditGolden(root =>
        {
            var dau = DauMocCamKet(root).First();
            var preimage = string.Join('\n', dau["preimage"]!.GetValue<string>().Split('\n')
                .Where(d => !d.StartsWith("btcTargetHeight=", StringComparison.Ordinal)));
            dau["preimage"] = preimage;
            dau["digest"] = VanTay(preimage);
        }));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    [Theory]
    [InlineData("crlf")]
    [InlineData("mat-dong-cuoi")]
    public void ChuoiDongDauChiLechViLoiSaoChep_KhongKiemDuoc_ChuKhongVuOanLaDuLieuBiSua(string kieu)
    {
        var hong = Parse(EditGolden(root =>
        {
            var dau = DauMocCamKet(root).First();
            var goc = dau["preimage"]!.GetValue<string>();
            dau["preimage"] = kieu == "crlf" ? goc.Replace("\n", "\r\n") : goc.TrimEnd('\n');
        }));

        var item = HangMuc(hong);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("bản gốc", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void ThoiDiemChotMocNeoCongBoKhongDocDuoc_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var la = Parse(EditGolden(root => CamKetNeo(root)["anchorFrozenAt"] = "hom qua"));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(la).Status);
    }

    // ── Câu giải thích cho chế độ người dân ─────────────────────────────────────────────

    [Fact]
    public void CauGiaiThich_KhiDat_KhongDeLoHex()
    {
        var item = HangMuc(Golden());
        var vanTay = Golden().Timestamps!.First(t => t!.Scope == "FREEZE")!.Digest!;

        Assert.False(string.IsNullOrWhiteSpace(item.Explanation));
        Assert.DoesNotContain(vanTay, item.Explanation, StringComparison.Ordinal);
    }
}
