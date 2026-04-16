using DuongVanDung.WebApp.Models.Auth;
using Microsoft.Data.SqlClient;

namespace DuongVanDung.WebApp.Services.Auth;

public sealed class CompanyUserService : ICompanyUserService
{
    private readonly string _connStr;
    private readonly IPasswordHashService _hasher;

    public CompanyUserService(IConfiguration cfg, IPasswordHashService hasher)
    {
        _connStr = cfg.GetConnectionString("DefaultConnection")
                   ?? throw new InvalidOperationException("Missing DefaultConnection");
        _hasher = hasher;
    }

    public CompanyUserRecord? ValidateCredentials(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return null;

        try
        {
            using var cn = new SqlConnection(_connStr);
            cn.Open();

            using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
                SELECT Username, DisplayName, PasswordSalt, PasswordHash, Roles
                FROM TaiKhoan
                WHERE Username = @u AND IsActive = 1";
            cmd.Parameters.AddWithValue("@u", username.Trim().ToLower());

            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;

            var user = new CompanyUserRecord
            {
                Username     = r.GetString(0),
                DisplayName  = r.GetString(1),
                PasswordSalt = r.GetString(2),
                PasswordHash = r.GetString(3),
                Roles        = r.GetString(4).Split(',', StringSplitOptions.RemoveEmptyEntries)
            };

            return _hasher.Verify(password, user.PasswordSalt, user.PasswordHash) ? user : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateLastLoginAsync(string username)
    {
        try
        {
            await using var cn = new SqlConnection(_connStr);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "UPDATE TaiKhoan SET NgayDangNhapCuoi = GETDATE() WHERE Username = @u";
            cmd.Parameters.AddWithValue("@u", username.ToLower());
            await cmd.ExecuteNonQueryAsync();
        }
        catch { /* không chặn login nếu update thất bại */ }
    }
}
