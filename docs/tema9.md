# Tema 9 Dapper, NoSQL y persistencia combinada

## Índice

**Manual I: Dapper, Persistencia Híbrida y Escalabilidad Distribuida**

1. Dapper y Modelos de Lectura Especializados
2. Patrones de Persistencia Híbrida y Políglota
3. Escalabilidad de Datos en Sistemas Distribuidos

**Cuadernillos del Alumno (laboratorio)**

- 9.1 Read Models con Dapper
- 9.2 Auditoría con NoSQL (MongoDB)
- 9.3 Documentar el Mapa de Persistencia

---

# Manual I: Dapper, Persistencia Híbrida y Escalabilidad Distribuida

## 1. Dapper y Modelos de Lectura Especializados

### 1.1. Principios de Dapper como Micro-ORM frente a EF Core

En el ecosistema de .NET 10, el acceso a datos está dominado por Entity Framework Core (EF Core), un ORM (Object-Relational Mapper) completo y robusto. Sin embargo, arquitecturas de alto rendimiento a menudo requieren herramientas más ligeras y cercanas al metal. Aquí es donde entra Dapper, clasificado como un "Micro-ORM".

Dapper fue desarrollado por el equipo de Stack Overflow para resolver sus propios problemas de rendimiento en consultas de lectura. A diferencia de EF Core, Dapper no intenta abstraer la base de datos ni gestionar el estado de las entidades. Técnicamente, Dapper es simplemente un conjunto de métodos de extensión sobre la interfaz `IDbConnection` de ADO.NET. Su única responsabilidad es ejecutar consultas SQL crudas y mapear los resultados a objetos C# (POCOs - _Plain Old CLR Objects_) de la forma más rápida posible mediante la emisión de código IL (_Intermediate Language_) en tiempo de ejecución.

La diferencia fundamental radica en la sobrecarga conceptual y de procesamiento:

- **EF Core** utiliza un Change Tracker para observar modificaciones en los objetos, traduce expresiones LINQ a SQL mediante un motor complejo, y gestiona el patrón Unit of Work a través del `DbContext`. Todo esto consume memoria y ciclos de CPU.
- **Dapper** carece de Change Tracker, no traduce LINQ a SQL (el desarrollador escribe el SQL explícitamente) y no tiene concepto de Unit of Work. Esta ausencia de características es precisamente lo que reduce su sobrecarga en tiempo y memoria: entre la consulta SQL y el objeto C# apenas hay código intermedio.

### 1.2. Escenarios de Uso: Cuándo Elegir Dapper

La elección entre EF Core y Dapper no debe verse como una guerra, sino como una selección de la herramienta adecuada para el trabajo adecuado. Dapper brilla en escenarios específicos donde la simplicidad y el rendimiento justifican la escritura manual de SQL.

Los escenarios ideales para Dapper incluyen:

1. **Modelos de Lectura Especializados:** en arquitecturas que separan lecturas de escrituras (como veremos conceptualmente en el tema de CQRS), las consultas de lectura suelen requerir proyecciones complejas, uniones (`JOIN`s) de múltiples tablas y tiempos de respuesta ajustados. Dapper permite escribir la consulta SQL exacta y mapearla a un DTO plano sin la sobrecarga de instanciar entidades de dominio.
2. **APIs de Alto Rendimiento (High Throughput):** endpoints con mucho tráfico que solo necesitan devolver datos sin modificarlos.
3. **Bases de Datos Legacy:** cuando se trabaja con esquemas de bases de datos antiguos, desnormalizados o con convenciones de nomenclatura extrañas que son difíciles de mapear mediante la API Fluida de EF Core.
4. **Sentencias de escritura sencillas sin lógica de dominio:** un `UPDATE` o un `INSERT` puntual (cambiar un estado, marcar un mensaje como procesado) se expresa con `ExecuteAsync` en una línea, sin cargar entidades.

**Atención a las operaciones masivas.** Es habitual leer que Dapper es "la opción para cargas masivas", pero no es exacto. Cuando se pasa una lista como parámetro de `ExecuteAsync`, Dapper ejecuta la sentencia una vez por cada elemento: es cómodo, pero son tantas sentencias como filas. Para volúmenes grandes hay herramientas específicas:

- **Inserción masiva en SQL Server:** `SqlBulkCopy` (de `Microsoft.Data.SqlClient`), que envía las filas en bloque.
- **Actualización o borrado por conjuntos con EF Core:** `ExecuteUpdateAsync` y `ExecuteDeleteAsync` (tratados en el tema 8), que generan una única sentencia `UPDATE`/`DELETE` sin cargar entidades. En EF Core 10, `ExecuteUpdateAsync` admite además una lambda normal (no un árbol de expresión), lo que permite añadir asignaciones de forma condicional.

```csharp
// Una sentencia INSERT por cada elemento de la lista (no es una carga masiva)
await connection.ExecuteAsync(
    "INSERT INTO SensorReadings (SensorId, Value, ReadAt) VALUES (@SensorId, @Value, @ReadAt)",
    readings); // readings: List<SensorReading>

// Carga masiva real en SQL Server
using var bulk = new SqlBulkCopy(connection) { DestinationTableName = "SensorReadings" };
await bulk.WriteToServerAsync(readingsTable); // readingsTable: DataTable con las mismas columnas
```

Por el contrario, Dapper no es recomendable para la lógica de dominio compleja donde las reglas de negocio dictan cómo cambian las entidades, ya que obligaría al desarrollador a gestionar manualmente las transacciones, el estado y la concurrencia optimista, reinventando lo que EF Core ya resuelve.

### 1.3. Implementación Práctica de Consultas de Alto Rendimiento

Para ilustrar la simplicidad y potencia de Dapper, implementaremos un repositorio de lectura que obtiene un resumen de cliente.

Paquetes NuGet de los ejemplos de este tema: `Dapper` y `Microsoft.Data.SqlClient` (SQL Server), `MongoDB.Driver` (MongoDB) y `Microsoft.Extensions.Caching.StackExchangeRedis` (Redis como `IDistributedCache`). Los ejemplos suponen un proyecto `Microsoft.NET.Sdk.Web` con `ImplicitUsings` activado, que ya importa `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting` y `Microsoft.Extensions.Logging`.

```csharp
using Microsoft.Data.SqlClient;
using Dapper;

public class CustomerReadRepository
{
    private readonly string _connectionString;

    // El contenedor de DI no sabe resolver un 'string' suelto: recibimos IConfiguration
    // y leemos la cadena de conexión de la sección ConnectionStrings.
    public CustomerReadRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Customers")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Customers'.");
    }

    public async Task<CustomerSummaryDto?> GetCustomerSummaryAsync(int customerId)
    {
        // 1. Definimos la consulta SQL cruda y optimizada
        const string sql = @"
            SELECT
                c.Id,
                c.FullName,
                c.Email,
                c.RegistrationDate,
                (SELECT COUNT(*) FROM Orders o WHERE o.CustomerId = c.Id) AS TotalOrders
            FROM Customers c
            WHERE c.Id = @CustomerId";

        // 2. Instanciamos la conexión. El bloque 'using' garantiza que se cierre y devuelva al pool.
        using var connection = new SqlConnection(_connectionString);

        // 3. Ejecutamos la consulta usando Dapper.
        // Pasamos un objeto anónimo para parametrizar la consulta y evitar SQL Injection.
        var result = await connection.QuerySingleOrDefaultAsync<CustomerSummaryDto>(
            sql,
            new { CustomerId = customerId }
        );

        return result;
    }
}

// DTO plano para la respuesta
public record CustomerSummaryDto(
    int Id,
    string FullName,
    string Email,
    DateTime RegistrationDate,
    int TotalOrders
);
```

**Explicación del código:**

- `QuerySingleOrDefaultAsync<T>`: es un método de extensión de Dapper. Ejecuta la consulta y espera un solo registro o nulo. Si la base de datos devuelve más de un registro, lanzará una excepción, lo cual es correcto para una búsqueda por ID.
- `new { CustomerId = customerId }`: Dapper mapea automáticamente las propiedades de este objeto anónimo a los parámetros `@CustomerId` en la cadena SQL. Esto es crucial para la seguridad (prevención de inyección SQL) y el rendimiento (reutilización de planes de ejecución en el motor de base de datos).
- El mapeo hacia `CustomerSummaryDto` es automático y se basa en la coincidencia de nombres entre las columnas devueltas por el `SELECT` y las propiedades del DTO. Con un `record` posicional, Dapper usa su constructor: las columnas deben coincidir en nombre y tipo con los parámetros.
- Dapper abre la conexión si la recibe cerrada y la vuelve a cerrar al terminar; el `using` la devuelve al pool.
- El repositorio se registra en `Program.cs` con `builder.Services.AddScoped<CustomerReadRepository>();`.

**Otros métodos de Dapper que usaremos en el tema:**

| Método                         | Devuelve                                          | Uso típico                               |
| ------------------------------ | ------------------------------------------------- | ---------------------------------------- |
| `QueryAsync<T>`                | `IEnumerable<T>` (vacío si no hay filas)          | Listados                                 |
| `QuerySingleOrDefaultAsync<T>` | `T` o `null`; excepción si hay más de una fila    | Búsqueda por clave                       |
| `QueryFirstOrDefaultAsync<T>`  | La primera fila o `null`                          | Obtener "el más reciente" con `ORDER BY` |
| `ExecuteAsync`                 | `int`: número de filas afectadas                  | `INSERT`, `UPDATE`, `DELETE`             |
| `ExecuteScalarAsync<T>`        | El valor de la primera columna de la primera fila | `COUNT(*)`, `SCOPE_IDENTITY()`           |

El valor que devuelve `ExecuteAsync` permite saber si una actualización encontró la fila: si un `UPDATE ... WHERE Id = @Id` devuelve 0, el registro no existe. Si se añade una condición al `WHERE` (por ejemplo, `WHERE Id = @Id AND Stock >= @Quantity`), la comprobación y la modificación ocurren en una sola sentencia atómica y un 0 significa "no existe o no cumple la condición". Leer primero con un `SELECT` y actualizar después en otra sentencia deja una ventana en la que otra petición puede modificar la fila entre ambas.

```csharp
public async Task<bool> RenameCustomerAsync(int customerId, string fullName)
{
    using var connection = new SqlConnection(_connectionString);
    int rows = await connection.ExecuteAsync(
        "UPDATE Customers SET FullName = @FullName WHERE Id = @Id",
        new { Id = customerId, FullName = fullName });
    return rows == 1; // false si el cliente no existe
}
```

**Transacciones con Dapper.** Dapper no tiene Unit of Work: si dos sentencias deben confirmarse juntas, se abre la conexión (`OpenAsync`), se inicia la transacción con `connection.BeginTransaction()`, se pasa en cada llamada con el parámetro `transaction: tx` y se confirma con `tx.Commit()`. Si se lanza una excepción o se sale del método antes del `Commit`, el `using` de la transacción la deshace. Si se olvida pasar `transaction`, `SqlClient` lanza una `InvalidOperationException` porque la conexión tiene una transacción local pendiente. El apartado 2.5 muestra un ejemplo completo.

**Dapper y EF Core en la misma transacción.** Si un caso de uso escribe con EF Core y con Dapper, ambos pueden compartir conexión y transacción: se inicia la transacción desde el `DbContext`, se obtiene su conexión con `GetDbConnection()` (no se libera: pertenece al contexto) y se pasa a Dapper la transacción subyacente con `GetDbTransaction()`.

```csharp
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage; // GetDbTransaction()

await using var tx = await db.Database.BeginTransactionAsync(ct);
var connection = db.Database.GetDbConnection();   // la misma conexión, ya abierta

await connection.ExecuteAsync(
    "UPDATE Products SET Stock = Stock - @Qty WHERE Id = @Id",
    new { Id = productId, Qty = quantity }, transaction: tx.GetDbTransaction());

db.Orders.Add(order);
await db.SaveChangesAsync(ct);        // se ejecuta dentro de la transacción en curso
await tx.CommitAsync(ct);             // las dos escrituras o ninguna
```

Si el contexto usa reintentos (`EnableRetryOnFailure`), EF Core rechaza las transacciones iniciadas por el usuario salvo que todo el bloque se ejecute dentro de `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`.

### 1.4. Mapeo Avanzado de Relaciones Complejas (Uno a Muchos)

Uno de los desafíos comunes al abandonar EF Core es cómo mapear relaciones complejas, como un pedido (`Order`) que tiene múltiples líneas de pedido (`OrderDetails`). En EF Core, un `.Include(o => o.Details)` resuelve esto. En Dapper, debemos escribir un `JOIN` y utilizar una técnica de agrupación en memoria.

A continuación, mostramos cómo resolver el problema de "N+1 consultas" y mapear una relación uno a muchos en una sola llamada a la base de datos.

```csharp
public async Task<OrderDto?> GetOrderWithDetailsAsync(int orderId)
{
    const string sql = @"
        SELECT
            o.Id, o.OrderDate, o.TotalAmount, o.Status, -- Columnas de Order
            d.Id, d.ProductId, d.Quantity, d.UnitPrice  -- Columnas de OrderDetail
        FROM Orders o
        LEFT JOIN OrderDetails d ON o.Id = d.OrderId
        WHERE o.Id = @OrderId";

    using var connection = new SqlConnection(_connectionString);

    // Diccionario para mantener la referencia única del pedido en memoria
    var orderDictionary = new Dictionary<int, OrderDto>();

    // QueryAsync<T1, T2, TReturn> permite mapear múltiples objetos por fila
    var result = await connection.QueryAsync<OrderDto, OrderDetailDto, OrderDto>(
        sql,
        map: (order, detail) =>
        {
            // Si el pedido no está en el diccionario, lo añadimos
            if (!orderDictionary.TryGetValue(order.Id, out var currentOrder))
            {
                currentOrder = order;
                currentOrder.Details = new List<OrderDetailDto>();
                orderDictionary.Add(currentOrder.Id, currentOrder);
            }

            // Si hay un detalle (LEFT JOIN puede devolver nulos), lo añadimos a la lista
            if (detail != null)
            {
                currentOrder.Details.Add(detail);
            }

            return currentOrder;
        },
        param: new { OrderId = orderId },
        splitOn: "Id" // Indica a Dapper dónde empieza el segundo objeto (OrderDetailDto)
    );

    // Como Dapper devuelve una fila por cada detalle, tomamos el primer elemento distinto
    return result.FirstOrDefault();
}

public class OrderDto
{
    public int Id { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<OrderDetailDto> Details { get; set; } = new();
}

public class OrderDetailDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

**Explicación del código:**

- **El parámetro `splitOn`:** es fundamental. Indica el nombre de la columna donde empieza el segundo objeto (`OrderDetailDto`). Dapper la busca empezando por el final de la fila y nunca toma la primera columna como corte, así que aquí encuentra el segundo `Id` (`d.Id`). Su valor por defecto es `"Id"`: si el segundo objeto no empieza en una columna con ese nombre y no se indica `splitOn`, Dapper lanza una `ArgumentException` ("splitOn column 'Id' was not found").
- **El Diccionario:** como la consulta SQL devuelve múltiples filas para el mismo pedido (una por cada detalle), la función `map` se ejecutará varias veces para el mismo `OrderDto`. El diccionario asegura que solo mantengamos una instancia del pedido en memoria y le vayamos agregando los detalles a su lista.
- **Pedido sin líneas:** con `LEFT JOIN`, las columnas del detalle llegan a `NULL`. Cuando la columna de corte (la primera del segundo objeto) es `NULL`, Dapper pasa `null` como segundo argumento de `map`; por eso se comprueba `detail != null`. Si el pedido no existe, no hay filas, `map` no se ejecuta y `FirstOrDefault()` devuelve `null`.
- **Orden de las líneas:** el diccionario conserva el orden en que llegan las filas, así que si las líneas deben salir ordenadas se añade `ORDER BY` a la consulta (por ejemplo, `ORDER BY d.Id`).
- **Columnas de corte con otro nombre:** si la clave del segundo objeto no se llama `Id`, se usa un alias y se indica ese nombre en `splitOn`. Por ejemplo, con `d.Id AS DetailId` y una propiedad `DetailId` en el DTO, se escribe `splitOn: "DetailId"`. Con tres objetos por fila se usa `QueryAsync<T1, T2, T3, TReturn>` y `splitOn` lleva las dos columnas separadas por comas (`"DetailId,ProductId"`).

### 1.5. Errores Comunes y Mejores Prácticas con Dapper

**Errores Comunes:**

1. **Concatenación de strings para parámetros:** escribir `$"SELECT * FROM Users WHERE Name = '{name}'"` es un fallo crítico de seguridad que abre la puerta a la inyección SQL. Siempre se deben usar parámetros: `new { Name = name }`.
2. **Olvidar el bloque `using`:** no liberar la `IDbConnection` provocará el agotamiento del pool de conexiones (_Connection Pool Exhaustion_), haciendo que la aplicación deje de responder bajo carga.
3. **Usar Dapper para grafos de objetos extremadamente profundos:** si necesitas mapear relaciones de 4 o 5 niveles de profundidad (A -> B -> C -> D), el uso de diccionarios y `splitOn` se vuelve inmanejable y propenso a errores. En esos casos, es mejor hacer múltiples consultas concurrentes o reconsiderar el modelo de lectura.

**Mejores Prácticas:**

1. **Separación estricta:** utiliza Dapper exclusivamente en repositorios de lectura o servicios de consulta. Mantén EF Core para los comandos que modifican el estado del dominio.
2. **Consultas en archivos externos:** para consultas SQL muy largas, considera almacenarlas en archivos `.sql` incrustados como recursos (_Embedded Resources_) en lugar de tener strings gigantes en el código C#.
3. **Uso de `QueryMultiple`:** si necesitas devolver varios conjuntos de datos independientes para una misma vista (ej. listas desplegables y datos del usuario), usa `connection.QueryMultipleAsync` para ejecutar un solo viaje a la base de datos que devuelva múltiples `SELECT`s. Los resultados se leen en el mismo orden en que aparecen las consultas:

```csharp
const string sql = @"
    SELECT Id, FullName, Email FROM Customers WHERE Id = @Id;
    SELECT Id, Name FROM Countries ORDER BY Name;";

using var connection = new SqlConnection(_connectionString);
using var grid = await connection.QueryMultipleAsync(sql, new { Id = customerId });

var customer = await grid.ReadSingleOrDefaultAsync<CustomerEditDto>(); // primer SELECT
var countries = (await grid.ReadAsync<CountryDto>()).ToList();         // segundo SELECT
```

## 2. Patrones de Persistencia Híbrida y Políglota

### 2.1. El Ecosistema Híbrido: Relacional, NoSQL y Caché Distribuida

En arquitecturas modernas y sistemas distribuidos, el enfoque de "una única base de datos relacional para todo" (típico de los monolitos tradicionales) se convierte rápidamente en un cuello de botella. Diferentes partes de una aplicación tienen diferentes necesidades de lectura, escritura, estructura y latencia. La persistencia políglota es el patrón arquitectónico que aboga por utilizar diferentes tecnologías de almacenamiento de datos según la naturaleza del dato y el caso de uso.

Un ecosistema híbrido típico en .NET 10 combina tres pilares:

1. **Almacenamiento Relacional (ej. SQL Server, PostgreSQL):** proporciona consistencia fuerte (ACID), integridad referencial y es ideal para datos transaccionales críticos, como operaciones financieras, inventario estricto o facturación.
2. **Almacenamiento NoSQL Documental (ej. MongoDB, Azure Cosmos DB):** ofrece estructuras flexibles (sin esquema fijo), escalabilidad horizontal y es adecuado para almacenar agregados completos de dominio, catálogos de productos con atributos variables, o registros de auditoría y eventos.
3. **Caché Distribuida (ej. Redis):** almacenamiento en memoria clave-valor con latencias muy bajas. Se utiliza para reducir la carga en las bases de datos principales almacenando datos de acceso frecuente y mutación infrecuente (ej. configuraciones, sesiones de usuario, catálogos base).

### 2.2. Equilibrio entre Rendimiento, Coherencia y Coste Operativo

Diseñar un sistema híbrido requiere equilibrar tres factores críticos, a menudo en tensión entre sí:

- **Rendimiento (Latencia y Throughput):** introducir Redis mejora el rendimiento de las lecturas repetidas. Sin embargo, escribir en múltiples sistemas (ej. guardar en SQL y luego actualizar Redis y Cosmos DB) aumenta la latencia de la operación de escritura.
- **Coherencia (Consistency):** según el Teorema CAP, en sistemas distribuidos debemos elegir entre consistencia y disponibilidad ante particiones de red. En un sistema híbrido, mantener la consistencia fuerte entre SQL Server y MongoDB es prácticamente imposible sin transacciones distribuidas (las cuales son un antipatrón por su impacto en el rendimiento). Por tanto, se debe abrazar la **consistencia eventual**: aceptar que durante un breve periodo de tiempo, la caché o la base de datos NoSQL pueden tener datos ligeramente desactualizados respecto a la base de datos relacional.
- **Coste Operativo y Económico:** mantener tres clústeres de bases de datos diferentes requiere conocimientos especializados en el equipo de operaciones (DevOps). Además, los costes de licenciamiento o consumo en la nube (ej. Request Units en Cosmos DB frente a vCores en SQL Azure) se multiplican. No se debe introducir persistencia políglota a menos que los requisitos de escala del negocio lo exijan absolutamente.

### 2.3. Implementación Práctica de un Flujo de Datos Híbrido

Para ilustrar cómo estas tecnologías colaboran, implementaremos un servicio de catálogo de productos. El flujo de lectura seguirá el patrón **Cache-Aside** (el tema 13 lo estudia a fondo junto con otras estrategias de caché): primero se busca en Redis; si no está, se consulta SQL Server con Dapper, se guarda el resultado en Redis y se devuelve. Además, cada consulta de un producto genera un evento de telemetría que se guarda en MongoDB sin retrasar la respuesta.

Para esto último no se lanza la inserción "al aire" (`_ = InsertOneAsync(...)`): una tarea que nadie espera queda fuera de cualquier control, sus excepciones se pierden, puede seguir ejecutándose cuando el contenedor de DI ya ha liberado sus dependencias y no respeta el apagado ordenado de la aplicación. El patrón recomendado en .NET es una cola en memoria con `System.Threading.Channels` que la petición alimenta, y un `BackgroundService` que la vacía.

**1. La cola en memoria (singleton):**

```csharp
using System.Threading.Channels;

public class ProductViewQueue
{
    // Cola acotada: si se llena, se descartan las escrituras nuevas en lugar de bloquear la petición
    private readonly Channel<ProductViewEvent> _channel =
        Channel.CreateBounded<ProductViewEvent>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropWrite
        });

    // TryWrite no espera; con DropWrite, si la cola está llena el evento se descarta
    public bool TryEnqueue(ProductViewEvent viewEvent) => _channel.Writer.TryWrite(viewEvent);

    public IAsyncEnumerable<ProductViewEvent> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
```

**2. El servicio de lectura:**

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Data.SqlClient;
using Dapper;

public class ProductCatalogService(
    IDistributedCache cache,
    IConfiguration configuration,
    ProductViewQueue viewQueue,
    ILogger<ProductCatalogService> logger)
{
    private readonly string _sqlConnectionString = configuration.GetConnectionString("Catalog")
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'Catalog'.");

    public async Task<ProductDto?> GetProductAsync(int productId, CancellationToken ct = default)
    {
        string cacheKey = $"product:{productId}";
        ProductDto? product = null;

        // 1. Intentar leer de Redis. Si Redis no responde, se degrada a SQL Server (no se devuelve un 500)
        try
        {
            var cachedData = await cache.GetStringAsync(cacheKey, ct);
            if (!string.IsNullOrEmpty(cachedData))
                product = JsonSerializer.Deserialize<ProductDto>(cachedData);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Caché no disponible para {CacheKey}", cacheKey);
        }

        if (product is null)
        {
            // 2. Fallo de caché: consultar SQL Server con Dapper
            using var connection = new SqlConnection(_sqlConnectionString);
            product = await connection.QuerySingleOrDefaultAsync<ProductDto>(
                new CommandDefinition(
                    "SELECT Id, Name, Price, Category FROM Products WHERE Id = @Id",
                    new { Id = productId }, cancellationToken: ct));

            if (product is not null)
            {
                // 3. Guardar en caché con caducidad (TTL); un fallo aquí tampoco rompe la petición
                try
                {
                    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(product),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                        }, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "No se pudo escribir {CacheKey} en caché", cacheKey);
                }
            }
        }

        // 4. Encolar la telemetría: no espera a MongoDB
        if (product is not null)
        {
            viewQueue.TryEnqueue(new ProductViewEvent
            {
                ProductId = product.Id,
                Category = product.Category,
                ViewedAt = DateTime.UtcNow
            });
        }

        return product;
    }
}

public record ProductDto(int Id, string Name, decimal Price, string Category);
```

`CommandDefinition` es la forma de pasar a Dapper un `CancellationToken` (junto con la consulta, los parámetros y, si hace falta, la transacción).

**3. El proceso en segundo plano que escribe en MongoDB:**

```csharp
using MongoDB.Bson;
using MongoDB.Driver;

public class ProductViewEvent
{
    public ObjectId Id { get; set; }            // MongoDB lo genera al insertar
    public int ProductId { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime ViewedAt { get; set; }
}

public class ProductViewWriter(
    ProductViewQueue queue,
    IMongoDatabase database,
    ILogger<ProductViewWriter> logger) : BackgroundService
{
    private readonly IMongoCollection<ProductViewEvent> _views =
        database.GetCollection<ProductViewEvent>("ProductViews");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // El bucle termina cuando se detiene la aplicación (stoppingToken)
        await foreach (var viewEvent in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await _views.InsertOneAsync(viewEvent, cancellationToken: stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un fallo puntual no debe detener el servicio: se registra y se sigue
                logger.LogError(ex, "Error guardando la vista del producto {ProductId}", viewEvent.ProductId);
            }
        }
    }
}
```

**4. Registro en `Program.cs`:**

```csharp
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));

// MongoClient es seguro para hilos y está pensado para reutilizarse: singleton
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase("catalog"));

builder.Services.AddSingleton<ProductViewQueue>();             // una única cola para toda la aplicación
builder.Services.AddHostedService<ProductViewWriter>();        // el consumidor de la cola
builder.Services.AddScoped<ProductCatalogService>();

var app = builder.Build();

app.MapGet("/products/{id:int}", async (int id, ProductCatalogService svc, CancellationToken ct) =>
    await svc.GetProductAsync(id, ct) is { } p ? Results.Ok(p) : Results.NotFound());

app.Run();
```

**Explicación del flujo:**

- **Redis (`IDistributedCache`):** protege a la base de datos relacional de lecturas repetidas. El TTL de 15 minutos evita que un dato quede obsoleto indefinidamente, pero no sustituye a la invalidación explícita cuando el dato cambia (apartado 2.5).
- **SQL Server (Dapper):** sigue siendo la fuente de la verdad (_Source of Truth_). Si Redis falla o el dato ha caducado, se consulta directamente.
- **MongoDB (`IMongoCollection`):** recibe los eventos de lectura sin afectar a las transacciones de negocio de SQL Server.
- **Cola + `BackgroundService`:** la petición solo añade el evento a memoria. Como la cola vive en memoria, los eventos pendientes se pierden si el proceso se reinicia y, con `DropWrite`, también si la cola se llena: es aceptable para telemetría, nunca para datos de negocio (para esos, el patrón Outbox del apartado 2.5).
- **Ciclo de vida:** la cola y el cliente de MongoDB son singleton; el servicio de catálogo, scoped. Un `BackgroundService` se registra como singleton, así que no debe recibir en su constructor un servicio scoped; si lo necesita, crea un ámbito con `IServiceScopeFactory.CreateScope()` en cada iteración.

### 2.4. Consultas y escrituras idempotentes en MongoDB

`MongoDB.Driver` trabaja con clases C# normales (POCO). Una propiedad llamada `Id` se asocia automáticamente al campo `_id` del documento. Para identificadores `ObjectId`, `int` o `string` no hace falta configuración; con `Guid`, la versión 3 del driver pide configurar explícitamente su representación (por ejemplo, `BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard))` al arrancar). En este tema usamos `ObjectId` e `int` para no depender de esa configuración.

**Leer con filtro, orden y límite.** `Find` admite una expresión lambda que el driver traduce a un filtro de MongoDB; `SortByDescending`, `Limit` y `ToListAsync` completan la consulta. `CountDocumentsAsync` cuenta los documentos que cumplen un filtro.

```csharp
using MongoDB.Driver;

public class ProductViewQueries(IMongoDatabase database)
{
    private readonly IMongoCollection<ProductViewEvent> _views =
        database.GetCollection<ProductViewEvent>("ProductViews");

    // Últimas vistas de un producto desde una fecha, de la más reciente a la más antigua
    public Task<List<ProductViewEvent>> GetRecentViewsAsync(
        int productId, DateTime sinceUtc, int max, CancellationToken ct) =>
        _views.Find(v => v.ProductId == productId && v.ViewedAt >= sinceUtc)
              .SortByDescending(v => v.ViewedAt)
              .Limit(max)
              .ToListAsync(ct);

    // Número de vistas de una categoría
    public Task<long> CountByCategoryAsync(string category, CancellationToken ct) =>
        _views.CountDocumentsAsync(v => v.Category == category, cancellationToken: ct);

    // Índice compuesto que sirve a la primera consulta (crearlo de nuevo no duplica el índice)
    public Task<string> CreateIndexesAsync(CancellationToken ct) =>
        _views.Indexes.CreateOneAsync(new CreateIndexModel<ProductViewEvent>(
            Builders<ProductViewEvent>.IndexKeys
                .Ascending(v => v.ProductId)
                .Descending(v => v.ViewedAt)), cancellationToken: ct);
}
```

Cuando el filtro se construye por partes, se usa `Builders<T>.Filter`, que combina condiciones con `&`:

```csharp
var f = Builders<ProductViewEvent>.Filter;
var filter = f.Eq(v => v.Category, "Libros") & f.Gte(v => v.ViewedAt, sinceUtc);
var views = await _views.Find(filter).ToListAsync(ct);
```

**Escribir de forma idempotente (upsert).** Para mantener en MongoDB una copia de un dato cuyo dueño es SQL Server (un documento de búsqueda de productos, por ejemplo), la identidad del documento es la clave de origen y se escribe con `ReplaceOneAsync` y `IsUpsert = true`: si el documento existe, se reemplaza; si no, se crea. Procesar el mismo cambio dos veces deja el mismo resultado, que es justo lo que se necesita cuando la sincronización puede repetirse.

```csharp
public class ProductSearchDocument
{
    public int Id { get; set; }                 // mismo Id que la fila de SQL Server
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// Dentro de un servicio que tiene IMongoCollection<ProductSearchDocument> _products
public Task UpsertAsync(ProductSearchDocument doc, CancellationToken ct) =>
    _products.ReplaceOneAsync(p => p.Id == doc.Id, doc,
        new ReplaceOptions { IsUpsert = true }, ct);
```

En cambio, `InsertOneAsync` dos veces con el mismo `_id` lanza una `MongoWriteException` por clave duplicada, y con un `_id` generado en cada inserción crearía dos documentos.

### 2.5. Gestión de la Consistencia Eventual en Sistemas Políglotas

El mayor reto de la persistencia híbrida ocurre durante las operaciones de escritura. Si un usuario actualiza el precio de un producto, debemos actualizar SQL Server, invalidar la caché en Redis y actualizar un modelo de búsqueda en NoSQL.

¿Qué ocurre si la actualización en SQL Server tiene éxito, pero la red falla antes de invalidar Redis? El sistema queda en un estado inconsistente.

Para resolver esto, se evitan las transacciones distribuidas (Two-Phase Commit - 2PC): MongoDB y Redis no participan en una transacción de SQL Server, y aunque pudieran, bloquearían recursos en varios sistemas a la vez. En su lugar se aplica la consistencia eventual con el **Patrón Outbox**, que el tema 7 presentó para publicar mensajes y que aquí usamos para sincronizar almacenes:

1. Se abre una transacción local en SQL Server.
2. Se actualiza el registro del producto.
3. En la misma transacción, se inserta un registro en una tabla `OutboxMessages` indicando "ProductoActualizado".
4. Se hace el commit de la transacción. (Ambas cosas ocurren o ninguna).
5. Un proceso en segundo plano lee la tabla `OutboxMessages` y actualiza MongoDB e invalida Redis (o publica el evento en un bus), reintentando en caso de fallo hasta lograrlo.

**Paso 1 a 4: escritura atómica con Dapper.** La tabla de ejemplo es `OutboxMessages (Id uniqueidentifier, Type nvarchar(100), Payload nvarchar(max), OccurredAt datetime2, ProcessedAt datetime2 NULL)`.

```csharp
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Dapper;

public class ProductPriceService(IConfiguration configuration)
{
    private readonly string _cs = configuration.GetConnectionString("Catalog")
        ?? throw new InvalidOperationException("Falta la cadena de conexión 'Catalog'.");

    public async Task<bool> ChangePriceAsync(int productId, decimal newPrice, CancellationToken ct)
    {
        using var connection = new SqlConnection(_cs);
        await connection.OpenAsync(ct);
        using var tx = connection.BeginTransaction();

        int rows = await connection.ExecuteAsync(
            "UPDATE Products SET Price = @Price WHERE Id = @Id",
            new { Id = productId, Price = newPrice }, transaction: tx);
        if (rows == 0) return false;            // no existe: el using deshace la transacción

        await connection.ExecuteAsync(
            @"INSERT INTO OutboxMessages (Id, Type, Payload, OccurredAt, ProcessedAt)
              VALUES (@Id, @Type, @Payload, @OccurredAt, NULL)",
            new
            {
                Id = Guid.NewGuid(),
                Type = "ProductChanged",
                Payload = JsonSerializer.Serialize(new ProductChanged(productId)),
                OccurredAt = DateTime.UtcNow
            }, transaction: tx);

        tx.Commit();                            // precio y mensaje, juntos o ninguno
        return true;
    }
}

public record ProductChanged(int ProductId);
public record OutboxRow(Guid Id, string Payload);
```

**Paso 5: el procesador de la bandeja de salida.** Un `BackgroundService` con `PeriodicTimer` consulta cada pocos segundos los mensajes pendientes, sincroniza y los marca como procesados.

```csharp
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Dapper;
using MongoDB.Driver;

public class ProductOutboxProcessor(
    IConfiguration configuration,
    IMongoDatabase database,
    IDistributedCache cache,
    ILogger<ProductOutboxProcessor> logger) : BackgroundService
{
    private readonly string _cs = configuration.GetConnectionString("Catalog")!;
    private readonly IMongoCollection<ProductSearchDocument> _products =
        database.GetCollection<ProductSearchDocument>("ProductSearch");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await ProcessPendingAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Se registra y se reintenta en el siguiente ciclo: el mensaje sigue pendiente
                logger.LogError(ex, "Error procesando OutboxMessages");
            }
        }
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        using var connection = new SqlConnection(_cs);
        var pending = await connection.QueryAsync<OutboxRow>(
            @"SELECT TOP (20) Id, Payload FROM OutboxMessages
              WHERE ProcessedAt IS NULL AND Type = 'ProductChanged'
              ORDER BY OccurredAt");

        foreach (var msg in pending)
        {
            var productId = JsonSerializer.Deserialize<ProductChanged>(msg.Payload)!.ProductId;

            // Se lee el estado ACTUAL de la fuente de verdad, no el del momento del evento
            var doc = await connection.QuerySingleOrDefaultAsync<ProductSearchDocument>(
                "SELECT Id, Name, Price, SYSUTCDATETIME() AS UpdatedAt FROM Products WHERE Id = @Id",
                new { Id = productId });

            if (doc is not null)
                await _products.ReplaceOneAsync(p => p.Id == doc.Id, doc,
                    new ReplaceOptions { IsUpsert = true }, ct);        // idempotente
            else
                await _products.DeleteOneAsync(p => p.Id == productId, ct); // el producto ya no existe

            await cache.RemoveAsync($"product:{productId}", ct);             // invalidación explícita

            // Se marca al final: si el proceso cae antes, el mensaje se repetirá sin daño
            await connection.ExecuteAsync(
                "UPDATE OutboxMessages SET ProcessedAt = SYSUTCDATETIME() WHERE Id = @Id",
                new { msg.Id });
        }
    }
}
```

Se registra con `builder.Services.AddHostedService<ProductOutboxProcessor>();` (el `IDistributedCache` de Redis y el `IMongoDatabase` ya son singleton).

**Por qué es seguro:**

- **Entrega al menos una vez.** El mensaje solo se marca como procesado después de sincronizar. Si el proceso se detiene entre medias, el siguiente ciclo lo repite. Por eso cada paso debe ser idempotente: `ReplaceOneAsync` con `IsUpsert`, `DeleteOneAsync` y `RemoveAsync` dejan el mismo resultado aunque se ejecuten dos veces.
- Leer el estado actual en lugar de copiar el valor del mensaje evita que dos mensajes procesados fuera de orden dejen en MongoDB un precio antiguo.
- **Varias instancias.** Si la aplicación se escala a varias réplicas, todas ejecutan el procesador y pueden tomar el mismo mensaje. Con escrituras idempotentes el resultado es correcto; para evitar el trabajo duplicado se reserva el lote en SQL (por ejemplo, con las sugerencias `UPDLOCK, READPAST` dentro de una transacción) o se ejecuta el procesador en una sola instancia.
- **Limpieza.** Los mensajes procesados se borran periódicamente para que la tabla no crezca sin límite.

### 2.6. Errores Comunes y Mejores Prácticas en Persistencia Híbrida

**Errores Comunes:**

1. **Ignorar la invalidación de caché:** confiar únicamente en el TTL para invalidar datos críticos. Si un precio cambia, la caché debe invalidarse explícitamente en el momento de la escritura, no esperar 15 minutos.
2. **Fire and forget (`_ = TareaAsync()`) o colas en memoria para datos críticos:** lanzar una tarea sin esperarla pierde sus excepciones y no sobrevive a un reinicio. Incluso la cola en memoria del apartado 2.3 pierde los eventos pendientes si el proceso cae; es válida para telemetría, pero guardar así un pedido o un dato que se factura es un error grave. Para esos datos se usa el patrón Outbox.
3. **Sincronización síncrona en cascada:** hacer un endpoint HTTP que guarde en SQL, luego haga un `await` para guardar en Mongo, y luego un `await` para Redis. Esto acopla temporalmente los tres sistemas; si Mongo está caído, toda la operación falla.

**Mejores Prácticas:**

1. **Idempotencia:** al diseñar procesos en segundo plano que sincronizan datos hacia NoSQL, asegúrate de que procesar el mismo mensaje dos veces no duplique la información (usando la clave de origen como `_id` y `ReplaceOneAsync` con `IsUpsert = true`).
2. **Degradación elegante (_Graceful Degradation_):** si Redis está caído, la aplicación debe capturar la excepción y consultar directamente a SQL Server, registrando un aviso, pero sin devolver un error 500 al usuario final (como hace `ProductCatalogService` en el apartado 2.3; el tema 13 amplía esta técnica).
3. **Segregación de responsabilidades:** mantén el código que interactúa con SQL estrictamente separado del que interactúa con NoSQL, preferiblemente detrás de interfaces de repositorio distintas.

## 3. Escalabilidad de Datos en Sistemas Distribuidos

### 3.1. Bases de Datos Alineadas con Bounded Contexts

A medida que un sistema crece y evoluciona hacia una arquitectura de microservicios, el patrón de "Base de Datos Compartida" (_Shared Database_) se convierte en el mayor obstáculo para la escalabilidad y la agilidad. Si múltiples servicios leen y escriben en las mismas tablas, cualquier cambio en el esquema requiere coordinación entre múltiples equipos, rompiendo la autonomía.

La solución arquitectónica es alinear las bases de datos con los Bounded Contexts (Contextos Delimitados, concepto fundamental de Domain-Driven Design). Esto significa que cada microservicio debe ser el único dueño absoluto de sus datos.

- El microservicio de Catálogo tiene su propia base de datos (quizás MongoDB).
- El microservicio de Pedidos tiene su propia base de datos (quizás SQL Server).
- Ningún otro servicio puede conectarse directamente a la base de datos de Pedidos. Si el servicio de Facturación necesita datos de un pedido, debe solicitarlos a través de la API del servicio de Pedidos o suscribirse a sus eventos de dominio.

Este aislamiento permite que cada base de datos escale de forma independiente según su carga específica, y permite a cada equipo elegir la tecnología de persistencia que mejor se adapte a su contexto (persistencia políglota a nivel de sistema).

### 3.2. Patrones de Replicación de Lectura (Read Replicas)

Incluso dentro de un único microservicio, la base de datos puede convertirse en un cuello de botella si el volumen de lecturas supera con creces al de escrituras (un escenario muy común en aplicaciones web).

El patrón de Replicación de Lectura consiste en tener un nodo principal (_Primary/Master_) que recibe todas las operaciones de escritura (`INSERT`, `UPDATE`, `DELETE`) y uno o varios nodos secundarios (_Replicas/Slaves_) que mantienen una copia asíncrona de los datos y se utilizan exclusivamente para operaciones de lectura (`SELECT`).

Para implementar esto en .NET 10 a nivel de acceso a datos, podemos crear una factoría de conexiones que enrute el tráfico según la intención de la operación.

En SQL Server con grupos de disponibilidad Always On (y en Azure SQL con escalado horizontal de lectura), las lecturas se envían a una réplica secundaria indicando `ApplicationIntent=ReadOnly` en la cadena de conexión; el servidor (el agente de escucha del grupo de disponibilidad, con el enrutamiento de solo lectura configurado) redirige esa conexión a una réplica legible. Sin ese parámetro, la conexión llega al nodo principal.

```json
{
  "ConnectionStrings": {
    "PrimaryDb": "Server=tcp:ag-listener,1433;Database=Fleet;Integrated Security=true;TrustServerCertificate=true",
    "ReplicaDb": "Server=tcp:ag-listener,1433;Database=Fleet;Integrated Security=true;TrustServerCertificate=true;ApplicationIntent=ReadOnly;MultiSubnetFailover=true"
  }
}
```

```csharp
using System.Data;
using Microsoft.Data.SqlClient;

public interface IDbConnectionFactory
{
    IDbConnection GetReadConnection();
    IDbConnection GetWriteConnection();
}

public class SqlDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _primaryConnectionString;
    private readonly string _replicaConnectionString;

    public SqlDbConnectionFactory(IConfiguration configuration)
    {
        // Cadena del nodo principal (lecturas y escrituras)
        _primaryConnectionString = configuration.GetConnectionString("PrimaryDb")
            ?? throw new InvalidOperationException("Falta 'PrimaryDb'.");

        // Cadena con ApplicationIntent=ReadOnly: se enruta a una réplica de solo lectura
        _replicaConnectionString = configuration.GetConnectionString("ReplicaDb")
            ?? throw new InvalidOperationException("Falta 'ReplicaDb'.");
    }

    public IDbConnection GetReadConnection()
    {
        // Si hay un retraso de replicación (replication lag),
        // asumimos que el negocio tolera consistencia eventual en estas lecturas.
        var connection = new SqlConnection(_replicaConnectionString);
        connection.Open();
        return connection;
    }

    public IDbConnection GetWriteConnection()
    {
        // Nodo principal: consistencia fuerte en escrituras
        var connection = new SqlConnection(_primaryConnectionString);
        connection.Open();
        return connection;
    }
}
```

La factoría no guarda estado, así que se registra como singleton: `builder.Services.AddSingleton<IDbConnectionFactory, SqlDbConnectionFactory>();`.

**Uso en un repositorio.** Cuando un repositorio necesita hacer una consulta, llama a `GetReadConnection()`; cuando necesita escribir, a `GetWriteConnection()`. La conexión la crea la factoría, pero la libera quien la usa, con `using`:

```csharp
using Dapper;

public class VehicleRepository(IDbConnectionFactory factory)
{
    public async Task<IReadOnlyList<VehicleDto>> GetByDepotAsync(int depotId)
    {
        using var connection = factory.GetReadConnection();     // réplica
        var rows = await connection.QueryAsync<VehicleDto>(
            "SELECT Id, Plate, Status FROM Vehicles WHERE DepotId = @DepotId ORDER BY Plate",
            new { DepotId = depotId });
        return rows.AsList();
    }

    public async Task<bool> SetStatusAsync(int vehicleId, string status)
    {
        using var connection = factory.GetWriteConnection();    // principal
        return await connection.ExecuteAsync(
            "UPDATE Vehicles SET Status = @Status WHERE Id = @Id",
            new { Id = vehicleId, Status = status }) == 1;
    }
}

public record VehicleDto(int Id, string Plate, string Status);
```

Así se descarga el nodo principal y las lecturas escalan añadiendo réplicas. Como la réplica recibe los cambios de forma asíncrona, una lectura inmediatamente posterior a `SetStatusAsync` puede devolver todavía el estado anterior (apartado 3.5). Por eso, las lecturas que forman parte de una escritura (comprobar un dato antes de modificarlo, leer lo que se acaba de guardar) se hacen con la conexión de escritura.

### 3.3. Principios de Particionamiento Horizontal (Sharding)

Cuando el volumen de datos o la cantidad de escrituras supera la capacidad física del servidor más grande disponible (escalabilidad vertical), se debe recurrir al Particionamiento Horizontal o Sharding.

El Sharding divide una gran base de datos en múltiples bases de datos más pequeñas (_Shards_) distribuidas en diferentes servidores. Cada Shard tiene el mismo esquema, pero contiene un subconjunto diferente de los datos.

La clave del éxito en el sharding es la elección de la **Shard Key** (Clave de Partición). Esta clave determina a qué base de datos irá a parar un registro. En aplicaciones multi-inquilino (SaaS), el `TenantId` (ID del cliente) es la Shard Key por excelencia.

A continuación, mostramos un enrutador conceptual para seleccionar la cadena de conexión correcta basándose en el `TenantId`.

```csharp
public interface IShardLocator
{
    string GetConnectionStringForTenant(string tenantId);
}

public class TenantShardLocator : IShardLocator
{
    private readonly Dictionary<string, string> _shardMap;

    public TenantShardLocator(IConfiguration configuration)
    {
        // En un entorno real, este mapa podría residir en una base de datos
        // de enrutamiento central o en un servicio de configuración distribuida.
        _shardMap = new Dictionary<string, string>
        {
            { "tenant_europe_1", configuration.GetConnectionString("ShardEU")! },
            { "tenant_europe_2", configuration.GetConnectionString("ShardEU")! },
            { "tenant_us_1", configuration.GetConnectionString("ShardUS")! },
            { "tenant_asia_1", configuration.GetConnectionString("ShardAsia")! }
        };
    }

    public string GetConnectionStringForTenant(string tenantId)
    {
        if (_shardMap.TryGetValue(tenantId, out var connectionString))
        {
            return connectionString;
        }

        // Estrategia de fallback o asignación de un nuevo shard por defecto
        throw new InvalidOperationException($"No shard configured for tenant {tenantId}");
    }
}
```

**Implementación en el acceso a datos:** el middleware de la API extrae el `TenantId` del token JWT del usuario. Este ID se pasa al repositorio, el cual utiliza el `IShardLocator` para obtener la cadena de conexión específica. De esta manera, si el cliente de Estados Unidos genera una carga masiva, no consume los recursos de la base de datos de los clientes en Europa, ya que están físicamente en servidores distintos.

### 3.4. Desacoplamiento y Evolución Independiente del Sistema

La combinación de bases de datos por Bounded Context, replicación de lectura y sharding proporciona un nivel de desacoplamiento que es el objetivo final de cualquier arquitectura distribuida moderna.

Este desacoplamiento previene las dependencias centralizadas. En un monolito con una base de datos única, si la tabla de "Usuarios" se bloquea por una consulta pesada de informes, todo el sistema (ventas, inventario, facturación) se cae.

Al distribuir los datos:

1. **Aislamiento de Fallos (_Fault Isolation_):** si el clúster NoSQL del catálogo de productos sufre una interrupción, los usuarios no podrán ver nuevos productos, pero el microservicio de Pedidos (respaldado por su propio SQL Server) seguirá procesando las compras de los productos que ya están en el carrito.
2. **Evolución Tecnológica:** un equipo puede decidir migrar su microservicio de SQL Server a PostgreSQL para reducir costes de licencias, sin tener que pedir permiso ni coordinarse con los demás equipos de la empresa, ya que nadie más accede a su base de datos.
3. **Escalado Granular:** solo se invierte dinero en escalar las bases de datos que lo necesitan.

### 3.5. Errores Comunes y Mejores Prácticas en Escalabilidad de Datos

**Errores Comunes:**

1. **Cross-Shard Joins:** intentar hacer un `JOIN` SQL entre datos que residen en diferentes Shards. Esto es imposible a nivel de motor de base de datos. Si necesitas cruzar datos, debes hacerlo en memoria en la aplicación, lo cual es lento y costoso. Por eso la elección de la Shard Key es vital (todos los datos que se consultan juntos deben vivir en el mismo Shard).
2. **Ignorar el Replication Lag:** al usar Read Replicas, hay un pequeño retraso (milisegundos o segundos) entre que se escribe en el Master y el dato llega a la réplica. Si un usuario actualiza su perfil e inmediatamente es redirigido a una vista de lectura que consulta la réplica, podría ver sus datos antiguos.
3. **El Antipatrón de la Base de Datos de Integración:** permitir que herramientas de Business Intelligence (BI) o servicios de terceros se conecten directamente a las bases de datos operacionales de los microservicios para extraer datos. Esto acopla el esquema y degrada el rendimiento.

**Mejores Prácticas:**

1. **Read-Your-Own-Writes:** para solucionar el problema del Replication Lag, implementa lógica en el frontend o en la API para que, tras una escritura, las siguientes lecturas de ese usuario específico se dirijan temporalmente al nodo Master durante unos segundos, mientras que el resto de usuarios leen de las réplicas.
2. **Data Pump / CDC para Analítica:** en lugar de permitir conexiones directas para BI, utiliza herramientas de Change Data Capture (CDC) para extraer los cambios de las bases de datos operacionales en tiempo real y volcarlos en un Data Warehouse o Data Lake centralizado, diseñado específicamente para analítica.
3. **Monitorización de Shards:** vigila de cerca el tamaño y la carga de cada Shard. Es común el problema del "Hot Tenant" (un cliente que crece desproporcionadamente y satura su Shard). Debes tener procedimientos operativos para rebalancear o mover un inquilino a un Shard dedicado si es necesario.

---

# Cuadernillos del Alumno (laboratorio)

En esta parte se aplica la teoría de la Parte I sobre el proyecto de ejemplo **B2BOrders**: un modelo de lectura con Dapper, auditoría en MongoDB y la documentación del mapa de persistencia.

---

## Cuadernillo 9.1: Read Models con Dapper

**Bloque 9: Dapper, NoSQL y persistencia combinada** · Duración: 10-15 min · Nivel: Intermedio

**Objetivo:** implementar un modelo de lectura (CQRS) ultra-rápido usando Dapper, evitando la sobrecarga de Entity Framework para consultas de listado.

### Conceptos Clave

1. **CQRS (Lite):** separar las Lecturas (Read) de las Escrituras (Write). Usamos EF Core para lógica de negocio compleja (Write) y Dapper para consultas rápidas directas a pantalla (Read).
2. **Micro-ORM (Dapper):** a diferencia de EF Core, Dapper no rastrea cambios ni maneja relaciones complejas automáticamente. Tú escribes el SQL, y Dapper rellena los objetos. Es muy rápido ("_Bare metal performance_").
3. **Proyecciones SQL:** en Dapper, si la columna de la BBDD se llama `TotalAmount_Amount` y tu clase C# `TotalAmount`, debes usar un alias en SQL (`AS TotalAmount`) para que coincidan.
4. **Paginación SQL Server:** usamos `OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY`. Requisito: siempre debe haber una cláusula `ORDER BY` antes.

### Laboratorio 1: El Modelo de Lectura (ReadModel)

Definimos un DTO plano, optimizado para lo que necesita ver el usuario en la tabla del Frontend, sin lógica de dominio.

**Archivo:** `B2BOrders.Application/Orders/ReadModels/OrderSummaryReadModel.cs`

```csharp
namespace B2BOrders.Application.Orders.ReadModels;

public class OrderSummaryReadModel
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; } // Coincidirá con el alias SQL
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
```

### Laboratorio 2: Implementación con Dapper

Creamos el repositorio que usa `SqlConnection` directamente.

**Dependencias necesarias:**

```powershell
dotnet add B2BOrders.Infrastructure package Dapper
dotnet add B2BOrders.Infrastructure package Microsoft.Data.SqlClient
```

**Archivo:** `B2BOrders.Infrastructure/Orders/DapperOrderReadRepository.cs`

```csharp
using System.Data;
using B2BOrders.Application.Orders.ReadModels;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace B2BOrders.Infrastructure.Orders;

public class DapperOrderReadRepository : IOrderReadRepository
{
    private readonly string _connectionString;

    public DapperOrderReadRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found.");
    }

    public async Task<IEnumerable<OrderSummaryReadModel>> GetPageAsync(int page, int pageSize)
    {
        using var connection = new SqlConnection(_connectionString);

        // NOTA CLAVE SOBRE SQL:
        // 1. Usamos 'AS TotalAmount' para mapear la columna de EF Core (con guion bajo) a nuestra propiedad C#.
        // 2. Simulamos CustomerName con CONCAT porque no tenemos tabla Customers en este lab.
        // 3. Status se convierte de int a texto con CASE.
        var sql = @"
            SELECT
                o.Id AS OrderId,
                CONCAT('Cliente ', o.CustomerId) AS CustomerName,
                o.TotalAmount_Amount AS TotalAmount,
                CASE o.Status
                    WHEN 0 THEN 'Pending'
                    WHEN 1 THEN 'Paid'
                    WHEN 2 THEN 'Shipped'
                    ELSE 'Unknown'
                END AS Status,
                o.CreatedAt
            FROM Orders o
            WHERE o.IsDeleted = 0
            ORDER BY o.CreatedAt DESC
            OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

        // Parámetros anónimos de Dapper
        var parameters = new
        {
            Skip = (page - 1) * pageSize,
            Take = pageSize // El nombre debe coincidir EXACTO con la variable @Take del SQL
        };

        return await connection.QueryAsync<OrderSummaryReadModel>(sql, parameters);
    }
}
```

### Laboratorio 3: El Endpoint

Exponemos la consulta. Fíjate que inyectamos `IOrderReadRepository`, desacoplando completamente el endpoint de la tecnología de base de datos.

**Archivo:** `B2BOrders.Api/OrdersEndpoints.cs`

```csharp
app.MapGet("/orders/summary", async (
    int? page,
    int? pageSize,
    IOrderReadRepository repository) =>
{
    var result = await repository.GetPageAsync(page ?? 1, pageSize ?? 20);
    return Results.Ok(result);
});
```

### Guía de Resolución de Problemas (Troubleshooting)

Errores reales que enfrentamos durante la implementación y sus soluciones:

| Error / Síntoma                                       | Causa                                                                                              | Solución                                                                                     |
| ----------------------------------------------------- | -------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| `Must declare the scalar variable "@Take"`            | En el objeto anónimo de parámetros pusimos `PageSize = pageSize` en lugar de `Take = pageSize`.    | El nombre de la propiedad C# debe ser idéntico al parámetro SQL (`@Take` -> `Take`).         |
| `Invalid column name 'TotalAmount'`                   | La base de datos tiene `TotalAmount_Amount` (convención EF Core), pero el SQL pedía `TotalAmount`. | Usar alias en SQL: `o.TotalAmount_Amount AS TotalAmount`.                                    |
| `Incorrect syntax near 'o'` / `Invalid usage of NEXT` | Un comentario SQL (`--`) ocultó accidentalmente la línea `ORDER BY` al concatenar el string.       | SQL Server exige `ORDER BY` para usar `OFFSET`. Asegurarse de que el string SQL esté limpio. |
| Violación de acceso (No connection string)            | Olvidamos registrar la cadena de conexión o leerla de `IConfiguration`.                            | Verificar `appsettings.json` y la inyección en el constructor.                               |

### Siguiente Paso: Vídeo 9.2

Ahora que sabemos leer rápido con SQL, vamos a conectar una segunda base de datos (NoSQL) para guardar logs de auditoría sin ensuciar nuestro SQL Server transaccional. ¡MongoDB/CosmosDB nos espera!

---

## Cuadernillo 9.2: Auditoría con NoSQL (MongoDB)

**Bloque 9: Dapper, NoSQL y persistencia combinada** · Duración: 10-15 min · Nivel: Intermedio

**Objetivo:** implementar un sistema de almacenamiento híbrido (SQL + NoSQL) para guardar logs de auditoría masivos sin afectar el rendimiento de la base de datos transaccional.

### Conceptos Clave

1. **Persistencia Políglota:** es la práctica de utilizar diferentes tecnologías de almacenamiento de datos para manejar distintas necesidades dentro de un mismo sistema.
   - **SQL Server:** para datos con integridad referencial estricta (Pedidos, Pagos).
   - **MongoDB:** para datos masivos, no estructurados o de escritura rápida (Logs, Auditoría, Analítica).
2. **Documentos vs Tablas:** en MongoDB no hay tablas ni esquemas fijos. Guardamos documentos BSON (formato binario de JSON). Esto permite guardar objetos dinámicos (como la propiedad `Data`) sin hacer migraciones.
3. **`MongoClient`:** el driver oficial de C# gestiona el pool de conexiones automáticamente. Se suele registrar como Singleton.
4. **Inyección en Minimal APIs:** para usar un nuevo servicio en un endpoint, no basta con registrarlo en el contenedor; debemos añadirlo explícitamente a los parámetros de la lambda (`..., IAuditService auditService`).

### Laboratorio 1: Infraestructura (Docker)

Levantamos una instancia de MongoDB aislada.

**Comando (PowerShell/Terminal):**

```powershell
docker run -d -p 27017:27017 --name mongo_audit mongo:latest
```

**Paquete NuGet:**

```powershell
dotnet add B2BOrders.Infrastructure package MongoDB.Driver
```

### Laboratorio 2: El Modelo Documental

Definimos la estructura del log. Usamos atributos `[Bson...]` para controlar la serialización.

**Archivo:** `B2BOrders.Infrastructure/Audit/AuditLogDocument.cs`

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace B2BOrders.Infrastructure.Audit;

public class AuditLogDocument
{
    [BsonId] // Indica que es la Clave Primaria de Mongo
    [BsonRepresentation(BsonType.ObjectId)] // Convierte automáticamente string <-> ObjectId
    public string? Id { get; set; }

    public string Action { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; }

    // Guardamos cualquier objeto (el payload del log)
    public object? Data { get; set; }
}
```

### Laboratorio 3: El Servicio de Auditoría

Implementamos la lógica de conexión e inserción.

**Archivo:** `B2BOrders.Infrastructure/Audit/MongoAuditService.cs`

```csharp
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace B2BOrders.Infrastructure.Audit;

public interface IAuditService
{
    Task LogAsync(string action, string user, object data);
}

public class MongoAuditService : IAuditService
{
    private readonly IMongoCollection<AuditLogDocument> _auditCollection;

    public MongoAuditService(IConfiguration configuration)
    {
        // Leemos la connection string de appsettings.json
        var connectionString = configuration.GetConnectionString("MongoConnection");

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase("B2BOrders_Audit");

        // Obtenemos (o creamos si no existe) la colección
        _auditCollection = database.GetCollection<AuditLogDocument>("AuditLogs");
    }

    public async Task LogAsync(string action, string user, object data)
    {
        var log = new AuditLogDocument
        {
            Action = action,
            User = user,
            TimeStamp = DateTime.UtcNow,
            Data = data
        };

        await _auditCollection.InsertOneAsync(log);
    }
}
```

### Laboratorio 4: Configuración e Integración

**Paso crítico:** registrar el servicio y configurar la cadena de conexión.

**1. Configuración (`appsettings.json`):**

```jsonc
"ConnectionStrings": {
    "DefaultConnection": "Server=localhost;... (SQL) ...",
    "MongoConnection": "mongodb://localhost:27017" // NUEVA
}
```

**2. Inyección (`Program.cs`):**

```csharp
builder.Services.AddSingleton<IAuditService, MongoAuditService>();
```

**3. Uso en el Endpoint (`OrdersEndpoints.cs`):**

```csharp
app.MapGet("/orders/summary", async (
    int? page,
    int? pageSize,
    IOrderReadRepository repository,
    IAuditService auditService) => // Inyección explícita
{
    var result = await repository.GetPageAsync(page ?? 1, pageSize ?? 20);

    // Guardamos en Mongo
    await auditService.LogAsync(
        action: "GetOrdersSummary",
        user: "user",
        data: new { Page = page, PageSize = pageSize, Count = result.Count() });

    return Results.Ok(result);
});
```

### Guía de Resolución de Problemas (Troubleshooting)

Errores que resolvimos durante la sesión:

| Error / Síntoma                                  | Causa                                                                                              | Solución                                                                                |
| ------------------------------------------------ | -------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| `CS0103: The name 'auditService' does not exist` | Se usó la variable `auditService` dentro del endpoint sin declararla en los parámetros del método. | Añadir `IAuditService auditService` en la definición de la lambda del `MapGet`.         |
| `ArgumentNullException` (Value cannot be null)   | `configuration.GetConnectionString("MongoConnection")` devolvió null.                              | Añadir la clave `"MongoConnection": "mongodb://localhost:27017"` en `appsettings.json`. |
| Warnings `CS8618` (Non-nullable property...)     | Propiedades del documento declaradas como `string` sin inicializar.                                | Inicializar con `string.Empty` o marcarlas como nullable (`string?`).                   |
| `MongoTimeoutException`                          | El contenedor Docker no está corriendo o el puerto está bloqueado.                                 | Ejecutar `docker ps` y verificar que el puerto 27017 está mapeado.                      |

### Resumen

Ya tenemos un sistema híbrido SQL + NoSQL.

---

## Cuadernillo 9.3: Documentar el Mapa de Persistencia

**Bloque 9: Dapper, NoSQL y persistencia combinada** · Duración: 5-10 min · Nivel: Arquitecto / Tech Lead

**Objetivo:** crear una referencia única ("_Source of Truth_") que explique qué tecnologías de datos se usan en el proyecto y por qué, evitando la deuda técnica y el caos arquitectónico.

### Conceptos Clave

1. **Persistencia Políglota (_Polyglot Persistence_):** el uso estratégico de múltiples tecnologías de almacenamiento (SQL, NoSQL, Cache, Search Engine) en un mismo sistema para aprovechar las fortalezas de cada una.
2. **Data Spaghetti:** el anti-patrón que ocurre cuando mezclamos tecnologías sin criterio, creando un sistema donde nadie sabe dónde están los datos ni cómo acceder a ellos correctamente.
3. **Documentación como Código (_Docs-as-Code_):** la práctica de guardar la documentación técnica en el mismo repositorio que el código (en archivos Markdown), versionada con Git. Esto asegura que la documentación evolucione junto con el software.
4. **Justificación Técnica:** no basta con decir "Usamos MongoDB". Hay que explicar por qué (volumen, esquema flexible, velocidad de escritura) para detener debates futuros innecesarios.

### Laboratorio: Creación del Mapa

Vamos a crear un documento que servirá de guía para cualquier desarrollador que entre al equipo.

#### Paso 1: Estructura de carpetas

En la raíz de tu solución (al nivel del `.sln`), crea una carpeta llamada `docs`.

#### Paso 2: El archivo Markdown

Crea un archivo llamado `persistence-map.md` dentro de `docs` y pega el siguiente contenido. Siente la libertad de adaptarlo si has hecho cambios propios.

**Archivo:** `docs/persistence-map.md`

```markdown
# Mapa de Persistencia - B2BOrders

Este documento define la estrategia de acceso a datos del servicio B2BOrders.
Cualquier cambio en la tecnología de base de datos debe reflejarse aquí.

## Estrategia General

Usamos una arquitectura de **Persistencia Políglota** para optimizar cada caso de uso según sus necesidades críticas (Integridad vs. Velocidad vs. Volumen).

1. **Transaccional (Write):** Prioridad a la integridad referencial y ACID.
2. **Consultas (Read):** Prioridad al rendimiento de lectura y bajo overhead.
3. **Histórico (Audit):** Prioridad a la velocidad de escritura masiva y flexibilidad de esquema.

## Tabla de Mapeo Tecnológico

| Dominio / Módulo  | Almacenamiento | Tecnología de Acceso | Patrón                    | Justificación                                                                                                  |
| :---------------- | :------------- | :------------------- | :------------------------ | :------------------------------------------------------------------------------------------------------------- |
| **Orders (Core)** | SQL Server     | **EF Core**          | Repository / Domain Model | Requiere validaciones de negocio complejas, Value Objects (Money) y consistencia transaccional fuerte.         |
| **Customers**     | SQL Server     | **EF Core**          | Repository                | Relacional estricto. Integridad referencial con Pedidos.                                                       |
| **Order Reports** | SQL Server     | **Dapper**           | Read Model / SQL Directo  | Necesitamos consultas ultra-rápidas para listados (Grids/Tablas). EF Core introduce overhead innecesario aquí. |
| **Audit Logs**    | **MongoDB**    | **Mongo Driver**     | Fire-and-forget           | Datos masivos, inmutables y con estructura variable (JSON). No requiere JOINs ni esquemas fijos.               |

## Notas de Implementación

- **Conexiones:** Las cadenas de conexión para SQL (`DefaultConnection`) y Mongo (`MongoConnection`) están separadas en `appsettings.json`.
- **Testing:** Los tests de integración usan un contenedor SQL Server dedicado (Puerto 1435) y Mongo (Puerto 27017).
```

### Reflexión: ¿Para qué sirve esto?

Tener este documento ahorra horas de reuniones y explicaciones:

1. **Onboarding:** un nuevo desarrollador lee esto en 2 minutos y entiende que no debe usar EF Core para los Logs ni Dapper para guardar pedidos complejos.
2. **Code Reviews:** te da autoridad para rechazar Pull Requests que violen la arquitectura (ej: "Estás intentando leer reportes con EF Core, por favor usa Dapper según el mapa de persistencia").
3. **Mantenimiento:** ayuda a los DBAs y DevOps a saber qué infraestructuras son críticas y cómo escalarlas independientemente.
