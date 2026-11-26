var builder = WebApplication.CreateBuilder(args);

var cadena = builder.Configuration
    .GetConnectionString("Catalogo")!;

builder.Services.AddCatalogo(cadena);
builder.Services.AddScoped<ServicioCatalogo>();

var app = builder.Build();

app.Run();