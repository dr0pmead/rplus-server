#nullable disable
namespace RPlus.SDK.Auth.Enums;

public enum OtpVerifyStatus
{
    Success,
    NotFound,
    Expired,
    Blocked,
    AttemptsExceeded,
    InvalidCode,
    UserBlocked,
    AccountNotFound,
    UserInactive,
    UserNotFound,
}
