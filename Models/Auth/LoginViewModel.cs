using System.ComponentModel.DataAnnotations;

namespace DuongVanDung.WebApp.Models.Auth;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Tên đăng nhập là bắt buộc.")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
