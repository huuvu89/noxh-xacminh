using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Trail bằng chứng <b>đóng vai</b> thứ mà vỏ UI đọc được từ kho: các bản ghi của fixture chuẩn vàng,
/// chia thành lô và móc xích đúng khuôn <c>NOXH-TRAIL-v1</c> backend đẩy lên.
///
/// Nói thẳng ranh giới, vì nó quyết định các ca ĐẠT ở đây nặng tới đâu:
///  · <b>Lượt bốc</b> dựng lại từ chính nhật ký bốc trong báo cáo ⇒ ca khớp chỉ chứng minh đường ống
///    chạy đúng, không chứng minh backend đúng. Sức nặng nằm ở các ca lệch.
///  · <b>Cam kết ngẫu nhiên máy chủ</b> và <b>đầu chuỗi băm từng vòng</b> lấy từ giá trị backend
///    thật đã ghim trong khối dấu thời gian (preimage <c>FREEZE</c> và <c>STEPCHAIN:{vòng}</c>) —
///    đem so với giá trị công cụ tự tính lại từ nhật ký bốc, tức là hai đường độc lập.
///
/// Trail thật do chính đường ghi của backend sinh ra vẫn là việc còn nợ: cần bật đường đẩy bằng
/// chứng trong bộ sinh fixture, không phải việc của lõi kiểm.
/// </summary>
internal static class TrailGolden
{
    private const string MaTienTrinh = "a1b2c3d4";

    private static readonly DateTime BatDau = new(2026, 8, 12, 20, 27, 0, DateTimeKind.Utc);

    /// <summary>
    /// Chia bản ghi thành từng lô cỡ này — backend gom theo cửa sổ 2 giây, ở đây gom theo số bản ghi
    /// cho tất định. Lô nhỏ để fixture có nhiều mắt xích: một lô duy nhất thì chuỗi móc xích không có
    /// gì để kiểm.
    /// </summary>
    private const int CoLo = 8;

    public static KhoBangChung Doc(CheDoDocKho cheDo = CheDoDocKho.AnDanh) => Kho(TatCaBanGhi(), cheDo: cheDo);

    public static KhoBangChung Kho(
        IReadOnlyList<object> banGhi,
        bool daLietKeHet = true,
        CheDoDocKho cheDo = CheDoDocKho.AnDanh) =>
        KhoBangChung.Doc(cheDo, DoiTuong(banGhi), "s3.thu-nghiem.vn/bang-chung-noxh", daLietKeHet);

    public static List<object> TatCaBanGhi(string? maDuAn = null) =>
        [.. LuotBoc(maDuAn), CamKet(maDuAn: maDuAn), .. DauChuoi(maDuAn: maDuAn)];

    // ── Bản ghi từng loại ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Vé do máy bốc thay sinh ở close-draw, không đi qua đường bốc vé nên KHÔNG lên trail — fixture
    /// phải giống chỗ đó, kẻo phép đối chiếu được nghiệm thu trên một trail không có thật.
    /// </summary>
    public static List<object> LuotBoc(string? maDuAn = null) =>
        [.. NhatKy().Where(e => e.AutoDrawn != true).Select(e => LuotBocCua(e, maDuAn))];

    /// <summary>Trail đầy đủ nhưng một lượt bốc mang nội dung khác — dựng ca "khác nội dung".</summary>
    public static List<object> ThayLuotBoc(DrawLogEntry buoc, string? maHoSoMoi = null, string? noiDungVeMoi = null)
    {
        var thay = DungLoTrail.LuotBoc(
            MaDuAn(null),
            buoc.Round,
            maHoSoMoi ?? buoc.ApplicantId,
            buoc.DeckId,
            buoc.Position,
            noiDungVeMoi ?? buoc.Payload);

        return
        [
            .. NhatKy().Where(e => e.AutoDrawn != true)
                .Select(e => e.EntryHash == buoc.EntryHash ? thay : LuotBocCua(e, null)),
            CamKet(),
            .. DauChuoi(),
        ];
    }

    /// <summary>Cam kết ngẫu nhiên máy chủ backend đã ghim — đọc từ chuỗi được đóng dấu thời gian.</summary>
    public static object CamKet(int lanChot = 1, string? camKetBMoi = null, string? maDuAn = null)
    {
        var freeze = Preimage("FREEZE");

        return DungLoTrail.CamKetNgauNhien(
            MaDuAn(maDuAn),
            Dong(freeze, "rServerCommitA="),
            camKetBMoi ?? Dong(freeze, "rServerCommitB="),
            Dong(freeze, "rServerCommitC="),
            lanChot);
    }

    /// <summary>
    /// Đầu chuỗi băm từng vòng backend đã ghim, cũng đọc từ chuỗi được đóng dấu thời gian.
    /// <paramref name="soBuocSaiCuaVong"/> làm hỏng số bước khai của đúng một vòng.
    /// </summary>
    public static List<object> DauChuoi(string? soBuocSaiCuaVong = null, string? maDuAn = null)
    {
        var banGhi = new List<object>();

        foreach (var vong in new[] { "A1", "A2", "B", "C" })
        {
            var preimage = Preimage($"STEPCHAIN:{vong}");
            var soBuoc = int.Parse(Dong(preimage, "stepCount=")!);

            banGhi.Add(DungLoTrail.DauChuoi(
                MaDuAn(maDuAn),
                vong,
                vong == soBuocSaiCuaVong ? soBuoc + 1 : soBuoc,
                Dong(preimage, "chainHead=")));
        }

        return banGhi;
    }

    // ── Fixture chuẩn vàng ──────────────────────────────────────────────────────────────────

    public static TransparencyReport Bao()
    {
        var ketQua = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);

        return ketQua.Report!;
    }

    public static IReadOnlyList<DrawLogEntry> NhatKy() => Bao().DrawLog ?? [];

    private static string MaDuAn(string? thayThe) => thayThe ?? Bao().ProjectId!;

    private static object LuotBocCua(DrawLogEntry buoc, string? maDuAn) =>
        DungLoTrail.LuotBoc(
            MaDuAn(maDuAn), buoc.Round, buoc.ApplicantId, buoc.DeckId, buoc.Position, buoc.Payload);

    /// <summary>Chuỗi đã đóng dấu thời gian của một phạm vi — nguồn giá trị backend thật trong fixture.</summary>
    private static string Preimage(string scope) =>
        (Bao().Timestamps ?? [])
        .First(t => string.Equals(t?.Scope, scope, StringComparison.Ordinal))!
        .Preimage!;

    private static string? Dong(string preimage, string khoa) =>
        preimage.Split('\n').FirstOrDefault(d => d.StartsWith(khoa, StringComparison.Ordinal))?[khoa.Length..];

    // ── Đóng gói thành lô trên kho ──────────────────────────────────────────────────────────

    private static IReadOnlyList<DoiTuongKho> DoiTuong(IReadOnlyList<object> banGhi)
    {
        var doiTuong = new List<DoiTuongKho>();
        string? keyTruoc = null;
        string? shaTruoc = null;

        for (var i = 0; i * CoLo < banGhi.Count; i++)
        {
            var soLo = i + 1;
            var luc = BatDau.AddSeconds(2 * soLo);
            var trongLo = banGhi.Skip(i * CoLo).Take(CoLo).ToList();
            var noiDung = DungLoTrail.NoiDung(soLo, MaTienTrinh, luc, trongLo, keyTruoc, shaTruoc);

            keyTruoc = DungLoTrail.Key(soLo, MaTienTrinh, luc);
            shaTruoc = DungLoTrail.Sha256Hex(noiDung);
            doiTuong.Add(new DoiTuongKho(keyTruoc, noiDung));
        }

        return doiTuong;
    }
}
