# Módulo de Rutas de Mensarel (DDD, .NET 10)

## 1. Raíz de agregado `Ruta`

Archivo `Ruta.cs` (namespace `Mensarel.Dominio`):

```csharp
namespace Mensarel.Dominio;

public sealed record RutaCerradaDomainEvent(Guid RutaId, Guid ClienteId, Dinero Total, int NumeroParadas) : IDomainEvent;

public class Ruta : Entidad<Guid> {
    private const int MaxParadas = 20;
    private const string MonedaRuta = "EUR";
    private readonly List<Parada> _paradas = new();

    public Guid ClienteId { get; private set; }
    public bool EstaCerrada { get; private set; }
    public IReadOnlyCollection<Parada> Paradas => _paradas.AsReadOnly();

    private Ruta() { }

    public Ruta(Guid id, Guid clienteId) : base(id) => ClienteId = clienteId;

    public void AñadirParada(string direccion, Dinero coste) {
        ArgumentNullException.ThrowIfNull(coste);
        if (EstaCerrada)
            throw new InvalidOperationException("No se pueden añadir paradas a una ruta cerrada.");
        if (_paradas.Count >= MaxParadas)
            throw new InvalidOperationException($"Una ruta admite como máximo {MaxParadas} paradas.");
        if (coste.Moneda != MonedaRuta)
            throw new InvalidOperationException($"Todos los costes deben estar en {MonedaRuta}.");

        _paradas.Add(new Parada(Guid.NewGuid(), direccion, coste));
    }

    public Dinero CalcularTotal() => _paradas.Aggregate(new Dinero(0m, MonedaRuta), (total, parada) => total.Sumar(parada.Coste));

    public void Cerrar() {
        if (EstaCerrada)
            throw new InvalidOperationException("La ruta ya está cerrada.");
        if (_paradas.Count == 0)
            throw new InvalidOperationException("No se puede cerrar una ruta sin paradas.");

        EstaCerrada = true;
        AñadirEventoDominio(new RutaCerradaDomainEvent(Id, ClienteId, CalcularTotal(), _paradas.Count));
    }
}
```

| Requisito                                       | Mecanismo en el código                                                                                 | Resultado  |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------ | ---------- |
| Estado solo modificable desde sus métodos       | `private set` en `ClienteId` y `EstaCerrada`                                                           | **Cumple** |
| Colección de paradas protegida                  | Lista privada `_paradas` expuesta con `AsReadOnly()`                                                   | **Cumple** |
| Constructor público `(Guid id, Guid clienteId)` | Constructor público que llama a `base(id)`; el constructor privado sin parámetros es solo para EF Core | **Cumple** |
| Regla 1: máximo 20 paradas                      | `_paradas.Count >= 20` lanza `InvalidOperationException`                                               | **Cumple** |
| Regla 2: ruta cerrada no admite paradas         | `EstaCerrada` lanza `InvalidOperationException`                                                        | **Cumple** |
| Regla 3: costes en EUR                          | `coste.Moneda != "EUR"` lanza `InvalidOperationException`                                              | **Cumple** |
| Las paradas solo nacen dentro del agregado      | Constructor `internal` de `Parada`, usado solo desde `Ruta`                                            | **Cumple** |

**Veredicto: las tres reglas de `AñadirParada` se validan antes de ejecutar `Add`**, por lo que un incumplimiento nunca modifica la ruta.

Notas de diagnóstico:

- **Por qué se incumplían las reglas en producción:** `Ruta` tenía setters públicos y una `List<Parada>` pública, así que cualquier código podía hacer `ruta.Paradas.Add(...)` saltándose `RutaService`.
- **`RutaService` ignoraba la parada 21 sin avisar:** su `if (Count < 20)` no tenía `else`, de modo que el llamador creía que había añadido la parada. Ahora se lanza una excepción.
- **`RutaService` desaparece:** su lógica vive en `Ruta.AñadirParada`. Los llamadores deben usar `ruta.AñadirParada(direccion, coste)`.
- **`CalcularTotal()`** parte de `Dinero(0, "EUR")` y suma los costes con `Sumar`, así que una ruta sin paradas devuelve 0 EUR en lugar de fallar.

## 2. Cierre de la ruta y evento

`Cerrar()` y `RutaCerradaDomainEvent` están en el mismo archivo `Ruta.cs`.

| Situación                | Condición evaluada        | Resultado                            |
| ------------------------ | ------------------------- | ------------------------------------ |
| Ruta ya cerrada          | `EstaCerrada == true`     | **`InvalidOperationException`**      |
| Ruta sin paradas         | `_paradas.Count == 0`     | **`InvalidOperationException`**      |
| Ruta abierta con paradas | Ninguna de las anteriores | **`EstaCerrada = true` y un evento** |

| Dato que necesita Facturación | Campo del evento                                    |
| ----------------------------- | --------------------------------------------------- |
| Identificador de la ruta      | `RutaId`                                            |
| Cliente                       | `ClienteId`                                         |
| Total en euros                | `Total` (`Dinero`, calculado con `CalcularTotal()`) |
| Número de paradas             | `NumeroParadas`                                     |

**Veredicto: `Cerrar()` registra un único evento con los cuatro datos que exige la regla 5**, y solo lo hace después de pasar las dos validaciones.

Notas de diagnóstico:

- Las dos validaciones van antes de `EstaCerrada = true`, de modo que un cierre fallido deja la ruta abierta y sin eventos.
- El evento se registra con `AñadirEventoDominio` y no se publica aquí: el agregado solo lo anota, y la infraestructura lo despacha al guardar.
- `Total` es un `Dinero` y no un `decimal` para que el evento lleve también la moneda y Facturación no tenga que suponerla.

## 3. Consulta de la pantalla de supervisión

Archivo `ConsultasRutas.cs` (namespace `Mensarel.Aplicacion`):

```csharp
using Mensarel.Dominio;
using Mensarel.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace Mensarel.Aplicacion;

public sealed record ResumenRutaDto(Guid Id, int NumeroParadas, decimal TotalEuros);

public sealed class ConsultasRutas(LogisticaDbContext contexto) {
    public Task<List<ResumenRutaDto>> ObtenerRutasAbiertasAsync(CancellationToken cancellationToken) =>
        contexto.Rutas
            .AsNoTracking()
            .Where(ruta => !ruta.EstaCerrada)
            .Select(ruta => new ResumenRutaDto(
                ruta.Id,
                ruta.Paradas.Count(),
                ruta.Paradas.Sum(p => p.Coste.Cantidad)))
            .ToListAsync(cancellationToken);
}
```

| Elemento                   | Implementación                            | Motivo                                   |
| -------------------------- | ----------------------------------------- | ---------------------------------------- |
| Solo rutas abiertas        | `.Where(ruta => !ruta.EstaCerrada)`       | El filtro se ejecuta en la base de datos |
| Sin cargar agregados       | `.Select(...)` directo a `ResumenRutaDto` | EF no materializa `Ruta` ni `Parada`     |
| Sin seguimiento de cambios | `.AsNoTracking()`                         | Es una consulta de solo lectura          |
| Número de paradas          | `ruta.Paradas.Count()`                    | Se traduce a `COUNT` en SQL              |
| Total en euros             | `ruta.Paradas.Sum(p => p.Coste.Cantidad)` | Se traduce a `SUM` en SQL                |

**Veredicto: la consulta devuelve identificador, número de paradas y total en euros de cada ruta abierta con un único `SELECT`**, sin cargar ningún agregado.

Notas de diagnóstico:

- Una ruta abierta sin paradas aparece con 0 paradas y total 0, porque `Sum` sobre un conjunto vacío devuelve 0 en EF Core.
- Como todos los costes están en EUR por la regla 3, sumar `Cantidad` directamente es correcto y el DTO puede llamarse `TotalEuros`.
- Algunos proveedores (por ejemplo SQLite) no soportan `SUM` sobre `decimal`; con SQL Server funciona sin cambios.

## 4. Por qué la consulta no usa `Ruta.CalcularTotal()`

**Elegida: (b) proyectar el total en SQL con `Sum`.**

La consulta no usa `Ruta.CalcularTotal()` porque es un método C# que EF Core no puede traducir a SQL. Usarlo obligaría a cargar cada `Ruta` con todas sus `Parada` en memoria solo para sumar, mientras que la proyección deja el cálculo en la base de datos y trae únicamente tres valores por ruta.

- **(a) Descartada:** cargar las rutas con `Include(r => r.Paradas)` y llamar a `CalcularTotal()` carga hasta 20 paradas por ruta, activa el seguimiento de cambios y materializa agregados completos que la pantalla no necesita. Es justo lo que pide evitar el enunciado.
- **Regla del agregado frente a consulta:** `CalcularTotal()` es la forma correcta de obtener el total cuando se trabaja con una ruta concreta dentro del dominio, por ejemplo en `Cerrar()`. Para listados de lectura se consulta directamente, porque ahí no hay invariantes que proteger.

## 5. Casos de prueba

| Caso                                      | Traza en el código                            | Resultado                                                                 |
| ----------------------------------------- | --------------------------------------------- | ------------------------------------------------------------------------- |
| Añadir la parada número 21                | `_paradas.Count` vale 20, se cumple `>= 20`   | **Excepción; la ruta sigue con 20 paradas**                               |
| Añadir una parada de 5 USD                | `Moneda` vale `"USD"`, distinta de `"EUR"`    | **Excepción**                                                             |
| Cerrar una ruta sin paradas               | `_paradas.Count == 0`                         | **Excepción; `EstaCerrada` sigue en `false`**                             |
| Paradas de 12,50 EUR y 7,25 EUR, y cerrar | 0 + 12,50 + 7,25 = 19,75; 2 paradas           | **`EstaCerrada` es `true` y un único evento con total 19,75 y 2 paradas** |
| Añadir una parada a la ruta anterior      | `EstaCerrada` es `true`, se comprueba primero | **Excepción**                                                             |

**Veredicto: los cinco casos se cumplen según la traza del código.** No he compilado el código ni ejecutado pruebas, así que conviene confirmarlo con `dotnet test`.

## 6. Suposiciones sobre el manual

- `Dinero` tiene constructor `Dinero(decimal, string)`, propiedades `Cantidad` y `Moneda`, y `Sumar` devuelve `Dinero`. Si los nombres difieren, el ajuste es de una línea en `Ruta.cs` y en `ConsultasRutas.cs`.
- `Entidad<TId>` ofrece el constructor `base(id)` y el método `AñadirEventoDominio(IDomainEvent)`.
- `LogisticaDbContext` está en `Mensarel.Infraestructura`; hay que ajustar los `using` a los namespaces reales.
- EF Core usa el campo `_paradas` por convención. Si las paradas no se cargan, hay que añadir `builder.Navigation(r => r.Paradas).UsePropertyAccessMode(PropertyAccessMode.Field);`.
