namespace Gateway.API.Auth;

public class FixedUserOptions
{
    public const string SectionName = "FixedUser";

    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
