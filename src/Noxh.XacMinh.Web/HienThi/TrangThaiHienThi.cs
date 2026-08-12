namespace Noxh.XacMinh.Web.HienThi;

/// <summary>
/// Người dân: mỗi hạng mục một dòng kết luận kèm câu giải thích, không hex.
/// Chuyên sâu: thêm kỳ vọng / tính được / preimage / số liệu thô cho giám sát và kiểm toán.
/// </summary>
public enum CheDoHienThi
{
    NguoiDan,
    ChuyenSau,
}

/// <summary>
/// Lựa chọn chế độ, sống theo phiên chứ không theo lần nạp file: đổi sang chuyên sâu rồi thả file
/// khác vào thì vẫn còn chuyên sâu.
/// </summary>
public sealed class TrangThaiHienThi
{
    public CheDoHienThi CheDo { get; private set; } = CheDoHienThi.NguoiDan;

    public bool ChuyenSau
    {
        get => CheDo == CheDoHienThi.ChuyenSau;
        set => Dat(value ? CheDoHienThi.ChuyenSau : CheDoHienThi.NguoiDan);
    }

    /// <summary>Component nào đang vẽ theo chế độ thì nghe sự kiện này để tự vẽ lại.</summary>
    public event Action? DaDoi;

    public void Dat(CheDoHienThi cheDo)
    {
        if (CheDo == cheDo) return;

        CheDo = cheDo;
        DaDoi?.Invoke();
    }
}
