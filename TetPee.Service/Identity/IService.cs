using Google.Apis.Auth;

namespace TetPee.Service.Identity;

public interface IService
{
    public Task<Response.IdentityResponse> Login(string email, string password);
    
    Task<string> LoginWithGoogleAsync(GoogleJsonWebSignature.Payload payload);
    
    Task ForgotPasswordAsync(Request.ForgotPasswordRequest request);
    Task ResetPasswordAsync(Request.ResetPasswordRequest request);
}