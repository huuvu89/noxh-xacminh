namespace Noxh.XacMinh.Core.Units;

/// <summary>
/// Danh mục căn hộ của dự án — <b>dữ liệu đầu vào do ban tổ chức công bố</b>, không phải thứ công cụ
/// tự chứng minh. <see cref="Sha256"/> băm trên đúng byte của file danh mục, để người kiểm đối chiếu
/// được bằng <c>sha256sum</c> thay vì tin bản công cụ mang theo.
/// </summary>
public sealed record UnitCatalog(string Sha256, IReadOnlyList<UnitCatalogType> Types)
{
    public int Total { get; } = Types.Sum(t => t.UnitCodes.Count);
}

/// <summary>Một loại căn và các mã căn thuộc loại đó, giữ nguyên thứ tự trong file danh mục.</summary>
public sealed record UnitCatalogType(string TypeCode, IReadOnlyList<string> UnitCodes);
