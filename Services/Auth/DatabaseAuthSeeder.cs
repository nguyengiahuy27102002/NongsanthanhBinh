using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace DuongVanDung.WebApp.Services.Auth;

/// <summary>
/// Chạy một lần lúc khởi động: tạo bảng TaiKhoan nếu chưa có và seed tài khoản mặc định.
/// </summary>
public sealed class DatabaseAuthSeeder
{
    private readonly string _connStr;
    private readonly IPasswordHashService _hasher;

    public DatabaseAuthSeeder(IConfiguration cfg, IPasswordHashService hasher)
    {
        _connStr = cfg.GetConnectionString("DefaultConnection")
                   ?? throw new InvalidOperationException("Missing DefaultConnection");
        _hasher = hasher;
    }

    public async Task SeedAsync()
    {
        await using var cn = new SqlConnection(_connStr);
        await cn.OpenAsync();

        // ── Tạo bảng nếu chưa có ────────────────────────────────────────────
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
                               WHERE TABLE_NAME = 'TaiKhoan')
                BEGIN
                    CREATE TABLE TaiKhoan (
                        Id            INT IDENTITY(1,1) PRIMARY KEY,
                        Username      NVARCHAR(50)  NOT NULL UNIQUE,
                        DisplayName   NVARCHAR(100) NOT NULL,
                        PasswordSalt  NVARCHAR(128) NOT NULL,
                        PasswordHash  NVARCHAR(256) NOT NULL,
                        Roles         NVARCHAR(200) NOT NULL DEFAULT N'Staff',
                        IsActive      BIT           NOT NULL DEFAULT 1,
                        NgayTao       DATETIME      NOT NULL DEFAULT GETDATE(),
                        NgayDangNhapCuoi DATETIME   NULL
                    )
                END";
            await cmd.ExecuteNonQueryAsync();
        }

        // ── Kiểm tra bảng đã có user chưa ───────────────────────────────────
        await using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM TaiKhoan";
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);
            if (count > 0) return; // Đã có dữ liệu, không seed lại
        }

        // ── Danh sách tài khoản mặc định ─────────────────────────────────────
        var accounts = new[]
        {
            (Username: "admin",  DisplayName: "Quản trị hệ thống", Password: "Admin@2026",  Roles: "Admin,Manager"),
            (Username: "viet",   DisplayName: "Việt",               Password: "Viet@2026",   Roles: "Manager"),
            (Username: "hien",   DisplayName: "Hiền",               Password: "Hien@2026",   Roles: "Staff"),
            (Username: "trang",  DisplayName: "Trang",              Password: "Trang@2026",  Roles: "Staff"),
            (Username: "duyen",  DisplayName: "Duyên",              Password: "Duyen@2026",  Roles: "Staff"),
        };

        foreach (var (username, displayName, password, roles) in accounts)
        {
            var (salt, hash) = GenerateHash(password);

            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO TaiKhoan (Username, DisplayName, PasswordSalt, PasswordHash, Roles)
                VALUES (@u, @d, @s, @h, @r)";
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@d", displayName);
            cmd.Parameters.AddWithValue("@s", salt);
            cmd.Parameters.AddWithValue("@h", hash);
            cmd.Parameters.AddWithValue("@r", roles);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static (string salt, string hash) GenerateHash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password, saltBytes, 100_000, HashAlgorithmName.SHA256, 32);
        return (Convert.ToBase64String(saltBytes), Convert.ToBase64String(hashBytes));
    }
}
