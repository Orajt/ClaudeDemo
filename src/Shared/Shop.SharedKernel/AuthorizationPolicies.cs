namespace Shop.SharedKernel;

/// <summary>
/// Nazwy polityk autoryzacji. Definiuje je Host, używają kontrolery modułów (<c>[Authorize(Policy = …)]</c>).
/// Stałe są tutaj, bo projekty *.Api nie mogą referencjonować Host.
/// </summary>
public static class AuthorizationPolicies
{
    public const string Sales = "Sales";
    public const string Accounting = "Accounting";
    public const string Gateway = "Gateway";

    /// <summary>Odczyt widoku płatności zamówień – sprzedaż i księgowość (kontrakt: „Role: sales, accounting”).</summary>
    public const string SalesOrAccounting = "SalesOrAccounting";
}
