using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Crypto;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Units;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #14 — tái lập vòng cuối (C): một chồng phiếu duy nhất trên quỹ căn dư chung, và <b>số dự
/// khuyết</b>. Số dự khuyết là hoán vị dựng từ hạt giống rồi gán vào các ô dự khuyết theo vị trí
/// tăng dần, nên nó độc lập với thời điểm bấm — tái lập được nó là chứng minh trực tiếp điều đó.
/// Quy mô danh sách dự khuyết không nằm trong mã băm đầu vào, nên phải lấy từ báo cáo.
/// </summary>
public class TaiLapVongCanDuTests
{
    private const string VongCanDu = "C";

    /// <summary>Quỹ căn dư của fixture: 20 căn trong danh mục trừ 16 căn đã phân ở vòng ưu tiên và vòng bốc thẳng.</summary>
    private static readonly string[] QuyCanDu = ["1PN-002", "1PN-006", "2PN-009", "2PN-011"];

    /// <summary>Cùng quỹ đó sau khi xáo bằng hạt giống vòng C — thứ tự chảy vào chồng phiếu.</summary>
    private static readonly string[] QuyDaXao = ["2PN-009", "2PN-011", "1PN-006", "1PN-002"];

    /// <summary>Số dự khuyết theo thứ tự các ô dự khuyết trong chồng phiếu đã công bố.</summary>
    private static readonly int[] SoDuKhuyetTheoO = [3, 2, 1, 4, 5];

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static UnitCatalog DanhMuc()
    {
        var ketQua = UnitCatalogJson.Parse(GoldenFixture.UnitCatalogBytes());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);
        return ketQua.Catalog!;
    }

    private static CheckResult TaiLap(TransparencyReport report, UnitCatalog? danhMuc, string vong = VongCanDu) =>
        Assert.Single(
            Verifier.Verify(new VerificationInput(report, danhMuc)).Items,
            i => i.Id == $"{CheckIds.DeckRebuild}:{vong}");

    private static DeckRebuild DungLai(TransparencyReport report, UnitCatalog? danhMuc)
    {
        var taiLap = DeckRebuilder.Rebuild(report, report.Decks!.Single(d => d.Round == VongCanDu), danhMuc);

        Assert.NotNull(taiLap);
        return taiLap!;
    }

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonObject ChongPhieuC(JsonObject root) =>
        root["decks"]!.AsArray().First(d => d!["round"]!.GetValue<string>() == VongCanDu)!.AsObject();

    /// <summary>Ghi lại nội dung vé kèm mã băm của chính nội dung đó — dựng ca "niêm phong bản đã sửa".</summary>
    private static void DatVe(JsonObject chongPhieu, IReadOnlyList<string> ve)
    {
        chongPhieu["tickets"] = new JsonArray([.. ve.Select(v => (JsonNode)JsonValue.Create(v)!)]);
        chongPhieu["deckHash"] = CanonicalDeckSerializer.Hash(ve);
    }

    private static byte[] HatGiongGocC(TransparencyReport report) =>
        Hex.Doc(report.EntropySources!.Single(n => n.Round == "C").MasterSeed)!;

    private static IReadOnlyList<string> VeCongBo(TransparencyReport report) =>
        report.Decks!.Single(d => d.Round == VongCanDu).Tickets!.Select(v => v!).ToList();

    private static IReadOnlyList<int> SoDuKhuyetCua(IEnumerable<string> ve) =>
        ve.Where(v => v.StartsWith("DU_KHUYET:", StringComparison.Ordinal))
            .Select(v => int.Parse(v["DU_KHUYET:".Length..]))
            .ToList();

    // ── AC1: chồng phiếu vòng cuối dựng lại và so mã băm với bản công bố ─────────────────

    [Fact]
    public void AC1_ChongPhieuVongCanDu_DungLaiDuoc_VaKhopMaBamDaNiemPhong()
    {
        var golden = Golden();

        var item = TaiLap(golden, DanhMuc());

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Equal(golden.Decks!.Single(d => d.Round == VongCanDu).DeckHash, item.Expected);
        Assert.Equal(item.Expected, item.Actual);
    }

    [Fact]
    public void AC1_BanDungLai_RaDungTungLaVeDangCongBo_ChuKhongChiTrungMaBam()
    {
        var golden = Golden();

        Assert.Equal(VeCongBo(golden), DungLai(golden, DanhMuc()).Tickets);
    }

    /// <summary>
    /// Quỹ căn dư của vòng cuối là <b>một quỹ chung</b>, không đi theo loại căn: mọi căn chưa ai
    /// nhận sau vòng bốc thẳng đều vào chung một chồng phiếu.
    /// </summary>
    [Fact]
    public void AC1_QuyCanDuChung_SuyRaTuBangKetQua_GomMoiCanChuaAiNhanSauVongTruoc()
    {
        var taiLap = DungLai(Golden(), DanhMuc());

        Assert.Equal(QuyCanDu, taiLap.LeftoverUnits);
        Assert.Equal(16, taiLap.AllocatedBefore!.Count);
        Assert.Null(taiLap.TypeCode);
    }

    /// <summary>
    /// Vector ghim: quỹ căn dư sau khi xáo bằng <c>C:units</c> — thứ tự các căn chảy vào chồng phiếu.
    /// Không báo cáo nào công bố dãy này, nó là thứ công cụ tự dựng lại rồi đem đối chiếu.
    /// </summary>
    [Fact]
    public void AC1_QuyCanDu_XaoBangHatGiongVongC_RaDungCacCanDaThanhVeTrung()
    {
        var golden = Golden();

        var taiLap = DungLai(golden, DanhMuc());

        Assert.Equal(QuyDaXao, taiLap.PoolUnits);
        Assert.Equal(
            VeCongBo(golden)
                .Where(v => v.StartsWith("TRUNG:", StringComparison.Ordinal))
                .Select(v => v["TRUNG:".Length..])
                .Order(StringComparer.Ordinal),
            taiLap.PoolUnits!.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AC1_DoiMotByteHatGiongVongC_ChongPhieuVongCanDuLech_VongTruocKhongDoi()
    {
        var lech = Parse(EditGolden(root =>
        {
            var nguon = root["nguonNgauNhien"]!.AsArray().First(n => n!["round"]!.GetValue<string>() == "C")!.AsObject();
            var hex = nguon["masterSeed"]!.GetValue<string>();
            nguon["masterSeed"] = hex[..^2] + (Convert.ToByte(hex[^2..], 16) ^ 0x01).ToString("x2");
        }));

        Assert.Equal(CheckStatus.KhongDat, TaiLap(lech, DanhMuc()).Status);
        Assert.Equal(CheckStatus.Dat, TaiLap(lech, DanhMuc(), "B:2PN").Status);
    }

    /// <summary>
    /// Ban tổ chức tự sắp lại chồng phiếu rồi niêm phong chính bản sắp đặt (mã băm công bố khớp nội
    /// dung công bố). Quỹ căn dư không đổi ⇒ suy diễn vẫn nhất quán ⇒ phải kết luận thẳng KHÔNG ĐẠT.
    /// </summary>
    [Fact]
    public void AC1_ChongPhieuSapLaiRoiNiemPhongBanSapDat_KetLuanKhongDat_ChuKhongNe()
    {
        var sapDat = Parse(EditGolden(root =>
        {
            var ve = ChongPhieuC(root)["tickets"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
            var i = ve.FindIndex(v => v != ve[0]);
            (ve[0], ve[i]) = (ve[i], ve[0]);
            DatVe(ChongPhieuC(root), ve);
        }));

        var item = TaiLap(sapDat, DanhMuc());

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.NotEqual(item.Expected, item.Actual);
    }

    // ── AC2: số dự khuyết của từng ô dựng lại và khớp nội dung vé công bố ────────────────

    [Fact]
    public void AC2_SoDuKhuyetTungO_DungLaiTuHatGiong_KhopSoTrenVeDaCongBo()
    {
        var golden = Golden();

        var taiLap = DungLai(golden, DanhMuc());

        Assert.Equal(SoDuKhuyetTheoO, taiLap.WaitlistNumbers);
        Assert.Equal(SoDuKhuyetCua(VeCongBo(golden)), taiLap.WaitlistNumbers);
    }

    /// <summary>
    /// Đổi chỗ hai số dự khuyết rồi niêm phong bản đã đổi: nếu công cụ chỉ đếm số ô dự khuyết thì ca
    /// này lọt lưới. Đây chính là ca "bấm sớm được hạng nhỏ hơn" nếu ban tổ chức tự đánh số lại.
    /// </summary>
    [Fact]
    public void AC2_DoiChoHaiSoDuKhuyet_KetLuanKhongDat_ChuKhongChiDemSoOduKhuyet()
    {
        var doiSo = Parse(EditGolden(root =>
        {
            var ve = ChongPhieuC(root)["tickets"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
            var o = Enumerable.Range(0, ve.Count).Where(i => ve[i].StartsWith("DU_KHUYET:")).Take(2).ToList();
            (ve[o[0]], ve[o[1]]) = (ve[o[1]], ve[o[0]]);
            DatVe(ChongPhieuC(root), ve);
        }));

        Assert.Equal(CheckStatus.KhongDat, TaiLap(doiSo, DanhMuc()).Status);
    }

    [Fact]
    public void AC2_SoDuKhuyet_HienRaXemDuoc_KemNhanVaHatGiongDanXuatRiengCuaNo()
    {
        var item = TaiLap(Golden(), DanhMuc());

        Assert.Equal(
            LotteryLabels.CWaitlist,
            Assert.Single(item.Metrics, m => m.Label.Contains("Nhãn dẫn xuất số dự khuyết", StringComparison.Ordinal))
                .Value);
        Assert.Contains(item.Metrics, m => m.Label.Contains("Hạt giống dẫn xuất số dự khuyết", StringComparison.Ordinal));
        Assert.Contains("3 · 2 · 1 · 4 · 5",
            Assert.Single(item.Metrics, m => m.Label.Contains("Số dự khuyết dựng lại", StringComparison.Ordinal)).Value);
    }

    /// <summary>Quy mô danh sách dự khuyết là dữ liệu của dự án, không nằm trong mã băm — lấy từ báo cáo.</summary>
    [Fact]
    public void AC2_QuyMoDanhSachDuKhuyet_LayTuBaoCao_VaHienRaXemDuoc()
    {
        var taiLap = DungLai(Golden(), DanhMuc());

        Assert.Equal(5, taiLap.WaitlistSize);
        Assert.Equal(5, taiLap.WaitlistCount);
        Assert.Contains(
            "5",
            Assert.Single(
                TaiLap(Golden(), DanhMuc()).Metrics,
                m => m.Label.Contains("Quy mô danh sách dự khuyết", StringComparison.Ordinal)).Value);
    }

    /// <summary>
    /// Báo cáo không công bố quy mô danh sách dự khuyết: công cụ hiểu là "không giới hạn" (đúng như
    /// máy chủ hiểu khi dự án bỏ trống), nhưng đó là một <b>giả định</b> — lệch thì báo chưa kiểm
    /// được, không được kết luận KHÔNG ĐẠT dựa trên giả định của chính mình.
    /// </summary>
    [Fact]
    public void AC2_BaoCaoKhongCongBoQuyMoDuKhuyet_LechThiKhongKiemDuoc_ChuKhongKhongDat()
    {
        var thieuQuyMo = Parse(EditGolden(root => root.Remove("waitlistSize")));

        var item = TaiLap(thieuQuyMo, DanhMuc());

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("dự khuyết", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("giả định", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AC2_QuyMoDuKhuyetAm_KhongKiemDuoc_ChuKhongDungBuaMotConSoVoLy()
    {
        var quyMoAm = Parse(EditGolden(root => root["waitlistSize"] = -1));

        Assert.Equal(CheckStatus.KhongKiemDuoc, TaiLap(quyMoAm, DanhMuc()).Status);
    }

    // ── AC3: bỏ qua toàn bộ vòng cuối (không ai tham gia) ───────────────────────────────

    /// <summary>
    /// Không ai xác nhận tham gia thì máy chủ bỏ qua cả vòng cuối: báo cáo không có chồng phiếu vòng
    /// C nào. Đó là buổi lễ hợp lệ — công cụ không được dựng ra một hạng mục KHÔNG ĐẠT từ chỗ trống.
    /// </summary>
    [Fact]
    public void AC3_BoQuaToanBoVongCanDu_KhongCoHangMucVongC_VaKhongHangMucNaoKhongDat()
    {
        var boQua = Parse(EditGolden(root =>
        {
            var decks = root["decks"]!.AsArray();
            var i = Enumerable.Range(0, decks.Count)
                .First(k => decks[k]!["round"]!.GetValue<string>() == VongCanDu);
            decks.RemoveAt(i);
        }));

        var bao = Verifier.Verify(new VerificationInput(boQua, DanhMuc()));

        Assert.DoesNotContain(bao.Items, i => i.Id == $"{CheckIds.DeckRebuild}:{VongCanDu}");
        Assert.DoesNotContain(bao.Items, i => i.Status == CheckStatus.KhongDat);
        Assert.Equal(CheckStatus.Dat, TaiLap(boQua, DanhMuc(), "B:2PN").Status);
    }

    [Fact]
    public void AC3_ChongPhieuVongCanDuRong_KhongNemLoi_VaVanDoiChieuMaBam()
    {
        var rong = Parse(EditGolden(root =>
        {
            var chongPhieu = ChongPhieuC(root);
            chongPhieu["size"] = 0;
            chongPhieu["wonCount"] = 0;
            DatVe(chongPhieu, []);
        }));

        var item = TaiLap(rong, DanhMuc());

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Empty(DungLai(rong, DanhMuc()).Tickets!);
    }

    // ── AC4: không còn căn dư nhưng vẫn có người tham gia ───────────────────────────────

    /// <summary>
    /// Vòng cuối vẫn chạy khi quỹ căn dư rỗng: cả chồng phiếu chỉ còn phiếu dự khuyết và vé không
    /// trúng. Công cụ phải dựng lại được và kết luận ĐẠT — không được coi quỹ rỗng là dữ liệu thiếu.
    /// </summary>
    [Fact]
    public void AC4_HetCanDuNhungVanCoNguoiThamGia_DungLaiDuoc_VaKetLuanDat()
    {
        var hetCan = Parse(HetCanDuNhungVanBoc(out var veMongDoi));

        var item = TaiLap(hetCan, DanhMuc());
        var taiLap = DungLai(hetCan, DanhMuc());

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Equal(veMongDoi, taiLap.Tickets);
        Assert.Empty(taiLap.LeftoverUnits!);
        Assert.Equal(0, taiLap.WonCount);
        Assert.Equal(5, taiLap.WaitlistCount);
    }

    /// <summary>
    /// Fixture "hết căn dư": mọi căn của danh mục đã được phân trước vòng cuối, nhưng 21 người vẫn
    /// bốc. Chồng phiếu mong đợi dựng thẳng theo công thức của máy chủ (5 dự khuyết + 16 không trúng),
    /// không lấy lại từ công cụ — nếu không thì test chỉ so công cụ với chính nó.
    /// </summary>
    private static string HetCanDuNhungVanBoc(out IReadOnlyList<string> veMongDoi)
    {
        var golden = Golden();
        var master = HatGiongGocC(golden);
        const int quyMo = 21;
        const int soDuKhuyet = 5;

        var truocKhiXao = new List<string>(quyMo);
        truocKhiXao.AddRange(Enumerable.Repeat("DU_KHUYET", soDuKhuyet));
        truocKhiXao.AddRange(Enumerable.Repeat("KHONG_TRUNG", quyMo - soDuKhuyet));

        var daXao = SeededShuffle.Shuffle(truocKhiXao, MasterSeed.RoundSeed(master, LotteryLabels.CDeck));
        var so = SeededShuffle.Shuffle(
            Enumerable.Range(1, soDuKhuyet).ToList(),
            MasterSeed.RoundSeed(master, LotteryLabels.CWaitlist));
        var k = 0;
        var ve = daXao.Select(v => v == "DU_KHUYET" ? $"DU_KHUYET:{so[k++]}" : v).ToList();
        veMongDoi = ve;

        return EditGolden(root =>
        {
            // Hai căn của vòng cuối chuyển sang tầng vòng bốc thẳng, hai căn chưa ai nhận thì thêm
            // dòng đã phân — quỹ căn dư suy ra còn rỗng.
            foreach (var dong in root["ketQua"]!["rows"]!.AsArray())
                if (dong!["tier"]!.GetValue<string>() == "Leftover")
                    dong["tier"] = "Regular";

            foreach (var can in new[] { "1PN-006", "2PN-009" })
                root["ketQua"]!["rows"]!.AsArray().Add(new JsonObject
                {
                    ["applicantId"] = Guid.NewGuid().ToString(),
                    ["won"] = true,
                    ["tier"] = "Regular",
                    ["unitCode"] = can,
                });

            var chongPhieu = ChongPhieuC(root);
            chongPhieu["size"] = quyMo;
            chongPhieu["wonCount"] = 0;
            DatVe(chongPhieu, ve);
        });
    }

    // ── Suy diễn mâu thuẫn / thiếu dữ liệu → CHƯA ĐỦ DỮ LIỆU ────────────────────────────

    [Fact]
    public void KhongCoDanhMucCan_KhongKiemDuoc_KemHuongDanNapDanhMuc()
    {
        var item = TaiLap(Golden(), danhMuc: null);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("danh mục căn", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nạp", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VeTrungCongBoCanNamNgoaiQuyCanDuSuyRa_KhongKiemDuoc_ChuKhongKhongDat()
    {
        var lechQuy = Parse(EditGolden(root =>
        {
            var ve = ChongPhieuC(root)["tickets"]!.AsArray();
            var i = Enumerable.Range(0, ve.Count).First(k => ve[k]!.GetValue<string>().StartsWith("TRUNG:"));
            ve[i] = JsonValue.Create("TRUNG:2PN-001");
        }));

        var item = TaiLap(lechQuy, DanhMuc());

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("2PN-001", item.Explanation, StringComparison.Ordinal);
    }

    /// <summary>
    /// Số vé trúng công bố ít hơn số căn dư suy ra: máy chủ luôn đưa hết quỹ căn dư vào chồng phiếu,
    /// nên hai con số lệch nhau nghĩa là suy diễn quỹ căn dư sai — chưa kiểm được, chứ không kết luận.
    /// </summary>
    [Fact]
    public void SoVeTrungLechSoCanDuSuyRa_KhongKiemDuoc_VaNeuRoHaiConSo()
    {
        var lechSo = Parse(EditGolden(root => ChongPhieuC(root)["wonCount"] = 3));

        var item = TaiLap(lechSo, DanhMuc());

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("3", item.Explanation, StringComparison.Ordinal);
        Assert.Contains("4", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void BaoCaoKhongCongBoBangKetQua_KhongKiemDuoc_VaNoiRoViSaoKhongSuyDuoc()
    {
        var khongKetQua = Parse(EditGolden(root => root.Remove("ketQua")));

        var item = TaiLap(khongKetQua, DanhMuc());

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("quỹ căn dư", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BaoCaoKhongCongBoHatGiongVongC_KhongKiemDuoc()
    {
        var chuaDongCong = Parse(EditGolden(root =>
        {
            var nguon = root["nguonNgauNhien"]!.AsArray().First(n => n!["round"]!.GetValue<string>() == "C")!.AsObject();
            nguon["masterSeed"] = null;
        }));

        Assert.Equal(CheckStatus.KhongKiemDuoc, TaiLap(chuaDongCong, DanhMuc()).Status);
    }

    // ── Giải thích cho người dân: hạng dự khuyết không phụ thuộc thời điểm bấm ───────────

    [Fact]
    public void HangMucDat_GiaiThichNoiRoHangDuKhuyetKhongPhuThuocThoiDiemBam()
    {
        var item = TaiLap(Golden(), DanhMuc());

        Assert.Equal(CheckStatus.Dat, item.Status);
        Assert.Contains("bấm", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dự khuyết", item.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    // ── Lưới ô phiếu của vòng cuối ──────────────────────────────────────────────────────

    [Fact]
    public void LuoiVongCanDu_MoiODeuDanhDauKhopBanDungLai()
    {
        var luoi = Assert.Single(DeckGridBuilder.Build(Golden(), DanhMuc()), l => l.Round == VongCanDu);

        Assert.NotEmpty(luoi.Cells);
        Assert.All(luoi.Cells, o => Assert.True(o.MatchesRebuild));
        Assert.Equal(luoi.Cells.Count, luoi.Summary.CellsMatchingRebuild);
    }
}
