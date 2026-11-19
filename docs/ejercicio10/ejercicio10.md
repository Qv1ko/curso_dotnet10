# Versionado y comprobaciones de salud del servicio de Reservas

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// TAREA 1: versionado
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    });

// TAREA 3: comprobaciones de salud
builder.Services.AddHealthChecks()
    .AddCheck(
        "repositorio",
        () => RepositorioDisponible()
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Base de datos no disponible"),
        tags: ["ready"]);

var app = builder.Build();

var reservas = app.NewVersionedApi("Reservas");

// TAREA 2: grupos v1 y v2
var v1 = reservas.MapGroup("/api/v{version:apiVersion}/reservas")
    .HasDeprecatedApiVersion(1, 0);
v1.MapGet("/{id}", (string id) =>
    Results.Ok(new { reservaId = id, noches = 3, importeTotal = 240.00m }));

var v2 = reservas.MapGroup("/api/v{version:apiVersion}/reservas")
    .HasApiVersion(2, 0);
v2.MapGet("/{id}", (string id) =>
    Results.Ok(new { reservaId = id, numeroNoches = 3, importeTotal = 240.00m, moneda = "EUR" }));

// TAREA 3: endpoints de salud
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

app.Run();

// Simula la comprobación de la base de datos de Reservas
static bool RepositorioDisponible() => true;
```

## Cómo está resuelto

- **Tarea 1:** versión por defecto 1.0, asumida si falta (`AssumeDefaultVersionWhenUnspecified`), cabeceras de versiones activadas (`ReportApiVersions`) y lectura solo del segmento de la URL (`UrlSegmentApiVersionReader`). Con ese lector el segmento es obligatorio, así que asumir la versión por defecto no tiene efecto práctico, pero el requisito lo pide y es inocuo. Se encadena `AddApiExplorer` con `GroupNameFormat = "'v'V"`, que da los grupos `v1` y `v2`.
- **Tarea 2:** dos grupos colgando de `reservas`, primero v1 (`HasDeprecatedApiVersion(1, 0)`) y después v2 (`HasApiVersion(2, 0)`), cada uno con su `GET /{id}` según el contrato. `importeTotal` es `decimal`, por eso se serializa como `240.00`.
- **Tarea 3:** la comprobación `repositorio` con la etiqueta `ready`. `/health/live` usa `Predicate = _ => false` (no ejecuta nada) y `/health/ready` filtra por la etiqueta `ready`.

## Tarea 4: predicciones

### (a) Versionado

| Petición                                                       | Predicción | Justificación                                                                                                                                      |
| -------------------------------------------------------------- | ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GET /api/v1/reservas/R-1`                                     | **200 OK** | La obsolescencia solo se comunica en cabeceras; la versión 1.0 sigue funcionando.                                                                  |
| `GET /api/v2/reservas/R-1`, cabecera `api-supported-versions`  | **`2.0`**  | Los dos grupos comparten el API `"Reservas"` y `ReportApiVersions` informa de sus versiones; la 1.0, al ser obsoleta, no figura en las soportadas. |
| `GET /api/v2/reservas/R-1`, cabecera `api-deprecated-versions` | **`1.0`**  | La versión 1.0 está declarada como obsoleta en el mismo conjunto de versiones.                                                                     |

### (b) Salud con `RepositorioDisponible()` devolviendo `false`

| Endpoint        | Predicción                  | Justificación                                                                                             |
| --------------- | --------------------------- | --------------------------------------------------------------------------------------------------------- |
| `/health/live`  | **200 OK**                  | El predicado excluye todas las comprobaciones y un informe vacío cuenta como sano.                        |
| `/health/ready` | **503 Service Unavailable** | `repositorio` pasa a no sano ("Base de datos no disponible") y el estado por defecto para no sano es 503. |

## Probarlo

```bash
ASPNETCORE_URLS=http://localhost:5000 dotnet run

curl -i http://localhost:5000/api/v1/reservas/R-1
curl -i http://localhost:5000/api/v2/reservas/R-1     # mira las cabeceras api-*-versions
curl -i http://localhost:5000/health/live
curl -i http://localhost:5000/health/ready
```

Para comprobar (b), cambia el `true` por `false` en `RepositorioDisponible()` y repite los dos últimos `curl`:

```bash
sed -i 's/RepositorioDisponible() => true/RepositorioDisponible() => false/' Program.cs   # en macOS: sed -i ''
```
