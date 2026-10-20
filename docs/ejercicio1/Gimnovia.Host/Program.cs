using Gimnovia.Modules.Bookings;
using Gimnovia.Modules.Bookings.Contracts;
using Gimnovia.Modules.Members;

var builder = WebApplication.CreateBuilder(args);
var cs = builder.Configuration.GetConnectionString("Gimnovia")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'Gimnovia'.");

builder.Services.AddOpenApi();
builder.Services.AddMembersModule(cs);
builder.Services.AddBookingsModule(cs);

var app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.MapPost("/bookings", async (BookClassRequest request, IBookingService service) =>
{
    var result = await service.BookAsync(request);
    return result.Succeeded
        ? Results.Created($"/bookings/{result.BookingId}", result)
        : Results.BadRequest(result.Error);
});

app.Run();