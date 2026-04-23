using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TetPee.Service.GoogleAuthService;

public class Service : IService
{
    private readonly GoogleAuthOptions _options;
    private readonly ILogger<Service> _logger;

    public Service(
        IOptions<GoogleAuthOptions> options,
        ILogger<Service> logger,
        IConfiguration configuration)
    {
        _options = options.Value;
        _logger = logger;
        
        // Bind dữ liệu theo đúng pattern bạn đang dùng
        configuration.GetSection(nameof(GoogleAuthOptions)).Bind(_options);
    }

    public async Task<GoogleJsonWebSignature.Payload> VerifyGoogleTokenAsync(Request.GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            _logger.LogWarning("GoogleAuthOptions: ClientId is empty or not configured.");
            throw new ArgumentNullException(nameof(_options.ClientId), "Google ClientId is missing");
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings()
            {
                // Audience = new List<string>() { _options.ClientId }
                Audience = new List<string>() 
                { 
                    _options.ClientId, 
                    "407408718192.apps.googleusercontent.com" // ID của Google OAuth Playground
                }
            };

            // Tiến hành verify token với Google
            var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
            
            return payload;
        }
        catch (InvalidJwtException ex)
        {
            // Bắt riêng lỗi Token không hợp lệ/hết hạn
            _logger.LogWarning(ex, "Google token validation failed: Invalid or expired token.");
            throw; 
        }
        catch (Exception ex)
        {
            // Bắt các lỗi khác (lỗi mạng, server Google sập...)
            _logger.LogWarning(ex, "An unexpected error occurred while verifying Google token.");
            throw;
        }
    }
}