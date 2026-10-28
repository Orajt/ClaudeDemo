using System.Net;

namespace Shop.IntegrationTests;

public sealed class HealthTests(ShopFixture fixture)
{
    [Fact]
    [Trait("AC", "AC-8")]
    [Trait("AC", "AC-19")]
    public async Task Health_ReturnsOk()
    {
        await fixture.Host.Scenario(scenario =>
        {
            scenario.Get.Url("/health");
            scenario.StatusCodeShouldBe(HttpStatusCode.OK);
        });
    }
}
