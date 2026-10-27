using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Spike.DataHub;
using Spike.Orders;
using Wolverine.Tracking;

namespace Spike.Tests;

internal static class TestHelpers
{
    private static readonly TimeSpan TrackingTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Wywołuje komendę i czeka, aż cała kaskada wiadomości (także w innych modułach) zostanie obsłużona (H6).</summary>
    public static Task<ITrackedSession> InvokeAndWaitAsync(this IHost host, object command) =>
        host.TrackActivity().Timeout(TrackingTimeout).InvokeMessageAndWaitAsync(command);

    public static TrackedSessionConfiguration TrackWithTimeout(this IHost host) =>
        host.TrackActivity().Timeout(TrackingTimeout);

    public static async Task<Guid> PlaceAndPayOrderAsync(this IHost host, string customerName, decimal totalAmount)
    {
        var orderId = Guid.CreateVersion7();
        await host.InvokeAndWaitAsync(new PlaceOrder(orderId, customerName, totalAmount));
        await host.InvokeAndWaitAsync(new ConfirmPayment(orderId, $"TX-{orderId:N}", Version: 1));
        return orderId;
    }

    public static async Task<OrderPaymentRow?> FindOrderPaymentRowAsync(this IHost host, Guid orderId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataHubDbContext>();
        return await db.OrderPayments.AsNoTracking().SingleOrDefaultAsync(x => x.OrderId == orderId);
    }

    public static async Task<T?> ScalarAsync<T>(this NpgsqlDataSource db, string sql, params object[] parameters)
    {
        await using var command = CreateCommand(db, sql, parameters);
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)result;
    }

    public static async Task<List<T>> ListAsync<T>(this NpgsqlDataSource db, string sql, params object[] parameters)
    {
        await using var command = CreateCommand(db, sql, parameters);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<T>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetFieldValue<T>(0));
        }

        return values;
    }

    private static NpgsqlCommand CreateCommand(NpgsqlDataSource db, string sql, object[] parameters)
    {
        var command = db.CreateCommand(sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(new NpgsqlParameter { Value = parameter });
        }

        return command;
    }
}
