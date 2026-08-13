namespace Noxh.XacMinh.Core.Kho;

/// <summary>
/// Khoá truy cập kho bằng chứng người kiểm dán vào. Chỉ sống trong bộ nhớ tab: không ghi đĩa, không
/// cookie, không gửi đi đâu ngoài chính kho — và <see cref="SigV4"/> chỉ ký được GET/HEAD, nên kể cả
/// khoá dán nhầm có quyền ghi thì công cụ cũng không dùng nổi quyền đó.
/// </summary>
public sealed record KhoaKho(string MaKhoa, string BiMat, string? ThePhien = null)
{
    public bool DuDeKy => !string.IsNullOrWhiteSpace(MaKhoa) && !string.IsNullOrWhiteSpace(BiMat);
}

/// <summary>
/// Hai chế độ đọc kho, đúng hai hoàn cảnh có thật: <b>trong lễ</b> kho chưa mở công khai nên người
/// kiểm dán khoá chỉ-đọc; <b>sau lễ</b> kho mở, ai cũng đọc ẩn danh được.
/// </summary>
public enum CheDoDocKho
{
    AnDanh,
    KhoaChiDoc,
}

/// <summary>Địa chỉ kho — người kiểm nhập theo thông báo của ban tổ chức, công cụ không đoán hộ.</summary>
public sealed record ThongSoKho(string DiemCuoi, string Bucket, string Vung, string TienTo)
{
    public bool DuDeDoc => !string.IsNullOrWhiteSpace(DiemCuoi) && !string.IsNullOrWhiteSpace(Bucket);

    public string MoTa => $"{DiemCuoi.TrimEnd('/')}/{Bucket}" + (string.IsNullOrEmpty(TienTo) ? string.Empty : $"/{TienTo}");
}
