namespace Shop.SharedKernel;

/// <summary>
/// Naruszenie reguły biznesowej (HTTP 422). Kod: <c>&lt;moduł&gt;.&lt;reguła-kebab-case&gt;</c>,
/// np. <c>orders.payment-amount-mismatch</c>. Komunikat po polsku.
/// </summary>
public sealed class BusinessRuleViolationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
