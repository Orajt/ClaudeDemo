using JasperFx;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Spike.Host;

// Host spike'u służy do poleceń JasperFx: db-list, db-patch, db-assert, describe, codegen.
var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("spike")
    ?? "Host=localhost;Port=5433;Database=spike;Username=postgres;Password=postgres";

SpikeComposition.Configure(builder, connectionString);

using var host = builder.Build();
return await host.RunJasperFxCommands(args);
