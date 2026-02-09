using RPlus.Kernel.Integration.Application.Services;

namespace RPlus.Kernel.Integration.Api.Services;

public interface IQrTokenValidator
{
    QrTokenValidationResult Validate(string token);
}

public sealed record QrTokenValidationResult(bool Success, Guid UserId, string Error);

public sealed class QrTokenValidator : IQrTokenValidator
{
    private readonly IQrTokenStore _store;

    public QrTokenValidator(IQrTokenStore store)
    {
        _store = store;
    }

    public QrTokenValidationResult Validate(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new QrTokenValidationResult(false, Guid.Empty, "missing_qr_token");

        var userId = _store.TryGetUserId(token);
        if (!userId.HasValue || userId.Value == Guid.Empty)
            return new QrTokenValidationResult(false, Guid.Empty, "qr_token_expired");

        return new QrTokenValidationResult(true, userId.Value, string.Empty);
    }
}
