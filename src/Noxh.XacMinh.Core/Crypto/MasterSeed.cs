using System.Security.Cryptography;
using System.Text;

namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>Lottery.Api/Common/Crypto/MasterSeed.cs</c>, phần
/// <c>RoundSeed</c> lấy từ <c>CryptoHelper.RoundSeed</c> mà nó gọi tới).
/// MASTER_SEED_V = SHA-256(R_server_V ‖ R_supervisor ‖ H_blockchain_V) — nối byte thô, không dấu
/// phân cách, không hex: nối nhầm dạng là ra một hạt giống khác hoàn toàn.
/// R_supervisor nhập tại gate A và TÁI DÙNG cho B, C; R_server tươi mỗi gate;
/// H_blockchain là hash block đào SAU khi đóng cổng vòng.
/// </summary>
public static class MasterSeed
{
    /// <summary>MASTER_SEED_V = SHA-256(R_server ‖ R_supervisor ‖ H_blockchain).</summary>
    public static byte[] Build(byte[] rServer, byte[] rSupervisor, byte[] hBlockchain)
    {
        var combined = new byte[rServer.Length + rSupervisor.Length + hBlockchain.Length];
        Buffer.BlockCopy(rServer, 0, combined, 0, rServer.Length);
        Buffer.BlockCopy(rSupervisor, 0, combined, rServer.Length, rSupervisor.Length);
        Buffer.BlockCopy(hBlockchain, 0, combined, rServer.Length + rSupervisor.Length, hBlockchain.Length);
        return SHA256.HashData(combined);
    }

    /// <summary>roundSeed_V(label) = SHA-256(MASTER_SEED_V ‖ UTF-8(label)).</summary>
    public static byte[] RoundSeed(byte[] masterSeed, string label)
    {
        var labelBytes = Encoding.UTF8.GetBytes(label);
        var combined = new byte[masterSeed.Length + labelBytes.Length];
        Buffer.BlockCopy(masterSeed, 0, combined, 0, masterSeed.Length);
        Buffer.BlockCopy(labelBytes, 0, combined, masterSeed.Length, labelBytes.Length);
        return SHA256.HashData(combined);
    }
}
