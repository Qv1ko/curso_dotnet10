
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;

public record OutboxRow(Guid Id, string Payload);
public record BookRow(int Id, string Title, int Stock);

public class BookAvailabilityDocument {
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Stock { get; set; }
    public bool Available { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookOutboxProcessor(IConfiguration configuration, IMongoClient mongo, IDistributedCache cache, ILogger<BookOutboxProcessor> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var collection = mongo.GetDatabase("bookstore")
            .GetCollection<BookAvailabilityDocument>("BookAvailability");

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await using var db = new SqlConnection(configuration.GetConnectionString("Bookstore"));

                var messages = await db.QueryAsync<OutboxRow>(new CommandDefinition(
                    @"SELECT TOP (50) Id, Payload FROM OutboxMessages
                      WHERE Type = 'BookStockChanged' AND ProcessedAt IS NULL
                      ORDER BY OccurredAt, Id",
                    cancellationToken: stoppingToken));

                foreach (var message in messages) {
                    try {
                        var evt = JsonSerializer.Deserialize<BookStockChanged>(message.Payload)!;

                        await using var sql = new SqlConnection(configuration.GetConnectionString("Bookstore"));

                        var book = await sql.QuerySingleOrDefaultAsync<BookRow>(
                            new CommandDefinition(
                                "SELECT Id, Title, Stock FROM Books WHERE Id = @Id",
                                new { Id = evt.BookId },
                                cancellationToken: stoppingToken));

                        if (book == null)
                            await collection.DeleteOneAsync(x => x.Id == evt.BookId, stoppingToken);
                        else
                            await collection.ReplaceOneAsync(x => x.Id == book.Id,
                                new BookAvailabilityDocument
                                {
                                    Id = book.Id,
                                    Title = book.Title,
                                    Stock = book.Stock,
                                    Available = book.Stock > 0,
                                    UpdatedAt = DateTime.UtcNow
                                },
                                new ReplaceOptions { IsUpsert = true }, stoppingToken);

                        await cache.RemoveAsync($"book:{evt.BookId}", stoppingToken);

                        await db.ExecuteAsync(new CommandDefinition(
                            "UPDATE OutboxMessages SET ProcessedAt = SYSUTCDATETIME() WHERE Id = @Id",
                            new { message.Id }, cancellationToken: stoppingToken));
                    } catch (Exception ex) when (!stoppingToken.IsCancellationRequested) {
                        logger.LogError(ex, "Error procesando mensaje {Id}", message.Id);
                    }
                }
            } catch (Exception ex) when (!stoppingToken.IsCancellationRequested) {
                logger.LogError(ex, "Error consultando Outbox");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
