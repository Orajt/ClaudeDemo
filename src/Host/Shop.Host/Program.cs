using JasperFx;
using Shop.DataHub.Infrastructure;
using Shop.Host.Authentication;
using Shop.Host.Api;
using Shop.Host.Messaging;
using Shop.Orders.Infrastructure;
using Shop.Settlements.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddShopAuthentication();
builder.AddShopControllers();

builder.AddOrdersModule();
builder.AddSettlementsModule();
builder.AddDataHubModule();
builder.AddShopMessaging();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

// Polecenia JasperFx (db-list, db-patch, db-assert, describe…) albo zwykły start aplikacji.
return await app.RunJasperFxCommands(args);

/// <summary>Widoczny dla testów integracyjnych (AlbaHost.For&lt;Program&gt;).</summary>
public partial class Program;
