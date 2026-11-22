# Tema 6 Comunicación HTTP Avanzada\*\*

## Índice

**Manual I: Contratos HTTP y Comparativa de Paradigmas de Comunicación**

1. Fundamentos de Contratos HTTP Estables en .NET 10
2. Modelado Estructurado de Errores y Versionado de APIs
3. Comparativa Conceptual de Paradigmas de Comunicación
4. Criterios de Selección y Topología del Sistema
5. El Lado Cliente: Consumir Servicios HTTP y gRPC desde .NET 10

**Cuadernillos del Alumno (laboratorio)**

- 6.1 Configuración Avanzada y Contratos
- 6.2 Resiliencia con Polly
- 6.3 Observabilidad y Contratos

---

# Manual I: Contratos HTTP y Comparativa de Paradigmas de Comunicación

## 1. Fundamentos de Contratos HTTP Estables en .NET 10

### 1.1. La Importancia de la Coherencia en Sistemas Distribuidos

En una arquitectura distribuida moderna, los servicios rara vez operan de forma aislada. La comunicación entre ellos, así como la comunicación con clientes externos (aplicaciones web, aplicaciones para móvil o integradores de terceros), depende de contratos estrictos y predecibles. Un contrato HTTP no es más que un acuerdo implícito o explícito sobre cómo se estructuran las peticiones y las respuestas, qué cabeceras son obligatorias, qué formato tienen los cuerpos de los mensajes y, fundamentalmente, qué significan los códigos de estado devueltos.

La falta de coherencia en estos contratos es una de las principales fuentes de errores en producción. Si un servicio devuelve un código `200 OK` junto con un mensaje de error en el cuerpo del JSON para indicar que una validación ha fallado, está rompiendo la semántica del protocolo HTTP. Esto provoca que los mecanismos de resiliencia (como reintentos automáticos o cortocircuitos) y las herramientas de observabilidad (que se estudiarán en detalle en el tema 11) interpreten erróneamente que la operación ha sido exitosa, enmascarando fallos críticos y dificultando enormemente la resolución de problemas.

Elaborar contratos estables implica diseñar APIs que sean intuitivas para los desarrolladores que las consumen, que respeten los estándares de la industria y que puedan evolucionar en el tiempo sin romper las integraciones existentes. En el ecosistema de .NET 10 disponemos de herramientas integradas para garantizar esta coherencia desde el primer momento: resultados tipados (`TypedResults`), el formato estándar de errores `ProblemDetails` y, como novedad de ASP.NET Core 10, validación integrada para Minimal APIs.

### 1.2. Uso Semántico y Correcto de los Códigos de Estado HTTP

El protocolo HTTP define un conjunto rico de códigos de estado agrupados en cinco categorías. Utilizar el código exacto para cada situación es el primer paso hacia un contrato robusto. A continuación, se detallan los códigos más relevantes y su aplicación estricta en el diseño de APIs:

**Respuestas Satisfactorias (2xx):**

- **200 OK:** la petición se ha procesado correctamente y la respuesta incluye un cuerpo con los datos solicitados. Es el estándar para peticiones `GET` exitosas o `PUT`/`PATCH` que devuelven el recurso actualizado.
- **201 Created:** la petición ha tenido éxito y ha creado uno o más recursos nuevos. Es la respuesta recomendada cuando un `POST` crea un recurso, y debe incluir una cabecera `Location` con la URI del nuevo recurso. No es obligatoria para todo `POST`: un `POST` que ejecuta una acción sin crear un recurso direccionable puede responder `200 OK` (con cuerpo) o `204 No Content`, y uno que deja el trabajo en marcha, `202 Accepted`.
- **202 Accepted:** la petición ha sido aceptada para procesamiento, pero este no ha concluido. Es fundamental en operaciones asíncronas (cuyos patrones de mensajería se verán en el tema 7).
- **204 No Content:** la petición se ha procesado con éxito, pero no hay contenido que devolver en el cuerpo. Es el estándar ideal para operaciones `DELETE` o para `PUT` cuando no se devuelve el estado actualizado.

**Errores del Cliente (4xx):**

- **400 Bad Request:** la petición está mal formada, falta información obligatoria o no supera las reglas de validación del dominio.
- **401 Unauthorized:** el cliente no ha proporcionado credenciales válidas para la operación. Se refiere a la autenticación (quién eres).
- **403 Forbidden:** el cliente está autenticado, pero no tiene los permisos necesarios para acceder al recurso. Se refiere a la autorización (qué puedes hacer).
- **404 Not Found:** el recurso solicitado no existe. RFC 9110 permite también devolver `404` en lugar de `403` cuando el servidor no quiere revelar que el recurso existe; si la API adopta esa política, debe aplicarla de forma coherente y documentarla en el contrato. Sin esa decisión explícita, un usuario autenticado sin permisos recibe `403`.
- **409 Conflict:** la petición no se puede procesar debido a un conflicto con el estado actual del recurso (por ejemplo, intentar crear un usuario con un correo electrónico que ya existe o un problema de concurrencia optimista).
- **422 Unprocessable Entity:** aunque la sintaxis de la petición es correcta (es un JSON válido), la semántica es errónea (por ejemplo, reglas de negocio complejas que no se cumplen). En muchas APIs modernas, el `400` ha absorbido este caso de uso, pero el `422` es más preciso semánticamente.

**Errores del Servidor (5xx):**

- **500 Internal Server Error:** un error genérico e inesperado en el servidor. Nunca debe usarse para errores de validación del cliente.
- **502 Bad Gateway:** el servidor, actuando como puerta de enlace o proxy, recibió una respuesta inválida del servidor upstream.
- **504 Gateway Timeout:** el servidor, actuando como puerta de enlace o intermediario, no recibió a tiempo la respuesta del servicio del que depende.
- **503 Service Unavailable:** el servidor no está listo para manejar la petición, generalmente por sobrecarga o mantenimiento. Puede acompañarse de la cabecera `Retry-After` y es una señal para que los clientes reintenten más tarde (los patrones de reintento se estudian en el tema 13).

### 1.3. Ejemplo Práctico: Implementación de Respuestas Estandarizadas en ASP.NET Core 10

En este ejemplo, construiremos un endpoint con Minimal APIs de .NET 10 que usa los códigos de estado de forma semántica al crear un recurso (un pedido). Tres piezas lo hacen posible:

- **Validación integrada (ASP.NET Core 10).** Tras `builder.Services.AddValidation()`, los parámetros de los endpoints cuyos tipos llevan atributos de `System.ComponentModel.DataAnnotations` (`[Required]`, `[Range]`, `[StringLength]`...) se validan antes de ejecutar el endpoint. Si no son válidos, el endpoint no llega a ejecutarse y se responde `400 Bad Request` con un cuerpo `HttpValidationProblemDetails` (un `ProblemDetails` con la propiedad `errors`, que agrupa los mensajes por campo). Un endpoint concreto puede excluirse con `.DisableValidation()`.
- **`TypedResults`.** Devuelve resultados con tipo (`Created<T>`, `Ok<T>`, `ProblemHttpResult`...), que además documentan automáticamente las respuestas en el documento OpenAPI. `TypedResults.Created(uri, valor)` responde `201` con la cabecera `Location`; `TypedResults.Problem(...)` responde con un cuerpo `ProblemDetails` y el código que se indique.
- **`Results<T1, T2>`.** Tipo de retorno que declara las respuestas posibles de un endpoint.

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddSingleton<IOrderService, InMemoryOrderService>();

var app = builder.Build();

app.MapPost("/api/orders", async Task<Results<Created<OrderResponse>, ProblemHttpResult>> (
    CreateOrderRequest request, IOrderService orderService) =>
{
    // 1. La validación de entrada (400) ya la ha hecho AddValidation

    // 2. Regla de negocio que depende del estado actual: 409 Conflict
    if (await orderService.HasPendingOrdersAsync(request.CustomerId!))
    {
        return TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Pedido en curso",
            detail: $"El cliente {request.CustomerId} ya tiene un pedido en curso.");
    }

    // 3. Creación correcta: 201 Created con Location y el recurso en el cuerpo
    var orderId = await orderService.CreateOrderAsync(request);
    return TypedResults.Created($"/api/orders/{orderId}",
        new OrderResponse(orderId, request.CustomerId!, "Created"));
});

app.Run();

public class CreateOrderRequest
{
    [Required]
    public string? CustomerId { get; set; }

    [Range(0.01, 100000)]
    public decimal TotalAmount { get; set; }
}

public record OrderResponse(Guid Id, string CustomerId, string Status);

public interface IOrderService
{
    Task<bool> HasPendingOrdersAsync(string customerId);
    Task<Guid> CreateOrderAsync(CreateOrderRequest request);
}

public class InMemoryOrderService : IOrderService
{
    private readonly HashSet<string> _conPedido = [];
    public Task<bool> HasPendingOrdersAsync(string customerId) =>
        Task.FromResult(_conPedido.Contains(customerId));
    public Task<Guid> CreateOrderAsync(CreateOrderRequest request)
    {
        _conPedido.Add(request.CustomerId!);
        return Task.FromResult(Guid.NewGuid());
    }
}
```

El contrato HTTP queda explícito. Si el cliente envía un cuerpo sin `customerId`, recibe un `400` con el detalle de los campos erróneos. Si viola una regla que depende del estado actual, recibe un `409` con un `ProblemDetails`. Si tiene éxito, recibe un `201 Created` con la ruta exacta del recurso recién creado. Como el lambda es asíncrono y devuelve dos tipos de resultado distintos, su tipo de retorno se declara explícitamente delante de la lista de parámetros.

## 2. Modelado Estructurado de Errores y Versionado de APIs

### 2.1. Estandarización de Errores con ProblemDetails (RFC 9457)

Devolver el código de estado HTTP correcto es fundamental, pero a menudo insuficiente. El cliente necesita saber por qué se produjo un error `400` o `409`. Históricamente, cada equipo de desarrollo inventaba su propio formato JSON para los errores, lo que obligaba a los consumidores de la API a escribir código de análisis (parsing) personalizado para cada servicio con el que se integraban.

Para solucionar esta fragmentación, el IETF publicó el estándar RFC 9457 (que reemplaza al anterior RFC 7807), conocido como "Problem Details for HTTP APIs". Este estándar define un formato JSON unificado para devolver información sobre errores. ASP.NET Core lo soporta de forma integrada: `AddProblemDetails()` registra el servicio `IProblemDetailsService`, que escribe estas respuestas con el tipo de contenido `application/problem+json` y añade automáticamente la extensión `traceId`.

Un objeto `ProblemDetails` estándar contiene las siguientes propiedades principales:

- **type:** una URI que identifica el tipo de problema. Proporciona documentación legible por humanos sobre el error.
- **title:** un resumen corto y legible del problema (por ejemplo, "One or more validation errors occurred.").
- **status:** el código de estado HTTP (repite el código de la respuesta para que la información sea autocontenida).
- **detail:** una explicación legible y específica de esta ocurrencia concreta del problema.
- **instance:** una URI que identifica la ocurrencia específica del problema, útil para correlacionar con los logs del servidor.

Adoptar `ProblemDetails` garantiza que cualquier cliente, ya sea una aplicación web en React, una aplicación para móvil en .NET MAUI o un servicio de backend en Go, sepa exactamente cómo deserializar e interpretar los errores de nuestra API sin necesidad de documentación adicional sobre el formato del error.

### 2.2. Ejemplo Práctico: Configuración Global de ProblemDetails en .NET 10

La forma recomendada de convertir las excepciones no controladas en respuestas `ProblemDetails` es implementar la interfaz `IExceptionHandler` (disponible desde .NET 8) y escribir la respuesta con `IProblemDetailsService`, para que todas las respuestas de error del servicio compartan formato.

> **Regla de seguridad importante:** en los errores 500 nunca se devuelve `exception.Message`, porque puede filtrar detalles internos (nombres de tablas, rutas, cadenas de conexión). El cliente recibe un mensaje genérico y el `traceId`, y el detalle queda en los logs.

```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// 1. Servicio de ProblemDetails (con una extensión común a todas las respuestas)
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["servicio"] = "productos");

// 2. Manejador de excepciones propio
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// 3. Middleware de manejo de excepciones
app.UseExceptionHandler();

app.MapGet("/api/products/{id}", (int id) =>
{
    if (id <= 0)
        throw new InvalidDomainOperationException("El identificador del producto debe ser mayor que cero.");
    return TypedResults.Ok(new { Id = id, Name = "Producto de prueba" });
});

app.Run();

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            // Excepción de dominio: su mensaje está pensado para el cliente
            InvalidDomainOperationException e =>
                (StatusCodes.Status400BadRequest, "Regla de negocio violada", e.Message),
            // Cualquier otra: mensaje genérico, sin detalles internos
            _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor",
                  "Se ha producido un error inesperado. Indique el traceId al soporte.")
        };

        if (status >= 500)
            logger.LogError(exception, "Error no controlado en {Ruta}", httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = status, Title = title, Detail = detail }
        });
    }
}

public class InvalidDomainOperationException(string message) : Exception(message);
```

**Claves del ejemplo:**

- `TryHandleAsync` devuelve `true` si ha escrito la respuesta; si devuelve `false`, el middleware pasa al siguiente manejador registrado o a su respuesta por defecto. Se pueden registrar varios manejadores con `AddExceptionHandler<T>()`; se ejecutan en el orden de registro.
- `IProblemDetailsService.TryWriteAsync` escribe el `ProblemDetails` con `Content-Type: application/problem+json`, completa `type` con el enlace al RFC correspondiente al código si no se indica, añade `traceId` y aplica `CustomizeProblemDetails`.
- **Novedad de .NET 10:** cuando un `IExceptionHandler` devuelve `true`, el middleware ya no registra la excepción en los logs ni en las métricas por defecto. Por eso el manejador registra él mismo los errores 500. Para recuperar el comportamiento anterior existe la opción `ExceptionHandlerOptions.SuppressDiagnosticsCallback`.

Si un cliente solicita `/api/products/0`, recibe un `400` con un JSON como este:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Regla de negocio violada",
  "status": 400,
  "detail": "El identificador del producto debe ser mayor que cero.",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
  "servicio": "productos"
}
```

### 2.3. Estrategias de Versionado para Evolución sin Rupturas

Los contratos HTTP no son estáticos; evolucionan a medida que cambian los requisitos del negocio. Sin embargo, modificar un contrato existente (por ejemplo, eliminar un campo, cambiar su tipo de datos o requerir un nuevo parámetro) romperá las aplicaciones cliente que dependen de la estructura anterior. Para evitar esto, es imperativo implementar una estrategia de versionado desde el día cero.

Existen cuatro estrategias principales para versionar una API HTTP:

1. **Versionado por URI (URL Path):**
   - Ejemplo: `https://api.example.com/v1/clientes`
   - Ventajas: es la más explícita y fácil de entender. Se puede probar directamente en el navegador. El enrutamiento es trivial.
   - Desventajas: rompe el principio REST de que una URI identifica un recurso único (el cliente es el mismo, independientemente de la versión de la representación).
2. **Versionado por Cabecera (Header):**
   - Ejemplo: petición a `https://api.example.com/clientes` con la cabecera `X-Api-Version: 1.0`
   - Ventajas: mantiene las URIs limpias y semánticamente correctas.
   - Desventajas: es más difícil de probar manualmente (requiere herramientas como Postman o curl).
3. **Versionado por Cadena de Consulta (Query String):**
   - Ejemplo: `https://api.example.com/clientes?api-version=1.0`
   - Ventajas: fácil de probar en el navegador, mantiene la URI base intacta.
   - Desventajas: puede interferir con los parámetros de filtrado o paginación, ensuciando la URL.
4. **Versionado por Media Type (Accept Header / Content Negotiation):**
   - Ejemplo: petición con cabecera `Accept: application/vnd.empresa.clientes.v1+json`
   - Ventajas: es la aproximación más purista y alineada con los principios REST originales (HATEOAS).
   - Desventajas: es la más compleja de implementar y de consumir por parte de los clientes.

El versionado por URI es el más extendido por su pragmatismo; el versionado por cabecera es habitual cuando se quiere mantener estables las URIs.

### 2.4. Versionado en la Práctica con Asp.Versioning

La configuración completa de la biblioteca Asp.Versioning (paquetes `Asp.Versioning.Http` y `Asp.Versioning.Mvc.ApiExplorer`, un único `NewVersionedApi` con un grupo por versión, versiones obsoletas y cabeceras de versiones) se estudió en el tema 5. Aquí basta recordar cómo se combinan varias estrategias: el lector de versión decide de dónde se lee, y la plantilla de ruta decide si el segmento existe.

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new HeaderApiVersionReader("X-Api-Version"),
        new QueryStringApiVersionReader("api-version"));
});

// ...
var users = app.NewVersionedApi("Users");
var v1 = users.MapGroup("/api/users").HasApiVersion(1.0);
var v2 = users.MapGroup("/api/users").HasApiVersion(2.0);

v1.MapGet("/{id:int}", (int id) => TypedResults.Ok(new { Id = id, FullName = "Juan Pérez" }));
v2.MapGet("/{id:int}", (int id) => TypedResults.Ok(new { Id = id, FirstName = "Juan", LastName = "Pérez" }));
```

Con esta configuración, `GET /api/users/123` con la cabecera `X-Api-Version: 2.0` (o con `?api-version=2.0`) obtiene la versión 2; sin cabecera ni parámetro se asume la 1.0. Separar `FullName` en dos campos es un cambio incompatible, por eso exige una versión nueva en lugar de modificar la 1.0.

## 3. Comparativa Conceptual de Paradigmas de Comunicación

Hasta ahora hemos asumido el uso de REST sobre HTTP como mecanismo por defecto. Sin embargo, en arquitecturas distribuidas modernas, REST es solo una de las herramientas disponibles. La elección del paradigma de comunicación adecuado es una decisión arquitectónica crítica que afecta al rendimiento, la experiencia del desarrollador y la escalabilidad del sistema. Analizaremos los tres paradigmas dominantes: REST, GraphQL y gRPC.

### 3.1. REST: Flexibilidad y Adopción Universal

REST (Representational State Transfer) es un estilo arquitectónico basado en los estándares de la web. Utiliza los métodos HTTP (`GET`, `POST`, `PUT`, `DELETE`) para operar sobre recursos identificados por URIs.

**Características principales:**

- **Orientado a recursos:** todo es un sustantivo (ej. `/clientes`, `/facturas`).
- **Sin estado (Stateless):** cada petición contiene toda la información necesaria para ser procesada.
- **Cachable:** las respuestas pueden ser almacenadas en caché por intermediarios (proxies, CDNs) utilizando cabeceras HTTP estándar (`Cache-Control`).

**Ventajas:**

- **Adopción universal:** cualquier lenguaje o plataforma puede consumir una API REST.
- Excelente para integraciones públicas y APIs expuestas a terceros.
- Fácil de depurar utilizando herramientas estándar del navegador.

**Limitaciones:**

- **Over-fetching (sobrecarga de datos):** el servidor define la estructura de la respuesta. Si un cliente móvil solo necesita el nombre del usuario, pero el endpoint `/users/1` devuelve 50 campos, se desperdicia ancho de banda.
- **Under-fetching (falta de datos):** si el cliente necesita el usuario y sus últimos 5 pedidos, a menudo tiene que hacer múltiples peticiones HTTP (`/users/1` y luego `/users/1/orders`), aumentando la latencia.

### 3.2. GraphQL: Consultas Expresivas y Optimización de Payloads

GraphQL, desarrollado originalmente por Facebook, no es un estilo arquitectónico, sino un lenguaje de consulta para APIs y un entorno de ejecución para esas consultas. A diferencia de REST, donde hay múltiples endpoints, GraphQL expone un único endpoint (generalmente `/graphql`) a través del cual los clientes envían consultas detallando exactamente qué datos necesitan.

**Características principales:**

- **Tipado fuerte:** el servidor define un esquema estricto (Schema) de los tipos de datos disponibles y sus relaciones.
- **El cliente tiene el control:** el cliente especifica la forma de la respuesta.

**Ventajas:**

- **Reduce el Over-fetching y el Under-fetching:** el cliente pide exactamente los campos que necesita. Esto es crítico en aplicaciones para móvil con conexiones de red inestables o limitadas.
- **Agregación eficiente:** permite obtener datos de múltiples entidades relacionadas en una sola petición HTTP.
- **Evolución sin versionado:** se pueden añadir nuevos campos al esquema sin afectar a las consultas existentes. Los campos antiguos se pueden marcar como obsoletos (`@deprecated`).

**Limitaciones:**

- **Complejidad en el servidor:** resolver consultas anidadas complejas puede provocar problemas de rendimiento (el famoso problema N+1 en acceso a datos).
- **Caché compleja:** al usar un único endpoint y peticiones `POST`, el almacenamiento en caché a nivel de red (CDNs) es muy difícil de implementar en comparación con REST.

### 3.3. gRPC: Alto Rendimiento y Contratos Estrictos con Protobuf

gRPC (gRPC Remote Procedure Calls) es un framework de llamadas a procedimientos remotos creado por Google. A diferencia de REST y GraphQL, que suelen usar JSON sobre HTTP/1.1, gRPC utiliza Protocol Buffers (Protobuf) como lenguaje de definición de interfaz (IDL) y formato de serialización binaria, y funciona sobre HTTP/2 (en .NET también puede usar HTTP/3).

**Características principales:**

- **Contratos estrictos (Protobuf):** el contrato se define en un archivo `.proto`. A partir de este archivo, se genera automáticamente el código cliente y servidor en múltiples lenguajes (C#, Java, Go, Python).
- **Serialización binaria:** los mensajes se codifican en un formato binario compacto, que en general ocupa menos y se procesa más deprisa que el JSON equivalente.
- **HTTP/2 nativo:** soporta multiplexación (múltiples peticiones simultáneas sobre una sola conexión TCP) y streaming bidireccional.

**Ventajas:**

- **Rendimiento:** menor coste de serialización y deserialización que JSON y conexiones HTTP/2 multiplexadas.
- **Seguridad de tipos:** cliente y servidor se generan desde el mismo `.proto`, así que las discrepancias de tipos se detectan al compilar. Aun así, el `.proto` también se versiona: no se reutilizan ni cambian los números de campo existentes.
- **Generación de código:** elimina la necesidad de escribir clientes HTTP manuales.

**Limitaciones:**

- **Legibilidad:** el tráfico de red es binario, lo que dificulta la depuración sin herramientas específicas.
- **Compatibilidad con navegadores:** los navegadores web no exponen el control total sobre HTTP/2 necesario para gRPC puro, requiriendo proxies intermediarios (como gRPC-Web) para su uso desde aplicaciones frontend.

### 3.4. Ejemplo Práctico: Comparativa de Implementación de un Endpoint de Lectura

Para ilustrar las diferencias, veamos cómo se implementaría la obtención del perfil de un cliente en los tres paradigmas dentro del ecosistema .NET 10.

#### 1. Implementación en REST (Minimal APIs)

El enfoque REST es directo y se basa en la URI para identificar el recurso.

```csharp
// REST: El servidor decide qué datos devolver
app.MapGet("/api/customers/{id}", (int id) =>
{
    // Se devuelve el objeto completo serializado a JSON
    return Results.Ok(new Customer
    {
        Id = id,
        Name = "Empresa S.A.",
        Email = "contacto@example.com",
        Address = "Calle Principal 123"
    });
});
```

Respuesta típica (JSON): ocupa más ancho de banda si el cliente solo quería el nombre.

#### 2. Implementación en GraphQL (usando Hot Chocolate en .NET)

La biblioteca más usada en .NET para servidores GraphQL es Hot Chocolate, de ChilliCream (paquete NuGet `HotChocolate.AspNetCore`). Primero definimos la consulta; el cliente enviará un documento indicando qué campos desea.

```csharp
// GraphQL: Definición del tipo y la consulta
public class Query
{
    public Customer GetCustomer(int id) => new Customer
    {
        Id = id,
        Name = "Empresa S.A.",
        Email = "contacto@example.com",
        Address = "Calle Principal 123"
    };
}

// Configuración en Program.cs (paquete HotChocolate.AspNetCore)
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>();

app.MapGraphQL();
```

Petición del cliente:

```graphql
query {
  customer(id: 1) {
    name
    email
  }
}
```

Respuesta: el servidor procesa la consulta y devuelve únicamente `name` y `email`. Hot Chocolate expone el método `GetCustomer` como el campo `customer` (quita el prefijo `Get` y usa camelCase). Por defecto, al abrir `/graphql` en el navegador, Hot Chocolate sirve el IDE web de ChilliCream, Nitro (antes llamado Banana Cake Pop), para escribir y probar consultas.

#### 3. Implementación en gRPC

En el servidor se usa el paquete `Grpc.AspNetCore` (la plantilla `dotnet new grpc` ya lo incluye), que aporta el runtime de gRPC para ASP.NET Core, `Google.Protobuf` y las herramientas de generación de código (`Grpc.Tools`). El proceso comienza fuera del código C#, definiendo el contrato en un archivo `.proto`.

```protobuf
// Archivo: customer.proto
syntax = "proto3";
option csharp_namespace = "GrpcServices";

// Definición del servicio
service CustomerService {
  rpc GetCustomer (CustomerRequest) returns (CustomerResponse);
}

// Definición de los mensajes (fuertemente tipados)
message CustomerRequest {
  int32 id = 1;
}

message CustomerResponse {
  int32 id = 1;
  string name = 2;
  string email = 3;
  string address = 4;
}
```

El archivo se declara en el `.csproj` para que se genere el código de servidor al compilar:

```xml
<ItemGroup>
  <PackageReference Include="Grpc.AspNetCore" Version="2.*" />
</ItemGroup>
<ItemGroup>
  <Protobuf Include="Protos\customer.proto" GrpcServices="Server" />
</ItemGroup>
```

El atributo `GrpcServices` admite `Server`, `Client`, `Both` (valor por defecto) o `None` (solo mensajes). Al compilar se genera, en el espacio de nombres de `csharp_namespace`, una clase por mensaje (los campos en snake_case pasan a PascalCase: `precio_centimos` se convierte en `PrecioCentimos`) y la clase `CustomerService` con la base de servidor `CustomerService.CustomerServiceBase` y el cliente `CustomerService.CustomerServiceClient`. Nosotros implementamos la lógica:

```csharp
using Grpc.Core;
using GrpcServices; // Espacio de nombres generado por Protobuf

public class CustomerGrpcService : CustomerService.CustomerServiceBase
{
    public override Task<CustomerResponse> GetCustomer(
        CustomerRequest request,
        ServerCallContext context)
    {
        // Devolvemos el mensaje generado fuertemente tipado
        return Task.FromResult(new CustomerResponse
        {
            Id = request.Id,
            Name = "Empresa S.A.",
            Email = "contacto@example.com",
            Address = "Calle Principal 123"
        });
    }
}

// En Program.cs
builder.Services.AddGrpc();          // registra los servicios de gRPC
// ...
app.MapGrpcService<CustomerGrpcService>();
```

**Resultado:** la comunicación es binaria y el cliente consume un método C# generado automáticamente (`client.GetCustomerAsync(request)`), como se ve en el apartado 5.4. gRPC necesita HTTP/2: con HTTPS, Kestrel negocia HTTP/2 automáticamente; sin TLS, el endpoint debe configurarse solo para HTTP/2.

### 3.5. Errores y Plazos en gRPC

gRPC no usa códigos de estado HTTP para los errores de negocio, sino sus propios códigos (`Grpc.Core.StatusCode`). El servidor comunica un error lanzando `RpcException`:

```csharp
throw new RpcException(new Status(StatusCode.NotFound, $"No existe el cliente {request.Id}"));
```

Los códigos más usados y su equivalente HTTP aproximado:

| Código gRPC        | Significado                          | Equivalente HTTP habitual |
| ------------------ | ------------------------------------ | ------------------------- |
| `OK`               | Éxito                                | 200                       |
| `InvalidArgument`  | Datos de entrada no válidos          | 400                       |
| `NotFound`         | El recurso no existe                 | 404                       |
| `AlreadyExists`    | El recurso ya existe                 | 409                       |
| `PermissionDenied` | Sin permisos                         | 403                       |
| `Unauthenticated`  | Sin credenciales válidas             | 401                       |
| `DeadlineExceeded` | Se agotó el plazo de la llamada      | 504                       |
| `Unavailable`      | Servicio no disponible (transitorio) | 503                       |
| `Internal`         | Error interno                        | 500                       |

El cliente puede fijar un plazo (_deadline_): el momento, en UTC, a partir del cual deja de esperar. Si se supera, la llamada termina con `RpcException` cuyo `StatusCode` es `DeadlineExceeded`, y el servidor ve cancelado su `context.CancellationToken`. Una excepción no controlada en el servidor llega al cliente como `Unknown`, sin el mensaje original (salvo que se active `EnableDetailedErrors`, pensado solo para desarrollo).

## 4. Criterios de Selección y Topología del Sistema

Elegir entre REST, GraphQL y gRPC no es una cuestión de cuál es "mejor" en términos absolutos, sino de cuál es el más adecuado para la topología específica de la comunicación, el tipo de cliente y los requisitos no funcionales del sistema.

### 4.1. Evaluación según el Tipo de Cliente y Requisitos de Latencia

El factor más determinante a la hora de elegir el paradigma de comunicación es la naturaleza del cliente que va a consumir el servicio.

**Comunicaciones Servidor a Servidor (Backend to Backend):** cuando dos microservicios dentro de la misma red privada (por ejemplo, dentro de un clúster de Kubernetes) necesitan comunicarse de forma síncrona con volúmenes altos, la latencia y el rendimiento son críticos. En este escenario, gRPC suele ser la opción preferente. Al utilizar HTTP/2 y serialización binaria, reduce el coste de serialización y aprovecha mejor las conexiones. Además, al compartir los archivos `.proto` entre los equipos de backend, se garantiza un contrato estricto que evita errores de integración.

**Aplicaciones para Móvil (Mobile Apps):** los dispositivos móviles operan frecuentemente en redes inestables (3G/4G con pérdida de paquetes) y tienen recursos de batería limitados. En este contexto, minimizar el número de peticiones de red y el tamaño de los datos descargados es vital. GraphQL brilla en este escenario. Permite a la aplicación móvil solicitar exactamente los datos que necesita para renderizar una pantalla específica en una sola petición, evitando el over-fetching y el encadenamiento de peticiones.

**Aplicaciones Web (SPA) y APIs Públicas:** para aplicaciones web construidas con frameworks como React o Angular, tanto REST como GraphQL son opciones válidas. Sin embargo, si la API se va a exponer públicamente para que integradores externos o terceros la consuman, REST sigue siendo el estándar de la industria. Su curva de aprendizaje es mínima, la semántica HTTP es universalmente comprendida y permite aprovechar infraestructuras de caché (CDNs) de forma nativa, lo cual es muy complejo de lograr con GraphQL o gRPC.

### 4.2. Impacto del Tamaño del Payload en la Decisión Arquitectónica

El tamaño del mensaje (payload) que viaja por la red tiene un impacto directo en el rendimiento general del sistema, especialmente bajo cargas altas.

- **JSON (REST y GraphQL):** es texto plano. Aunque se puede comprimir utilizando algoritmos como GZIP o Brotli a nivel de servidor, el proceso de compresión y descompresión consume CPU. Además, JSON es verboso; repite los nombres de las propiedades en cada objeto de un array. Si un servicio devuelve una lista de 10,000 registros, el payload JSON será considerablemente grande y lento de parsear.
- **Protobuf (gRPC):** es binario y posicional. No transmite los nombres de las propiedades, solo los valores asignados a identificadores numéricos (los números definidos en el archivo `.proto`). Por eso sus mensajes suelen ser bastante más pequeños que el JSON equivalente y más rápidos de deserializar. La ganancia real depende de la forma de los datos y conviene medirla con pruebas de rendimiento (tema 2).

Por lo tanto, si el sistema requiere transferir grandes volúmenes de datos estructurados frecuentemente (por ejemplo, sincronización de catálogos o procesamiento de telemetría en tiempo real), gRPC ofrece una ventaja clara. Si los payloads son pequeños y la legibilidad humana es prioritaria para la depuración, REST con JSON es preferible.

### 4.3. Matriz de Decisión para Arquitecturas .NET 10

Para consolidar estos conceptos, a continuación se presenta una matriz de decisión conceptual que los arquitectos de software en .NET 10 deben utilizar al diseñar la comunicación entre componentes:

| Criterio / Escenario          | REST (JSON)                                              | GraphQL                                                     | gRPC (Protobuf)                                            |
| ----------------------------- | -------------------------------------------------------- | ----------------------------------------------------------- | ---------------------------------------------------------- |
| **Topología Principal**       | API Pública / Integración con terceros                   | API para Frontend / Móvil (BFF)                             | Comunicación interna Servidor-Servidor                     |
| **Acoplamiento del Contrato** | Débil (basado en documentación OpenAPI)                  | Fuerte (basado en Schema estricto)                          | Muy Fuerte (basado en compilación de `.proto`)             |
| **Rendimiento y Latencia**    | Medio (sobrecarga de texto y HTTP/1.1 o HTTP/2)          | Medio (sobrecarga de procesamiento de consultas complejas)  | Alto (binario, HTTP/2 nativo, multiplexación)              |
| **Control del Payload**       | Fijo (definido por el servidor, riesgo de over-fetching) | Dinámico (definido por el cliente, altamente optimizado)    | Fijo (pero extremadamente ligero por ser binario)          |
| **Soporte de Caché (Red)**    | Excelente (nativo HTTP, CDNs, proxies)                   | Pobre (requiere soluciones complejas a nivel de aplicación) | Pobre (no diseñado para caché de red tradicional)          |
| **Facilidad de Depuración**   | Alta (herramientas de desarrollo del navegador, Postman) | Alta (herramientas como GraphiQL o Nitro)                   | Baja (requiere herramientas específicas para leer binario) |

**Recomendación Arquitectónica Final:** en una arquitectura de microservicios madura construida con .NET 10, rara vez se utiliza un solo paradigma. El patrón de diseño más recomendado es el Backend for Frontend (BFF) combinado con microservicios internos.

1. Los clientes externos (móvil, web) se comunican con un servicio de puerta de enlace (API Gateway o BFF) utilizando REST o GraphQL, optimizando la experiencia del cliente y facilitando la integración.
2. Ese servicio de puerta de enlace, para resolver la petición del cliente, se comunica con los microservicios internos de dominio (Catálogo, Pedidos, Usuarios) utilizando preferentemente gRPC, por su rendimiento, su seguridad de tipos y su eficiencia en la red interna del clúster (REST sigue siendo válido cuando el volumen es bajo o el servicio también es público).

Esta combinación híbrida permite aprovechar las fortalezas de cada paradigma, mitigando sus debilidades y construyendo un sistema distribuido robusto, escalable y fácil de mantener. La observabilidad de estas interacciones complejas, fundamental para operar este tipo de topologías, se abordará en profundidad en el tema 11 del curso.

## 5. El Lado Cliente: Consumir Servicios HTTP y gRPC desde .NET 10

Un contrato estable solo es la mitad de la comunicación: el servicio que llama también debe gestionar bien las conexiones, los tiempos de espera y los errores. Las políticas de resiliencia (reintentos, cortocircuito) se estudian en el tema 13; aquí se ven las bases.

### 5.1. IHttpClientFactory y Clientes Tipados

Crear un `new HttpClient()` en cada llamada agota los sockets, y un único `HttpClient` estático no detecta cambios de DNS. `IHttpClientFactory` (incluido en ASP.NET Core) resuelve ambos problemas: gestiona y recicla los manejadores HTTP subyacentes. La forma más clara de usarlo es un cliente tipado: una clase que recibe un `HttpClient` en su constructor y encapsula las llamadas a un servicio.

```csharp
builder.Services.AddHttpClient<InventarioClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Inventario:Url"]!);
    client.Timeout = TimeSpan.FromSeconds(3);
});
```

`AddHttpClient<T>` registra `InventarioClient` como servicio transitorio y le inyecta un `HttpClient` ya configurado. Si se usa el descubrimiento de servicios del tema 5, la dirección base puede ser un nombre lógico (`https+http://inventario`) añadiendo `.AddServiceDiscovery()`.

### 5.2. Tiempos de Espera e Interpretación de Errores

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc;

public sealed class InventarioClient(HttpClient http)
{
    public async Task<StockDto?> ObtenerStockAsync(string sku, CancellationToken ct)
    {
        using var respuesta = await http.GetAsync($"/api/stock/{Uri.EscapeDataString(sku)}", ct);

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
            return null;                                   // ausencia esperada: no es un error

        if (!respuesta.IsSuccessStatusCode)
        {
            var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>(ct);
            throw new ServicioRemotoException((int)respuesta.StatusCode,
                problema?.Title ?? "Error sin detalle");
        }

        return await respuesta.Content.ReadFromJsonAsync<StockDto>(ct);
    }
}

public record StockDto(string Sku, int Unidades);
public class ServicioRemotoException(int status, string titulo)
    : Exception($"Inventario respondió {status}: {titulo}")
{
    public int Status { get; } = status;
}
```

- `HttpClient.Timeout` (100 segundos por defecto) limita la duración total de cada petición. Si se supera, la llamada lanza `TaskCanceledException` (con una `TimeoutException` como excepción interna); si la cancela el `CancellationToken` del llamante, también es una `OperationCanceledException`, sin esa excepción interna.
- `ReadFromJsonAsync<T>` (espacio de nombres `System.Net.Http.Json`, incluido en los usings implícitos del SDK web) deserializa el cuerpo. Como los errores del servicio llegan como `ProblemDetails`, el cliente puede leer siempre el mismo formato.
- El cliente decide qué respuestas son normales (un `404` en una consulta) y cuáles son errores. Un servicio intermedio que no puede completar su trabajo por culpa de otro suele responder `502` si la respuesta recibida no es válida y `504` si se agotó el tiempo de espera.
- Pasar el `CancellationToken` de la petición entrante (en Minimal APIs, un parámetro `CancellationToken` del endpoint) cancela la llamada saliente si el cliente original abandona.

### 5.3. Propagación de Cabeceras entre Servicios

Para seguir una operación a través de varios servicios, ciertas cabeceras de la petición entrante (un identificador de correlación, un idioma) deben reenviarse en las llamadas salientes. El paquete `Microsoft.AspNetCore.HeaderPropagation` lo automatiza:

```csharp
builder.Services.AddHeaderPropagation(options =>
    options.Headers.Add("X-Correlation-Id"));

builder.Services.AddHttpClient<InventarioClient>(/* ... */)
    .AddHeaderPropagation();          // este cliente reenvía las cabeceras configuradas

var app = builder.Build();
app.UseHeaderPropagation();          // captura las cabeceras de cada petición entrante
```

Si la petición entrante no trae la cabecera, no se añade a la saliente. La propagación del contexto de trazas (`traceparent`) no necesita este paquete: `HttpClient` la añade de forma integrada, como se verá en el tema 11.

### 5.4. Clientes gRPC con la Factoría de Clientes

El cliente gRPC se genera desde el mismo `.proto`, esta vez con `GrpcServices="Client"`, y se registra con la integración de `IHttpClientFactory` del paquete `Grpc.Net.ClientFactory`:

```xml
<ItemGroup>
  <PackageReference Include="Grpc.Net.ClientFactory" Version="2.*" />
  <PackageReference Include="Google.Protobuf" Version="3.*" />
  <PackageReference Include="Grpc.Tools" Version="2.*" PrivateAssets="All" />
</ItemGroup>
<ItemGroup>
  <Protobuf Include="Protos\customer.proto" GrpcServices="Client" />
</ItemGroup>
```

```csharp
using Grpc.Core;
using GrpcServices;

builder.Services
    .AddGrpcClient<CustomerService.CustomerServiceClient>(o =>
        o.Address = new Uri(builder.Configuration["Clientes:Url"]!))
    .AddHeaderPropagation();          // AddGrpcClient devuelve un IHttpClientBuilder

// ...
app.MapGet("/api/clientes/{id:int}", async (
    int id, CustomerService.CustomerServiceClient cliente, CancellationToken ct) =>
{
    try
    {
        var respuesta = await cliente.GetCustomerAsync(
            new CustomerRequest { Id = id },
            deadline: DateTime.UtcNow.AddSeconds(2),
            cancellationToken: ct);
        return Results.Ok(new { respuesta.Id, respuesta.Name });
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
    {
        return Results.Problem(statusCode: 404, title: "Cliente no encontrado");
    }
    catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
    {
        return Results.Problem(statusCode: 504, title: "El servicio de clientes no respondió a tiempo");
    }
});
```

- `AddGrpcClient<T>` registra el cliente generado para inyectarlo en endpoints y servicios; `Address` es la dirección del servidor gRPC.
- Cada método del servicio genera una versión síncrona y otra asíncrona (`GetCustomerAsync`), que aceptan como parámetros opcionales `headers`, `deadline` y `cancellationToken`.
- Los errores llegan como `RpcException`; su propiedad `StatusCode` permite traducirlos a respuestas HTTP con los equivalentes de la tabla del apartado 3.5. Un filtro `when` en cada `catch` separa los casos.

---

# Cuadernillos del Alumno (laboratorio)

En esta parte se aplica la teoría de la Parte I sobre el proyecto de ejemplo **B2BOrders**, endureciendo la comunicación HTTP entre el Monolito y el microservicio de Facturación creado en el Bloque 5.

---

## Cuadernillo 6.1: Configuración Avanzada y Contratos

**Bloque 6: Comunicación HTTP Avanzada** · Duración: 9 min · Nivel: Avanzado

### Objetivos de la Lección

1. Eliminar los "Magic Strings" (URLs y valores fijos) del código fuente.
2. Implementar el Patrón Options (`IOptions<T>`) para mapear la configuración del `appsettings.json` a clases C# fuertemente tipadas.
3. Inyectar dicha configuración en el `HttpClientFactory` para centralizar la gestión de rutas y timeouts.

### Concepto Clave: El Patrón Options

En .NET, no leemos valores sueltos del JSON (`config["Billing:Url"]`). En su lugar, definimos una clase que represente esa sección.

- **Ventaja 1:** si cambias el nombre de una propiedad, el compilador te avisa.
- **Ventaja 2:** puedes validar los valores al arrancar.
- **Ventaja 3:** permite recarga en caliente (Hot Reload) de la configuración sin reiniciar la app (usando `IOptionsSnapshot` o `IOptionsMonitor`, aunque aquí usamos la versión básica `IOptions`).

### Laboratorio: Externalizar la Configuración

Vamos a mover la URL del Microservicio de Facturación fuera del código compilado.

#### Paso 1: La Clase de Opciones (`BillingOptions.cs`)

Creamos el molde que albergará nuestros datos.

**Ubicación:** `B2BOrders.Infrastructure/Billing/BillingOptions.cs`

```csharp
namespace B2BOrders.Infrastructure.Billing;

public class BillingOptions
{
    // Constante para evitar erratas al buscar en el JSON
    public const string SectionName = "Billing";

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30; // Valor por defecto seguro
}
```

#### Paso 2: Definir Valores (`appsettings.json`)

Añadimos la sección correspondiente en el archivo de configuración de la API.

**Ubicación:** `B2BOrders.Api/appsettings.json`

```jsonc
{
  // ... otras secciones ...
  "ConnectionStrings": { ... },

  "Billing": {
    "BaseUrl": "http://localhost:5200",
    "TimeoutSeconds": 10
  }
}
```

(Asegúrate de que el puerto 5200 coincide con el que usa tu proyecto `BillingService.Api`).

#### Paso 3: Registro en el Contenedor (`Program.cs`)

Vinculamos el JSON con la Clase C#.

**Ubicación:** `B2BOrders.Api/Program.cs`

```csharp
using B2BOrders.Infrastructure.Billing; // Importante

// ... (zona de configuración de servicios)

// Vinculación: JSON "Billing" -> Clase BillingOptions
builder.Services.Configure<BillingOptions>(
    builder.Configuration.GetSection(BillingOptions.SectionName)
);
```

#### Paso 4: Consumo en HttpClient

Modificamos el registro del Cliente HTTP para que lea de las opciones en lugar de tener la URL fija.

**Ubicación:** `B2BOrders.Api/Program.cs`

```csharp
using Microsoft.Extensions.Options; // Necesario para IOptions

// ...

// Registramos el cliente usando una función lambda que recibe el ServiceProvider (sp)
builder.Services.AddHttpClient<IBillingService, HttpBillingService>((sp, client) =>
{
    // Recuperamos el objeto de configuración ya relleno
    var options = sp.GetRequiredService<IOptions<BillingOptions>>().Value;

    // Asignamos los valores
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
```

### Checklist de Verificación

Al terminar, verifica lo siguiente:

- [ ] He creado la clase `BillingOptions` con propiedades `BaseUrl` y `TimeoutSeconds`.
- [ ] Mi `appsettings.json` tiene la sección `"Billing"` con la URL correcta (`http://localhost:XXXX`).
- [ ] En `Program.cs`, he usado `services.Configure<BillingOptions>(...)`.
- [ ] He modificado `AddHttpClient` para usar `sp.GetRequiredService<IOptions<BillingOptions>>()`.
- [ ] Al ejecutar `dotnet run` en ambos proyectos y lanzar un pago, sigue funcionando correctamente.

### Siguiente Paso

Nuestro sistema ya es configurable, pero sigue siendo frágil ante fallos de red.

En el Vídeo 6.2, introduciremos **Polly** para dotar a nuestro cliente HTTP de "superpoderes" como reintentos automáticos (Retries) y fusibles (Circuit Breakers).

---

## Cuadernillo 6.2: Resiliencia con Polly

**Bloque 6: Comunicación HTTP Avanzada** · Duración: 8 min · Nivel: Avanzado

### Objetivos de la Lección

1. Integrar Polly (`Microsoft.Extensions.Http.Polly`) en nuestro cliente HTTP.
2. Implementar una política de Reintentos (Retries) con espera exponencial para errores transitorios (5xx, Red).
3. Distinguir entre errores recuperables (Técnicos) y errores definitivos (Negocio/4xx), lanzando Excepciones Personalizadas.

### Concepto Clave: Tipos de Error HTTP

No todos los errores son iguales. Polly nos ayuda a automatizar la decisión de "volver a intentar".

| Código HTTP                  | Significado                                                                | Estrategia                 |
| ---------------------------- | -------------------------------------------------------------------------- | -------------------------- |
| **2xx (Success)**            | Todo bien.                                                                 | Procesar respuesta.        |
| **5xx (Server Error) / 408** | El servidor explotó o hay timeout. Probablemente se arregle en un segundo. | Reintentar (Polly).        |
| **4xx (Bad Request)**        | Enviamos datos mal o saldo insuficiente. Reintentar no arreglará nada.     | Fallar rápido (Excepción). |

### Laboratorio 1: Instalación

Necesitamos el paquete oficial que conecta `IHttpClientBuilder` con Polly.

**Terminal:**

```powershell
dotnet add B2BOrders.Api package Microsoft.Extensions.Http.Polly
```

### Laboratorio 2: Configuración de Políticas (`Program.cs`)

Enseñamos al cliente HTTP a ser persistente ante fallos técnicos.

**Archivo:** `B2BOrders.Api/Program.cs`

```csharp
using Polly; // Necesario

// ...

builder.Services.AddHttpClient<IBillingService, HttpBillingService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<BillingOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
})
// MAGIA DE POLLY
.AddTransientHttpErrorPolicy(builder =>
    // Si falla (5xx o Red), reintenta 3 veces.
    // Espera: 2s, luego 4s, luego 8s (Backoff exponencial)
    builder.WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
);
```

### Laboratorio 3: Excepciones de Negocio

Creamos una excepción específica para cuando el banco nos rechaza (pero la red funciona bien).

**Archivo:** `B2BOrders.Application/Billing/Exceptions/PaymentRejectedException.cs`

```csharp
namespace B2BOrders.Application.Billing.Exceptions;

public class PaymentRejectedException : Exception
{
    public PaymentRejectedException(string message) : base(message) { }
}
```

### Laboratorio 4: Manejo de Respuestas (`HttpBillingService`)

Aquí aplicamos la lógica inteligente: si es 4xx lanzamos excepción propia; si es 5xx, dejamos que Polly lo intente (y si falla mucho, explotará solo).

**Archivo:** `B2BOrders.Infrastructure/Billing/HttpBillingService.cs`

```csharp
using System.Net.Http.Json;
using B2BOrders.Application.Billing;
using B2BOrders.Application.Billing.Exceptions;

namespace B2BOrders.Infrastructure.Billing;

// DTO interno (Asegúrate de que coincida con el del Microservicio)
record ExternalPaymentRequest(int OrderId, decimal Amount, string Currency, string PaymentMethod);
record ExternalPaymentResponse(string PaymentId, string Status); // ¡Cuidado con el nombre PaymentId!

public class HttpBillingService : IBillingService
{
    private readonly HttpClient _httpClient;

    public HttpBillingService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<PaymentResult> CreatePaymentAsync(int orderId, decimal amount, string currency)
    {
        var request = new ExternalPaymentRequest(orderId, amount, currency, "CreditCard");

        // 1. Llamada HTTP (Polly intercepta aquí si hay error 5xx)
        var response = await _httpClient.PostAsJsonAsync("/payments", request);

        // 2. Éxito (200 OK)
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<ExternalPaymentResponse>();

            // Usamos 'PaymentId' correctamente escrito
            return new PaymentResult(true, result?.PaymentId, null);
        }

        // 3. Error de Negocio (4xx) -> NO Reintentar
        if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
        {
            // Lanzamos la excepción (NO usar 'return')
            throw new PaymentRejectedException($"Pago rechazado: {response.StatusCode}");
        }

        // 4. Error Técnico (5xx) -> Si llegamos aquí, Polly ya reintentó 3 veces y se rindió.
        throw new HttpRequestException($"Servicio de pagos no disponible: {response.StatusCode}");
    }
}
```

### Guía de Resolución de Problemas (Troubleshooting)

| Error                                                                         | Causa                                                                                      | Solución                                                                                   |
| ----------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------ |
| `CS0029: Cannot implicitly convert type '...Exception' to '...PaymentResult'` | Intentaste hacer `return new Exception(...)` en lugar de `throw`.                          | Cambia `return` por `throw`. Las excepciones se lanzan.                                    |
| `CS1061: ExternalPaymentResponse does not contain definition for 'PaymentId'` | Error tipográfico en el `record` (ej: escribiste `PaimentId`).                             | Corrige la definición del `record` al final del archivo para que sea `PaymentId`.          |
| El reintento no funciona (falla inmediatamente)                               | `AddTransientHttpErrorPolicy` no captura excepciones 404 o 400, solo 5xx y errores de red. | Asegúrate de simular un fallo real (apagando el microservicio) para ver a Polly en acción. |

### Checklist de Verificación

- [ ] Paquete `Microsoft.Extensions.Http.Polly` instalado.
- [ ] `Program.cs` tiene `.AddTransientHttpErrorPolicy(...)` con reintentos.
- [ ] `HttpBillingService` lanza `PaymentRejectedException` si recibe un 400/404.
- [ ] He verificado que los nombres de propiedades (`PaymentId`) coinciden exactamente.

### Siguiente Paso

Nuestro `Program.cs` se está llenando de lógica de configuración (Polly, Options, HttpClient...).

En el Vídeo 6.3, aplicaremos Clean Architecture creando métodos de extensión (`AddBillingInfrastructure`) para encapsular todo este ruido y dejar el arranque de la API limpio.

---

## Cuadernillo 6.3: Observabilidad y Contratos

**Bloque 6: Comunicación HTTP Avanzada** · Duración: 8 min · Nivel: Avanzado

### Objetivos de la Lección

1. Instrumentar el cliente HTTP para tener trazabilidad completa (Logging Estructurado).
2. Medir el rendimiento de las llamadas externas (Latencia) usando `Stopwatch`.
3. Documentar las reglas de juego (Contratos de API) para estandarizar el desarrollo.

### Concepto Clave: Logging Estructurado

En lugar de escribir texto plano (`"Error en pedido " + id`), usamos Placeholders (`"Error en pedido {OrderId}", id`).

- **Texto Plano:** difícil de buscar. Si buscas "pedido", te salen mil resultados distintos.
- **Estructurado:** el sistema de logs guarda `OrderId` como una propiedad separada. Puedes filtrar `OrderId == 1` en herramientas como Kibana, Seq o Datadog.

### Laboratorio 1: Instrumentación (`HttpBillingService`)

Transformamos el servicio para que nos diga qué está pasando.

**Archivo:** `B2BOrders.Infrastructure/Billing/HttpBillingService.cs`

```csharp
using System.Diagnostics; // Para el Stopwatch
using System.Net.Http.Json;
using B2BOrders.Application.Billing;
using B2BOrders.Application.Billing.Exceptions; // Si tienes la excepción aquí
using Microsoft.Extensions.Logging; // Para ILogger

namespace B2BOrders.Infrastructure.Billing;

// DTOs internos (Coinciden con el JSON del microservicio)
record ExternalPaymentRequest(int OrderId, decimal Amount, string Currency);
record ExternalPaymentResponse(string PaymentId, string Status);

public class HttpBillingService : IBillingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpBillingService> _logger; // 1. Inyección

    public HttpBillingService(HttpClient httpClient, ILogger<HttpBillingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PaymentResult> CreatePaymentAsync(int orderId, decimal amount, string currency)
    {
        var request = new ExternalPaymentRequest(orderId, amount, currency);

        // 2. Cronómetro: Empezamos a medir
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Log de Inicio (Information)
            _logger.LogInformation("Iniciando pago para Order {OrderId} de {Amount} {Currency}",
                orderId, amount, currency);

            // Llamada HTTP (Polly maneja reintentos aquí dentro)
            var response = await _httpClient.PostAsJsonAsync("/payments", request);

            stopwatch.Stop(); // Paramos el reloj

            // CASO 1: Éxito (2xx)
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ExternalPaymentResponse>();

                // Log de Éxito con Latencia
                _logger.LogInformation("Pago completado para Order {OrderId}. ID Externo: {PaymentId}. Tiempo: {Elapsed}ms",
                    orderId, result?.PaymentId, stopwatch.ElapsedMilliseconds);

                return new PaymentResult(true, result?.PaymentId, null);
            }

            // CASO 2: Error de Negocio (4xx) - No es culpa del servidor, es de la petición
            if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
            {
                _logger.LogWarning("Pago rechazado para Order {OrderId}. Status: {Status}. Tiempo: {Elapsed}ms",
                    orderId, response.StatusCode, stopwatch.ElapsedMilliseconds);

                throw new PaymentRejectedException($"Pago rechazado: {response.StatusCode}");
            }

            // CASO 3: Error Técnico (5xx) - Si llegamos aquí, Polly ya reintentó y falló
            throw new Exception($"El servicio falló tras {stopwatch.ElapsedMilliseconds}ms. Status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            stopwatch.Stop(); // Asegurar parada si explota antes

            // Log de Error (Critical/Error)
            _logger.LogError(ex, "ERROR CRÍTICO procesando Order {OrderId} tras {Elapsed}ms",
                orderId, stopwatch.ElapsedMilliseconds);

            throw; // Re-lanzamos para que el Handler lo sepa
        }
    }
}
```

### Laboratorio 2: Guía de Contratos (Documentación)

Creamos la "Constitución" de nuestras APIs para evitar debates innecesarios.

**Archivo:** crear `docs/http-contracts-guidelines.md` en la raíz de la solución.

```markdown
# Guía de Ingeniería: Contratos HTTP e Integración

## 1. Códigos de Estado (Status Codes)

- **200 OK**: Éxito síncrono. Devuelve JSON con resultado.
- **400 Bad Request**: Error del cliente (validación, negocio). **No reintentar**.
- **404 Not Found**: Recurso no encontrado.
- **500 Internal Server Error**: Bug en servidor. **Reintentar (Polly)**.
- **503 Service Unavailable**: Servicio caído/saturado. **Reintentar con Backoff**.

## 2. Observabilidad Mínima

Todo cliente HTTP debe generar logs estructurados incluyendo:

1. **Correlation ID** (OrderId, TransactionId).
2. **Latencia** (ms).
3. **StatusCode** final.

## 3. Resiliencia

- Timeout máximo: 10 segundos.
- Política de reintentos exponencial (2s, 4s, 8s) para errores 5xx y timeouts.
```

### Guía de Resolución de Problemas (Troubleshooting Real)

Estos son los errores que hemos resuelto durante la sesión:

| Síntoma / Error                               | Causa Probable                                                                                        | Solución                                                                                                 |
| --------------------------------------------- | ----------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `SocketException (10061): Connection Refused` | El Monolito llama a un puerto donde no hay nada escuchando.                                           | 1. Iniciar Microservicio (`dotnet run`). 2. Verificar puerto en `appsettings.json` vs terminal.          |
| `TaskCanceledException` (Timeout)             | El servicio tarda más de 10s o Polly está reintentando hasta agotar el tiempo.                        | Verificar si el microservicio está colgado o si la red falla. Revisar configuración de `TimeoutSeconds`. |
| `PaymentId` llega como `(null)`               | El JSON del microservicio tiene una propiedad distinta al DTO del monolito (ej: `id` vs `paymentId`). | Alinear los nombres de las propiedades en los `record` de ambos proyectos.                               |
| Warning EF Core (Decimal)                     | No se especificó precisión para columnas de dinero (`Amount`).                                        | Configurar `HasPrecision(18, 2)` en `OnModelCreating` y generar migración.                               |
