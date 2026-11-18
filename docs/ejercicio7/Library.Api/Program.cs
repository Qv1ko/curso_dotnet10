using Library.Application;
using Library.Domain;
using Library.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Library")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Library'.");

builder.Services.AddOpenApi();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(connectionString);

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapPost("/api/loans", async (LendBookHttpRequest body, ILendBookUseCase useCase, CancellationToken ct) => {
    if (body.Days is < 1 or > 30)
        return Results.ValidationProblem(
            new Dictionary<string, string[]> { ["Days"] = ["Days debe estar entre 1 y 30."] });
    try {
        var result = await useCase.ExecuteAsync(new LendBookRequest(body.BookId, body.MemberId, body.Days), ct);
        return Results.Created($"/api/loans/{result.LoanId}", result);
    } catch (LoanLimitExceededException ex) {
        return Results.Conflict(ex.Message);
    }
});

app.Run();

public sealed record LendBookHttpRequest(Guid BookId, Guid MemberId, int Days);
