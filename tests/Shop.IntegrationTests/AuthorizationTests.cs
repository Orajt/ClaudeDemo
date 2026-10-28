using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Alba;

namespace Shop.IntegrationTests;

public sealed class AuthorizationTests(ShopFixture fixture)
{
    private const string RolesClaim = "roles";

    private static readonly object ValidOrder = new
    {
        customer = new { name = "Anna Test", email = "anna@example.com" },
        lines = new[] { new { sku = "SKU-1", productName = "Produkt", quantity = 1, unitPrice = 10.0 } }
    };

    [Fact]
    [Trait("AC", "AC-6")]
    public async Task PlaceOrder_WithoutToken_Returns401()
    {
        // Klient bez stubu JWT – żadnego nagłówka Authorization.
        using var client = fixture.Host.Server.CreateClient();

        var response = await client.PostAsJsonAsync("/api/orders", ValidOrder, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [Trait("AC", "AC-6")]
    [InlineData("POST", "/api/orders", "accounting")]
    [InlineData("POST", "/api/orders", "gateway")]
    [InlineData("GET", "/api/datahub/order-payments", "gateway")]
    [InlineData("GET", "/api/datahub/settlement-reports", "sales")]
    public async Task ProtectedEndpoint_WithoutRequiredRole_Returns403(string method, string url, string role)
    {
        await CallAsync(method, url, role, expectedStatus: 403);
    }

    [Theory]
    [Trait("AC", "AC-6")]
    [InlineData("POST", "/api/orders", "sales")]
    [InlineData("GET", "/api/datahub/order-payments", "sales")]
    [InlineData("GET", "/api/datahub/order-payments", "accounting")]
    [InlineData("GET", "/api/datahub/settlement-reports", "accounting")]
    public async Task ProtectedEndpoint_WithRequiredRole_ReachesControllerStub(string method, string url, string role)
    {
        // Zaślepka rzuca NotImplementedException → 500 oznacza, że uwierzytelnianie, polityka i routing zadziałały.
        await CallAsync(method, url, role, expectedStatus: 500);
    }

    [Fact]
    [Trait("AC", "AC-7")]
    public async Task ControllerStub_ReturnsProblemJsonWithStageMessage()
    {
        var result = await CallAsync("POST", "/api/orders", "sales", expectedStatus: 500);

        result.Context.Response.ContentType.ShouldStartWith("application/problem+json");
        var body = await result.ReadAsTextAsync();
        body.ShouldContain("NotImplementedException: Etap 3");
        body.ShouldContain("\"code\":\"unexpected-error\"");
    }

    private async Task<IScenarioResult> CallAsync(string method, string url, string role, int expectedStatus)
    {
        return await fixture.Host.Scenario(scenario =>
        {
            scenario.WithClaim(new Claim(RolesClaim, role));
            if (method == "POST")
                scenario.Post.Json(ValidOrder).ToUrl(url);
            else
                scenario.Get.Url(url);

            scenario.StatusCodeShouldBe(expectedStatus);
        });
    }
}
