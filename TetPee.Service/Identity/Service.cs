using System.Security.Claims;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TetPee.Repository;
using TetPee.Service.JwtService;
using TetPee.Service.MailService;
using TetPee.Service.Util;

namespace TetPee.Service.Identity;

public class Service : IService
{
    private readonly JwtService.IService _jwtService;
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _jwtOption = new();
    private readonly ILogger<Service> _logger;
    private readonly MailService.IService _mailService;

    public Service(IConfiguration configuration, JwtService.IService jwtService, AppDbContext dbContext,
        ILogger<Service> logger, MailService.IService mailService)
    {
        _jwtService = jwtService;
        _dbContext = dbContext;
        _logger = logger;
        _mailService = mailService;
        configuration.GetSection(nameof(JwtOptions)).Bind(_jwtOption);
    }

    public async Task<Response.IdentityResponse> Login(string email, string password)
    {
        var user = await _dbContext.Users
            .Include(x => x.Seller)
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            throw new Exception("User not found");
        }

        // Kiểm tra mật khẩu bằng Argon2
        bool isPasswordValid = Argon2Hasher.VerifyHash(password, user.HashedPassword);

        if (!isPasswordValid) //user.HashedPassword != password
        {
            throw new Exception("Invalid password");
        }

        var claims = new List<Claim>
        {
            new Claim("UserId", user.Id.ToString()),
            new Claim("Email", user.Email),
            new Claim("Role", user.Role),
            new Claim(ClaimTypes.Role, user.Role),
            // Phải có claim này để phân quyền cho các API endpoint, nếu thiếu claim này thì sẽ không phân quyền được
            new Claim(ClaimTypes.Expired,
                DateTimeOffset.UtcNow.AddMinutes(_jwtOption.ExpireMinutes).ToString()),
        };

        if (user.Role == "Seller")
        {
            // var seller = await _dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == user.Id);
            // if (seller != null)
            // {
            //     claims.Add(new Claim("SellerId", seller.Id.ToString()));
            // }
            claims.Add(new Claim("SellerId", user.Seller!.Id.ToString()));
        }

        var token = _jwtService.GenerateAccessToken(claims);

        var result = new Response.IdentityResponse()
        {
            AccessToken = token
        };

        return result;
    }
    /*
     * https://developers.google.com/oauthplayground/
     *
     */

    public async Task<string> LoginWithGoogleAsync(GoogleJsonWebSignature.Payload payload)
    {
        // 1. Tìm User (nhớ dùng Include để lấy luôn thông tin Seller nếu có)
        var user = await _dbContext.Users
            .Include(u => u.Seller) // SỬA Ở ĐÂY: Phải có Include thì user.Seller mới không bị null
            .FirstOrDefaultAsync(u => u.Email == payload.Email);

        if (user == null)
        {
            // 2A. CHƯA CÓ: Tạo tài khoản mới
            user = new Repository.Entity.User()
            {
                Email = payload.Email,
                FirstName = payload.GivenName ?? "User",
                LastName = payload.FamilyName ?? "",
                ImageUrl = payload.Picture,
                IsVerify = payload.EmailVerified,
                GoogleId = payload.Subject,
                HashedPassword = null! // Database ko cho rỗng
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created new user from Google Login: {Email}", user.Email);
        }
        else
        {
            // 2B. ĐÃ CÓ: Cập nhật thông tin
            bool isUpdated = false;

            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = payload.Subject;
                isUpdated = true;
            }

            if (!user.IsVerify && payload.EmailVerified)
            {
                user.IsVerify = true;
                isUpdated = true;
            }

            if (isUpdated)
            {
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Updated existing user with Google credentials: {Email}", user.Email);
            }
        }

        // 3. Tạo Token
        var claims = new List<Claim>
        {
            new Claim("UserId", user.Id.ToString()),
            new Claim("Email", user.Email),
            new Claim("Role", user.Role),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(ClaimTypes.Expired, DateTimeOffset.UtcNow.AddMinutes(_jwtOption.ExpireMinutes).ToString()),
        };

        // SỬA Ở ĐÂY: Thêm check user.Seller != null cho an toàn tuyệt đối
        if (user.Role == "Seller" && user.Seller != null)
        {
            claims.Add(new Claim("SellerId", user.Seller.Id.ToString()));
        }

        var token = _jwtService.GenerateAccessToken(claims);

        return token;
    }

    public async Task ForgotPasswordAsync(Request.ForgotPasswordRequest request)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        // Bảo mật: Nếu user không tồn tại, ta vẫn return bình thường để hacker 
        // không dùng API này dò xem email nào đăng ký trên hệ thống.
        if (user == null) return;

        // Nếu là tài khoản Google (không có password thật) thì không cho đổi
        if (user.GoogleId != null && user.HashedPassword == null)
            throw new Exception("Tài khoản này đăng nhập bằng Google, không thể đổi mật khẩu.");

        // Sinh mã 6 số ngẫu nhiên (từ 100000 đến 999999 để đảm bảo luôn có 6 chữ số)
        int resetCode = new Random().Next(100000, 999999);

        // Lưu mã và thời hạn (15 phút) vào DB
        user.VerifyCode = resetCode;
        user.VerifyCodeExpiryTime = DateTimeOffset.UtcNow.AddMinutes(15);
        await _dbContext.SaveChangesAsync();

        // Khởi tạo MailContent với HTML Template
        var mailContent = new MailContent()
        {
            To = user.Email,
            Subject = "Mã xác nhận khôi phục mật khẩu - TetPee",
            Body = $@"
<!DOCTYPE html>
<html>
<head>
  <meta charset='UTF-8'>
  <meta name='viewport' content='width=device-width, initial-scale=1.0'>
</head>
<body style='margin:0; padding:0; font-family: Arial, sans-serif; background-color:#f4f6f8;'>
  
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f4f6f8; padding: 20px 0;'>
    <tr>
      <td align='center'>
        
        <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff; border-radius:10px; overflow:hidden; box-shadow:0 4px 10px rgba(0,0,0,0.05);'>
          
          <tr>
            <td style='background: linear-gradient(90deg, #FF9800, #F57C00); padding: 20px; text-align:center; color:white;'>
              <h1 style='margin:0;'>Khôi Phục Mật Khẩu 🔐</h1>
            </td>
          </tr>

          <tr>
            <td style='padding: 30px; color:#333; line-height:1.6;'>
              
              <h2 style='margin-top:0;'>Xin chào {user.FirstName}, 👋</h2>
              
              <p>
                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản <strong>TetPee</strong> của bạn.
              </p>

              <p>
                Dưới đây là mã xác thực (OTP) của bạn. Mã này sẽ <strong>hết hạn sau 15 phút</strong>:
              </p>

              <div style='text-align:center; margin: 30px 0;'>
                <span style='background:#FFF3E0; color:#F57C00; padding:15px 30px; border-radius:8px; font-size:32px; font-weight:bold; letter-spacing: 5px; border: 2px dashed #FF9800; display:inline-block;'>
                  {resetCode}
                </span>
              </div>

              <p style='color: #D32F2F; font-size: 14px;'>
                <em>Tuyệt đối không chia sẻ mã này cho bất kỳ ai. Nếu bạn không yêu cầu đổi mật khẩu, vui lòng bỏ qua email này để bảo vệ tài khoản.</em>
              </p>

              <p>
                Trân trọng,<br/>
                <strong>Đội ngũ TetPee</strong>
              </p>
            </td>
          </tr>

          <tr>
            <td style='background:#f1f1f1; padding:15px; text-align:center; font-size:12px; color:#777;'>
              © {DateTime.Now.Year} TetPee. All rights reserved.
            </td>
          </tr>

        </table>

      </td>
    </tr>
  </table>

</body>
</html>"
        };

        // Gọi hàm SendMail truyền nguyên object vào
        await _mailService.SendMail(mailContent);
        _logger.LogInformation("Sent password reset code to {Email}", user.Email);
    }

    public async Task ResetPasswordAsync(Request.ResetPasswordRequest request)
    {
        // 1. Tìm user trong Database dựa vào Email
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
            throw new Exception("Tài khoản không tồn tại.");

        // 2. KIỂM TRA BẢO MẬT KÉP (Mã khớp + Còn hạn)
        // DateTimeOffset.UtcNow lấy thời gian chuẩn quốc tế hiện tại để so sánh
        if (user.VerifyCode != request.Code || 
            user.VerifyCodeExpiryTime == null || 
            user.VerifyCodeExpiryTime < DateTimeOffset.UtcNow)
        {
            // Ném lỗi chung chung để hacker không biết là do sai mã hay do hết hạn
            throw new Exception("Mã xác thực không hợp lệ hoặc đã hết hạn.");
        }

        // 3. Băm mật khẩu mới bằng Utils (Argon2) của bạn
        // Lưu ý: Nếu Utils của bạn nằm ở namespace khác, nhớ using ở đầu file
        user.HashedPassword = Argon2Hasher.HashPassword(request.NewPassword);

        // 4. TIÊU HỦY MÃ OTP (Cực kỳ quan trọng)
        // Phải set về null để user (hoặc hacker) không thể dùng lại mã này lần 2
        user.VerifyCode = null;
        user.VerifyCodeExpiryTime = null;

        // 5. Lưu vào Database
        await _dbContext.SaveChangesAsync();
    
        _logger.LogInformation("User {Email} successfully reset their password.", user.Email);
    }
}