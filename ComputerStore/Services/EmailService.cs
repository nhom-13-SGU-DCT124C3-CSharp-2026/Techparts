using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Hosting;

namespace ComputerStore.Services;

/// <summary>Gửi mail đặt lại mật khẩu. Chỉ gửi link reset — không bao giờ gửi mật khẩu.</summary>
public interface IEmailService
{
    Task SendPasswordResetAsync(string email, string resetLink);
}

/// <summary>
/// Gửi mail thật qua SMTP. Đọc cấu hình section "Smtp" trong appsettings.json:
/// Host, Port, EnableSsl, Username, Password, From.
/// Nếu Host trống thì ném InvalidOperationException — cấu hình sai phải báo to,
/// không được lặng lẽ bỏ qua việc gửi mail.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(string email, string resetLink)
    {
        var section = _config.GetSection("Smtp");
        var host = section["Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException(
                "Chưa cấu hình SMTP (section \"Smtp\" trong appsettings.json) nên không thể gửi mail đặt lại mật khẩu.");

        var port = section.GetValue<int>("Port", 587);
        var enableSsl = section.GetValue<bool>("EnableSsl", true);
        var username = section["Username"];
        var password = section["Password"];
        var from = section["From"];
        if (string.IsNullOrWhiteSpace(from)) from = username;

        using var message = new MailMessage(from, email,
            "Đặt lại mật khẩu TechParts",
            "Bạn (hoặc ai đó) đã yêu cầu đặt lại mật khẩu cho tài khoản TechParts.\n\n" +
            "Bấm vào link sau để đặt mật khẩu mới (link chỉ dùng được 1 lần và hết hạn sau 1 giờ):\n" +
            resetLink + "\n\n" +
            "Nếu bạn không yêu cầu, hãy bỏ qua mail này — mật khẩu của bạn không thay đổi.");
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = enableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };
        if (!string.IsNullOrWhiteSpace(username))
            client.Credentials = new NetworkCredential(username, password);

        await client.SendMailAsync(message);
        _logger.LogInformation("Đã gửi mail đặt lại mật khẩu tới {Email}.", email);
    }
}

/// <summary>
/// "Gửi mail" cho môi trường Development khi chưa cấu hình SMTP:
/// chỉ ghi link reset vào log (ILogger), không gửi mail thật, không hiện link ra màn hình.
/// TUYỆT ĐỐI không dùng ở Production.
/// </summary>
public class DevLogEmailService : IEmailService
{
    private readonly ILogger<DevLogEmailService> _logger;

    public DevLogEmailService(ILogger<DevLogEmailService> logger) => _logger = logger;

    public Task SendPasswordResetAsync(string email, string resetLink)
    {
        _logger.LogWarning(
            "SMTP chưa cấu hình (chỉ dùng ở Development): link đặt lại mật khẩu cho {Email}: {ResetLink}",
            email, resetLink);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Chọn triển khai IEmailService theo cấu hình + môi trường:
/// - Có Smtp:Host trong cấu hình → SmtpEmailService (gửi mail thật).
/// - Chưa cấu hình + môi trường Development → DevLogEmailService (chỉ ghi log).
/// - Chưa cấu hình + môi trường khác (Production...) → ném lỗi, không lặng lẽ ghi log link.
/// Xem dòng đăng ký DI trong FIX_LOG_2_PASSWORD.md (Program.cs không sửa ở hạng mục này).
/// </summary>
public static class EmailServiceFactory
{
    public static IEmailService Create(IConfiguration config, IWebHostEnvironment env, ILoggerFactory loggers)
    {
        if (!string.IsNullOrWhiteSpace(config["Smtp:Host"]))
            return new SmtpEmailService(config, loggers.CreateLogger<SmtpEmailService>());
        if (env.IsDevelopment())
            return new DevLogEmailService(loggers.CreateLogger<DevLogEmailService>());
        throw new InvalidOperationException(
            "Chưa cấu hình SMTP (section \"Smtp\" trong appsettings.json) nên không thể gửi mail đặt lại mật khẩu ở môi trường Production.");
    }
}
