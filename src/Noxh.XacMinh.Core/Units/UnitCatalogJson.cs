using System.Text.Json;
using Noxh.XacMinh.Core.Crypto;

namespace Noxh.XacMinh.Core.Units;

/// <summary>
/// Nạp file danh mục căn (dạng đã công bố: <c>{"mã loại": [{"unitCode": ..., ...}]}</c>).
/// Đọc bằng <see cref="JsonDocument"/> chứ không deserialize qua kiểu: vừa khỏi reflection (bản
/// publish có trimming), vừa chỉ được ra chỗ hỏng nằm ở loại nào, căn thứ mấy. Không ném ngoại lệ
/// ra ngoài — file lạ là chuyện thường ngày, mà một ngoại lệ lọt lên Blazor là một trang trắng.
/// </summary>
public static class UnitCatalogJson
{
    private static readonly JsonDocumentOptions Options = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static ReadOnlySpan<byte> Bom => [0xEF, 0xBB, 0xBF];

    public static UnitCatalogParseResult Parse(byte[]? noiDung)
    {
        if (noiDung is null || noiDung.Length == 0)
            return UnitCatalogParseResult.Failed(
                "Chưa đọc được nội dung file danh mục căn — file rỗng hoặc chưa chọn được file.");

        // Băm trên byte nguyên bản của file (kể cả BOM): có vậy `sha256sum` của người kiểm mới ra
        // đúng con số công cụ hiện lên.
        var maBam = Hex.Sha256Hex(noiDung);

        // BOM lọt vào khi file được ghi lại bằng Notepad/Excel; bộ đọc JSON không tự bỏ qua nó.
        var utf8 = noiDung.AsMemory(noiDung.AsSpan().StartsWith(Bom) ? Bom.Length : 0);

        try
        {
            using var doc = JsonDocument.Parse(utf8, Options);

            return Doc(doc.RootElement, maBam);
        }
        catch (JsonException ex)
        {
            return UnitCatalogParseResult.Failed(MoTaLoiCuPhap(ex));
        }
    }

    private static UnitCatalogParseResult Doc(JsonElement root, string maBam)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return UnitCatalogParseResult.Failed(
                "File này không phải danh mục căn: danh mục phải là một đối tượng JSON dạng "
                + "{\"mã loại căn\": [danh sách căn]}.");

        var loai = new List<UnitCatalogType>();
        var daThay = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var khoi in root.EnumerateObject())
        {
            var tenLoai = MoTaGiaTri.Gon(khoi.Name);

            // JSON cho phép trùng khoá, danh mục thì không: hai khối cùng mã loại thì "quỹ căn loại
            // L" là khối nào cũng là đoán.
            if (loai.Any(l => string.Equals(l.TypeCode, khoi.Name, StringComparison.Ordinal)))
                return UnitCatalogParseResult.Failed(
                    $"Loại căn '{tenLoai}' xuất hiện nhiều lần trong file danh mục.");

            if (khoi.Value.ValueKind != JsonValueKind.Array)
                return UnitCatalogParseResult.Failed(
                    $"Loại căn '{tenLoai}' phải là một danh sách căn.");

            var ma = new List<string>();
            var thuTu = 0;

            foreach (var can in khoi.Value.EnumerateArray())
            {
                thuTu++;

                if (can.ValueKind != JsonValueKind.Object
                    || DocMaCan(can) is not { } maCan
                    || string.IsNullOrWhiteSpace(maCan))
                    return UnitCatalogParseResult.Failed(
                        $"Loại căn '{tenLoai}', căn thứ {thuTu}: thiếu mã căn ('unitCode').");

                if (daThay.TryGetValue(maCan, out var loaiTruoc))
                    return UnitCatalogParseResult.Failed(
                        $"Mã căn '{MoTaGiaTri.Gon(maCan)}' xuất hiện nhiều lần (loại "
                        + $"'{MoTaGiaTri.Gon(loaiTruoc)}' và loại '{tenLoai}').");

                daThay.Add(maCan, khoi.Name);
                ma.Add(maCan);
            }

            if (ma.Count == 0)
                return UnitCatalogParseResult.Failed($"Loại căn '{tenLoai}' không có căn nào.");

            loai.Add(new UnitCatalogType(khoi.Name, ma));
        }

        if (loai.Count == 0)
            return UnitCatalogParseResult.Failed("File danh mục không có loại căn nào.");

        // Sắp theo mã loại: thứ tự hiển thị không được đổi theo thứ tự khoá người nạp file viết ra.
        loai.Sort((a, b) => string.CompareOrdinal(a.TypeCode, b.TypeCode));

        return UnitCatalogParseResult.Ok(new UnitCatalog(maBam, loai));
    }

    /// <summary>Nhận cả <c>unitCode</c> lẫn <c>UnitCode</c>: bản xuất lại bằng công cụ khác hay ra
    /// PascalCase, mà đó là lệch cách viết chứ không phải thiếu dữ liệu.</summary>
    private static string? DocMaCan(JsonElement can)
    {
        if (!can.TryGetProperty("unitCode", out var ma) && !can.TryGetProperty("UnitCode", out ma))
            return null;

        return ma.ValueKind == JsonValueKind.String ? ma.GetString() : null;
    }

    private static string MoTaLoiCuPhap(JsonException ex)
    {
        var viTri = ex.LineNumber is { } dong
            ? $" (dòng {dong + 1}, ký tự {(ex.BytePositionInLine ?? 0) + 1})"
            : string.Empty;

        return $"File danh mục căn không phải JSON hợp lệ{viTri}.";
    }
}

/// <summary>Kết quả nạp danh mục: hoặc có danh mục, hoặc có một câu giải thích cho người dùng.</summary>
public sealed class UnitCatalogParseResult
{
    private UnitCatalogParseResult(UnitCatalog? catalog, string? errorMessage)
    {
        Catalog = catalog;
        ErrorMessage = errorMessage;
    }

    public UnitCatalog? Catalog { get; }

    public string? ErrorMessage { get; }

    public bool Success => Catalog is not null;

    public static UnitCatalogParseResult Ok(UnitCatalog catalog) => new(catalog, null);

    public static UnitCatalogParseResult Failed(string message) => new(null, message);
}
