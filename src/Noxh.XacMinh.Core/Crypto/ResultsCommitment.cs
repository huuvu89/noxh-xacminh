using System.Globalization;
using System.Text;

namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>Lottery.Api/Common/Crypto/ResultsCommitment.cs</c> —
/// <c>HashHex</c>). Canonical: sắp theo định danh hồ sơ dạng "D" (ordinal, không phụ thuộc culture),
/// mỗi dòng <c>{applicantId}\t{won 1/0}\t{tier}\t{typeCode|-}\t{unitCode|-}\t{waitlistRank|-}\n</c>,
/// UTF-8 không BOM.
///
/// Canonical CHỈ gồm các trường bất biến lúc niêm phong. Các trường đổi hợp lệ sau lễ (giao kết
/// quả, huỷ kết quả, mốc niêm phong) KHÔNG nằm trong đây — thêm bớt trường là công cụ báo sai một
/// bảng kết quả còn nguyên.
/// </summary>
public static class ResultsCommitment
{
    /// <summary>Một dòng canonical — phần bất biến của một kết quả, dùng mã công khai chứ không phải khoá nội bộ.</summary>
    public sealed record Row(
        Guid ApplicantId,
        bool Won,
        string Tier,
        string? TypeCode,
        string? UnitCode,
        int? WaitlistRank);

    /// <summary>Chuỗi canonical dạng chữ — vừa là đầu vào của phép băm, vừa là preimage cho chế độ chuyên sâu.</summary>
    public static string CanonicalText(IEnumerable<Row> rows)
    {
        var dong = rows.Select(ThanhDong).ToList();

        // Backend chỉ cần sắp theo định danh hồ sơ vì cơ sở dữ liệu đã bảo đảm mỗi hồ sơ một dòng;
        // file người dùng thả vào KHÔNG có ràng buộc đó, nên hai dòng trùng định danh phải xếp theo
        // một trật tự cố định — nếu không cùng một file lại cho hai mã băm khác nhau.
        dong.Sort(static (a, b) =>
        {
            var theoHoSo = string.CompareOrdinal(a.Khoa, b.Khoa);
            return theoHoSo != 0 ? theoHoSo : string.CompareOrdinal(a.Text, b.Text);
        });

        return string.Concat(dong.Select(d => d.Text));
    }

    /// <summary>SHA-256 hex (thường) của canonical — chính là <c>resultsHash</c> đã công bố.</summary>
    public static string HashHex(IEnumerable<Row> rows) =>
        Hex.Sha256Hex(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            .GetBytes(CanonicalText(rows)));

    private sealed record DongCanonical(string Khoa, string Text);

    private static DongCanonical ThanhDong(Row r)
    {
        var khoa = r.ApplicantId.ToString("D");
        var sb = new StringBuilder();

        sb.Append(khoa).Append('\t')
          .Append(r.Won ? '1' : '0').Append('\t')
          .Append(r.Tier).Append('\t')
          .Append(r.TypeCode ?? "-").Append('\t')
          .Append(r.UnitCode ?? "-").Append('\t')
          .Append(r.WaitlistRank?.ToString(CultureInfo.InvariantCulture) ?? "-").Append('\n');

        return new DongCanonical(khoa, sb.ToString());
    }
}
