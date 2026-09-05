using System.Text.Json.Nodes;
using Noxh.XacMinh.Core.Decks;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.Core.Verification;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Vé #9 — lưới ô phiếu. Phần dựng lưới (phân loại vé, ghép nhật ký bốc vào từng ô, đếm số liệu
/// tóm tắt, lọc và tra cứu theo mã định danh hồ sơ) là logic thuần nên nằm ở lõi và kiểm ở đây; vỏ Blazor
/// chỉ vẽ lại thứ lõi đã tính.
/// </summary>
public class LuoiOPhieuTests
{
    private const string VongC = "C";

    private static TransparencyReport Golden()
    {
        var nap = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(nap.Success, nap.ErrorMessage);
        return nap.Report!;
    }

    private static IReadOnlyList<DeckGrid> LuoiChuanVang() => DeckGridBuilder.Build(Golden());

    private static DeckGrid Luoi(string vong) => Assert.Single(LuoiChuanVang(), l => l.Round == vong);

    /// <summary>Số liệu do chính báo cáo công bố — thanh tóm tắt phải khớp với nó, không tự bịa.</summary>
    private static JsonObject ChongPhieuCongBo(string vong) =>
        JsonNode.Parse(GoldenFixture.Json())!.AsObject()["decks"]!.AsArray()
            .First(d => d!["round"]!.GetValue<string>() == vong)!.AsObject();

    private static IReadOnlyList<DrawLogEntry> NhatKy(string vong) =>
        Golden().DrawLog!.Where(e => e.Round == vong).ToList();

    private static TransparencyReport BaoCao(IReadOnlyList<Deck>? decks, IReadOnlyList<DrawLogEntry>? nhatKy = null) =>
        new() { Decks = decks, DrawLog = nhatKy };

    private static Deck ChongPhieu(string round, params string?[] tickets) =>
        new() { Round = round, Size = tickets.Length, Tickets = tickets };

    // ── AC1: mỗi chồng phiếu ra một lưới, mỗi vé là một ô mang loại kết quả ──────────────

    [Fact]
    public void AC1_MoiChongPhieuCongBo_RaMotLuoiRieng()
    {
        var golden = Golden();

        var luoi = DeckGridBuilder.Build(golden);

        Assert.Equal(golden.Decks!.Count, luoi.Count);
        Assert.Equal(golden.Decks!.Select(d => d.Round), luoi.Select(l => l.Round));
    }

    [Fact]
    public void AC1_SoOTrongLuoi_BangSoVeChongPhieuCongBo()
    {
        foreach (var luoi in LuoiChuanVang())
        {
            var congBo = ChongPhieuCongBo(luoi.Round!);

            Assert.Equal(congBo["size"]!.GetValue<int>(), luoi.Cells.Count);
            Assert.Equal(Enumerable.Range(0, luoi.Cells.Count), luoi.Cells.Select(o => o.Position));
        }
    }

    [Theory]
    [InlineData("TRUNG:2PN-001", TicketKind.Trung)]
    [InlineData("TRUNG_QUYEN_MUA", TicketKind.Trung)]
    [InlineData("CHO_PHAN_LOAI_DU", TicketKind.ChoPhanLoaiDu)]
    [InlineData("DU_KHUYET:3", TicketKind.DuKhuyet)]
    [InlineData("KHONG_TRUNG", TicketKind.KhongTrung)]
    [InlineData("KHONG_TRUNG_UU_TIEN", TicketKind.KhongTrung)]
    public void AC1_MoiVe_MangLoaiKetQuaCuaNoiDungVe(string payload, TicketKind loai)
    {
        var luoi = Assert.Single(DeckGridBuilder.Build(BaoCao([ChongPhieu("A1", payload)])));

        Assert.Equal(loai, luoi.Cells[0].Kind);
        Assert.Equal(payload, luoi.Cells[0].Payload);
        Assert.False(string.IsNullOrWhiteSpace(luoi.Cells[0].Label));
    }

    [Fact]
    public void AC1_VeLaMotChuoiKhongNhanRa_RaKhongRoLoai_ChuKhongDoanLaKhongTrung()
    {
        var luoi = Assert.Single(DeckGridBuilder.Build(BaoCao([ChongPhieu("A1", "TRUNGG", null)])));

        Assert.All(luoi.Cells, o => Assert.Equal(TicketKind.KhongRo, o.Kind));
    }

    [Fact]
    public void AC1_ChongPhieuChuaCongBoNoiDungVe_RaLuoiTrongChuKhongVoOan()
    {
        var chuaMo = new Deck { Round = "C", Size = 21, Tickets = null };

        var luoi = Assert.Single(DeckGridBuilder.Build(BaoCao([chuaMo])));

        Assert.Empty(luoi.Cells);
    }

    // ── AC2: từng ô mang vị trí, nội dung vé, mã định danh hồ sơ, người bấm hay máy bốc ────────

    [Fact]
    public void AC2_ODaBoc_MangMaHoSoGiaVaNhanNguoiBamHayMayBoc_CuaDungLuotBocOViTriDo()
    {
        var luoi = Luoi(VongC);

        foreach (var luot in NhatKy(VongC))
        {
            var o = luoi.Cells[luot.Position!.Value];
            var boc = Assert.Single(o.Draws);

            Assert.Equal(luot.ApplicantId, boc.ApplicantId);
            Assert.Equal(luot.AutoDrawn, boc.AutoDrawn);
            Assert.Equal(luot.Payload, o.Payload);
        }
    }

    [Fact]
    public void AC2_FixtureChuanVang_CoCaOMayBocThayVaONguoiTuBam()
    {
        var o = LuoiChuanVang().SelectMany(l => l.Cells).ToList();

        Assert.Contains(o, x => x.Draws.Any(d => d.AutoDrawn == true));
        Assert.Contains(o, x => x.Draws.Any(d => d.AutoDrawn == false));
    }

    [Fact]
    public void AC2_HaiLuotBocCungMotO_GiuCaHai_ChuKhongGiauBot()
    {
        var nhatKy = new List<DrawLogEntry>
        {
            new() { Round = "A1", Position = 0, ApplicantId = "hs-1", Payload = "TRUNG_QUYEN_MUA", AutoDrawn = false },
            new() { Round = "A1", Position = 0, ApplicantId = "hs-2", Payload = "TRUNG_QUYEN_MUA", AutoDrawn = true },
        };

        var luoi = Assert.Single(DeckGridBuilder.Build(BaoCao([ChongPhieu("A1", "TRUNG_QUYEN_MUA")], nhatKy)));

        Assert.Equal(2, luoi.Cells[0].Draws.Count);
    }

    [Fact]
    public void AC2_HaiChongPhieuCungTenVong_KhongGhepNhatKyBua()
    {
        var report = BaoCao(
            [ChongPhieu("A1", "TRUNG_QUYEN_MUA"), ChongPhieu("A1", "KHONG_TRUNG")],
            [new DrawLogEntry { Round = "A1", Position = 0, ApplicantId = "hs-1", AutoDrawn = false }]);

        var luoi = DeckGridBuilder.Build(report);

        Assert.All(luoi, l => Assert.All(l.Cells, o => Assert.Empty(o.Draws)));
        Assert.All(luoi, l => Assert.Null(l.Summary.UndrawnCells));
    }

    // ── AC3: bộ lọc hoạt động, thanh tóm tắt khớp số liệu báo cáo ────────────────────────

    [Fact]
    public void AC3_TomTat_QuyMoVaSoVeTrung_KhopSoLieuChongPhieuCongBo()
    {
        foreach (var luoi in LuoiChuanVang())
        {
            var congBo = ChongPhieuCongBo(luoi.Round!);

            Assert.Equal(congBo["size"]!.GetValue<int>(), luoi.Summary.Size);
            Assert.Equal(congBo["wonCount"]!.GetValue<int>(), luoi.Summary.WonCount);
        }
    }

    [Fact]
    public void AC3_TomTat_SoLuotNguoiBamVaMayBoc_KhopNhatKyBocCongBo()
    {
        foreach (var luoi in LuoiChuanVang())
        {
            var nhatKy = NhatKy(luoi.Round!);

            Assert.Equal(nhatKy.Count(e => e.AutoDrawn == false), luoi.Summary.ManualDraws);
            Assert.Equal(nhatKy.Count(e => e.AutoDrawn == true), luoi.Summary.AutoDraws);
        }
    }

    /// <summary>Cùng một con số với hạng mục kiểm "Vé từng lượt bốc" — hai chỗ lệch nhau là tự mâu thuẫn.</summary>
    [Fact]
    public void AC3_TomTat_SoOChuaAiBoc_KhopSoLieuThoCuaHangMucKiem()
    {
        var golden = Golden();
        var hangMuc = Verifier.Verify(new VerificationInput(golden)).Items;

        foreach (var luoi in DeckGridBuilder.Build(golden))
        {
            var soLieu = hangMuc
                .Single(i => i.Id == $"{CheckIds.DrawTicketMatch}:{luoi.Round}")
                .Metrics.Single(m => m.Label == "Số ô phiếu không có lượt bốc");

            Assert.Equal(soLieu.Value, luoi.Summary.UndrawnCells?.ToString());
        }
    }

    [Fact]
    public void AC3_BaoCaoKhongCoNhatKyBoc_SoLuotVaSoOChuaBocLaKhongBiet_ChuKhongPhaiSoKhong()
    {
        var luoi = Assert.Single(DeckGridBuilder.Build(BaoCao([ChongPhieu("A1", "TRUNG_QUYEN_MUA")])));

        Assert.Null(luoi.Summary.ManualDraws);
        Assert.Null(luoi.Summary.AutoDraws);
        Assert.Null(luoi.Summary.UndrawnCells);
        Assert.False(luoi.Summary.HasDrawLog);
    }

    [Fact]
    public void AC3_LocChiVeTrung_ChiConOTrung_VaDuSoVeTrungCuaChongPhieu()
    {
        var luoi = Luoi(VongC);

        var o = luoi.Filter(DeckFilter.ChiTrung, null);

        Assert.Equal(luoi.Summary.WonCount, o.Count);
        Assert.All(o, x => Assert.Equal(TicketKind.Trung, x.Kind));
    }

    [Fact]
    public void AC3_LocChiMayBoc_ChiConOCoLuotMayBocThay()
    {
        var luoi = LuoiChuanVang().First(l => l.Cells.Any(o => o.Draws.Any(d => d.AutoDrawn == true)));

        var o = luoi.Filter(DeckFilter.ChiMayBoc, null);

        Assert.NotEmpty(o);
        Assert.All(o, x => Assert.Contains(x.Draws, d => d.AutoDrawn == true));
        Assert.Equal(luoi.Summary.AutoDraws, o.Count);
    }

    [Fact]
    public void AC3_LocChiChuaAiBoc_ChiConOKhongCoLuotBocNao()
    {
        var luoi = Luoi(VongC);

        var o = luoi.Filter(DeckFilter.ChiChuaBoc, null);

        Assert.Equal(luoi.Summary.UndrawnCells, o.Count);
        Assert.All(o, x => Assert.False(x.Drawn));
    }

    [Fact]
    public void AC3_KhongCoNhatKyBoc_LocTheoLuotBoc_KhongTraONao_KeoNhanNhamLaChuaAiBoc()
    {
        var luoi = Assert.Single(DeckGridBuilder.Build(BaoCao([ChongPhieu("A1", "TRUNG_QUYEN_MUA")])));

        Assert.Empty(luoi.Filter(DeckFilter.ChiChuaBoc, null));
        Assert.Empty(luoi.Filter(DeckFilter.ChiMayBoc, null));
        Assert.Single(luoi.Filter(DeckFilter.TatCa, null));
    }

    // ── AC4: chồng phiếu ~900 vé vẫn dựng được lưới ──────────────────────────────────────

    [Fact]
    public void AC4_ChongPhieu900Ve_DungDuocLuoiDayDu()
    {
        var ve = Enumerable.Range(0, 900).Select(i => i % 3 == 0 ? $"TRUNG:2PN-{i:D3}" : "KHONG_TRUNG").ToArray();
        var nhatKy = Enumerable.Range(0, 880)
            .Select(i => new DrawLogEntry { Round = "B:2PN", Position = i, ApplicantId = $"hs-{i}", AutoDrawn = i % 7 == 0 })
            .ToList();

        var luoi = Assert.Single(DeckGridBuilder.Build(BaoCao([ChongPhieu("B:2PN", ve)], nhatKy)));

        Assert.Equal(900, luoi.Cells.Count);
        Assert.Equal(300, luoi.Summary.WonCount);
        Assert.Equal(20, luoi.Summary.UndrawnCells);
    }

    // ── AC5: ô chưa có lượt bốc phân biệt rõ với ô đã bốc ────────────────────────────────

    [Fact]
    public void AC5_OChuaCoLuotBoc_PhanBietVoiODaBoc()
    {
        var luoi = Luoi(VongC);
        var viTriDaBoc = NhatKy(VongC).Select(e => e.Position!.Value).ToHashSet();

        Assert.All(luoi.Cells, o => Assert.Equal(viTriDaBoc.Contains(o.Position), o.Drawn));
        Assert.Contains(luoi.Cells, o => !o.Drawn);
    }

    // ── AC6: tìm ô theo mã định danh hồ sơ ───────────────────────────────────────────────

    [Fact]
    public void AC6_TimTheoMaHoSoGia_RaDungOCuaHoSoDo()
    {
        var luoi = Luoi(VongC);
        var luot = NhatKy(VongC)[3];

        var o = Assert.Single(luoi.Filter(DeckFilter.TatCa, luot.ApplicantId));

        Assert.Equal(luot.Position, o.Position);
    }

    [Fact]
    public void AC6_TimTheoMotDoanMaHoSo_KhongPhanBietHoaThuong()
    {
        var luoi = Luoi(VongC);
        var luot = NhatKy(VongC)[0];
        var doan = luot.ApplicantId![..8].ToUpperInvariant();

        var o = luoi.Filter(DeckFilter.TatCa, $"  {doan}  ");

        Assert.Contains(o, x => x.Position == luot.Position);
    }

    [Fact]
    public void AC6_TimMaHoSoKhongCoTrongChongPhieu_RaRong_ChuKhongRaCaChongPhieu()
    {
        var luoi = Luoi(VongC);

        Assert.Empty(luoi.Filter(DeckFilter.TatCa, "hs-khong-co-that"));
    }

    [Fact]
    public void AC6_OTrongTimKiem_VanChiuCaBoLocDangChon()
    {
        var luoi = Luoi(VongC);
        var luot = NhatKy(VongC).First(e => e.Payload == "KHONG_TRUNG");

        Assert.Empty(luoi.Filter(DeckFilter.ChiTrung, luot.ApplicantId));
    }
}
