using System.Security.Cryptography;

namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>Lottery.Api/Common/Crypto/MasterSeed.cs</c>) — chỉ giữ
/// <see cref="Build"/>, phần <c>RoundSeed</c> để vé tái lập vòng bốc mang sang.
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
}
