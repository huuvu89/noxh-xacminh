using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// AC1/AC4/AC5 — hạng mục kiểm mã băm chồng phiếu, đi qua đúng seam <see cref="Verifier.Verify"/>.
/// Bản bị tráo vé dựng từ chính fixture chuẩn vàng chứ không phải một file rời: sinh lại fixture là
/// việc có chủ ý và thường xuyên, một file "đã sửa" chép cứng sẽ lệch khỏi bản vàng ngay lần sau.
/// </summary>
public class DeckHashVerificationTests
{
    private static TransparencyReport Golden() => Parse(GoldenFixture.Json());

    private static TransparencyReport Parse(string json)
    {
        var result = TransparencyJson.Parse(json);
        Assert.True(result.Success, result.ErrorMessage);
        return result.Report!;
    }

    private static VerificationReport Verify(TransparencyReport report) =>
        Verifier.Verify(new VerificationInput(report));

    private static IReadOnlyList<CheckResult> DeckHashItems(VerificationReport report) =>
        report.Items.Where(i => i.Id.StartsWith(CheckIds.DeckHash, StringComparison.Ordinal)).ToList();

    /// <summary>Sửa JSON gốc bằng JsonNode — đúng thứ người dùng thả vào, không phải model đã nạp.</summary>
    private static string EditGolden(Action<JsonObject> edit)
    {
        var root = JsonNode.Parse(GoldenFixture.Json())!.AsObject();
        edit(root);
        return root.ToJsonString();
    }

    /// <summary>
    /// Tráo hai vé <b>khác nội dung</b> trong một chồng phiếu, trả về chỉ số chồng phiếu bị tráo.
    /// Tráo hai vé trùng nội dung thì mã băm không đổi — đó không phải phép thử.
    /// </summary>
    private static int TraoHaiVe(JsonObject root)
    {
        var decks = root["decks"]!.AsArray();
        for (var d = 0; d < decks.Count; d++)
        {
            var tickets = decks[d]!["tickets"]!.AsArray();
            var dau = tickets[0]?.GetValue<string>();
            for (var i = 1; i < tickets.Count; i++)
            {
                var sau = tickets[i]?.GetValue<string>();
                if (sau == dau) continue;
                tickets[0] = JsonValue.Create(sau);
                tickets[i] = JsonValue.Create(dau);
                return d;
            }
        }

        throw new InvalidOperationException("Fixture không có chồng phiếu nào chứa hai vé khác nội dung.");
    }

    // ── AC1: từng chồng phiếu một kết luận ───────────────────────────────────────────────

    [Fact]
    public void AC1_MoiChongPhieu_MotHangMucKetLuanRieng()
    {
        var golden = Golden();

        var items = DeckHashItems(Verify(golden));

        Assert.Equal(golden.Decks!.Count, items.Count);
        foreach (var deck in golden.Decks!)
            Assert.Contains(items, i => i.Id == $"{CheckIds.DeckHash}:{deck.Round}");
    }

    [Fact]
    public void AC1_HangMuc_MangGiaTriKyVongVaTinhDuoc_DeTuKiemLai()
    {
        var item = DeckHashItems(Verify(Golden()))[0];

        Assert.False(string.IsNullOrWhiteSpace(item.Expected));
        Assert.Equal(item.Expected, item.Actual);
        Assert.False(string.IsNullOrWhiteSpace(item.Preimage));
        Assert.False(string.IsNullOrWhiteSpace(item.Explanation));
    }

    // ── AC5: fixture chuẩn vàng → ĐẠT ────────────────────────────────────────────────────

    [Fact]
    public void AC5_FixtureChuanVang_MoiChongPhieuDatVaKetLuanChungDat()
    {
        var report = Verify(Golden());

        Assert.All(DeckHashItems(report), i => Assert.Equal(CheckStatus.Dat, i.Status));
        Assert.Equal(CheckStatus.Dat, report.Overall);
    }

    // ── AC5: tráo hai vé trong một chồng phiếu → KHÔNG ĐẠT ───────────────────────────────

    [Fact]
    public void AC5_TraoHaiVeTrongMotChongPhieu_ChongPhieuDoKhongDat()
    {
        var traoO = -1;
        var tampered = Parse(EditGolden(root => traoO = TraoHaiVe(root)));

        var item = DeckHashItems(Verify(tampered))[traoO];

        Assert.Equal(CheckStatus.KhongDat, item.Status);
        Assert.NotEqual(item.Expected, item.Actual);
    }

    [Fact]
    public void AC5_TraoHaiVe_KhongLamHongKetLuanCuaChongPhieuKhac()
    {
        var traoO = -1;
        var tampered = Parse(EditGolden(root => traoO = TraoHaiVe(root)));

        var report = Verify(tampered);

        Assert.Equal(CheckStatus.KhongDat, report.Overall);
        Assert.All(
            DeckHashItems(report).Where((_, i) => i != traoO),
            i => Assert.Equal(CheckStatus.Dat, i.Status));
    }

    // ── AC4: thiếu nội dung vé → KHÔNG KIỂM ĐƯỢC, không phải ĐẠT ─────────────────────────

    [Fact]
    public void AC4_ChongPhieuThieuNoiDungVe_KhongKiemDuoc()
    {
        var thieuVe = Parse(EditGolden(root => root["decks"]![0]!.AsObject().Remove("tickets")));

        var item = DeckHashItems(Verify(thieuVe))[0];

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
    }

    [Fact]
    public void AC4_ChongPhieuThieuMaBamNiemPhong_KhongKiemDuoc()
    {
        var thieuHash = Parse(EditGolden(root => root["decks"]![0]!.AsObject()["deckHash"] = null));

        var item = DeckHashItems(Verify(thieuHash))[0];

        Assert.Equal(CheckStatus.KhongKiemDuoc, item.Status);
    }

    [Fact]
    public void AC4_BaoCaoKhongCoChongPhieuNao_KhongKiemDuoc_ChuKhongImLang()
    {
        var rong = Parse(EditGolden(root => root["decks"] = new JsonArray()));

        var report = Verify(rong);

        Assert.NotEmpty(DeckHashItems(report));
        Assert.Equal(CheckStatus.KhongKiemDuoc, report.Overall);
    }

    [Fact]
    public void AC4_MotHangMucKhongKiemDuoc_KeoKetLuanChungXuong_KhongDeXanh()
    {
        var thieuVe = Parse(EditGolden(root => root["decks"]![0]!.AsObject().Remove("tickets")));

        Assert.Equal(CheckStatus.KhongKiemDuoc, Verify(thieuVe).Overall);
    }

    [Fact]
    public void AC4_VuaSaiVuaThieu_KetLuanChungLaKhongDat()
    {
        // Thiếu dữ liệu không được che mất một chồng phiếu sai.
        var lai = Parse(EditGolden(root =>
        {
            var traoO = TraoHaiVe(root);
            var decks = root["decks"]!.AsArray();
            decks[(traoO + 1) % decks.Count]!.AsObject().Remove("tickets");
        }));

        Assert.Equal(CheckStatus.KhongDat, Verify(lai).Overall);
    }
}
