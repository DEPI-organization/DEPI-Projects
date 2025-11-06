public class AuthResponseDto
{
    public string Token { get; set; }
    public string ResfreshToken {  get; set; }
    public UserResponseDto User { get; set; }
    public DateTime ExpiresAt { get; set; }
}