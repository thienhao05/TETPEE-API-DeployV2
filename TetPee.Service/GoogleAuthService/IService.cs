using Google.Apis.Auth;

namespace TetPee.Service.GoogleAuthService;

public interface IService
{
    Task<GoogleJsonWebSignature.Payload> VerifyGoogleTokenAsync(Request.GoogleLoginRequest request);
    
    
}