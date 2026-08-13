using System.Security.Cryptography;
using System.Text.Json;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Fixtures.Tests;

/// <summary>
/// Hàng rào cho fixture chuẩn vàng (vé #1) — mỗi test mang tên một tiêu chí nghiệm thu.
/// Không hàm băm nào được tính lại ở đây: tính lại bằng mã tự viết trong test thì test chỉ đang
/// kiểm chính nó. Việc đó là của lõi kiểm chứng (vé #2 trở đi).
/// </summary>
public class GoldenFixtureTests
{
    private const string FixtureFile = "transparency-golden.json";
    private const string MetaFile = "transparency-golden.meta.json";
    private const string UnitCatalogFile = "apartment-units-golden.json";

    private static JsonElement Fixture() => Load(FixtureFile);
    private static JsonElement Meta() => Load(MetaFile);

    private static JsonElement Load(string fileName)
    {
        var path = RepoPaths.Fixture(fileName);
        Assert.True(File.Exists(path), $"Thiếu fixture: {path}");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.Clone();
    }

    private static IEnumerable<JsonElement> Decks() => Fixture().GetProperty("decks").EnumerateArray();

    private static IEnumerable<string> DeckRounds() =>
        Decks().Select(d => d.GetProperty("round").GetString()!);

    // ── AC1: fixture nằm trong repo, đọc được, đủ các khối dữ liệu ────────────────────────

    [Fact]
    public void AC1_FixtureDocDuoc_VaCoDuCacKhoiDuLieu()
    {
        var root = Fixture();

        foreach (var block in new[]
                 {
                     "projectId", "projectName", "completedAt", "waitlistSize",
                     "nguonNgauNhien", "camKetNeo", "dauThoiGian", "decks", "nhatKyBoc",
                     "ketQua", "thuatToan"
                 })
            Assert.True(root.TryGetProperty(block, out _), $"Fixture thiếu khối '{block}'");
    }

    [Fact]
    public void AC1_NguonNgauNhien_DuBaVong_VaDuTruongDeDungLaiMasterSeed()
    {
        var sources = Fixture().GetProperty("nguonNgauNhien").EnumerateArray().ToList();

        Assert.Equal(
            new[] { "A", "B", "C" },
            sources.Select(s => s.GetProperty("round").GetString()!).OrderBy(r => r, StringComparer.Ordinal));

        foreach (var s in sources)
            foreach (var field in new[]
                     {
                         "masterSeed", "rServer", "rServerCommit", "rSupervisor",
                         "blockHeight", "blockHash", "anchorChain", "inputHash"
                     })
                Assert.False(
                    s.GetProperty(field).ValueKind is JsonValueKind.Null or JsonValueKind.Undefined,
                    $"Nguồn ngẫu nhiên vòng {s.GetProperty("round").GetString()} thiếu '{field}'");
    }

    [Fact]
    public void AC1_CamKetNeo_VaDauThoiGian_CoMatVaDayDuTruong()
    {
        var anchor = Fixture().GetProperty("camKetNeo");
        Assert.True(anchor.GetProperty("ethTargetHeight").GetInt64() > 0);
        Assert.True(anchor.GetProperty("btcTargetHeight").GetInt64() > 0);
        Assert.NotEqual(JsonValueKind.Null, anchor.GetProperty("anchorFrozenAt").ValueKind);

        var tokens = Fixture().GetProperty("dauThoiGian").EnumerateArray().ToList();
        Assert.NotEmpty(tokens);
        foreach (var t in tokens)
            foreach (var field in new[] { "scope", "authority", "genTime", "serialNumber", "digest", "preimage" })
                Assert.False(
                    t.GetProperty(field).ValueKind is JsonValueKind.Null or JsonValueKind.Undefined,
                    $"Token dấu thời gian thiếu '{field}'");

        // Mốc entropy phải được niêm phong — thiếu nó là mất luận điểm chống grinding.
        Assert.Contains(tokens, t => t.GetProperty("scope").GetString() == "FREEZE");
    }

    [Fact]
    public void AC1_ChongPhieu_CoNoiDungVe_VaMaBamDaNiemPhong()
    {
        foreach (var deck in Decks())
        {
            var round = deck.GetProperty("round").GetString();
            Assert.False(string.IsNullOrWhiteSpace(deck.GetProperty("deckHash").GetString()),
                $"Chồng phiếu {round} thiếu deckHash");
            Assert.Equal(deck.GetProperty("size").GetInt32(), deck.GetProperty("tickets").GetArrayLength());
            Assert.NotEqual(JsonValueKind.Null, deck.GetProperty("sealedAt").ValueKind);
        }
    }

    [Fact]
    public void AC1_NhatKyBoc_CoDuHaiDinhDanh_DeTinhLaiChuoiBam()
    {
        var log = Fixture().GetProperty("nhatKyBoc").EnumerateArray().ToList();
        Assert.NotEmpty(log);

        foreach (var e in log)
            foreach (var field in new[] { "round", "applicantId", "deckId", "position", "payload", "entryHash" })
                Assert.False(
                    e.GetProperty(field).ValueKind is JsonValueKind.Null or JsonValueKind.Undefined,
                    $"Lượt bốc thiếu '{field}' — thiếu applicantId/deckId thì chuỗi băm chỉ là trang trí");

        // prevHash của mắt xích ĐẦU là null, các mắt xích sau phải có — verifier cần cả hai ca.
        Assert.Equal(JsonValueKind.Null, log[0].GetProperty("prevHash").ValueKind);
        Assert.All(log.Skip(1), e => Assert.Equal(JsonValueKind.String, e.GetProperty("prevHash").ValueKind));

        // Cả vé do người bấm lẫn vé máy bốc thay — hai ca hiển thị khác nhau trên lưới phiếu.
        Assert.Contains(log, e => e.GetProperty("autoDrawn").GetBoolean());
        Assert.Contains(log, e => !e.GetProperty("autoDrawn").GetBoolean());
    }

    [Fact]
    public void AC1_BangKetQua_CoMaBamKetQua_VaCacDongKetQua()
    {
        var ketQua = Fixture().GetProperty("ketQua");
        Assert.False(string.IsNullOrWhiteSpace(ketQua.GetProperty("resultsHash").GetString()));

        var rows = ketQua.GetProperty("rows").EnumerateArray().ToList();
        Assert.NotEmpty(rows);
        foreach (var r in rows)
            foreach (var field in new[]
                     {
                         "applicantId", "won", "tier", "typeCode", "unitCode", "waitlistRank", "cancelledAt"
                     })
                Assert.True(r.TryGetProperty(field, out _), $"Dòng kết quả thiếu '{field}'");
    }

    // ── AC2: dự án đã hoàn tất, có cả bốn vòng, có vé dự khuyết đã đánh số ────────────────

    [Fact]
    public void AC2_DuAn_DaHoanTat()
    {
        Assert.Equal(JsonValueKind.String, Fixture().GetProperty("completedAt").ValueKind);
    }

    [Fact]
    public void AC2_CoDuBonVong_A1_A2_B_C()
    {
        var rounds = DeckRounds().ToList();

        Assert.Contains("A1", rounds);
        Assert.Contains(rounds, r => r.StartsWith("A2:", StringComparison.Ordinal));
        Assert.Contains(rounds, r => r.StartsWith("B:", StringComparison.Ordinal));
        Assert.Contains("C", rounds);
    }

    [Fact]
    public void AC2_VongC_CoVeDuKhuyetDaDanhSo()
    {
        var cDeck = Decks().Single(d => d.GetProperty("round").GetString() == "C");
        var waitlist = cDeck.GetProperty("tickets").EnumerateArray()
            .Select(t => t.GetString()!)
            .Where(p => p.StartsWith("DU_KHUYET:", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(waitlist);
        // Số dự khuyết là hoán vị 1..wl — mỗi số xuất hiện đúng một lần.
        var numbers = waitlist.Select(p => int.Parse(p["DU_KHUYET:".Length..])).OrderBy(n => n).ToList();
        Assert.Equal(Enumerable.Range(1, numbers.Count), numbers);
    }

    [Fact]
    public void AC2_CacLoaiVe_DuDeVeLuoiPhieu()
    {
        var payloads = Decks()
            .SelectMany(d => d.GetProperty("tickets").EnumerateArray())
            .Select(t => t.GetString()!)
            .ToList();

        // Bốn màu ô phiếu của lưới: trúng · chờ phân loại dư · dự khuyết · không trúng.
        Assert.Contains(payloads, p => p == "TRUNG_QUYEN_MUA");
        Assert.Contains(payloads, p => p == "KHONG_TRUNG_UU_TIEN");
        Assert.Contains(payloads, p => p.StartsWith("TRUNG:", StringComparison.Ordinal));
        Assert.Contains(payloads, p => p == "CHO_PHAN_LOAI_DU");
        Assert.Contains(payloads, p => p == "KHONG_TRUNG");
    }

    // ── AC3: mô tả cách tái sinh fixture ─────────────────────────────────────────────────

    [Fact]
    public void AC3_CoMoTaCachTaiSinh_VaMaNguonSinhFixture()
    {
        var readme = RepoPaths.Fixture("README.md");
        Assert.True(File.Exists(readme), $"Thiếu mô tả cách tái sinh fixture: {readme}");

        var generator = RepoPaths.Fixture(Path.Combine("generator", "GoldenTransparencyFixtureGenerator.cs"));
        Assert.True(File.Exists(generator), $"Thiếu mã nguồn sinh fixture: {generator}");

        // Mô tả phải trỏ tới đúng file sinh ra fixture, nếu không thì người sau vẫn phải đoán.
        Assert.Contains("GoldenTransparencyFixtureGenerator.cs", File.ReadAllText(readme));
    }

    // ── AC4: ghi rõ fixture sinh từ phiên bản backend nào ────────────────────────────────

    [Fact]
    public void AC4_GhiRoPhienBanBackend_VaMocThoiGianSinhFixture()
    {
        var meta = Meta();

        var commit = meta.GetProperty("backendCommit").GetString();
        Assert.Matches("^[0-9a-f]{40}$", commit);
        Assert.NotEqual(JsonValueKind.Null, meta.GetProperty("backendCommittedAt").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, meta.GetProperty("generatedAtUtc").ValueKind);
    }

    // ── Vé #12: danh mục căn của chính dự án trong fixture ───────────────────────────────

    /// <summary>
    /// Tái lập vòng phân căn ưu tiên cần quỹ căn của <b>dự án trong fixture</b> — bản danh mục nhúng
    /// sẵn trong công cụ là của dự án khác. Danh mục thiếu một căn nào đó thì phép dựng lại quỹ căn
    /// ưu tiên ra một hoán vị khác, và test tái lập đỏ mà không nói được vì sao.
    /// </summary>
    [Fact]
    public void Ve12_CoDanhMucCanCuaDuAnTrongFixture_PhuDuMoiCanDaTrungTrongVe()
    {
        var path = RepoPaths.Fixture(UnitCatalogFile);
        Assert.True(File.Exists(path), $"Thiếu danh mục căn của dự án trong fixture: {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var maCan = doc.RootElement.EnumerateObject()
            .SelectMany(loai => loai.Value.EnumerateArray())
            .Select(can => can.GetProperty("unitCode").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        var canDaTrung = Decks()
            .SelectMany(d => d.GetProperty("tickets").EnumerateArray())
            .Select(t => t.GetString()!)
            .Where(p => p.StartsWith("TRUNG:", StringComparison.Ordinal))
            .Select(p => p["TRUNG:".Length..])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(canDaTrung);
        Assert.All(canDaTrung, can => Assert.Contains(can, maCan));
    }

    [Fact]
    public void Ve12_MaLoaiCanTrongDanhMuc_KhopVoiTenVongPhanCanUuTien()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(RepoPaths.Fixture(UnitCatalogFile)));
        var maLoai = doc.RootElement.EnumerateObject().Select(l => l.Name).ToHashSet(StringComparer.Ordinal);

        var loaiCuaVongUuTien = DeckRounds()
            .Where(r => r.StartsWith("A2:", StringComparison.Ordinal))
            .Select(r => r["A2:".Length..])
            .ToList();

        Assert.NotEmpty(loaiCuaVongUuTien);
        Assert.All(loaiCuaVongUuTien, loai => Assert.Contains(loai, maLoai));
    }

    [Fact]
    public void AC4_MaBamGhiTrongManifest_KhopVoiFixtureDangNamTrongRepo()
    {
        var declared = Meta().GetProperty("fixtureSha256").GetString();
        var actual = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(RepoPaths.Fixture(FixtureFile))))
            .ToLowerInvariant();

        Assert.Equal(declared, actual);
    }
}
