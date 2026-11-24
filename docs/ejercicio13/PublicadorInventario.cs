using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public sealed class PublicadorInventario(IServiceScopeFactory scopes, ILogger<PublicadorInventario> logger)
    : BackgroundService
{
    private const string Exchange = "inventario.eventos";
    private const int MaxIntentos = 5;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = "localhost" };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: ct);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<InventarioDbContext>();
            var pendientes = await db.Outbox
                .Where(m => m.ProcesadoEn == null && !m.Fallido)
                .OrderBy(m => m.OcurridoEn)
                .Take(20)
                .ToListAsync(ct);

            foreach (var m in pendientes)
            {
                try
                {
                    var props = new BasicProperties { Persistent = true, MessageId = m.Id.ToString() };
                    await channel.BasicPublishAsync(
                        Exchange, m.Tipo, mandatory: true, props, Encoding.UTF8.GetBytes(m.Contenido), ct);
                    m.ProcesadoEn = DateTime.UtcNow;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    m.Intentos++;
                    if (m.Intentos >= MaxIntentos)
                    {
                        m.Fallido = true;
                        logger.LogError(ex, "Mensaje {Id} marcado como fallido tras {Intentos} intentos", m.Id, m.Intentos);
                    }
                    else
                        logger.LogWarning(ex, "Fallo al publicar {Id} (intento {Intentos})", m.Id, m.Intentos);
                    break;
                }
            }

            await db.SaveChangesAsync(ct);
        }
    }
}
