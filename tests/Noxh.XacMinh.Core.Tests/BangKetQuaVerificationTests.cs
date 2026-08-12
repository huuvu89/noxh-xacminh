using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #7 — mã băm bảng kết quả chung cuộc, đi qua đúng seam <see cref="Verifier.Verify"/>. Đây là
/// hạng mục duy nhất phủ được những dòng KHÔNG có vé nào (người ưu tiên trực tiếp không phải bốc,
/// người được máy gom phân căn): sửa những dòng đó không mâu thuẫn với nhật ký bốc nên chuỗi băm
/// nhật ký vẫn ĐẠT.
/// </summary>
public class BangKetQuaVerificationTests
{
    /// <summary>23 dòng kết quả trong fixture chuẩn vàng (manifest: <c>soDongKetQua</c>).</summary>
    private const int SoDongKetQua = 23;

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static CheckResult HangMuc(TransparencyReport report) =>
        Assert.Single(Verifier.Verify(new VerificationInput(report)).Items, i => i.Id == CheckIds.ResultsHash);

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonObject KetQua(JsonObject root) => root["ketQua"]!.AsObject();

    private static JsonArray Dong(JsonObject root) => KetQua(root)["rows"]!.AsArray();

    /// <summary>Dòng trúng có mã căn — ca đáng sửa nhất của người muốn đổi kết quả.</summary>
    private static JsonObject DongCoCan(JsonObject root) =>
        Dong(root).First(r => r!["unitCode"] is not null)!.AsObject();

    private static JsonObject DongDuKhuyet(JsonObject root) =>
        Dong(root).First(r => r!["waitlistRank"] is not null)!.AsObject();

    // ── AC1: tính lại mã băm từ dữ liệu công bố và so với giá trị công bố ────────────────

    [Fact]
    public void AC1_FixtureChuanVang_TinhLaiMaBamBangKetQua_Dat()
    {
        var item = HangMuc(Golden());

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Equal(Golden().Results!.ResultsHash, item.Expected);
        Assert.Equal(item.Expected, item.Actual);
    }

    [Fact]
    public void AC1_TinhLaiTuDuLieuCongBo_ChuKhongTinCauCongBo_SuaMaBamCongBoThiKhongDat()
    {
        var sua = Parse(EditGolden(root =>
            KetQua(root)["resultsHash"] = new string('0', 64)));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC1_DaoThuTuDongTrongFile_VanDat_ViCanonicalTuSapXep()
    {
        var daoNguoc = Parse(EditGolden(root =>
        {
            var rows = Dong(root);
            var dao = rows.Select(r => r!.DeepClone()).Reverse().ToList();
            rows.Clear();
            foreach (var r in dao) rows.Add(r);
        }));

        Assert.Equal(CheckStatus.Dat, HangMuc(daoNguoc).Status);
    }

    [Fact]
    public void AC1_ChoDuLieuChuyenSau_Preimage_DuMotDongChoMoiDongKetQua()
    {
        var item = HangMuc(Golden());

        Assert.Equal(SoDongKetQua, item.Preimage!.TrimEnd('\n').Split('\n').Length);
    }

    [Fact]
    public void AC1_ThemMotDongKetQua_ChuyenKhongDat()
    {
        // Dòng không có vé nào chính là khe mà chuỗi băm nhật ký bốc không bịt được.
        var them = Parse(EditGolden(root => Dong(root).Add(new JsonObject
        {
            ["applicantId"] = "ffffffff-0000-0000-0000-0000000000ff",
            ["won"] = true,
            ["tier"] = "UuTienTrucTiep",
            ["typeCode"] = "2PN",
            ["unitCode"] = "2PN-012",
            ["waitlistRank"] = null,
            ["cancelledAt"] = null,
        })));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(them).Status);
        Assert.Equal(CheckStatus.KhongDat, Verifier.Verify(new VerificationInput(them)).Overall);
    }

    [Fact]
    public void AC1_XoaMotDongKetQua_ChuyenKhongDat()
    {
        var xoa = Parse(EditGolden(root => Dong(root).RemoveAt(0)));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(xoa).Status);
    }

    // ── AC2: dòng đã huỷ kết quả vẫn nằm trong phép băm đúng quy tắc canonical ───────────

    [Fact]
    public void AC2_DongDaHuyKetQua_VanNamTrongPhepBam_XoaDiLaKhongDat()
    {
        var huyRoiXoa = Parse(EditGolden(root =>
        {
            var dong = DongCoCan(root);
            dong["cancelledAt"] = "2026-08-12T10:00:00Z";
            Dong(root).Remove(dong);
        }));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(huyRoiXoa).Status);
    }

    [Fact]
    public void AC2_DongDaHuyKetQua_SuaPhanLoiVanBiBat()
    {
        var huyRoiSua = Parse(EditGolden(root =>
        {
            var dong = DongCoCan(root);
            dong["cancelledAt"] = "2026-08-12T10:00:00Z";
            dong["unitCode"] = "2PN-999";
        }));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(huyRoiSua).Status);
    }

    [Fact]
    public void AC2_SoLieuTho_NeuSoDongDaHuyKetQua_DeNguoiKiemDoiChieuVoiBienBan()
    {
        var huy = Parse(EditGolden(root => DongCoCan(root)["cancelledAt"] = "2026-08-12T10:00:00Z"));

        var item = HangMuc(huy);

        Assert.Contains(
            item.Metrics,
            m => m.Label.Contains("huỷ kết quả", StringComparison.Ordinal) && m.Value == "1");
    }

    // ── AC3: sửa một dòng kết quả (mã căn hoặc hạng dự khuyết) → KHÔNG ĐẠT ───────────────

    [Fact]
    public void AC3_SuaMaCanCuaMotDongKetQua_ChuyenKhongDat()
    {
        var sua = Parse(EditGolden(root => DongCoCan(root)["unitCode"] = "2PN-999"));

        var item = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.NotEqual(item.Expected, item.Actual);
        Assert.Equal(CheckStatus.KhongDat, Verifier.Verify(new VerificationInput(sua)).Overall);
    }

    [Fact]
    public void AC3_SuaHangDuKhuyetCuaMotDongKetQua_ChuyenKhongDat()
    {
        var sua = Parse(EditGolden(root =>
            DongDuKhuyet(root)["waitlistRank"] = DongDuKhuyet(root)["waitlistRank"]!.GetValue<int>() + 1));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    [Theory]
    [InlineData("won")]
    [InlineData("tier")]
    [InlineData("typeCode")]
    public void AC3_SuaTruongBatBienKhacCuaMotDongKetQua_ChuyenKhongDat(string truong)
    {
        var sua = Parse(EditGolden(root =>
        {
            var dong = DongCoCan(root);
            dong[truong] = truong == "won"
                ? !dong["won"]!.GetValue<bool>()
                : dong[truong]!.GetValue<string>() + "_DA_SUA";
        }));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    [Fact]
    public void AC3_DoiDinhDanhHoSoCuaMotDong_ChuyenKhongDat()
    {
        var sua = Parse(EditGolden(root =>
            DongCoCan(root)["applicantId"] = "ffffffff-0000-0000-0000-0000000000ff"));

        Assert.Equal(CheckStatus.KhongDat, HangMuc(sua).Status);
    }

    // ── AC4: đổi thời điểm huỷ của một dòng KHÔNG làm hạng mục đổi trạng thái ────────────

    [Fact]
    public void AC4_DoiThoiDiemHuyCuaMotDong_VanDat_ViCancelledAtKhongNamTrongMaBam()
    {
        var goc = HangMuc(Golden());
        var huy = Parse(EditGolden(root => DongCoCan(root)["cancelledAt"] = "2026-08-12T10:00:00Z"));

        var item = HangMuc(huy);

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Equal(goc.Actual, item.Actual);
    }

    [Fact]
    public void AC4_HuyRoiHuyLai_MocThoiGianKhacNhau_VanRaCungMaBam()
    {
        string MaBam(string moc) =>
            HangMuc(Parse(EditGolden(root => DongCoCan(root)["cancelledAt"] = moc))).Actual!;

        Assert.Equal(MaBam("2026-08-12T10:00:00Z"), MaBam("2026-09-01T23:59:59Z"));
    }

    [Fact]
    public void AC4_CauGiaiThich_NoiRoTruongDoiHopLeSauLeKhongNamTrongMaBam()
    {
        Assert.Contains("huỷ kết quả", HangMuc(Golden()).Explanation, StringComparison.Ordinal);
    }

    // ── Thiếu dữ liệu: KHÔNG KIỂM ĐƯỢC, không được biến thành KHÔNG ĐẠT ──────────────────

    [Fact]
    public void BaoCaoKhongCoKhoiKetQua_KhongKiemDuoc_ChuKhongImLang()
    {
        var thieu = Parse(EditGolden(root => root.Remove("ketQua")));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    [Fact]
    public void KhongCongBoMaBamBangKetQua_KhongKiemDuoc()
    {
        var thieu = Parse(EditGolden(root => KetQua(root)["resultsHash"] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    [Fact]
    public void KhongCongBoDongKetQuaNao_KhongKiemDuoc()
    {
        var rong = Parse(EditGolden(root => KetQua(root)["rows"] = new JsonArray()));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(rong).Status);
    }

    [Theory]
    [InlineData("applicantId")]
    [InlineData("won")]
    [InlineData("tier")]
    public void DongKetQuaThieuTruongBatBuoc_KhongKiemDuoc_ChuKhongKhongDat(string truong)
    {
        var thieu = Parse(EditGolden(root => DongCoCan(root)[truong] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(thieu).Status);
    }

    [Fact]
    public void DinhDanhHoSoKhongPhaiUuid_KhongKiemDuoc_ViCanonicalSapXepTheoDinhDanh()
    {
        var la = Parse(EditGolden(root => DongCoCan(root)["applicantId"] = "khong-phai-uuid"));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMuc(la).Status);
    }

    [Fact]
    public void HaiDongCungMotHoSo_KhongDat_ViMoiHoSoChiCoMotDongKetQua()
    {
        var trung = Parse(EditGolden(root => Dong(root).Add(DongCoCan(root).DeepClone())));

        var item = HangMuc(trung);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("một dòng kết quả", item.Explanation, StringComparison.Ordinal);
    }

    // ── Số liệu thô và câu giải thích ────────────────────────────────────────────────────

    [Fact]
    public void SoLieuTho_NeuSoDongKetQua_DeDoiChieuVoiBangCongBo()
    {
        var item = HangMuc(Golden());

        Assert.Contains(
            item.Metrics,
            m => m.Label.Contains("Số dòng kết quả", StringComparison.Ordinal)
                 && m.Value == SoDongKetQua.ToString());
    }

    [Fact]
    public void CauGiaiThich_KhongDeLoHex_ViCheDoNguoiDanVeThangCauNay()
    {
        var item = HangMuc(Golden());

        Assert.False(string.IsNullOrWhiteSpace(item.Explanation));
        Assert.DoesNotContain(Golden().Results!.ResultsHash!, item.Explanation, StringComparison.Ordinal);
    }
}
