using System.Security.Cryptography;

namespace Noxh.XacMinh.Core.Crypto;

/// <summary>
/// COPY NGUYÊN VĂN từ backend (<c>Lottery.Api/Common/Crypto/SeededShuffle.cs</c>). Không viết lại
/// theo trí nhớ, không "cải tiến": lệch một byte là công cụ dựng ra một chồng phiếu khác và báo
/// KHÔNG ĐẠT cho một buổi lễ sạch. Ba chỗ dễ viết sai nhất — bộ đếm PRNG 8 byte little-endian, xáo
/// từ cuối về đầu, lấy mẫu có loại bỏ để không lệch — đều nằm trong đây.
///
/// Deterministic Fisher-Yates shuffle using a seed-derived PRNG.
/// The PRNG is constructed by feeding the seed + counter into SHA-256
/// to generate an unlimited byte stream — making results reproducible
/// for any given seed.
/// </summary>
public static class SeededShuffle
{
    public static List<T> Shuffle<T>(IEnumerable<T> source, byte[] seed)
    {
        var list = source.ToList();
        var prng = new SeedPrng(seed);

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = prng.NextInt(0, i); // [0..i] inclusive
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    /// <summary>Deterministic PRNG using SHA-256(seed || counter).</summary>
    public class SeedPrng
    {
        private readonly byte[] _seed;
        private ulong _counter;
        private byte[] _buffer = [];
        private int _bufferPos;

        public SeedPrng(byte[] seed)
        {
            _seed = seed;
            RefillBuffer();
        }

        private void RefillBuffer()
        {
            // Derive 32 bytes: SHA-256(seed || counter as 8 bytes LE)
            var counterBytes = BitConverter.GetBytes(_counter++);
            var input = new byte[_seed.Length + 8];
            _seed.CopyTo(input, 0);
            counterBytes.CopyTo(input, _seed.Length);
            _buffer = SHA256.HashData(input);
            _bufferPos = 0;
        }

        private int NextByte()
        {
            if (_bufferPos >= _buffer.Length)
                RefillBuffer();
            return _buffer[_bufferPos++];
        }

        /// <summary>Returns a random int in [0, max] inclusive.</summary>
        public int NextInt(int min, int max)
        {
            if (min == max) return min;
            long range = (long)max - min + 1;

            // Rejection sampling to avoid modulo bias
            long limit = (long.MaxValue / range) * range;
            long sample;
            do
            {
                // Read 8 bytes
                long val = 0;
                for (int i = 0; i < 8; i++)
                    val = (val << 8) | (long)NextByte();
                sample = val & long.MaxValue; // make positive
            } while (sample >= limit);

            return (int)(min + (sample % range));
        }
    }
}
