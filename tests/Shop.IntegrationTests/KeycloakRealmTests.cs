using System.Text.Json;

namespace Shop.IntegrationTests;

/// <summary>Plik importu realmu Keycloak: role, klienci, mappery (audience i płaski claim roles) i użytkownicy deweloperscy.</summary>
public sealed class KeycloakRealmTests
{
    private static readonly string RealmPath = Path.Combine(RepositoryLocator.Root, "src", "Host", "Shop.AppHost", "Realms", "shop-realm.json");
    private static readonly JsonElement Realm = JsonDocument.Parse(File.ReadAllText(RealmPath)).RootElement;

    [Fact]
    [Trait("AC", "AC-11")]
    public void Realm_IsNamedShopAndDefinesRoles()
    {
        Realm.GetProperty("realm").GetString().ShouldBe("shop");

        var roles = Realm.GetProperty("roles").GetProperty("realm").EnumerateArray().Select(role => role.GetProperty("name").GetString());
        roles.ShouldBe(["sales", "accounting", "gateway"], ignoreOrder: true);
    }

    [Fact]
    [Trait("AC", "AC-11")]
    public void Clients_AreConfiguredForApiGuiAndDevCli()
    {
        Client("shop-api").GetProperty("bearerOnly").GetBoolean().ShouldBeTrue();

        var gui = Client("shop-gui");
        gui.GetProperty("publicClient").GetBoolean().ShouldBeFalse();
        gui.GetProperty("standardFlowEnabled").GetBoolean().ShouldBeTrue();

        var devCli = Client("shop-dev-cli");
        devCli.GetProperty("publicClient").GetBoolean().ShouldBeTrue();
        devCli.GetProperty("directAccessGrantsEnabled").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("shop-gui")]
    [InlineData("shop-dev-cli")]
    [Trait("AC", "AC-11")]
    public void TokenClients_MapAudienceAndFlatRolesClaim(string clientId)
    {
        var mappers = Client(clientId).GetProperty("protocolMappers").EnumerateArray().ToList();

        var audience = mappers.Single(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-audience-mapper").GetProperty("config");
        audience.GetProperty("included.custom.audience").GetString().ShouldBe("shop-api");
        audience.GetProperty("access.token.claim").GetString().ShouldBe("true");

        var roles = mappers.Single(mapper => mapper.GetProperty("protocolMapper").GetString() == "oidc-usermodel-realm-role-mapper").GetProperty("config");
        roles.GetProperty("claim.name").GetString().ShouldBe("roles");
        roles.GetProperty("access.token.claim").GetString().ShouldBe("true");
    }

    [Theory]
    [InlineData("anna", "sales")]
    [InlineData("jan", "accounting")]
    [InlineData("demopay", "gateway")]
    [Trait("AC", "AC-11")]
    public void DevelopmentUsers_HaveSingleExpectedRole(string username, string role)
    {
        var user = Realm.GetProperty("users").EnumerateArray().Single(candidate => candidate.GetProperty("username").GetString() == username);

        user.GetProperty("realmRoles").EnumerateArray().Select(value => value.GetString()).ShouldBe([role]);
    }

    private static JsonElement Client(string clientId) =>
        Realm.GetProperty("clients").EnumerateArray().Single(client => client.GetProperty("clientId").GetString() == clientId);
}
