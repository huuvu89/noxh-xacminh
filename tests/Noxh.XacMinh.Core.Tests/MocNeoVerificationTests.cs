using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #15 — mốc neo trên chuỗi khối công khai, đi qua đúng seam <see cref="Verifier.Verify"/>.
/// Hạng mục này trả lời hai câu: block ở độ cao đã cam kết có <b>thật</b> không, và cam kết có được
/// chốt <b>trước</b> khi block đó tồn tại không — lá chắn chống thử đi thử lại hạt giống.
///
/// Việc gọi mạng nằm ở vỏ UI: ở đây block đọc được đi vào lõi dưới dạng dữ liệu
/// (<see cref="VerificationInput.Blocks"/>), nên toàn bộ test chạy tất định, không cần mạng.
/// </summary>
public class MocNeoVerificationTests
{
    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var ketQua = TransparencyJson.Parse(json);
        Assert.True(ketQua.Success, ketQua.ErrorMessage);

        return ketQua.Report!;
    }

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);

        return root.ToJsonString();
    }

    private static JsonObject CamKetNeo(JsonObject root) => root["camKetNeo"]!.AsObject();

    private static IEnumerable<JsonObject> NguonNgauNhien(JsonObject root) =>
        root["nguonNgauNhien"]!.AsArray().Select(n => n!.AsObject());

    private static JsonArray Dau(JsonObject root) => root["dauThoiGian"]!.AsArray();

    private static IReadOnlyList<CheckResult> HangMuc(
        TransparencyReport report, IReadOnlyList<QuanSatKhoi>? doc = null) =>
        Verifier.Verify(new VerificationInput(report, null, doc)).Items
            .Where(i => i.Id.StartsWith(CheckIds.MocNeo, StringComparison.Ordinal))
            .ToList();

    private static CheckResult MotVong(
        TransparencyReport report, IReadOnlyList<QuanSatKhoi>? doc = null) =>
        HangMuc(report, doc)[0];

    // ── AC1: đọc block ở độ cao đã cam kết, cho đúng chuỗi khối từng vòng thực sự dùng ───

    [Fact]
    public void AC1_FixtureChuanVang_MoiVongMotKetLuanRieng()
    {
        var golden = Golden();

        var items = HangMuc(golden, QuanSatKhoiGolden.DocDuoc());

        Assert.Equal(golden.EntropySources!.Count, items.Count);
        foreach (var vong in golden.EntropySources!.Select(n => n.Round))
            Assert.Contains(items, i => i.Id == $"{CheckIds.MocNeo}:{vong}");
    }

    [Fact]
    public void AC1_BlockDocDuocODoCaoDaCamKet_MaBamKhop_ChotTruocKhiBlockRaDoi_Dat()
    {
        var items = HangMuc(Golden(), QuanSatKhoiGolden.DocDuoc());

        Assert.All(items, i => Assert.Equal(CheckStatus.Dat, i.Status));
    }

    [Fact]
    public void AC1_DocDungDoCaoDaCamKet_ChuKhongPhaiDoCaoNaoKhac()
    {
        // Nguồn công khai trả block ở một độ cao khác: không phải thứ đã cam kết, coi như chưa đọc.
        var lechDoCao = new[]
        {
            new QuanSatKhoi(
                ChuoiKhoiNeo.Ethereum,
                QuanSatKhoiGolden.DoCao + 1,
                QuanSatKhoiGolden.MaBam,
                QuanSatKhoiGolden.ThoiDiemDao,
                QuanSatKhoiGolden.TenNguon),
        };

        Assert.Equal(CheckStatus.KhongKiemDuoc, MotVong(Golden(), lechDoCao).Status);
    }

    [Fact]
    public void AC1_VongNeoVaoBitcoin_LayDoCaoDaCamKetCuaBitcoin_ChuKhongPhaiCuaEthereum()
    {
        var btc = Parse(EditGolden(root =>
        {
            var doCao = CamKetNeo(root)["btcTargetHeight"]!.GetValue<long>();
            foreach (var nguon in NguonNgauNhien(root))
            {
                nguon["anchorChain"] = "bitcoin";
                nguon["blockHeight"] = doCao;
            }
        }));

        var doc = new[]
        {
            new QuanSatKhoi(
                ChuoiKhoiNeo.Bitcoin,
                btc.AnchorCommitment!.BtcTargetHeight!.Value,
                btc.EntropySources![0].BlockHash,
                QuanSatKhoiGolden.ThoiDiemDao,
                QuanSatKhoiGolden.TenNguon),
        };

        Assert.Equal(CheckStatus.Dat, MotVong(btc, doc).Status);
        // Quan sát của chuỗi khác không được đem dùng thay.
        Assert.Equal(CheckStatus.KhongKiemDuoc, MotVong(btc, QuanSatKhoiGolden.DocDuoc()).Status);
    }

    [Fact]
    public void AC1_VongKhongNoiDungChuoiKhoiNao_KhongKiemDuoc()
    {
        var la = Parse(EditGolden(root =>
        {
            foreach (var nguon in NguonNgauNhien(root)) nguon["anchorChain"] = "chuoi-khoi-la";
        }));

        var item = MotVong(la, QuanSatKhoiGolden.DocDuoc());

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("chuoi-khoi-la", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC1_BaoCaoThieuKhoiCamKetNeo_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieu = Parse(EditGolden(root => root.Remove("camKetNeo")));

        Assert.Equal(CheckStatus.KhongKiemDuoc, MotVong(thieu, QuanSatKhoiGolden.DocDuoc()).Status);
    }

    // ── AC2: so mã băm block đọc được với mã băm công bố ────────────────────────────────

    [Fact]
    public void AC2_MaBamBlockDocDuocKhacMaBamCongBo_KhongDat()
    {
        var khac = new[]
        {
            new QuanSatKhoi(
                ChuoiKhoiNeo.Ethereum,
                QuanSatKhoiGolden.DoCao,
                new string('a', 64),
                QuanSatKhoiGolden.ThoiDiemDao,
                QuanSatKhoiGolden.TenNguon),
        };

        var item = MotVong(Golden(), khac);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.NotEqual(item.Expected, item.Actual);
        Assert.Equal(CheckStatus.KhongDat, Verifier.Verify(new VerificationInput(Golden(), null, khac)).Overall);
    }

    [Fact]
    public void AC2_VongChuaCongBoMaBamKhoiNeo_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieu = Parse(EditGolden(root =>
        {
            foreach (var nguon in NguonNgauNhien(root)) nguon["blockHash"] = null;
        }));

        Assert.Equal(CheckStatus.KhongKiemDuoc, MotVong(thieu, QuanSatKhoiGolden.DocDuoc()).Status);
    }

    [Fact]
    public void AC2_NguonCongKhaiKhongTraMaBam_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var trong = new[]
        {
            new QuanSatKhoi(
                ChuoiKhoiNeo.Ethereum,
                QuanSatKhoiGolden.DoCao,
                null,
                QuanSatKhoiGolden.ThoiDiemDao,
                QuanSatKhoiGolden.TenNguon),
        };

        Assert.Equal(CheckStatus.KhongKiemDuoc, MotVong(Golden(), trong).Status);
    }

    // ── AC3: so thời điểm cấp dấu thời gian với thời điểm block được đào ─────────────────

    [Fact]
    public void AC3_CamKetChotSauKhiBlockDaDuocDao_KhongDat()
    {
        // Block được đào TRƯỚC lúc dấu thời gian được cấp ⇒ người chốt đã biết mã băm dùng làm
        // hạt giống. Đây đúng là ca mà cả hạng mục này sinh ra để bắt.
        var daoTruoc = new[]
        {
            new QuanSatKhoi(
                ChuoiKhoiNeo.Ethereum,
                QuanSatKhoiGolden.DoCao,
                QuanSatKhoiGolden.MaBam,
                new DateTimeOffset(2026, 8, 12, 20, 20, 0, TimeSpan.Zero),
                QuanSatKhoiGolden.TenNguon),
        };

        var item = MotVong(Golden(), daoTruoc);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("thử đi thử lại", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_KetLuanNeuRoYNghiaCuaPhepSo_VaCaHaiMocThoiGian()
    {
        var item = MotVong(Golden(), QuanSatKhoiGolden.DocDuoc());

        Assert.Contains("thử đi thử lại", item.Explanation, StringComparison.Ordinal);
        Assert.Contains(item.Metrics, m => m.Label.Contains("block được đào", StringComparison.Ordinal));
        Assert.Contains(item.Metrics, m => m.Label.Contains("chốt cam kết", StringComparison.Ordinal));
    }

    [Fact]
    public void AC3_NguonCongKhaiKhongChoBietThoiDiemDao_KhongKiemDuoc_ChuKhongDat()
    {
        var thieuGio = new[]
        {
            new QuanSatKhoi(
                ChuoiKhoiNeo.Ethereum,
                QuanSatKhoiGolden.DoCao,
                QuanSatKhoiGolden.MaBam,
                null,
                QuanSatKhoiGolden.TenNguon),
        };

        Assert.Equal(CheckStatus.KhongKiemDuoc, MotVong(Golden(), thieuGio).Status);
    }

    [Fact]
    public void AC3_KhongCoDauThoiGianDocLap_MaBamKhopVanChiRaKhongKiemDuoc()
    {
        // Chỉ còn thời điểm ban tổ chức tự khai: không đủ để nói lá chắn còn nguyên.
        var khongDau = Parse(EditGolden(root => Dau(root).Clear()));

        var item = MotVong(khongDau, QuanSatKhoiGolden.DocDuoc());

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("tự khai", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_BanToChucTuKhaiChotSauKhiBlockRaDoi_VanKhongDat()
    {
        // Không có dấu độc lập, nhưng chính con số ban tổ chức khai đã nói cam kết có sau block.
        var khongDau = Parse(EditGolden(root =>
        {
            Dau(root).Clear();
            CamKetNeo(root)["anchorFrozenAt"] = "2026-08-12T20:35:00.0000000Z";
        }));

        Assert.Equal(CheckStatus.KhongDat, MotVong(khongDau, QuanSatKhoiGolden.DocDuoc()).Status);
    }

    // ── AC4: nguồn ngoài lỗi/chặn ⇒ CHƯA ĐỦ DỮ LIỆU kèm link tra cứu thủ công ────────────

    [Fact]
    public void AC4_ChuaTraCuuDuocBlockNao_KhongKiemDuoc_KemLinkTraCuuThuCong()
    {
        var item = MotVong(Golden());

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains($"https://etherscan.io/block/{QuanSatKhoiGolden.DoCao}", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_NguonNgoaiBaoLoi_KhongKiemDuoc_NoiRoLoi_VaKemLink()
    {
        var loi = new[]
        {
            new QuanSatKhoi(
                ChuoiKhoiNeo.Ethereum,
                QuanSatKhoiGolden.DoCao,
                Loi: "HTTP 503 từ nguồn công khai"),
        };

        var item = MotVong(Golden(), loi);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("HTTP 503", item.Explanation, StringComparison.Ordinal);
        Assert.Contains($"https://etherscan.io/block/{QuanSatKhoiGolden.DoCao}", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC4_KhongTraCuuDuoc_KhongBaoGioRaDat()
    {
        Assert.Equal(CheckStatus.KhongKiemDuoc, Verifier.Verify(new VerificationInput(Golden())).Overall);
    }

    // ── AC5: lõi thuần, tất định, không cần mạng ─────────────────────────────────────────

    [Fact]
    public void AC5_LoiKiemThuan_HaiLanKiemCungDuLieuRaKetQuaBangNhau()
    {
        var a = HangMuc(Golden(), QuanSatKhoiGolden.DocDuoc());
        var b = HangMuc(Golden(), QuanSatKhoiGolden.DocDuoc());

        Assert.Equal(a, b);
    }

    [Fact]
    public void AC5_LoiSuyRaDanhSachBlockCanTraCuu_DeVoUiDiGoiMang()
    {
        var can = MocNeoTraCuu.CanDoc(Golden());

        var yeu = Assert.Single(can);
        Assert.Equal(ChuoiKhoiNeo.Ethereum, yeu.ChuoiKhoi);
        Assert.Equal(QuanSatKhoiGolden.DoCao, yeu.DoCao);
        Assert.Equal($"https://etherscan.io/block/{QuanSatKhoiGolden.DoCao}", yeu.Link);
    }

    [Fact]
    public void AC5_BaoCaoThieuDuLieuNeo_KhongSinhYeuCauTraCuuNao()
    {
        var thieu = Parse(EditGolden(root => root.Remove("camKetNeo")));

        Assert.Empty(MocNeoTraCuu.CanDoc(thieu));
    }

    // ── AC6: fixture sửa độ cao block đã cam kết ⇒ hạng mục chuyển KHÔNG ĐẠT ─────────────

    [Fact]
    public void AC6_SuaDoCaoBlockDaCamKet_ChuyenKhongDat()
    {
        var sua = Parse(EditGolden(root => CamKetNeo(root)["ethTargetHeight"] = 999_999));

        var item = MotVong(sua, QuanSatKhoiGolden.DocDuoc());

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Equal("999999", item.Expected);
        Assert.Equal(QuanSatKhoiGolden.DoCao.ToString(), item.Actual);
    }

    [Fact]
    public void AC6_SuaDoCaoKhoiNeoMotVong_ChiVongDoKhongDat()
    {
        var sua = Parse(EditGolden(root => NguonNgauNhien(root).First()["blockHeight"] = 999_999));

        var items = HangMuc(sua, QuanSatKhoiGolden.DocDuoc());

        Assert.Equal(CheckStatus.KhongDat, items[0].Status);
        Assert.All(items.Skip(1), i => Assert.Equal(CheckStatus.Dat, i.Status));
    }

    [Fact]
    public void AC6_VongChuaCongBoDoCaoKhoiNeo_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieu = Parse(EditGolden(root =>
        {
            foreach (var nguon in NguonNgauNhien(root)) nguon["blockHeight"] = null;
        }));

        Assert.Equal(CheckStatus.KhongKiemDuoc, MotVong(thieu, QuanSatKhoiGolden.DocDuoc()).Status);
    }

    // ── Chế độ chuyên sâu: đủ số liệu để người kiểm tự mở link đối chiếu bằng mắt ────────

    [Fact]
    public void CheDoChuyenSau_NeuNguonCongKhaiDaHoi_VaLinkTraCuu()
    {
        var item = MotVong(Golden(), QuanSatKhoiGolden.DocDuoc());

        Assert.Contains(item.Metrics, m => m.Value == QuanSatKhoiGolden.TenNguon);
        Assert.Contains(item.Metrics, m => m.Value.Contains("etherscan.io", StringComparison.Ordinal));
    }

    [Fact]
    public void CauGiaiThich_KhiDat_KhongDeLoHex()
    {
        var item = MotVong(Golden(), QuanSatKhoiGolden.DocDuoc());

        Assert.DoesNotContain(QuanSatKhoiGolden.MaBam, item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void BaoCaoKhongCoNguonNgauNhien_VanRaMotKetLuanCanhBao_ChuKhongImLang()
    {
        var thieu = Parse(EditGolden(root => root.Remove("nguonNgauNhien")));

        var item = Assert.Single(HangMuc(thieu, QuanSatKhoiGolden.DocDuoc()));

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
    }
}
