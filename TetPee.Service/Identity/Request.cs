namespace TetPee.Service.Identity;

public class Request
{
    public class CreateUserRequest
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string FirstName { get; set; }
        public required string LastName { get; set; }
    }
    
     
    public class ForgotPasswordRequest
    {
        public required string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public required string Email { get; set; }
        public required int Code { get; set; }
        public required string NewPassword { get; set; }
    }
}