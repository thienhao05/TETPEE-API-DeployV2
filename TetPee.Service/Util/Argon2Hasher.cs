using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace TetPee.Service.Util;

public static class Argon2Hasher
{
    // Cấu hình tham số độ khó (Có thể tinh chỉnh tùy cấu hình Server của bạn)
    private const int DegreeOfParallelism = 8; // Số luồng CPU sử dụng
    private const int MemorySize = 1024 * 128; // RAM sử dụng (128 MB)
    private const int Iterations = 4;          // Số vòng lặp

    public static string HashPassword(string password)
    {
        // 1. Tạo chuỗi Salt ngẫu nhiên (16 byte)
        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
        }

        // 2. Tiến hành băm mật khẩu bằng thuật toán Argon2id
        using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
        {
            argon2.Salt = salt;
            argon2.DegreeOfParallelism = DegreeOfParallelism;
            argon2.Iterations = Iterations;
            argon2.MemorySize = MemorySize;

            byte[] hash = argon2.GetBytes(16);

            // 3. Nối Salt và Hash lại bằng dấu ":" để lưu vào 1 cột duy nhất
            // Format: chuỗi_Base64(Salt) : chuỗi_Base64(Hash)
            return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
        }
    }

    public static bool VerifyHash(string password, string hashedPasswordFromDb)
    {
        try
        {
            // 1. Tách Salt và Hash từ chuỗi lưu trong Database
            var parts = hashedPasswordFromDb.Split(':');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] expectedHash = Convert.FromBase64String(parts[1]);

            // 2. Băm mật khẩu người dùng vừa nhập với Salt cũ lấy từ DB
            using (var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password)))
            {
                argon2.Salt = salt;
                argon2.DegreeOfParallelism = DegreeOfParallelism;
                argon2.Iterations = Iterations;
                argon2.MemorySize = MemorySize;

                byte[] actualHash = argon2.GetBytes(16);

                // 3. So sánh 2 chuỗi Hash một cách an toàn (chống Timing Attacks)
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
        }
        catch
        {
            return false; // Nếu chuỗi sai format hoặc có lỗi thì trả về false
        }
    }
}