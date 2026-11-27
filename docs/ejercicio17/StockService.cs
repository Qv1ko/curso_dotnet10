
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;

public record BookStockChanged(int BookId);

public class StockService(IConfiguration configuration) {
    public async Task<bool> ReserveAsync(int bookId, int quantity, CancellationToken ct) {
        if (quantity < 1 || quantity > 10)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        await using var db = new SqlConnection(configuration.GetConnectionString("Bookstore"));
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);

        var rows = await db.ExecuteAsync(new CommandDefinition(
            "UPDATE Books SET Stock = Stock - @quantity WHERE Id = @bookId AND Stock >= @quantity",
            new { bookId, quantity }, tx, cancellationToken: ct));

        if (rows == 0) {
            await tx.RollbackAsync(ct);
            return false;
        }

        var payload = JsonSerializer.Serialize(new BookStockChanged(bookId));

        await db.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO OutboxMessages (Id, Type, Payload, OccurredAt)
              VALUES (@Id, 'BookStockChanged', @Payload, SYSUTCDATETIME())",
            new { Id = Guid.NewGuid(), Payload = payload }, tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);
        return true;
    }
}
