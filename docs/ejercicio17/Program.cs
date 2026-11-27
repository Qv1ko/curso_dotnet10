
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

builder.Services.AddScoped<StockService>();
builder.Services.AddHostedService<BookOutboxProcessor>();

var app = builder.Build();

app.MapPost("/books/{id:int}/reservations",
    async (int id, int quantity, StockService service, CancellationToken ct) => {
        try {
            var result = await service.ReserveAsync(id, quantity, ct);
            return result ? Results.NoContent() : Results.Conflict();
        } catch (ArgumentOutOfRangeException) {
            return Results.BadRequest();
        }
    });

app.Run();
