namespace Shop.SharedKernel;

/// <summary>
/// Próba ponownego utworzenia czegoś, co już istnieje (HTTP 409), np. ponowny import tego samego raportu.
/// </summary>
public sealed class DuplicateException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
