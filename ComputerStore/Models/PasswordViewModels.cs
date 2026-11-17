using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ComputerStore.Models;

/// <summary>
/// Quy tắc độ mạnh mật khẩu DÙNG CHUNG cho Đổi mật khẩu và Quên/Đặt lại mật khẩu.
/// Quy tắc: tối thiểu 8 ký tự, ít nhất 1 chữ cái và ít nhất 1 chữ số.
///
/// LƯU Ý VỀ RegisterVm (Models/ViewModels.cs): RegisterVm hiện chỉ yêu cầu
/// "tối thiểu 6 ký tự" (StringLength MinimumLength = 6), chưa có yêu cầu chữ + số.
/// File ViewModels.cs không thuộc phạm vi sửa của hạng mục này nên chưa thống nhất
/// được trong code — điều phối viên cần sửa RegisterVm.Password thành
/// MinimumLength = 8 + RegularExpression PasswordRules.Pattern (xem FIX_LOG_2_PASSWORD.md)
/// để đăng ký mới cũng dùng đúng 1 quy tắc này.
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;

    /// <summary>Ít nhất 1 chữ cái (A-Z/a-z) và ít nhất 1 chữ số, độ dài do MinLength quyết định.</summary>
    public const string Pattern = @"^(?=.*[A-Za-z])(?=.*\d).+$";

    public const string ErrorMessage = "Mật khẩu tối thiểu 8 ký tự, gồm ít nhất 1 chữ cái và 1 chữ số";

    private static readonly Regex StrongPattern =
        new(@"^(?=.*[A-Za-z])(?=.*\d).+$", RegexOptions.Compiled);

    /// <summary>Hàm pure kiểm tra độ mạnh — dùng ở controller (phòng thủ thêm ngoài DataAnnotations) và unit test.</summary>
    public static bool IsStrong(string password) =>
        !string.IsNullOrEmpty(password)
        && password.Length >= MinLength
        && StrongPattern.IsMatch(password);
}

public class ChangePasswordVm
{
    [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = PasswordRules.MinLength, ErrorMessage = PasswordRules.ErrorMessage)]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.ErrorMessage)]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; }

    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; }
}

public class ForgotPasswordVm
{
    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [Display(Name = "Email đã đăng ký")]
    public string Email { get; set; }
}

public class ResetPasswordVm
{
    [Required]
    public string Token { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
    [StringLength(100, MinimumLength = PasswordRules.MinLength, ErrorMessage = PasswordRules.ErrorMessage)]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.ErrorMessage)]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; }

    [Compare("NewPassword", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu mới")]
    public string ConfirmPassword { get; set; }
}
