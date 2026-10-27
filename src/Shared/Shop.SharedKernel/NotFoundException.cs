namespace Shop.SharedKernel;

/// <summary>
/// Brak zasobu (HTTP 404). Kod: <c>&lt;moduł&gt;.not-found</c>, np. <c>datahub.not-found</c>.
/// </summary>
public sealed class NotFoundException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
