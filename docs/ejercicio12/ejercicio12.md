# Contrato y manejo de errores del microservicio Salas

## Program.cs completo (tareas 1, 2 y 3)

```csharp
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IReservaStore, ReservaStoreSql>();

// TAREA 1: validación integrada (400 sin ejecutar el endpoint)
builder.Services.AddValidation();

// TAREA 3: manejador de excepciones
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErroresSalasHandler>();

var app = builder.Build();

app.UseExceptionHandler();

// TAREA 2: endpoint con TypedResults
app.MapPost("/api/salas/{salaId:int}/reservas", async Task<Results<Created<ReservaCreada>, ProblemHttpResult>> (
    int salaId, NuevaReserva datos, IReservaStore store) => {
    if (await store.EstaOcupadaAsync(salaId, datos.Fecha))
        return TypedResults.Problem(
            title: "Sala ocupada",
            detail: $"La sala {salaId} ya está reservada el {datos.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.",
            statusCode: StatusCodes.Status409Conflict);

    var id = await store.CrearAsync(salaId, datos);
    return TypedResults.Created(
        $"/api/salas/{salaId}/reservas/{id}",
        new ReservaCreada(id, salaId, datos.Fecha));
});

app.Run();

public class NuevaReserva {
    [Required]
    public string? Responsable { get; set; }

    public DateOnly Fecha { get; set; }

    [Range(1, 20)]
    public int Asistentes { get; set; }
}

public record ReservaCreada(int Id, int SalaId, DateOnly Fecha);

public interface IReservaStore {
    Task<bool> EstaOcupadaAsync(int salaId, DateOnly fecha);
    // Lanza SalaNoExisteException si la sala no existe
    Task<int> CrearAsync(int salaId, NuevaReserva datos);
}

public class SalaNoExisteException(int salaId): Exception($"No existe la sala {salaId}.");

// TAREA 3: filas 4 y 5 del contrato
public sealed class ErroresSalasHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ErroresSalasHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        ProblemDetails problem;
        if (exception is SalaNoExisteException) {
            problem = new ProblemDetails {
                Status = StatusCodes.Status404NotFound,
                Title = "Sala no encontrada",
                Detail = exception.Message
            };
        } else {
            logger.LogError(exception, "Error inesperado en {Metodo} {Ruta}",
                httpContext.Request.Method, httpContext.Request.Path);
            problem = new ProblemDetails {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno",
                Detail = "Error inesperado. Indique el traceId al soporte."
            };
        }

        problem.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
```

## Cómo está resuelto

### Tarea 1: validación (fila 1)

`[Required]` en `Responsable` y `[Range(1, 20)]` en `Asistentes`, más `builder.Services.AddValidation()`. Los datos inválidos devuelven 400 con el detalle de los campos antes de llegar al endpoint, y el `if` manual desaparece. `[Required]` también rechaza un responsable formado solo por espacios, que el código original dejaba pasar.

### Tarea 2: endpoint (filas 2 y 3)

El tipo de retorno es `Task<Results<Created<ReservaCreada>, ProblemHttpResult>>`:

- **Sala ocupada:** `TypedResults.Problem(...)` con 409, title "Sala ocupada" y la fecha formateada con `InvariantCulture`, para que el año no cambie según la cultura del servidor.
- **Creada:** `TypedResults.Created("/api/salas/{salaId}/reservas/{id}", new ReservaCreada(...))`, que da 201 con `Location` y cuerpo.

### Tarea 3: manejador (filas 4 y 5)

`ErroresSalasHandler` implementa `IExceptionHandler`:

- `SalaNoExisteException` devuelve 404 con title "Sala no encontrada" y el mensaje de la excepción como detail.
- Cualquier otra excepción se registra con `LogError` y devuelve 500 con title "Error interno" y el detail genérico. La excepción original nunca se copia a la respuesta.
- En ambos casos se añade `traceId` al cuerpo, para que "indique el traceId al soporte" tenga sentido.

Líneas de `Program.cs` que lo activan:

```csharp
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErroresSalasHandler>();
// ... tras builder.Build():
app.UseExceptionHandler();
```

## Tarea 4: predicciones

| Caso                                                                                               | Predicción                                                                 | Justificación                                                                                                                                                                                                                                                                                 |
| -------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| (a) `CrearAsync` lanza `InvalidOperationException("Conexión rechazada por srv-sql-02")`: respuesta | **500 ProblemDetails**: title "Error interno", detail genérico y `traceId` | Cualquier excepción distinta de `SalaNoExisteException` cae en la rama 500 del manejador.                                                                                                                                                                                                     |
| (a) ¿Ve el cliente "srv-sql-02"?                                                                   | **No**                                                                     | El manejador solo copia `exception.Message` para `SalaNoExisteException`; en el resto escribe un texto fijo y el mensaje original solo llega al log. Tampoco aparece en Development, porque el manejador atiende la excepción antes de que llegue la página de excepciones del desarrollador. |
| (b) ¿Quedaría en los logs sin el `LogError` del manejador?                                         | **No**                                                                     | Desde .NET 10, si `TryHandleAsync` devuelve `true`, el middleware ya no registra por defecto los diagnósticos de esa excepción, incluido el log de excepción no controlada; por eso el `LogError` es imprescindible.                                                                          |

Alternativa: `ExceptionHandlerOptions.SuppressDiagnosticsCallback` devolviendo `false` restaura el comportamiento anterior sin `LogError`, pero entonces las excepciones `SalaNoExisteException` (404) también se registrarían como error.

Referencia: [Exception handler diagnostics suppressed (breaking change en .NET 10)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/10/exception-handler-diagnostics-suppressed)
