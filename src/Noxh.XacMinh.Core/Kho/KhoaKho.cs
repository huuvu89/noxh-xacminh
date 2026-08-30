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
/// Ba chế độ đọc kho, đúng ba hoàn cảnh có thật: <b>trong lễ</b> kho chưa mở công khai nên người
/// kiểm dán khoá chỉ-đọc; <b>sau lễ</b> kho mở, ai cũng đọc ẩn danh được; và <b>khi trình duyệt
/// không gọi được kho</b> (kho chưa bật CORS, hoặc máy đang chạy bản offline) thì kho được tải sẵn
/// bằng script rồi nạp vào dưới dạng gói.
///
/// Chế độ thứ ba yếu hơn hai chế độ kia và phải nói ra chỗ yếu đó: đọc trực tiếp thì chính trình
/// duyệt người kiểm chứng kiến các byte đang nằm trên kho, còn nạp gói thì mắt xích đó do người
/// chạy script gánh. Công cụ vẫn tự băm lại từng byte trong gói, nhưng "byte này lấy từ kho ấy" là
/// thứ nó không kiểm được — nên chế độ đọc đi kèm mọi kết luận trail.
/// </summary>
public enum CheDoDocKho
{
    AnDanh,
    KhoaChiDoc,
    GoiNhapTay,
}

/// <summary>Địa chỉ kho — người kiểm nhập theo thông báo của ban tổ chức, công cụ không đoán hộ.</summary>
public sealed record ThongSoKho(string DiemCuoi, string Bucket, string Vung, string TienTo)
{
    public bool DuDeDoc => !string.IsNullOrWhiteSpace(DiemCuoi) && !string.IsNullOrWhiteSpace(Bucket);

    public string MoTa => $"{DiemCuoi.TrimEnd('/')}/{Bucket}" + (string.IsNullOrEmpty(TienTo) ? string.Empty : $"/{TienTo}");
}
