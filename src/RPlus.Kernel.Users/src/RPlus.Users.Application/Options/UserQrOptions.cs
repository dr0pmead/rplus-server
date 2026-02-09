namespace RPlus.Users.Application.Options;

public sealed class UserQrOptions
{
    public int TokenBytes { get; set; } = 16;
    public int TtlSeconds { get; set; } = 30;
}
