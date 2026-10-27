using Microsoft.Extensions.Hosting;

namespace Shop.DataHub.Infrastructure;

public static class DataHubInfrastructureExtensions
{
    public const string SchemaName = "datahub";

    /// <summary>
    /// Rejestracja modułu DataHub. DataHubDbContext (AddDbContextWithWolverineIntegration) dochodzi w etapie 5.
    /// </summary>
    public static IHostApplicationBuilder AddDataHubModule(this IHostApplicationBuilder builder) => builder;
}
