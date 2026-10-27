using Microsoft.AspNetCore.Authentication.JwtBearer;
using Shop.SharedKernel;

namespace Shop.Host.Authentication;

public static class AuthenticationSetup
{
    /// <summary>Nazwa zasobu Keycloak w AppHost – adres rozwiązuje service discovery Aspire.</summary>
    private const string KeycloakServiceName = "keycloak";
    private const string Realm = "shop";
    private const string Audience = "shop-api";

    /// <summary>Role realmu trafiają do tokenu jako płaski claim "roles" (mapper w shop-realm.json).</summary>
    private const string RolesClaim = "roles";

    public static WebApplicationBuilder AddShopAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddKeycloakJwtBearer(KeycloakServiceName, Realm, options =>
            {
                options.Audience = Audience;
                // Lokalny Keycloak działa po HTTP – tylko w Development.
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                // Zostawiamy nazwy claimów z tokenu ("roles"), bez mapowania na typy Microsoft.
                options.MapInboundClaims = false;
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.Sales, policy => policy.RequireClaim(RolesClaim, Roles.Sales))
            .AddPolicy(AuthorizationPolicies.Accounting, policy => policy.RequireClaim(RolesClaim, Roles.Accounting))
            .AddPolicy(AuthorizationPolicies.Gateway, policy => policy.RequireClaim(RolesClaim, Roles.Gateway))
            .AddPolicy(AuthorizationPolicies.SalesOrAccounting, policy => policy.RequireClaim(RolesClaim, Roles.Sales, Roles.Accounting));

        return builder;
    }

    /// <summary>Role realmu Keycloak (docs/architecture.md §8).</summary>
    private static class Roles
    {
        public const string Sales = "sales";
        public const string Accounting = "accounting";
        public const string Gateway = "gateway";
    }
}
