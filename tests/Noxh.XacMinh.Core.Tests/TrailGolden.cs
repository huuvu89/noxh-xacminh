using Noxh.XacMinh.Core.Kho;
using Noxh.XacMinh.Core.Transparency;
using Noxh.XacMinh.TestSupport;
using Xunit;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Trail bằng chứng <b>đóng vai</b> thứ mà vỏ UI đọc được từ kho: các lượt bốc của fixture chuẩn
/// vàng, chia thành lô và móc xích đúng khuôn <c>NOXH-TRAIL-v1</c> backend đẩy lên.
///
/// Nói thẳng ranh giới: đây <b>chưa</b> phải trail thật do backend sinh ra, mà dựng từ chính nhật ký
/// bốc trong fixture — đủ để kiểm chuỗi móc xích và khoảng trống số thứ tự lô (vé #18), nhưng vé
/// đối chiếu trail với báo cáo (#19) sẽ cần một fixture sinh từ chính đường ghi của backend, kẻo
/// phép đối chiếu chỉ so dữ liệu với chính nó.
/// </summary>
internal static class TrailGolden
{
    private const string MaTienTrinh = "a1b2c3d4";

    private static readonly DateTime BatDau = new(2026, 8, 12, 20, 27, 0, DateTimeKind.Utc);

    /// <summary>
    /// Chia nhật ký bốc thành từng lô cỡ này — backend gom theo cửa sổ 2 giây, ở đây gom theo số
    /// bước cho tất định. Lô nhỏ để fixture có nhiều mắt xích: một lô duy nhất thì chuỗi móc xích
    /// không có gì để kiểm.
    /// </summary>
    private const int CoLo = 8;

    public static KhoBangChung Doc(CheDoDocKho cheDo = CheDoDocKho.AnDanh) =>
        KhoBangChung.Doc(cheDo, DoiTuong(), "s3.thu-nghiem.vn/bang-chung-noxh");

    public static IReadOnlyList<DoiTuongKho> DoiTuong()
    {
        var nhatKy = Bao().DrawLog ?? [];
        var doiTuong = new List<DoiTuongKho>();
        string? keyTruoc = null;
        string? shaTruoc = null;

        for (var i = 0; i * CoLo < nhatKy.Count; i++)
        {
            var soLo = i + 1;
            var luc = BatDau.AddSeconds(2 * soLo);
            var banGhi = nhatKy.Skip(i * CoLo).Take(CoLo).Select(VeDaBoc).ToList();
            var noiDung = DungLoTrail.NoiDung(soLo, MaTienTrinh, luc, banGhi, keyTruoc, shaTruoc);

            keyTruoc = DungLoTrail.Key(soLo, MaTienTrinh, luc);
            shaTruoc = DungLoTrail.Sha256Hex(noiDung);
            doiTuong.Add(new DoiTuongKho(keyTruoc, noiDung));
        }

        return doiTuong;
    }

    private static object VeDaBoc(DrawLogEntry buoc) => new
    {
        kind = "TICKET_DRAWN",
        projectId = Bao().ProjectId,
        occurredAt = BatDau,
        payload = new
        {
            applicantId = buoc.ApplicantId,
            deckId = buoc.DeckId,
            round = buoc.Round,
            position = buoc.Position,
            ticketPayload = buoc.Payload,
        },
    };

    private static TransparencyReport Bao()
    {
        var ketQua = TransparencyJson.Parse(GoldenFixture.Json());
        Assert.True(ketQua.Success, ketQua.ErrorMessage);

        return ketQua.Report!;
    }
}
