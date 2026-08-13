namespace Noxh.XacMinh.Core.Verification.Checks;

/// <summary>Cách rút gọn giá trị lạ trước khi đưa vào câu giải thích, dùng chung cho mọi hạng mục.</summary>
internal static class MoTaGiaTri
{
    private const int Tran = 60;

    /// <summary>
    /// Giá trị trong câu giải thích phải đọc được; file bị sửa có thể nhồi chuỗi dài bất kỳ. Cắt lùi
    /// một ký tự khi chỗ cắt rơi vào giữa cặp surrogate — nửa cặp sẽ thành ký tự rác trên màn hình
    /// và trong bản xuất kết quả.
    /// </summary>
    public static string Gon(string giaTri)
    {
        if (giaTri.Length <= Tran) return giaTri;

        var cat = char.IsHighSurrogate(giaTri[Tran - 1]) ? Tran - 1 : Tran;

        return string.Concat(giaTri.AsSpan(0, cat), "…");
    }
}
