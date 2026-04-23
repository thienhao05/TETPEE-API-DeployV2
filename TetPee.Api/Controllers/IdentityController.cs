using Microsoft.AspNetCore.Mvc;
using TetPee.Service.Identity;
using TetPee.Service.Models;
using Request = TetPee.Service.GoogleAuthService.Request;
using GoogleAuthIService = TetPee.Service.GoogleAuthService.IService;
using ForgotPasswordRequest = TetPee.Service.Identity.Request.ForgotPasswordRequest;
using ResetPasswordRequest = TetPee.Service.Identity.Request.ResetPasswordRequest;

namespace TetPee.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class IdentityController : ControllerBase
{
    private readonly IService _identityService;
    private readonly GoogleAuthIService _googleAuthService;

    public IdentityController(IService identityService, GoogleAuthIService googleAuthService)
    {
        _identityService = identityService;
        _googleAuthService = googleAuthService;
    }

    [HttpGet("login")]
    public async Task<IActionResult> Login(string email, string password)
    {
        var result = await _identityService.Login(email, password);
        return Ok(ApiResponseFactory.SuccessResponse(result, "Login successful", HttpContext.TraceIdentifier));
    }

    [HttpPost("google-login")]
    public async Task<IActionResult> GoogleLogin([FromBody] Request.GoogleLoginRequest request)
    {
        // 1. Xác thực xem Token Google có phải hàng thật không
        var googlePayload = await _googleAuthService.VerifyGoogleTokenAsync(request);

        // 2. Đưa payload này cho IdentityService để xử lý DB và sinh ra JWT Token của hệ thống
        // (Lưu ý: Hàm LoginWithGoogleAsync là hàm bạn sẽ cần viết thêm trong IdentityService)
        var myAppToken = await _identityService.LoginWithGoogleAsync(googlePayload);

        // 3. Trả về Token của hệ thống mình cho Frontend xài
        return Ok(ApiResponseFactory.SuccessResponse(myAppToken, "Login successful", HttpContext.TraceIdentifier));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // Gọi service xử lý sinh mã và gửi mail
        await _identityService.ForgotPasswordAsync(request);

        // Luôn trả về HTTP 200 OK dù email có tồn tại hay không để chống hacker dò quét email
        return Ok(ApiResponseFactory.SuccessResponse(null,
            "If an account with this email exists, a verification code has been sent.", HttpContext.TraceIdentifier));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        // Gọi service kiểm tra mã OTP và cập nhật mật khẩu mới
        await _identityService.ResetPasswordAsync(request);

        return Ok(ApiResponseFactory.SuccessResponse(null, "Your password has been changed. Please log in again.",
            HttpContext.TraceIdentifier));
    }
}