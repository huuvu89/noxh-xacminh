// Sinh fixture chuẩn vàng bằng chính backend (cách chạy: fixtures/README.md).
// Chồng phiếu phải MỌC RA từ MASTER_SEED thật — fixture bịa tay biến mọi test tái lập về sau
// thành kiểm chính nó.

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using Lottery.Api.Common.Encryption;
using Lottery.Api.Common.Persistence;
using Lottery.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Lottery.IntegrationTests;

public class GoldenTransparencyFixtureGenerator : IClassFixture<TestWebAppFactory>
{
    /// <summary>R_supervisor cố định — mốc entropy của fixture phải tra ngược được bằng mắt.</summary>
    private const string RSupervisorHex = "5e1ec7ed5e1ec7ed5e1ec7ed5e1ec7ed5e1ec7ed5e1ec7ed5e1ec7ed5e1ec7ed";

    private const string ForceReason =
        "Fixture kiểm chứng — đóng cổng theo kịch bản dựng sẵn, không theo tỉ lệ xác nhận";

    private readonly TestWebAppFactory _factory;

    public GoldenTransparencyFixtureGenerator(TestWebAppFactory factory) => _factory = factory;

    private sealed record Seeded(Guid Id, int Index, ApplicantGroup Group, string TypeCode);

    // LockList chặn nếu lệch công thức Điều 7. Bộ số này thoả: H_ut=12, H_tt=40, T_ch=20 →
    // S_ut=6 = ΣPriorityUnits; 2PN round(6/20×12)=4; 1PN round(6/20×8)=2; nU1=2 ≤ 6.
    private static readonly (string Code, string Name, int TotalUnits, int PriorityUnits)[] Types =
    [
        ("2PN", "2 phòng ngủ", 12, 4),
        ("1PN", "1 phòng ngủ", 8, 2)
    ];

    private static readonly (ApplicantGroup Group, string TypeCode, int Count)[] Population =
    [
        (ApplicantGroup.U1, "2PN", 1),
        (ApplicantGroup.U1, "1PN", 1),
        // 6 U2 dồn vào 2PN (4 suất ưu tiên) để A2 chắc chắn có vé CHO_PHAN_LOAI_DU + bước máy gom A2g.

        (ApplicantGroup.U2, "2PN", 6),
        (ApplicantGroup.U3, "2PN", 1),
        (ApplicantGroup.U3, "1PN", 1),
        (ApplicantGroup.U4, "2PN", 1),
        (ApplicantGroup.U5, "1PN", 1),
        (ApplicantGroup.U6, "2PN", 16),
        (ApplicantGroup.U6, "1PN", 12)
    ];

    // Tên có dấu: ListHash băm chuỗi System.Text.Json mặc định (escape \uXXXX) — fixture toàn ASCII
    // giấu mất cái bẫy đó.
    private static readonly string[] Ho = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Huỳnh", "Phan", "Vũ", "Võ", "Đặng"];
    private static readonly string[] Dem = ["Văn", "Thị", "Hữu", "Ngọc", "Minh"];
    private static readonly string[] Ten = ["An", "Bình", "Cường", "Dung", "Hà", "Khánh", "Linh", "Mai", "Nam", "Oanh", "Phúc", "Quân", "Sơn", "Thảo", "Uyên", "Vinh"];

    [Fact]
    public async Task SinhFixture_MotDuAnChayTronBonVongToiHoanTat()
    {
        var outPath = Environment.GetEnvironmentVariable("NOXH_FIXTURE_OUT")
            ?? throw new InvalidOperationException(
                "Đặt NOXH_FIXTURE_OUT=<đường dẫn file JSON đích> trước khi chạy generator.");

        var (projectId, applicants) = await SeedProjectAsync();
        var admin = await _factory.CreateAdminClientAsync();
        var clients = new Dictionary<Guid, HttpClient>();
        foreach (var a in applicants)
            clients[a.Id] = _factory.CustomerClient(await _factory.GetCustomerTokenAsync(a.Id, projectId));

        // Khoá danh sách qua endpoint thật → ListHash có thật, nằm trong preimage mốc entropy.
        await PostOkAsync(admin, $"/admin/projects/{projectId}/applicants/lock");

        // Ghi luôn bảng danh sách vừa bị khoá: hạng mục "danh sách hồ sơ đầu vào" chỉ tái lập được
        // khi có cả bảng lẫn khoá chỉ mục mù, mà báo cáo minh bạch không mang theo thứ nào.
        await WriteDanhSachFixtureAsync(projectId, outPath);

        // ── Vòng A1 (quyền mua) ──────────────────────────────────────────────────────────
        await CeremonyFlow.StartGateAAsync(admin, projectId, RSupervisorHex);

        // Cả 6 U2 bốc hết thì số người trúng mới tất định là 4 (w = ΣPriorityUnits − nU1).
        // Ô phiếu bỏ trống đã có ở vòng B và C.
        var confirmedA = applicants.Where(a => a.Group == ApplicantGroup.U2).ToList();
        foreach (var a in confirmedA)
            await PostOkAsync(clients[a.Id], "/customer/lottery/confirm-join", new { round = "A" });

        (await CeremonyFlow.CloseGateA1Async(admin, projectId, force: true, reason: ForceReason))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var a1Winners = new List<Seeded>();
        foreach (var a in confirmedA)
        {
            var body = await DrawAsync(clients[a.Id], "A1");
            if (body.GetProperty("ketQua").GetString() == "TRUNG") a1Winners.Add(a);
        }

        (await CeremonyFlow.CloseDrawA1Async(admin, projectId)).StatusCode.Should().Be(HttpStatusCode.OK);

        // ── Vòng A2 (phân căn ưu tiên) ───────────────────────────────────────────────────
        // Chừa người cuối không bấm → close-draw-a2 bốc thay, fixture có cả vé autoDrawn.
        var a2 = applicants.Where(a => a.Group == ApplicantGroup.U1).Concat(a1Winners).ToList();
        a2.Should().HaveCountGreaterThan(1, "cần ít nhất 1 người bấm và 1 người để máy bốc thay");
        foreach (var a in a2.Take(a2.Count - 1))
            await DrawAsync(clients[a.Id], "A2");

        await PostOkAsync(admin, $"/admin/projects/{projectId}/lottery/close-draw-a2");

        // ── Vòng B (bốc thẳng theo loại căn) ─────────────────────────────────────────────
        (await CeremonyFlow.OpenGateBAsync(admin, projectId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var daCoKetQua = await ApplicantIdsWithResultAsync(projectId, wonOnly: false);
        var bParticipants = applicants
            .Where(a => !daCoKetQua.Contains(a.Id) && a.Index % 7 != 0) // vắng mặt rải rác
            .ToList();
        foreach (var a in bParticipants)
            await PostOkAsync(clients[a.Id], "/customer/lottery/confirm-join", new { round = "B" });

        (await CeremonyFlow.CloseGateBAsync(admin, projectId)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Người xác nhận nhưng không bấm: ô phiếu bỏ trống, căn của vé đó chảy xuống vòng C.
        foreach (var a in bParticipants.Where(a => a.Index % 5 != 0))
            await DrawAsync(clients[a.Id], "B");

        await PostOkAsync(admin, $"/admin/projects/{projectId}/lottery/close-draw-b");

        // ── Vòng C (căn dư + dự khuyết đánh số) ──────────────────────────────────────────
        (await CeremonyFlow.OpenGateCAsync(admin, projectId)).StatusCode.Should().Be(HttpStatusCode.OK);

        var daTrung = await ApplicantIdsWithResultAsync(projectId, wonOnly: true);
        var cParticipants = applicants
            .Where(a => !daTrung.Contains(a.Id) && a.Index % 9 != 0)
            .ToList();
        foreach (var a in cParticipants)
            await PostOkAsync(clients[a.Id], "/customer/lottery/confirm-join", new { round = "C" });

        (await CeremonyFlow.CloseGateCAsync(admin, projectId, force: true, reason: ForceReason))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var a in cParticipants.Where(a => a.Index % 6 != 0))
            await DrawAsync(clients[a.Id], "C");

        await PostOkAsync(admin, $"/admin/projects/{projectId}/lottery/close-draw-c");

        // ── Báo cáo minh bạch ────────────────────────────────────────────────────────────
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync($"/projects/{projectId}/transparency");
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"transparency chỉ mở sau khi lễ hoàn tất: {await resp.Content.ReadAsStringAsync()}");

        var raw = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        AssertFixtureIsUsable(doc.RootElement);

        var pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) + "\n";

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
        await File.WriteAllTextAsync(outPath, pretty, new UTF8Encoding(false));
        await File.WriteAllTextAsync(MetaPathOf(outPath), BuildMeta(outPath, pretty, doc.RootElement),
            new UTF8Encoding(false));

        // Danh mục căn của chính dự án này: tái lập vòng phân căn ưu tiên phải dựng lại quỹ căn từ
        // danh mục, mà bản nhúng sẵn trong công cụ là của dự án khác.
        await File.WriteAllTextAsync(UnitCatalogPathOf(outPath), BuildUnitCatalog(), new UTF8Encoding(false));
    }

    /// <summary>Fixture thiếu khối nào thì chặn tại nguồn, đừng ghi ra file rồi mới phát hiện.</summary>
    private static void AssertFixtureIsUsable(JsonElement root)
    {
        root.GetProperty("completedAt").ValueKind.Should().Be(JsonValueKind.String);

        var rounds = root.GetProperty("decks").EnumerateArray()
            .Select(d => d.GetProperty("round").GetString()!).ToList();
        rounds.Should().Contain("A1");
        rounds.Should().Contain(r => r.StartsWith("A2:", StringComparison.Ordinal));
        rounds.Should().Contain(r => r.StartsWith("B:", StringComparison.Ordinal));
        rounds.Should().Contain("C");

        var cTickets = root.GetProperty("decks").EnumerateArray()
            .Single(d => d.GetProperty("round").GetString() == "C")
            .GetProperty("tickets").EnumerateArray().Select(t => t.GetString()!).ToList();
        cTickets.Should().Contain(p => p.StartsWith("DU_KHUYET:", StringComparison.Ordinal),
            "vé dự khuyết đã đánh số là tiêu chí nghiệm thu của fixture");
        cTickets.Should().Contain(p => p.StartsWith("TRUNG:", StringComparison.Ordinal),
            "phải còn căn dư cho vòng C, nếu không vòng C chỉ còn vé trượt");

        // Đủ bốn loại vé của lưới phiếu — thiếu loại nào là công cụ kiểm chứng không có mẫu để vẽ.
        var payloads = root.GetProperty("decks").EnumerateArray()
            .SelectMany(d => d.GetProperty("tickets").EnumerateArray())
            .Select(t => t.GetString()!).ToList();
        payloads.Should().Contain("TRUNG_QUYEN_MUA");
        payloads.Should().Contain("KHONG_TRUNG_UU_TIEN");
        payloads.Should().Contain("CHO_PHAN_LOAI_DU");
        payloads.Should().Contain("KHONG_TRUNG");

        var log = root.GetProperty("nhatKyBoc").EnumerateArray().ToList();
        log.Should().NotBeEmpty();
        log.Should().Contain(e => e.GetProperty("autoDrawn").GetBoolean());
        log.Should().Contain(e => !e.GetProperty("autoDrawn").GetBoolean());
        log.Should().OnlyContain(e => e.GetProperty("applicantId").ValueKind == JsonValueKind.String
                                      && e.GetProperty("deckId").ValueKind == JsonValueKind.String
                                      && e.GetProperty("entryHash").ValueKind == JsonValueKind.String);

        root.GetProperty("dauThoiGian").EnumerateArray()
            .Should().Contain(t => t.GetProperty("scope").GetString() == "FREEZE");
        root.GetProperty("ketQua").GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("nguonNgauNhien").GetArrayLength().Should().Be(3);
    }

    /// <summary>
    /// Bảng danh sách hồ sơ đã khoá + khoá chỉ mục mù đã dùng, ở đúng dạng tổ giám sát dán vào công
    /// cụ kiểm chứng (bốn cột ngăn bằng tab: mã hồ sơ · họ tên · số định danh · nhóm đối tượng).
    ///
    /// Khoá ghi ra đây là khoá DEV mặc định của backend, vốn đã nằm công khai trong mã nguồn — nó
    /// chỉ mở được dữ liệu test. Khoá thật không bao giờ được đi vào repo công cụ kiểm chứng.
    /// </summary>
    private async Task WriteDanhSachFixtureAsync(Guid projectId, string outPath)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var khoa = scope.ServiceProvider.GetRequiredService<EncryptionKeyProvider>().KIdx;

        var project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId);
        var applicants = await db.Applicants.AsNoTracking()
            .Where(a => a.ProjectId == projectId)
            .ToListAsync();

        // Cùng phép sắp với LockListEndpoint (OrderBy mặc định) — bảng in ra phải cùng thứ tự với
        // chuỗi đã đem băm, kẻo người kiểm dán đúng bảng mà vẫn ra "không khớp".
        var ordered = applicants.OrderBy(a => a.MaHoSo).ToList();

        var bang = new StringBuilder("Mã hồ sơ\tHọ tên\tSố định danh\tNhóm đối tượng\n");
        foreach (var a in ordered)
            bang.Append($"{a.MaHoSo}\t{a.FullName}\t{a.Cccd}\t{a.Group}\n");

        var text = bang.ToString();
        var dir = Path.GetDirectoryName(Path.GetFullPath(outPath))!;

        await File.WriteAllTextAsync(Path.Combine(dir, "danh-sach-golden.tsv"), text, new UTF8Encoding(false));

        var meta = new
        {
            bang = "danh-sach-golden.tsv",
            bangSha256 = Convert.ToHexString(
                SHA256.HashData(new UTF8Encoding(false).GetBytes(text))).ToLowerInvariant(),
            listHash = project.ListHash,
            kIdxHex = Convert.ToHexString(khoa).ToLowerInvariant(),
            kIdxGhiChu =
                "Khoá chỉ mục mù CỦA MÔI TRƯỜNG TEST — chính là khoá dev mặc định nằm sẵn trong mã nguồn "
                + "backend (EncryptionKeyProvider: 'DEV_IDX_KEY_REPLACE_IN_PROD_32B!' dạng UTF-8). KHÔNG phải "
                + "khoá của bất kỳ hệ thống thật nào; khoá thật không bao giờ được vào repo này.",
            nguon = "danh sách hồ sơ do GoldenTransparencyFixtureGenerator seed vào backend rồi khoá qua "
                    + "POST /admin/projects/{id}/applicants/lock",
            generator = "fixtures/generator/GoldenTransparencyFixtureGenerator.cs",
            backendCommit = Environment.GetEnvironmentVariable("NOXH_BACKEND_COMMIT"),
            backendCommittedAt = Environment.GetEnvironmentVariable("NOXH_BACKEND_COMMITTED_AT"),
            soHoSo = ordered.Count,
            coCauNhom = ordered.GroupBy(a => a.Group).OrderBy(g => g.Key)
                .ToDictionary(g => g.Key.ToString(), g => g.Count()),
        };

        await File.WriteAllTextAsync(
            Path.Combine(dir, "danh-sach-golden.meta.json"),
            JsonSerializer.Serialize(meta, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            }) + "\n",
            new UTF8Encoding(false));
    }

    private static string UnitCatalogPathOf(string outPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath))!, "apartment-units-golden.json");

    /// <summary>Danh mục căn ở đúng định dạng ban tổ chức công bố: <c>{"mã loại": [{"unitCode": …}]}</c>.</summary>
    private static string BuildUnitCatalog()
    {
        var danhMuc = Types.ToDictionary(
            t => t.Code,
            t => Enumerable.Range(1, t.TotalUnits)
                .Select(u => new { unitCode = $"{t.Code}-{u:D3}", block = "A", floor = (u - 1) / 4 + 1 })
                .ToList());

        return JsonSerializer.Serialize(danhMuc, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) + "\n";
    }

    private static string MetaPathOf(string outPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath))!,
            Path.GetFileNameWithoutExtension(outPath) + ".meta.json");

    private static string BuildMeta(string outPath, string fixtureText, JsonElement root)
    {
        var decks = root.GetProperty("decks").EnumerateArray().ToList();
        var meta = new
        {
            fixture = Path.GetFileName(outPath),
            fixtureSha256 = Convert.ToHexString(
                SHA256.HashData(new UTF8Encoding(false).GetBytes(fixtureText))).ToLowerInvariant(),
            generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            generator = "fixtures/generator/GoldenTransparencyFixtureGenerator.cs",
            backendCommit = Environment.GetEnvironmentVariable("NOXH_BACKEND_COMMIT"),
            backendCommittedAt = Environment.GetEnvironmentVariable("NOXH_BACKEND_COMMITTED_AT"),
            rSupervisorHex = RSupervisorHex,
            soLieu = new
            {
                soHoSo = Population.Sum(p => p.Count),
                soCan = Types.Sum(t => t.TotalUnits),
                soChongPhieu = decks.Count,
                soVe = decks.Sum(d => d.GetProperty("size").GetInt32()),
                soLuotBoc = root.GetProperty("nhatKyBoc").GetArrayLength(),
                soDongKetQua = root.GetProperty("ketQua").GetProperty("rows").GetArrayLength(),
                soTokenDauThoiGian = root.GetProperty("dauThoiGian").GetArrayLength()
            }
        };

        return JsonSerializer.Serialize(meta, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }) + "\n";
    }

    // ── Hạ tầng ──────────────────────────────────────────────────────────────────────────

    private async Task<(Guid ProjectId, List<Seeded> Applicants)> SeedProjectAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var blind = scope.ServiceProvider.GetRequiredService<IBlindIndex>();

        var project = new Project
        {
            Name = "Dự án NƠXH mẫu — fixture kiểm chứng",
            Address = "Số 1 đường Mẫu, phường Mẫu",
            Investor = "Công ty CP Đầu tư Mẫu",
            Status = ProjectStatus.Preparing,
            LoginOpen = true,
            WaitlistSize = 5
        };
        db.Projects.Add(project);

        var typeByCode = new Dictionary<string, ApartmentType>(StringComparer.Ordinal);
        for (var i = 0; i < Types.Length; i++)
        {
            var (code, name, total, priority) = Types[i];
            var type = new ApartmentType
            {
                Project = project,
                Name = name,
                Code = code,
                TotalUnits = total,
                PriorityUnits = priority,
                DisplayOrder = i + 1
            };
            db.ApartmentTypes.Add(type);
            typeByCode[code] = type;

            for (var u = 1; u <= total; u++)
                db.ApartmentUnits.Add(new ApartmentUnit
                {
                    ApartmentType = type,
                    UnitCode = $"{code}-{u:D3}",
                    Floor = (u - 1) / 4 + 1,
                    Block = "A",
                    UnitNumber = u
                });
        }

        var seeded = new List<(Applicant Entity, int Index, ApplicantGroup Group, string TypeCode)>();
        var n = 0;
        foreach (var (group, typeCode, count) in Population)
            for (var c = 0; c < count; c++)
            {
                n++;
                var cccd = $"07901000{n:D4}";
                var phone = $"09{10_000_000 + n}";
                var applicant = new Applicant
                {
                    Project = project,
                    ApartmentType = typeByCode[typeCode],
                    MaHoSo = $"HS{n:D3}",
                    FullName = $"{Ho[n % Ho.Length]} {Dem[n % Dem.Length]} {Ten[n % Ten.Length]}",
                    Group = group,
                    Cccd = cccd,
                    CccdBlindIndex = blind.Compute(cccd),
                    CccdLast4 = cccd[^4..],
                    Phone = phone,
                    PhoneBlindIndex = blind.Compute(phone),
                    PhoneMasked = PiiMasking.MaskPhone(phone)
                };
                db.Applicants.Add(applicant);
                seeded.Add((applicant, n, group, typeCode));
            }

        await db.SaveChangesAsync();

        return (project.Id,
            seeded.Select(s => new Seeded(s.Entity.Id, s.Index, s.Group, s.TypeCode)).ToList());
    }

    private async Task<HashSet<Guid>> ApplicantIdsWithResultAsync(Guid projectId, bool wonOnly)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.LotteryResults.AsNoTracking()
                .Where(r => r.ProjectId == projectId && (!wonOnly || r.Won))
                .Select(r => r.ApplicantId)
                .ToListAsync())
            .ToHashSet();
    }

    private static async Task PostOkAsync(HttpClient client, string url, object? body = null)
    {
        var resp = body is null
            ? await client.PostAsync(url, null)
            : await client.PostAsJsonAsync(url, body);
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"POST {url} → {await resp.Content.ReadAsStringAsync()}");
    }

    private static async Task<JsonElement> DrawAsync(HttpClient client, string round)
    {
        var resp = await client.PostAsJsonAsync("/customer/lottery/draw", new { round });
        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"draw {round} → {await resp.Content.ReadAsStringAsync()}");
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }
}
