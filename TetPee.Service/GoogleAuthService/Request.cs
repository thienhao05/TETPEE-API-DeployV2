namespace TetPee.Service.GoogleAuthService;

public class Request
{
    public class GoogleLoginRequest
    {
        public string IdToken { get; set; } // Token do Google cấp ở Frontend
    }
}