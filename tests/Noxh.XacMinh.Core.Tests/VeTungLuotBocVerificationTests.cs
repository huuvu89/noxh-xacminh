using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #6 — đối chiếu kết quả từng lượt bốc với vé nằm ở đúng vị trí đó trong chồng phiếu đã niêm
/// phong, đi qua đúng seam <see cref="Verifier.Verify"/>. Bản bị tráo/bị sửa dựng từ chính fixture
/// chuẩn vàng: hạng mục mã băm chồng phiếu và hạng mục chuỗi băm nhật ký đều KHÔNG bắt được kiểu
/// tráo này khi nhật ký vẫn tự nhất quán, nên đây là phép kiểm riêng.
/// </summary>
public class VeTungLuotBocVerificationTests
{
    /// <summary>Vòng C: 21 ô phiếu / 19 lượt bốc — vòng duy nhất trong fixture đủ chỗ cho mọi ca.</summary>
    private const string VongMau = "C";

    private const int SoOPhieuVongC = 21;

    private const int SoLuotBocVongC = 19;

    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static IReadOnlyList<CheckResult> HangMuc(TransparencyReport report) =>
        Verifier.Verify(new VerificationInput(report)).Items
            .Where(i => i.Id == CheckIds.DrawTicketMatch
                        || i.Id.StartsWith($"{CheckIds.DrawTicketMatch}:", StringComparison.Ordinal))
            .ToList();

    private static CheckResult HangMucVong(TransparencyReport report, string vong) =>
        Assert.Single(HangMuc(report), i => i.Id == $"{CheckIds.DrawTicketMatch}:{vong}");

    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    private static JsonArray NhatKy(JsonObject root) => root["nhatKyBoc"]!.AsArray();

    private static JsonArray ChongPhieu(JsonObject root) => root["decks"]!.AsArray();

    private static JsonObject Deck(JsonObject root, string vong) =>
        ChongPhieu(root).First(d => d!["round"]!.GetValue<string>() == vong)!.AsObject();

    private static JsonObject Luot(JsonObject root, string vong, int viTri) =>
        NhatKy(root)
            .First(n => n!["round"]!.GetValue<string>() == vong && n["position"]!.GetValue<int>() == viTri)!
            .AsObject();

    private static IEnumerable<CheckMetric> DiemLech(CheckResult item) =>
        item.Metrics.Where(m => m.Label.StartsWith("Điểm lệch", StringComparison.Ordinal));

    // ── AC1: mọi lượt bốc được đối chiếu với vé ở đúng vị trí trong chồng phiếu ──────────

    [Fact]
    public void AC1_FixtureChuanVang_MoiVongCoKetLuanRieng_VaDeuDat()
    {
        var golden = Golden();

        var items = HangMuc(golden);

        Assert.Equal(golden.Decks!.Count, items.Count);
        Assert.All(items, i => Assert.Equal(CheckStatus.Dat, i.Status));
    }

    [Fact]
    public void AC1_DoiChieuTheoGiaTriViTri_KhongTheoThuTuDongTrongFile()
    {
        var daoNguoc = Parse(EditGolden(root =>
        {
            var nhatKy = NhatKy(root);
            var luot = nhatKy.Select(n => n!.DeepClone()).Reverse().ToList();
            nhatKy.Clear();
            foreach (var l in luot) nhatKy.Add(l);
        }));

        Assert.All(HangMuc(daoNguoc), i => Assert.Equal(CheckStatus.Dat, i.Status));
    }

    [Fact]
    public void AC1_ThuHoiViTri_ViTriKhongDonDieuTheoThoiGian_VanDat()
    {
        // Vị trí vé cấp bằng sequence không transactional: một lượt bị rollback ĐỐT một vị trí, lượt
        // sau được thu hồi tại chỗ nên nhận ô trống nhỏ hơn ⇒ dòng nằm sau trong nhật ký có thể mang
        // vị trí nhỏ hơn. Suy diễn "vị trí = thứ tự dòng" sẽ cho KHÔNG ĐẠT oan đúng vào ca này.
        var thuHoi = Parse(EditGolden(root =>
        {
            var nhatKy = NhatKy(root);
            var som = nhatKy.First(n => n!["round"]!.GetValue<string>() == VongMau
                                        && n["position"]!.GetValue<int>() == 0)!;
            var chuyen = som.DeepClone();
            nhatKy.Remove(som);
            nhatKy.Add(chuyen);
        }));

        Assert.Equal(CheckStatus.Dat, HangMucVong(thuHoi, VongMau).Status);
    }

    [Fact]
    public void AC1_TraoVeGiuaHaiLuotBocCungVong_KhongDat_VaNeuCaHaiDiemLech()
    {
        var trao = Parse(EditGolden(root =>
        {
            var a = Luot(root, VongMau, 0);
            var b = Luot(root, VongMau, 1);
            (a["payload"], b["payload"]) =
                (b["payload"]!.GetValue<string>(), a["payload"]!.GetValue<string>());
        }));

        var item = HangMucVong(trao, VongMau);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Equal(2, DiemLech(item).Count());
    }

    // ── AC2: lượt bốc thuộc vòng không có chồng phiếu tương ứng → cảnh báo, không im lặng ─

    [Fact]
    public void AC2_VongKhongCoChongPhieuTuongUng_NeuThanhCanhBao_ChuKhongImLangBoQua()
    {
        var thieuDeck = Parse(EditGolden(root => ChongPhieu(root).Remove(Deck(root, VongMau))));

        var item = HangMucVong(thieuDeck, VongMau);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains(VongMau, item.Explanation, StringComparison.Ordinal);
        Assert.Contains(SoLuotBocVongC.ToString(), item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC2_VongKhongCoChongPhieu_KeoKetLuanChungXuongKhongKiemDuoc_ChuKhongDat()
    {
        var thieuDeck = Parse(EditGolden(root => ChongPhieu(root).Remove(Deck(root, VongMau))));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMucVong(thieuDeck, VongMau).Status);
        Assert.NotEqual(CheckStatus.Dat, Verifier.Verify(new VerificationInput(thieuDeck)).Overall);
    }

    [Fact]
    public void AC2_LuotBocKhongCongBoVongNao_VanDuocNeuThanhMotHangMuc()
    {
        var khuyetVong = Parse(EditGolden(root => Luot(root, VongMau, 0)["round"] = null));

        var items = HangMuc(khuyetVong);

        Assert.Equal(Golden().Decks!.Count + 1, items.Count);
        Assert.Contains(items, i => i.Status == CheckStatus.KhongKiemDuoc);
    }

    // ── AC3: vị trí ngoài phạm vi chồng phiếu → KHÔNG ĐẠT kèm vị trí và phạm vi hợp lệ ───

    [Theory]
    [InlineData(SoOPhieuVongC)]
    [InlineData(-1)]
    public void AC3_ViTriNgoaiPhamViChongPhieu_KhongDat_NeuRoViTriVaPhamViHopLe(int viTriLa)
    {
        var ngoaiPhamVi = Parse(EditGolden(root => Luot(root, VongMau, 0)["position"] = viTriLa));

        var item = HangMucVong(ngoaiPhamVi, VongMau);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains(viTriLa.ToString(), item.Explanation, StringComparison.Ordinal);
        Assert.Contains($"0–{SoOPhieuVongC - 1}", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void AC3_ViTriNgoaiPhamVi_ChoDuLieuChuyenSau_KyVongLaPhamViConThucTeLaViTri()
    {
        var ngoaiPhamVi = Parse(EditGolden(root => Luot(root, VongMau, 0)["position"] = SoOPhieuVongC));

        var item = HangMucVong(ngoaiPhamVi, VongMau);

        Assert.Contains($"0–{SoOPhieuVongC - 1}", item.Expected!, StringComparison.Ordinal);
        Assert.Contains(SoOPhieuVongC.ToString(), item.Actual!, StringComparison.Ordinal);
    }

    // ── AC4: sửa nội dung vé của một lượt bốc → hạng mục chuyển KHÔNG ĐẠT ────────────────

    [Fact]
    public void AC4_SuaNoiDungVeMotLuotBoc_ChuyenKhongDat_VaNeuRoViTriLech()
    {
        var veGoc = string.Empty;
        var sua = Parse(EditGolden(root =>
        {
            var luot = Luot(root, VongMau, 0);
            veGoc = luot["payload"]!.GetValue<string>();
            luot["payload"] = veGoc + "_DA_SUA";
        }));

        var item = HangMucVong(sua, VongMau);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains("vị trí 0", item.Explanation, StringComparison.Ordinal);
        Assert.Equal(veGoc, item.Expected);
        Assert.Equal(veGoc + "_DA_SUA", item.Actual);
    }

    [Fact]
    public void AC4_SuaNoiDungVeMotLuotBoc_KeoKetLuanChungXuongKhongDat()
    {
        var sua = Parse(EditGolden(root =>
        {
            var luot = Luot(root, VongMau, 0);
            luot["payload"] = luot["payload"]!.GetValue<string>() + "_DA_SUA";
        }));

        Assert.Equal(CheckStatus.KhongDat, Verifier.Verify(new VerificationInput(sua)).Overall);
    }

    [Fact]
    public void AC4_FixtureChuanVang_KhongCoDiemLechNaoDuocNeuRa()
    {
        Assert.All(HangMuc(Golden()), i => Assert.Empty(DiemLech(i)));
    }

    // ── AC5: báo cáo liệt kê toàn bộ điểm lệch, không dừng ở điểm lệch đầu tiên ──────────

    [Fact]
    public void AC5_NhieuDiemLechTrongMotVong_LietKeDuCaBa_KhongDungODiemDauTien()
    {
        int[] viTriSua = [0, 5, 12];
        var sua = Parse(EditGolden(root =>
        {
            foreach (var vt in viTriSua)
            {
                var luot = Luot(root, VongMau, vt);
                luot["payload"] = luot["payload"]!.GetValue<string>() + "_DA_SUA";
            }
        }));

        var item = HangMucVong(sua, VongMau);

        Assert.Equal(viTriSua.Length, DiemLech(item).Count());
        Assert.All(viTriSua, vt =>
            Assert.Contains(DiemLech(item), m => m.Value.Contains($"vị trí {vt}:", StringComparison.Ordinal)));
    }

    [Fact]
    public void AC5_DiemLechONhieuVong_MoiVongDeuCoKetLuanRieng_KhongVongNaoBiChePhu()
    {
        var sua = Parse(EditGolden(root =>
        {
            foreach (var vong in new[] { "A1", VongMau })
            {
                var luot = Luot(root, vong, 0);
                luot["payload"] = luot["payload"]!.GetValue<string>() + "_DA_SUA";
            }
        }));

        var items = HangMuc(sua);

        Assert.Equal(CheckStatus.KhongDat, HangMucVong(sua, "A1").Status);
        Assert.Equal(CheckStatus.KhongDat, HangMucVong(sua, VongMau).Status);
        Assert.Equal(2, items.Count(i => i.Status == CheckStatus.KhongDat));
    }

    [Fact]
    public void AC5_HaiLuotBocCungNhanMotOPhieu_KhongDat()
    {
        var trung = Parse(EditGolden(root =>
        {
            var nhatKy = NhatKy(root);
            nhatKy.Add(Luot(root, VongMau, 0).DeepClone());
        }));

        var item = HangMucVong(trung, VongMau);

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.Contains(DiemLech(item), m => m.Value.Contains("vị trí 0", StringComparison.Ordinal));
    }

    // ── Thiếu dữ liệu: CHƯA ĐỦ DỮ LIỆU, không được biến thành KHÔNG ĐẠT ──────────────────

    [Theory]
    [InlineData("position")]
    [InlineData("payload")]
    public void ThieuDuLieuCuaLuotBoc_KhongKiemDuoc_ChuKhongKhongDat(string truong)
    {
        var thieu = Parse(EditGolden(root => Luot(root, VongMau, 0)[truong] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMucVong(thieu, VongMau).Status);
    }

    [Fact]
    public void ChongPhieuChuaCongBoNoiDungVe_KhongKiemDuoc()
    {
        var thieu = Parse(EditGolden(root => Deck(root, VongMau)["tickets"] = null));

        Assert.Equal(CheckStatus.KhongKiemDuoc, HangMucVong(thieu, VongMau).Status);
    }

    [Fact]
    public void ChongPhieuVaNhatKyKhaiHaiMaKhacNhau_KhongKiemDuoc_ViPhepGhepTheoTenVongMatCanCu()
    {
        // Khối decks của fixture không công bố mã chồng phiếu nên phép ghép đi theo tên vòng; khi cả
        // hai bên đều khai mã mà lệch nhau thì đối chiếu tiếp là đối chiếu với một chồng phiếu khác.
        var lechMa = Parse(EditGolden(root =>
            Deck(root, VongMau)["deckId"] = "00000000-0000-0000-0000-0000000000ff"));

        var item = HangMucVong(lechMa, VongMau);

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
        Assert.Contains("00000000-0000-0000-0000-0000000000ff", item.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void BaoCaoKhongCoNhatKyBoc_KhongKiemDuoc_ChuKhongImLang()
    {
        var rong = Parse(EditGolden(root => root["nhatKyBoc"] = new JsonArray()));

        var item = Assert.Single(HangMuc(rong));

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
    }

    [Fact]
    public void ThieuDuLieuKhongDuocChePhuMotDiemLechDaCo_KetLuanVanLaKhongDat()
    {
        var lai = Parse(EditGolden(root =>
        {
            var sua = Luot(root, VongMau, 0);
            sua["payload"] = sua["payload"]!.GetValue<string>() + "_DA_SUA";
            Luot(root, VongMau, 1)["position"] = null;
        }));

        Assert.Equal(CheckStatus.KhongDat, HangMucVong(lai, VongMau).Status);
    }

    // ── Số liệu thô: ô phiếu không có lượt bốc là chuyện bình thường, phải đếm đúng ───────

    [Fact]
    public void SoLieuTho_NeuSoOPhieuKhongCoLuotBoc_DeDoiChieuVoiBienBanNiemPhong()
    {
        var item = HangMucVong(Golden(), VongMau);

        Assert.Contains(item.Metrics, m => m.Value == SoOPhieuVongC.ToString());
        Assert.Contains(item.Metrics, m => m.Value == SoLuotBocVongC.ToString());
        Assert.Contains(
            item.Metrics,
            m => m.Label.Contains("không có lượt bốc", StringComparison.Ordinal)
                 && m.Value == (SoOPhieuVongC - SoLuotBocVongC).ToString());
    }

    [Fact]
    public void CauGiaiThich_KhongDeLoHex_ViCheDoNguoiDanVeThangCauNay()
    {
        var item = HangMucVong(Golden(), VongMau);

        Assert.False(string.IsNullOrWhiteSpace(item.Explanation));
        Assert.DoesNotContain(Golden().Decks!.Single(d => d.Round == VongMau).DeckHash!, item.Explanation);
    }
}
