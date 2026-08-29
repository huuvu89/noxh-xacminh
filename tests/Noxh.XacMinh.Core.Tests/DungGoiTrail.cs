using System.IO.Compression;
using System.Text;
using Noxh.XacMinh.Core.Kho;

namespace Noxh.XacMinh.Core.Tests;

/// <summary>
/// Dựng <b>gói trail</b> đúng khuôn script <c>cong-cu/tai-goi-trail.py</c> ghi ra: một file zip gồm
/// <c>manifest.json</c>, các trang <c>ListObjectsV2</c> nguyên văn, và byte thô từng lô.
///
/// Chép lại khuôn ở đây (thay vì gọi chính script) là chủ ý, giống cách <see cref="DungLoTrail"/>
/// chép khuôn lô của backend: đổi khuôn một bên mà quên bên kia thì chỗ lệch phải lộ ra ở test, chứ
/// không phải ở giữa hội trường. Hàng rào chống trôi giữa script và lõi nằm ở
/// <c>Noxh.XacMinh.Pipeline.Tests</c>.
/// </summary>
internal static class DungGoiTrail
{
    private const string Ns = "http://s3.amazonaws.com/doc/2006-03-01/";

    /// <summary>Một trang danh sách như kho trả về; <paramref name="dauTiepTuc"/> khác null là còn trang sau.</summary>
    public static string TrangDanhSach(IEnumerable<string> key, string? dauTiepTuc = null) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <ListBucketResult xmlns="{Ns}">
           <Name>bang-chung</Name>
           <IsTruncated>{(dauTiepTuc is null ? "false" : "true")}</IsTruncated>
           {(dauTiepTuc is null ? string.Empty : $"<NextContinuationToken>{dauTiepTuc}</NextContinuationToken>")}
           {string.Concat(key.Select(k => $"<Contents><Key>{k}</Key></Contents>"))}
         </ListBucketResult>
         """;

    public static string TrangTuChoi(string ma) =>
        $"""
         <?xml version="1.0" encoding="UTF-8"?>
         <Error><Code>{ma}</Code><Message>Access Denied</Message></Error>
         """;

    public const string ManifestMau = $$"""
        {
          "phienBan": "{{DocGoiTrail.PhienBanChuan}}",
          "diemCuoi": "https://s3.thu-nghiem.vn",
          "bucket": "bang-chung",
          "tienTo": "trail/",
          "vung": "us-east-1",
          "cheDoDoc": "an-danh",
          "taoLuc": "2026-08-15T09:00:00Z"
        }
        """;

    /// <summary>Gói đầy đủ cho một chuỗi lô: một trang danh sách liệt kê hết, và đủ nội dung từng lô.</summary>
    public static byte[] Goi(IReadOnlyList<DoiTuongKho> lo, string? manifest = ManifestMau) =>
        Goi([TrangDanhSach(lo.Select(l => l.Key))], lo, manifest);

    public static byte[] Goi(
        IReadOnlyList<string> trang,
        IReadOnlyList<DoiTuongKho> noiDung,
        string? manifest = ManifestMau)
    {
        using var bo = new MemoryStream();

        using (var goi = new ZipArchive(bo, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (manifest is not null) Them(goi, "manifest.json", Encoding.UTF8.GetBytes(manifest));

            for (var i = 0; i < trang.Count; i++)
                Them(goi, $"listing/{i:D3}.xml", Encoding.UTF8.GetBytes(trang[i]));

            foreach (var lo in noiDung) Them(goi, $"objects/{lo.Key}", lo.NoiDung);
        }

        return bo.ToArray();
    }

    private static void Them(ZipArchive goi, string ten, byte[] noiDung)
    {
        using var dong = goi.CreateEntry(ten).Open();
        dong.Write(noiDung);
    }
}
