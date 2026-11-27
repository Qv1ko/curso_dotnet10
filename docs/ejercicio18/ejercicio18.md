# Reservas de stock con Outbox y proyección en MongoDB para Zurbalia

## Tarea 1

```csharp
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

public class StockService(IConfiguration config)
{
    public async Task<bool> ReserveAsync(int bookId, int quantity, CancellationToken ct)
    {
        if (quantity is < 1 or > 10)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La cantidad debe estar entre 1 y 10.");

        await using var connection = new SqlConnection(config.GetConnectionString("Bookstore"));
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);

        var updated = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Books SET Stock = Stock - @quantity WHERE Id = @bookId AND Stock >= @quantity",
            new { bookId, quantity }, tx, cancellationToken: ct));
        if (updated == 0) return false;   // nada que confirmar: la transacción se revierte al liberarse

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO OutboxMessages (Id, Type, Payload, OccurredAt) VALUES (@Id, @Type, @Payload, @OccurredAt)",
            new
            {
                Id = Guid.NewGuid(),
                Type = nameof(BookStockChanged),
                Payload = JsonSerializer.Serialize(new BookStockChanged(bookId)),
                OccurredAt = DateTime.UtcNow
            },
            tx, cancellationToken: ct));
        await tx.CommitAsync(ct);
        return true;
    }
}
```

- La cantidad se valida **antes** de abrir la conexión, así que 0 u 11 lanzan `ArgumentOutOfRangeException` sin tocar la base de datos.
- El `UPDATE ... WHERE Stock >= @quantity` descuenta y comprueba el stock en una sola sentencia atómica, sin lectura previa, por lo que dos reservas simultáneas no pueden dejar el stock negativo.
- Mensaje Outbox y descuento van en la **misma transacción**: o se confirman los dos o ninguno.

| Llamada                  | Resultado                                                                      |
| ------------------------ | ------------------------------------------------------------------------------ |
| `(101, 2)`               | `true`, `Stock` pasa de 3 a 1 y se inserta un mensaje                          |
| `(102, 1)`               | `false`: stock 0, el `UPDATE` no afecta a ninguna fila y no se inserta mensaje |
| `(999, 1)`               | `false`: el libro no existe, mismo efecto                                      |
| `(101, 0)` o `(101, 11)` | `ArgumentOutOfRangeException` antes de conectar                                |

## Tareas 2 y 3

```csharp
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;

public class BookOutboxProcessor(
    IConfiguration config, IMongoClient mongo, IDistributedCache cache, ILogger<BookOutboxProcessor> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var availability = mongo.GetDatabase("bookstore").GetCollection<BookAvailabilityDocument>("BookAvailability");
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await using var db = new SqlConnection(config.GetConnectionString("Bookstore"));
                var pending = await db.QueryAsync<OutboxRow>(new CommandDefinition(
                    "SELECT TOP (50) Id, Payload FROM OutboxMessages " +
                    "WHERE ProcessedAt IS NULL AND Type = 'BookStockChanged' ORDER BY OccurredAt",
                    cancellationToken: ct));

                foreach (var message in pending)
                {
                    try { await ProcessAsync(db, availability, message, ct); }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Error procesando el mensaje {Id}", message.Id);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error leyendo la bandeja de salida");
            }
        }
    }

    private async Task ProcessAsync(
        SqlConnection db, IMongoCollection<BookAvailabilityDocument> availability, OutboxRow message, CancellationToken ct)
    {
        var bookId = JsonSerializer.Deserialize<BookStockChanged>(message.Payload)!.BookId;
        var book = await db.QuerySingleOrDefaultAsync<BookRow>(new CommandDefinition(
            "SELECT Id, Title, Stock FROM Books WHERE Id = @bookId", new { bookId }, cancellationToken: ct));

        if (book is null)
            await availability.DeleteOneAsync(d => d.Id == bookId, ct);
        else
            await availability.ReplaceOneAsync(d => d.Id == bookId, new BookAvailabilityDocument
            {
                Id = book.Id, Title = book.Title, Stock = book.Stock,
                Available = book.Stock > 0, UpdatedAt = DateTime.UtcNow
            }, new ReplaceOptions { IsUpsert = true }, ct);

        await cache.RemoveAsync($"book:{bookId}", ct);
        await db.ExecuteAsync(new CommandDefinition(
            "UPDATE OutboxMessages SET ProcessedAt = @now WHERE Id = @id",
            new { now = DateTime.UtcNow, id = message.Id }, cancellationToken: ct));
    }
}
```

- **Ciclo:** un `PeriodicTimer` de 10 s; cada vuelta lee los 50 mensajes `BookStockChanged` pendientes más antiguos (`ORDER BY OccurredAt`).
- **Estado, no evento:** por cada mensaje se relee el libro actual en SQL Server, de modo que la proyección refleja siempre el último valor aunque haya varios mensajes del mismo libro.
- **MongoDB sin duplicados:** `ReplaceOneAsync` con `IsUpsert = true` por `Id` (que el driver mapea a `_id`). Si el libro ya no existe, se borra el documento.
- **Redis:** se elimina la clave `book:{id}` para que la próxima lectura recargue la ficha.
- **Resiliencia:** hay un `try/catch` por mensaje (un fallo no bloquea al resto, y el mensaje que falla se reintenta en la vuelta siguiente) y otro por vuelta, para fallos al leer la bandeja. Solo la cancelación (`OperationCanceledException`) escapa, para que el servicio se pueda detener.
- **Detención tras MongoDB y antes de marcar (tarea 3):** el mensaje queda sin procesar y se repite tras el reinicio. El resultado final es el mismo, porque el upsert y el borrado en Redis son idempotentes.

## Tarea 4

```csharp
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStackExchangeRedisCache(o =>
    o.Configuration = builder.Configuration.GetConnectionString("Redis"));
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));
builder.Services.AddSingleton<StockService>();
builder.Services.AddHostedService<BookOutboxProcessor>();

var app = builder.Build();

app.MapPost("/books/{id:int}/reservations", async (int id, int quantity, StockService stock, CancellationToken ct) =>
{
    try
    {
        return await stock.ReserveAsync(id, quantity, ct) ? Results.NoContent() : Results.Conflict();
    }
    catch (ArgumentOutOfRangeException)
    {
        return Results.BadRequest("La cantidad debe estar entre 1 y 10.");
    }
});

app.Run();
```

**Ciclos de vida:**

- `IMongoClient`, **singleton**: es seguro entre hilos y está pensado para compartirse, porque cada instancia gestiona su propio pool de conexiones.
- Redis (`AddStackExchangeRedisCache`): el framework lo registra como singleton.
- `StockService`, **singleton**: no guarda estado, y abre y libera una conexión SQL en cada llamada.
- `BookOutboxProcessor`: `AddHostedService` lo registra como singleton.

**Respuestas del endpoint:** 204 si reserva, 409 si no hay stock o el libro no existe, 400 si la cantidad no está entre 1 y 10. Si falta `quantity` en la URL, el propio binding de ASP.NET Core devuelve 400.

## Comprobación de líneas

| Archivo                                 | Máximo |
| --------------------------------------- | ------ |
| `Ejercicio_Outbox_Stock_ApeNombre.cs`   | 40     |
| `Ejercicio_Outbox_Worker_ApeNombre.cs`  | 60     |
| `Ejercicio_Outbox_Program_ApeNombre.cs` | 30     |
