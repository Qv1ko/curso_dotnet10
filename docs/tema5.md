# Tema 5 - Microservicios en .NET 10

## Índice

**Manual I: Microservicios: Definición, Límites y API**

1. Definición Fundamental y Características de un Microservicio
2. Identificación de Límites Naturales y Descomposición del Sistema
3. Contratos de API como Base de Comunicación entre Servicios
4. Patrones de Despliegue para la Actualización Segura de Servicios
5. Estructura Mínima de un Microservicio en .NET 10

**Cuadernillos del Alumno (laboratorio)**

- 5.1 Extracción de Microservicios
- 5.2 Comunicación HTTP entre Servicios
- 5.3 Orquestación End-to-End

---

# Manual I: Microservicios: Definición, Límites y API

## 1. Definición Fundamental y Características de un Microservicio

La arquitectura de microservicios es una de las opciones más extendidas para construir sistemas distribuidos complejos. Sin embargo, el término "microservicio" es frecuentemente malinterpretado, reduciéndose erróneamente a una simple cuestión de tamaño o cantidad de líneas de código. En el ecosistema de .NET 10, diseñar un microservicio requiere una comprensión profunda de sus características definitorias, las cuales van mucho más allá de la mera separación de proyectos en una solución.

Un microservicio real se define por su capacidad para operar de forma aislada, su alineación estricta con las necesidades del negocio y la autonomía que otorga a los equipos de desarrollo. A continuación, desglosamos los pilares fundamentales que constituyen un microservicio auténtico.

### 1.1. Autonomía y Despliegue Independiente

La característica más crítica y no negociable de un microservicio es su autonomía. Un servicio es verdaderamente autónomo si, y solo si, puede ser modificado, probado, desplegado y escalado sin requerir la coordinación o el despliegue simultáneo de ningún otro servicio en el sistema.

Para lograr esta autonomía, el microservicio debe poseer su propio estado. Esto se traduce en la regla de oro de la arquitectura de microservicios: **una base de datos por servicio**. Si dos servicios comparten la misma base de datos relacional y acceden a las mismas tablas, no son microservicios; son un monolito distribuido. Compartir el estado a nivel de base de datos crea un acoplamiento fuerte que destruye la capacidad de despliegue independiente, ya que un cambio en el esquema por parte de un servicio puede romper inmediatamente al otro.

El despliegue independiente significa que el equipo responsable del servicio de "Facturación" puede lanzar una nueva versión a producción un martes a las 10:00 a.m., sin tener que avisar al equipo de "Catálogo" ni coordinar una ventana de mantenimiento global. Esta capacidad acorta los tiempos de entrega (Time to Market) y reduce el riesgo, ya que el radio de impacto de un despliegue fallido se limita, en principio, al servicio desplegado y a los consumidores que dependen de él.

### 1.2. Independencia Tecnológica y Responsabilidad de Equipos

Otra ventaja fundamental de los microservicios es la independencia tecnológica. Al estar aislados y comunicarse únicamente a través de contratos de red bien definidos (como HTTP/REST o gRPC, que se analizarán en el tema 6), los detalles de implementación interna son irrelevantes para el resto del sistema.

Esto permite que cada equipo elija la herramienta adecuada para el trabajo adecuado. Por ejemplo, un servicio de procesamiento de pagos transaccional podría estar construido con .NET 10 y Entity Framework Core utilizando SQL Server, mientras que un servicio de recomendaciones basado en grafos podría estar escrito en Python utilizando una base de datos Neo4j. Esta heterogeneidad tecnológica, aunque añade complejidad operativa, permite optimizar el rendimiento y los costes de cada capacidad de negocio de forma individual.

Además, los microservicios están ligados a la estructura organizativa. La Ley de Conway (repasada en el tema 1) explica por qué: cada servicio debería pertenecer a un equipo pequeño, multidisciplinar y autónomo, dueño del servicio desde la toma de requisitos hasta su monitorización en producción.

### 1.3. Alineación con Subdominios del Negocio

Un microservicio no debe ser un componente técnico abstracto (como un "Servicio de Acceso a Datos" o un "Servicio de Envío de Correos"), sino una representación directa de una capacidad o subdominio del negocio. Esta alineación es donde la arquitectura de microservicios se cruza con el Domain-Driven Design (DDD), cuyos fundamentos conceptuales se establecieron en el tema 3.

Cada microservicio debe encapsular un Contexto Delimitado (Bounded Context). Esto significa que el servicio es responsable de un modelo de negocio específico, con sus propias reglas, validaciones y lenguaje ubicuo. Por ejemplo, el concepto de "Cliente" puede existir tanto en el servicio de "Envíos" como en el de "Facturación", pero cada servicio modelará al cliente de manera diferente, almacenando solo los atributos que le importan a su subdominio (dirección física para envíos; NIF y datos fiscales para facturación).

### 1.4. Mitos y Confusiones Habituales en la Adopción

La popularización de los microservicios ha generado una serie de mitos que conducen a arquitecturas frágiles y costosas. Es imperativo desmentir estas confusiones antes de iniciar el diseño de un sistema en .NET 10.

#### 1.4.1. El Mito del Tamaño: Servicio Pequeño vs. Microservicio

El prefijo "micro" es, en muchos sentidos, el mayor error de marketing de esta arquitectura. Un microservicio no se define por tener pocas líneas de código, ni por gestionar una única entidad de base de datos. Asumir que cualquier servicio pequeño es un microservicio conduce al antipatrón de la "nanoservitización", donde el sistema se fragmenta en multitud de piezas minúsculas que no pueden hacer nada útil por sí solas.

El tamaño de un microservicio debe estar dictado por la cohesión del negocio. Un servicio debe ser lo suficientemente grande como para encapsular una capacidad de negocio completa y lo suficientemente pequeño como para ser mantenido por un solo equipo. Si para completar una operación de negocio simple (como crear un pedido) se requiere que cinco "microservicios" se comuniquen sincrónicamente entre sí, el tamaño es incorrecto y los límites están mal definidos.

#### 1.4.2. El Mito de la Escalabilidad Automática

Existe la falsa creencia de que dividir un monolito en microservicios hace que el sistema sea automáticamente escalable. La realidad es que los microservicios habilitan la escalabilidad, pero no la proporcionan por arte de magia.

Para que un microservicio en .NET 10 escale correctamente, debe diseñarse sin estado (stateless) en la capa de aplicación, delegando el estado a bases de datos o cachés distribuidas (como Redis). Además, requiere una infraestructura subyacente compleja, como orquestadores de contenedores (Kubernetes, que se abordará en el tema 14), balanceadores de carga y métricas de observabilidad precisas para determinar cuándo instanciar nuevas réplicas. Sin esta infraestructura y un diseño cuidadoso, una arquitectura de microservicios puede ser mucho más rígida y difícil de escalar que un monolito bien estructurado.

## 2. Identificación de Límites Naturales y Descomposición del Sistema

El mayor desafío en el diseño de microservicios no es escribir el código en C#, sino decidir dónde trazar las líneas que separan un servicio de otro. Una mala descomposición resulta en un sistema altamente acoplado, donde los cambios requieren modificaciones en múltiples repositorios y las caídas en red provocan fallos en cascada. En esta sección, analizaremos los criterios arquitectónicos para identificar los límites naturales de un sistema.

### 2.1. Enfoques Basados en Subdominios y Capacidades de Negocio

La técnica más fiable para descomponer un sistema es analizar las capacidades del negocio. En lugar de pensar en tablas de base de datos o capas técnicas, debemos observar qué hace la empresa para generar valor.

Supongamos una plataforma de comercio electrónico. Las capacidades de negocio podrían incluir:

- Gestión del Catálogo de Productos.
- Procesamiento de Pedidos.
- Gestión de Inventario.
- Facturación y Pagos.
- Logística y Envíos.

Cada una de estas capacidades es un candidato excelente para convertirse en un microservicio. Tienen ciclos de vida diferentes, reglas de negocio distintas y, a menudo, son gestionadas por departamentos diferentes dentro de la empresa. Al alinear los servicios con estas capacidades, garantizamos que los cambios solicitados por el negocio (por ejemplo, "necesitamos un nuevo método de pago") afecten a un único servicio ("Facturación y Pagos"), preservando la autonomía.

### 2.2. Eventos de Negocio como Fronteras Arquitectónicas

Otra forma poderosa de identificar límites es observar los eventos de negocio que ocurren en el sistema. Un evento de negocio es un hecho que ha ocurrido en el pasado y que es relevante para el dominio (por ejemplo, `PedidoConfirmado`, `PagoRechazado`, `StockAgotado`).

Los eventos actúan como fronteras naturales porque marcan el punto en el que la responsabilidad pasa de un dominio a otro. Cuando el servicio de "Pedidos" finaliza su trabajo, emite el evento `PedidoConfirmado`. A partir de ese momento, ya no es responsabilidad de "Pedidos" saber qué ocurre. El servicio de "Inventario" reaccionará a ese evento para reservar los artículos, y el servicio de "Facturación" reaccionará para emitir el cobro.

Si dos piezas de código necesitan comunicarse constantemente de forma síncrona (esperando una respuesta inmediata para poder continuar), probablemente pertenezcan al mismo microservicio. Si pueden comunicarse de forma asíncrona mediante eventos (cuyos fundamentos se verán en el tema 7), son excelentes candidatos para ser servicios separados.

### 2.3. Criterios de Escalado Diferencial

Las necesidades de rendimiento y escalado son indicadores técnicos clave para la separación de servicios. En un sistema real, no todas las partes soportan la misma carga de trabajo.

Imaginemos nuevamente el comercio electrónico. El catálogo de productos recibe un volumen muy alto de lecturas de usuarios que navegan por la web, mientras que el módulo de administración con el que los empleados añaden productos recibe unas pocas escrituras al día.

Si ambos módulos residen en el mismo servicio, para soportar el tráfico de lectura tendríamos que escalar también la lógica de escritura, desperdiciando recursos (memoria, CPU, licencias). Separar el "Catálogo (Lectura)" del "Backoffice de Productos (Escritura)" permite escalar el primero masivamente mediante múltiples réplicas en Kubernetes, mientras el segundo se mantiene con una única instancia, optimizando los costes de infraestructura.

### 2.4. Factores Organizativos: Autonomía y Ciclos de Despliegue

La estructura de la organización debe influir directamente en los límites de los microservicios. Si un único equipo de cinco desarrolladores es responsable de mantener diez microservicios, la sobrecarga cognitiva y operativa (gestión de repositorios, pipelines CI/CD, monitorización) los paralizará. En este escenario, un monolito modular (visto en el tema 4) sería una decisión mucho más sensata.

Por el contrario, si tenemos cincuenta desarrolladores trabajando en el mismo dominio, los conflictos de código, los cuellos de botella en las pruebas y la burocracia para desplegar justificarán la división en múltiples microservicios.

Además, los ciclos de despliegue dictan fronteras. Si el módulo de "Cálculo de Impuestos" cambia una vez al año debido a regulaciones legales, pero el módulo de "Motor de Recomendaciones" requiere despliegues diarios para afinar algoritmos de inteligencia artificial, mantenerlos juntos obligaría a someter al motor de recomendaciones a los estrictos y lentos procesos de validación del módulo de impuestos. Separarlos permite que cada uno evolucione a su propio ritmo.

### 2.5. Prevención de la Descomposición Arbitraria o Excesiva

El antipatrón más destructivo en esta arquitectura es el "Monolito Distribuido". Ocurre cuando se divide un sistema sin respetar la cohesión del negocio, creando servicios "charlatanes" (chatty services) que necesitan intercambiar muchos mensajes HTTP para completar una sola operación.

Las señales de alerta de una descomposición excesiva incluyen:

- **Transacciones distribuidas constantes:** si necesitas actualizar datos en tres servicios diferentes simultáneamente y requieres que todo se revierta si uno falla, esos tres servicios probablemente deberían ser uno solo.
- **Dependencias circulares:** el Servicio A llama al Servicio B, que a su vez llama al Servicio A.
- **Despliegues coordinados:** si para desplegar la versión 2.0 del Servicio A, estás obligado a desplegar la versión 2.0 del Servicio B al mismo tiempo, has perdido la autonomía.

#### 2.5.1. Ejemplo Práctico: Refactorización de un Límite Mal Definido

Analicemos un escenario común de mala descomposición y cómo solucionarlo.

**El Problema (Antipatrón):** un equipo decide crear un `ServicioPedidos` y un `ServicioLineasPedido` separados, argumentando que son tablas diferentes en la base de datos.

Cuando un cliente finaliza una compra, el flujo es el siguiente:

1. El cliente envía un JSON con el pedido y sus 5 líneas.
2. El `ServicioPedidos` crea la cabecera del pedido en su base de datos.
3. El `ServicioPedidos` hace 5 llamadas HTTP síncronas al `ServicioLineasPedido` para guardar cada línea.
4. Si la llamada de la tercera línea falla por un error de red, el sistema queda en un estado inconsistente (un pedido con solo 2 líneas guardadas).

Este diseño es desastroso. Carece de cohesión, multiplica la latencia de red por cinco, introduce puntos únicos de fallo y destruye la consistencia transaccional.

**La Solución (Refactorización basada en DDD):** aplicando los principios de diseño, reconocemos que un "Pedido" y sus "Líneas" forman un único Agregado (Aggregate Root). No tiene sentido que existan líneas de pedido huérfanas, ni un pedido sin líneas. Por lo tanto, deben pertenecer al mismo límite transaccional.

Refactorizamos el sistema unificándolos en un único `ServicioPedidos`.

Payload de entrada unificado, manejado por un solo microservicio:

```json
{
  "clienteId": "CUS-99281",
  "fechaCreacion": "2026-09-17T10:30:00Z",
  "direccionEnvio": "Calle Falsa 123, Madrid",
  "lineas": [
    { "productoId": "PROD-001", "cantidad": 2, "precioUnitario": 15.5 },
    { "productoId": "PROD-089", "cantidad": 1, "precioUnitario": 120.0 }
  ]
}
```

En .NET 10, este servicio recibirá el payload completo, validará las reglas de negocio (ej. stock disponible, importes correctos) y guardará la cabecera y las líneas en su propia base de datos utilizando una única transacción de Entity Framework Core. Hemos eliminado las llamadas de red internas, garantizado la consistencia de los datos (ACID) y reducido la complejidad operativa, demostrando que un límite bien definido es superior a una fragmentación arbitraria.

## 3. Contratos de API como Base de Comunicación entre Servicios

En una arquitectura de microservicios, los servicios son cajas negras. El único punto de interacción con el mundo exterior (ya sean clientes frontend u otros microservicios) es su Interfaz de Programación de Aplicaciones (API). Por lo tanto, el diseño, mantenimiento y evolución de esta API se convierte en una tarea crítica.

### 3.1. El Rol Fundamental del Contrato de API

El contrato de API (típicamente descrito con la especificación OpenAPI para servicios HTTP) es mucho más que documentación técnica; es un acuerdo vinculante entre el proveedor del servicio y sus consumidores. En .NET 10 el documento OpenAPI lo genera el paquete `Microsoft.AspNetCore.OpenApi` (incluido en la plantilla `webapi`) con `AddOpenApi()` y `MapOpenApi()`, y por defecto sigue la versión 3.1 de la especificación. La plantilla ya no incluye Swashbuckle; si se quiere una interfaz visual (Swagger UI, Scalar), se añade como paquete aparte.

Este contrato define exactamente qué operaciones están disponibles, qué formato deben tener los datos de entrada (cuerpo de la petición, cabeceras, parámetros de ruta) y qué respuestas se pueden esperar (códigos de estado HTTP, esquemas de error, formatos de datos).

Cuando un equipo publica un contrato de API, está haciendo una promesa a los consumidores: "Si me envías los datos con esta estructura, te garantizo que procesaré la solicitud y te responderé con este formato". Romper esta promesa modificando el contrato de forma incompatible provocará la caída inmediata de los servicios dependientes, violando el principio de autonomía.

### 3.2. Estrategias de Versionado en .NET 10

Dado que los requisitos del negocio cambian, las APIs deben evolucionar. Para introducir cambios sin romper a los clientes existentes, es obligatorio implementar estrategias de versionado desde el primer día. Las tres estrategias más comunes, todas soportadas por la biblioteca Asp.Versioning en .NET 10, son:

1. **Versionado por URL (URI Path):** la versión se incluye explícitamente en la ruta (ej. `api/v1/pedidos`). Es la más visible y fácil de probar desde un navegador, pero rompe la semántica estricta de REST (donde la URL debe representar el recurso, no la versión).
2. **Versionado por Cabecera (Header):** la URL se mantiene limpia (`api/pedidos`) y el cliente envía una cabecera HTTP específica (ej. `X-Api-Version: 2.0`). Es más puro arquitectónicamente, pero requiere herramientas específicas (como Postman o curl) para probarse fácilmente.
3. **Versionado por Query String:** se añade como parámetro en la URL (`api/pedidos?api-version=1.0`). Es un punto intermedio, útil para APIs públicas simples.

La mejor práctica en entornos corporativos es soportar múltiples métodos de lectura simultáneamente, dando flexibilidad a los consumidores.

#### 3.2.1. Ejemplo Práctico: Implementación de Versionado por URL y Cabecera

A continuación, mostramos cómo configurar el versionado en una API de Minimal APIs de .NET 10 con la biblioteca Asp.Versioning (versión 10.x, la primera que se integra con el OpenAPI integrado de .NET 10). Paquetes NuGet:

- `Asp.Versioning.Http`: versionado para Minimal APIs (`AddApiVersioning`, `NewVersionedApi`, `HasApiVersion`...).
- `Asp.Versioning.Mvc.ApiExplorer`: `AddApiExplorer`, que expone cada versión como grupo para la documentación.
- Opcional, `Asp.Versioning.OpenApi`: genera un documento OpenAPI por versión.

```powershell
dotnet add package Asp.Versioning.Http
dotnet add package Asp.Versioning.Mvc.ApiExplorer
```

**Paso 1: Configuración en `Program.cs`**

```csharp
using Asp.Versioning;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiVersioning(options =>
{
    // Si el cliente no especifica versión, se asume la 1.0
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;

    // Añade a las respuestas las cabeceras api-supported-versions y api-deprecated-versions
    options.ReportApiVersions = true;

    // Leer la versión del segmento de la URL o de una cabecera personalizada
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
})
.AddApiExplorer(options =>
{
    // Nombre de grupo por versión: v1, v2...
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

var app = builder.Build();
```

`AddApiVersioning` devuelve un constructor sobre el que se encadena `AddApiExplorer`. Si además se instala `Asp.Versioning.OpenApi`, se encadena `.AddOpenApi()` y se publica un documento por versión con `app.MapOpenApi().WithDocumentPerVersion();` (`/openapi/v1.json`, `/openapi/v2.json`).

**Paso 2: Definición de Endpoints con Minimal APIs**

Se declara una sola API versionada con `NewVersionedApi` y de ella cuelga un grupo de rutas por versión:

```csharp
// Una API versionada lógica: "Pedidos"
var pedidos = app.NewVersionedApi("Pedidos");

// Grupo de la versión 1
var v1 = pedidos.MapGroup("/api/v{version:apiVersion}/pedidos")
                .HasApiVersion(1.0);

v1.MapGet("/{id}", (string id) =>
        Results.Ok(new { Id = id, Total = 150.00m, Estado = "Procesando" }))
  .WithName("ObtenerPedidoV1");

// Grupo de la versión 2: misma ruta base, contrato distinto
var v2 = pedidos.MapGroup("/api/v{version:apiVersion}/pedidos")
                .HasApiVersion(2.0);

v2.MapGet("/{id}", (string id) =>
        Results.Ok(new
        {
            Identificador = id,      // campo renombrado: cambio incompatible
            ImporteTotal = 150.00m,
            Moneda = "EUR",
            Estado = "Procesando"
        }))
  .WithName("ObtenerPedidoV2");

app.Run();
```

Los clientes antiguos siguen llamando a `/api/v1/pedidos/123` y reciben la estructura original; los nuevos llaman a `/api/v2/pedidos/123`. El parámetro de ruta `{version:apiVersion}` es el que lee `UrlSegmentApiVersionReader`. La cabecera `X-Api-Version` solo interviene en grupos cuya ruta no lleva ese segmento (por ejemplo, `pedidos.MapGroup("/api/pedidos")`): en ellos el cliente elige versión con la cabecera y, si no la envía, se aplica la 1.0 por `AssumeDefaultVersionWhenUnspecified`. Ningún cliente existente se rompe.

`HasApiVersion` admite también las formas `HasApiVersion(1, 0)` y `HasApiVersion(new ApiVersion(1, 0))`.

### 3.3. Garantizar la Compatibilidad Hacia Atrás (Backward Compatibility)

El versionado explícito (crear una V2) debe ser el último recurso, ya que obliga a los consumidores a actualizar su código, lo cual genera fricción y costes organizativos. El objetivo principal de un equipo de microservicios debe ser evolucionar la V1 manteniendo la compatibilidad hacia atrás.

Para lograr esto, se deben respetar reglas estrictas al modificar un contrato de API existente:

**Cambios Permitidos (Seguros):**

- Añadir nuevos campos opcionales a una respuesta.
- Añadir nuevos campos opcionales a una petición.
- Añadir nuevos endpoints.

**Cambios Prohibidos (Rompen el contrato; requieren nueva versión):**

- Eliminar o renombrar un campo existente.
- Cambiar el tipo de dato de un campo (ej. de entero a cadena de texto).
- Hacer que un campo que era opcional pase a ser obligatorio.
- Cambiar la semántica de un campo (ej. un campo `precio` que antes incluía impuestos y ahora no, aunque el tipo de dato siga siendo numérico).

Para facilitar esta evolución, los consumidores deben aplicar el patrón del Lector Tolerante (Tolerant Reader) o la Ley de Postel: _"Sé conservador en lo que envías, y liberal en lo que aceptas"_. Esto significa que si un cliente recibe un JSON con campos adicionales que no reconoce, debe ignorarlos silenciosamente en lugar de lanzar una excepción de deserialización.

### 3.4. Gestión del Ciclo de Vida y Deprecación de Contratos

Mantener múltiples versiones de una API indefinidamente genera deuda técnica, duplica el esfuerzo de mantenimiento y aumenta la superficie de ataque de seguridad. Por ello, es vital establecer una política clara de ciclo de vida y deprecación (Sunsetting).

Cuando se lanza la V2, la V1 no se apaga inmediatamente. Se marca como obsoleta (deprecated). Con Asp.Versioning se indica en el propio grupo, sustituyendo `HasApiVersion` por `HasDeprecatedApiVersion`, para que el documento OpenAPI y las cabeceras HTTP lo reflejen:

```csharp
var v1 = pedidos.MapGroup("/api/v{version:apiVersion}/pedidos")
                .HasDeprecatedApiVersion(1.0); // la V1 sigue funcionando, pero figura como obsoleta
```

Una versión declarada con `HasDeprecatedApiVersion` sigue atendiendo peticiones. Con `ReportApiVersions = true`, las respuestas incluyen la cabecera `api-supported-versions` con las versiones vigentes y la cabecera `api-deprecated-versions` con las obsoletas. Así los consumidores pueden detectar que usan una versión en retirada sin leer la documentación.

Ojo con un detalle de Asp.Versioning: cuando cada grupo declara solo su versión, la respuesta de un endpoint informa de las versiones de su grupo y de las de los grupos declarados antes que él en la misma API versionada. Con el grupo v1 declarado primero y el v2 después, `/api/v2/pedidos/123` responde con `api-supported-versions: 2.0` y `api-deprecated-versions: 1.0`, mientras que `/api/v1/pedidos/123` solo lleva `api-deprecated-versions: 1.0`. Si se quiere que todas las respuestas anuncien el conjunto completo, cada grupo declara todas las versiones y cada endpoint se asigna a la suya con `MapToApiVersion`:

```csharp
var v1 = pedidos.MapGroup("/api/v{version:apiVersion}/pedidos")
                .HasDeprecatedApiVersion(1.0)
                .HasApiVersion(2.0);

v1.MapGet("/{id}", (string id) => Results.Ok(new { Id = id }))
  .MapToApiVersion(1.0);
```

El proceso organizativo debe incluir:

1. **Notificación:** avisar a todos los consumidores registrados que la V1 está obsoleta.
2. **Ventana de gracia:** establecer un plazo razonable (ej. 6 meses) para que los clientes migren a la V2.
3. **Apagón programado (Brownout):** apagar la V1 durante ventanas cortas (ej. 1 hora al día) para forzar a los clientes rezagados a darse cuenta del problema antes del apagado definitivo.
4. **Retirada definitiva:** eliminar el código de la V1 del repositorio.

## 4. Patrones de Despliegue para la Actualización Segura de Servicios

Incluso con contratos bien definidos y pruebas exhaustivas, desplegar una nueva versión de un microservicio en producción conlleva riesgos. En arquitecturas monolíticas tradicionales, el despliegue implicaba detener el servidor, copiar archivos y reiniciar, provocando tiempo de inactividad (downtime). En el ecosistema de microservicios, el objetivo es desplegar sin tiempo de inactividad (Zero Downtime Deployment).

Para lograr esto y minimizar el riesgo de introducir errores críticos a los usuarios, se utilizan patrones de despliegue avanzados.

### 4.1. Despliegue Blue-Green: Concepto y Aplicación

El patrón Blue-Green Deployment busca eliminar el tiempo de inactividad y proporcionar un mecanismo de reversión (rollback) instantáneo.

El concepto se basa en mantener dos entornos de producción idénticos:

- **Entorno Azul (Blue):** es el entorno activo actual, recibiendo el 100% del tráfico de los usuarios. Ejecuta la versión 1.0 del servicio.
- **Entorno Verde (Green):** es un entorno inactivo, aislado del tráfico real.

Cuando el equipo quiere desplegar la versión 2.0, la instala en el entorno Verde. Una vez desplegada, el equipo puede ejecutar pruebas de humo (smoke tests) y validaciones directamente sobre el entorno Verde, asegurándose de que la base de datos se conecta correctamente y la configuración es válida, todo ello sin afectar a los usuarios reales.

Cuando se confirma que el entorno Verde es estable, se reconfigura el enrutador o balanceador de carga (router) para que apunte al entorno Verde. A partir de ese cambio, el entorno Verde se convierte en el activo (recibiendo el 100% del tráfico) y el Azul pasa a ser el inactivo.

**Beneficio principal:** si a los cinco minutos de redirigir el tráfico se detecta un error crítico en la versión 2.0, la reversión es trivial. Solo hay que volver a cambiar el balanceador de carga para que apunte de nuevo al entorno Azul (que todavía tiene la versión 1.0 intacta). La reversión se reduce a un cambio de configuración, sin reinstalar nada.

### 4.2. Despliegue Canary: Reducción Progresiva del Riesgo

Mientras que el Blue-Green cambia el 100% del tráfico de golpe, el despliegue Canary (Canario) adopta un enfoque mucho más cauteloso, ideal para cambios de alto riesgo. El nombre proviene de la antigua práctica minera de llevar un canario a las minas de carbón; si el canario dejaba de cantar, indicaba la presencia de gases tóxicos, permitiendo a los mineros evacuar antes de sufrir daños.

En software, un despliegue Canary consiste en dirigir una pequeña fracción del tráfico de usuarios (por ejemplo, el 5%) a la nueva versión del servicio, mientras el 95% restante sigue siendo atendido por la versión antigua.

Durante esta fase, el equipo monitoriza intensamente las métricas de observabilidad de la nueva versión (tasas de error HTTP 500, latencia, consumo de CPU). Si las métricas son saludables, el tráfico se incrementa progresivamente (al 20%, luego al 50%, hasta llegar al 100%). Si en cualquier momento las métricas muestran degradación (el "canario muere"), el tráfico se devuelve al 100% a la versión antigua (manualmente o de forma automática si la plataforma lo permite), de modo que el fallo solo ha afectado a la fracción de usuarios dirigida al canario.

### 4.3. Ejemplo Práctico: Transición de Tráfico con YARP en .NET 10

Para implementar estos patrones, se requiere un proxy inverso o un API Gateway capaz de enrutar el tráfico dinámicamente. En el ecosistema .NET, YARP (Yet Another Reverse Proxy) es el proxy inverso de Microsoft, que se construye como una aplicación ASP.NET Core más. Paquete NuGet: `Yarp.ReverseProxy`.

Supongamos que tenemos dos destinos para el servicio de "Pagos" y queremos enviar el 90 % del tráfico a la versión estable (V1) y el 10 % a la nueva versión (V2). YARP trae estas políticas de balanceo integradas: `PowerOfTwoChoices` (la predeterminada), `FirstAlphabetical`, `Random`, `RoundRobin` y `LeastRequests`. Ninguna reparte por pesos, así que el reparto 90/10 se consigue con una política propia que lee un peso guardado en los metadatos (`Metadata`) de cada destino.

Configuración en `appsettings.json` del proyecto del gateway:

```json
{
  "ReverseProxy": {
    "Routes": {
      "ruta_pagos": {
        "ClusterId": "cluster_pagos",
        "Match": { "Path": "/api/pagos/{**catch-all}" }
      }
    },
    "Clusters": {
      "cluster_pagos": {
        "LoadBalancingPolicy": "Ponderada",
        "Destinations": {
          "pagos_v1_estable": {
            "Address": "https://pagos-v1.internal.example.com",
            "Metadata": { "Peso": "90" }
          },
          "pagos_v2_canary": {
            "Address": "https://pagos-v2.internal.example.com",
            "Metadata": { "Peso": "10" }
          }
        }
      }
    }
  }
}
```

#### 4.3.1. Política de balanceo ponderada

Una política de balanceo implementa `ILoadBalancingPolicy` (espacio de nombres `Yarp.ReverseProxy.LoadBalancing`). Tiene dos miembros: `Name`, el nombre que se usa en `LoadBalancingPolicy` de la configuración, y `PickDestination`, que recibe el `HttpContext` de la petición, el estado del clúster y la lista de destinos disponibles, y devuelve el destino elegido. Cada destino es un `DestinationState` (espacio de nombres `Yarp.ReverseProxy.Model`); su configuración está en `destino.Model.Config`, con `Address` y el diccionario `Metadata` (que puede ser null si el destino no declara metadatos).

`Program.cs` del gateway:

```csharp
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Las políticas propias se registran en DI como ILoadBalancingPolicy
builder.Services.AddSingleton<ILoadBalancingPolicy, PoliticaPonderada>();

var app = builder.Build();
app.MapReverseProxy();
app.Run();

public sealed class PoliticaPonderada : ILoadBalancingPolicy
{
    public string Name => "Ponderada";

    public DestinationState? PickDestination(
        HttpContext context, ClusterState cluster,
        IReadOnlyList<DestinationState> availableDestinations)
    {
        var total = availableDestinations.Sum(d => Peso(d));
        if (total == 0) return null; // ningún destino con peso: no se elige ninguno

        var tirada = Random.Shared.Next(total); // entero entre 0 y total - 1
        foreach (var destino in availableDestinations)
        {
            tirada -= Peso(destino);
            if (tirada < 0) return destino;
        }
        return null;
    }

    // Peso ausente, no numérico o negativo cuenta como 0
    private static int Peso(DestinationState destino) =>
        destino.Model.Config.Metadata is { } meta
        && meta.TryGetValue("Peso", out var valor)
        && int.TryParse(valor, out var peso) && peso > 0
            ? peso
            : 0;
}
```

**Cómo funciona:**

1. YARP recibe una petición a `/api/pagos/procesar`; la ruta `ruta_pagos` la asigna a `cluster_pagos`.
2. YARP busca entre las políticas registradas la que tiene el nombre indicado en `LoadBalancingPolicy` ("Ponderada") y le pasa los destinos disponibles.
3. La política suma los pesos (90 + 10 = 100), saca un número al azar entre 0 y 99 y recorre los destinos restando pesos: los valores 0-89 caen en la V1 y los 90-99 en la V2. Estadísticamente, unas 90 de cada 100 peticiones van a la V1.
4. Si `PickDestination` devuelve null, YARP no reenvía la petición y responde con un error 503.
5. Detalle importante: YARP solo llama a la política cuando hay dos o más destinos disponibles. Si solo queda uno, lo usa directamente, aunque su peso sea 0.
6. Si las métricas de la V2 son buenas, se cambian los pesos en la configuración (por ejemplo a 50/50 y después a 0/100). YARP recarga `appsettings.json` cuando cambia el archivo, sin reiniciar el gateway. Para revertir el canario basta con volver a 100/0. (La automatización de este proceso en entornos cloud se aborda en el tema 14).

Un `HttpContext` da acceso a la petición, así que una política también puede decidir por una cabecera (por ejemplo, `context.Request.Headers["X-Canal"]` devuelve los valores de esa cabecera, o un valor vacío si no viene) antes de aplicar el reparto por pesos.

#### 4.3.2. Comprobaciones de salud activas en el clúster

Para que el gateway deje de enviar tráfico a un destino caído, YARP puede sondear periódicamente un endpoint de salud de cada destino. Se configura en la sección `HealthCheck` del clúster:

```json
"cluster_pagos": {
  "LoadBalancingPolicy": "Ponderada",
  "HealthCheck": {
    "Active": {
      "Enabled": "true",
      "Interval": "00:00:10",
      "Timeout": "00:00:05",
      "Policy": "ConsecutiveFailures",
      "Path": "/health/ready"
    }
  },
  "Metadata": { "ConsecutiveFailuresHealthPolicy.Threshold": "3" },
  "Destinations": { }
}
```

- `Interval` y `Timeout` son la periodicidad del sondeo y su tiempo máximo (formato `hh:mm:ss`).
- `Policy` es la política que evalúa los sondeos; si se omite, YARP usa la integrada, `ConsecutiveFailures`, que marca un destino como no sano tras N sondeos fallidos seguidos (N se fija en el metadato del clúster `ConsecutiveFailuresHealthPolicy.Threshold`; por defecto, 2). Se considera correcta cualquier respuesta 2xx.
- La URL sondeada es la `Address` del destino más `Path`.
- Los destinos no sanos se excluyen de la lista que recibe `PickDestination`. Con la política de destinos disponibles predeterminada (`HealthyOrPanic`), si todos los destinos quedan no sanos, YARP vuelve a considerarlos todos disponibles en lugar de rechazar las peticiones.

El endpoint `/health/ready` lo expone cada microservicio, como se ve en el apartado 5.2.

## 5. Estructura Mínima de un Microservicio en .NET 10

Todo lo anterior se materializa en un proyecto ASP.NET Core pequeño pero completo. Este apartado reúne las piezas mínimas que debe tener cualquier microservicio del curso; los temas siguientes profundizan en cada una (comunicación HTTP en el tema 6, observabilidad en el 11, contenedores y Kubernetes en el 14).

### 5.1. Proyecto y Paquetes

Cada microservicio vive en su propio proyecto (y, preferiblemente, en su propio repositorio o carpeta con su pipeline):

```powershell
dotnet new webapi -n Pedidos.Api
cd Pedidos.Api
dotnet add package Asp.Versioning.Http
```

La plantilla `webapi` de .NET 10 genera un proyecto de Minimal APIs con `Microsoft.AspNetCore.OpenApi` ya referenciado. Su archivo de proyecto queda así (las versiones exactas las fija `dotnet add package`):

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.*" />
    <PackageReference Include="Asp.Versioning.Http" Version="10.*" />
  </ItemGroup>
</Project>
```

En `Program.cs`, la plantilla registra el documento OpenAPI (en .NET 10 también puede servirse en YAML):

```csharp
builder.Services.AddOpenApi();
// ...
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                  // /openapi/v1.json
    app.MapOpenApi("/openapi/{documentName}.yaml");    // mismo documento en YAML
}
```

ASP.NET Core 10 incorpora además validación integrada para Minimal APIs: tras `builder.Services.AddValidation();`, los parámetros y cuerpos anotados con atributos de DataAnnotations (`[Required]`, `[Range]`...) se validan antes de ejecutar el endpoint y, si no son válidos, se responde con un 400 en formato Problem Details. Es útil para hacer cumplir el contrato de entrada del servicio.

### 5.2. Comprobaciones de Salud: Vida y Disponibilidad

Un orquestador o un gateway necesita saber si una instancia está viva y si puede recibir tráfico. ASP.NET Core lo resuelve con las comprobaciones de salud (health checks), incluidas en el framework sin paquetes adicionales:

- **Vida (liveness):** ¿el proceso responde? No debe comprobar dependencias; si fallara por una base de datos caída, el orquestador reiniciaría instancias sanas sin motivo.
- **Disponibilidad (readiness):** ¿puede atender peticiones? Aquí sí se comprueban las dependencias imprescindibles (base de datos, broker).

```csharp
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

builder.Services.AddHealthChecks()
    .AddCheck("basedatos", () =>
        PuedoConectar() ? HealthCheckResult.Healthy()
                        : HealthCheckResult.Unhealthy("Sin conexión a la base de datos"),
        tags: ["ready"]);

// ...

// Vida: no ejecuta ninguna comprobación; responde 200 si el proceso atiende
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// Disponibilidad: solo las comprobaciones etiquetadas "ready"
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registro => registro.Tags.Contains("ready")
});
```

- `AddCheck(nombre, func, tags)` registra una comprobación a partir de una función que devuelve un `HealthCheckResult` (`Healthy`, `Degraded` o `Unhealthy`). `PuedoConectar()` representa aquí cualquier comprobación propia; para clases más elaboradas se implementa `IHealthCheck`.
- `HealthCheckOptions` está en el espacio de nombres `Microsoft.AspNetCore.Diagnostics.HealthChecks`. Su propiedad `Predicate` decide qué comprobaciones se ejecutan en cada endpoint.
- Por defecto, el endpoint responde 200 si el estado global es `Healthy` o `Degraded` y 503 si es `Unhealthy` (basta con que falle una comprobación); el cuerpo es el nombre del estado en texto plano.

### 5.3. Descubrimiento de Servicios por Configuración

Un microservicio no debería tener escritas en el código las direcciones físicas de los servicios a los que llama. El paquete `Microsoft.Extensions.ServiceDiscovery` permite usar nombres lógicos:

```csharp
builder.Services.AddServiceDiscovery();

builder.Services.AddHttpClient("catalogo", client =>
        client.BaseAddress = new Uri("https+http://catalogo"))
    .AddServiceDiscovery();
```

El esquema `https+http` indica que se busquen primero direcciones HTTPS y, si no hay, HTTP. Las direcciones se resuelven desde la sección `Services` de la configuración, que cambia por entorno sin tocar el código:

```json
{
  "Services": {
    "catalogo": {
      "https": ["localhost:7101"],
      "http": ["localhost:5101"]
    }
  }
}
```

En plataformas que ya ofrecen descubrimiento propio (Kubernetes, Azure Container Apps), el mismo código funciona resolviendo el nombre por DNS. El uso de estos clientes HTTP y su resiliencia se trata en el tema 6.

### 5.4. Empaquetado en Contenedor

Para que cada servicio se despliegue de forma independiente, se empaqueta en su propia imagen. El SDK de .NET 10 puede generarla sin Dockerfile:

```powershell
dotnet publish -c Release /t:PublishContainer
```

La construcción de imágenes con Dockerfile multietapa, su optimización y el despliegue en Kubernetes se estudian en el tema 14.

---

# Cuadernillos del Alumno (laboratorio)

En esta parte se aplica la teoría de la Parte I sobre el proyecto de ejemplo **B2BOrders**, extrayendo el módulo de Facturación (_Billing_) como primer microservicio.

---

## Cuadernillo 5.1: Extracción de Microservicios

**Bloque 5: Microservicios en .NET 10** · Duración: 10 min · Nivel: Avanzado

### Objetivos de la Lección

1. Comprender los criterios para extraer un Bounded Context (Facturación) fuera del Monolito.
2. Crear una Web API en .NET 10 desde cero y totalmente independiente.
3. Definir Contratos de Comunicación (DTOs) para desacoplar los servicios.

### Concepto Clave: ¿Por qué separar "Billing"?

No separamos servicios "porque sí" o "porque está de moda". Separamos Facturación porque cumple con los requisitos de un Subdominio Crítico:

| Razón                      | Explicación                                                                                                                                                              |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Aislamiento de Fallos**  | Si la tienda (Orders) se cae por un error de programación, el sistema de cobros (Billing) debe seguir vivo para procesar callbacks bancarios o devoluciones.             |
| **Seguridad y Compliance** | El manejo de pagos (tarjetas, PCI-DSS) requiere auditorías estrictas. Al aislarlo, reducimos el alcance de la auditoría a un solo proyecto pequeño.                      |
| **Ciclo de Vida Distinto** | Billing cambia poco (es muy estable), mientras que Orders cambia mucho (reglas de negocio, UI). No queremos desplegar Billing cada vez que cambiamos un color en la web. |

### Laboratorio: Creación del Microservicio

Vamos a crear un proyecto que no conoce al resto de la solución. No referencia a `Domain` ni a `Application`. Es un mundo aparte.

#### Paso 1: Generar el Proyecto

En tu terminal (carpeta raíz `C:\dev3\B2BOrders`), ejecuta:

```powershell
# 1. Crear la Web API vacía
dotnet new webapi -n B2BOrders.BillingService.Api

# 2. Añadirla a la solución (para verla en VS Code)
dotnet sln add B2BOrders.BillingService.Api/B2BOrders.BillingService.Api.csproj
```

#### Paso 2: Limpieza Inicial

Abre el nuevo proyecto.

1. Borra `WeatherForecast.cs` y su controlador si existen.
2. Limpia el `Program.cs` para dejar solo lo básico.

#### Paso 3: Definir el Servicio (`Program.cs`)

Copia este código en `B2BOrders.BillingService.Api/Program.cs`. Fíjate que incluimos los DTOs en el mismo archivo para simplificar (Vertical Slice Architecture).

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configuración básica
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// --- ENDPOINTS ---

// 1. Health Check simple
app.MapGet("/", () => "Billing Microservice is Online");

// 2. Proceso de Pago (Mock)
app.MapPost("/payments", (CreatePaymentRequest request) =>
{
    // Aquí simulamos la lógica compleja de conectar con el banco
    Console.WriteLine($"[Billing] Procesando pago para Orden #{request.OrderId}: {request.Amount} {request.Currency}");

    // Simulamos respuesta exitosa
    var response = new CreatePaymentResponse(
        PaymentId: Guid.NewGuid().ToString(),
        Status: "Approved"
    );

    return Results.Ok(response);
});

app.Run();

// --- CONTRATOS (DTOs) ---
// Estos objetos definen el lenguaje público de nuestro microservicio.

public record CreatePaymentRequest(
    int OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod
);

public record CreatePaymentResponse(
    string PaymentId,
    string Status
);
```

#### Paso 4: Ejecución y Prueba

Este proyecto debe correr en su propio puerto.

1. Ejecuta: `dotnet run --project B2BOrders.BillingService.Api`
2. Mira la consola para ver el puerto (ej: `http://localhost:5200`).
3. Abre otra terminal y prueba con `curl` o usa Postman:

```bash
# Ejemplo (ajusta el puerto a lo que te salga en consola)
curl -X POST http://localhost:5200/payments \
  -H "Content-Type: application/json" \
  -d '{"orderId": 123, "amount": 99.99, "currency": "EUR", "paymentMethod": "VISA"}'
```

### Checklist de Verificación

- [ ] Tengo un nuevo proyecto `B2BOrders.BillingService.Api` en la solución.
- [ ] Este proyecto NO tiene referencias a `B2BOrders.Domain` ni `Application`.
- [ ] Al ejecutarlo, responde en un puerto distinto al de la API principal.
- [ ] El endpoint `/payments` devuelve un JSON con `status: "Approved"`.

### Siguiente Paso

Ahora tenemos dos sistemas que no se hablan:

1. El Monolito (`B2BOrders.Api`) que quiere cobrar.
2. El Microservicio (`B2BOrders.BillingService.Api`) que sabe cobrar.

---

## Cuadernillo 5.2: Comunicación HTTP entre Servicios

**Bloque 5: Microservicios en .NET 10** · Duración: 10 min · Nivel: Avanzado

### Objetivos de la Lección

1. Implementar lógica de negocio real (validaciones y persistencia en memoria) en el microservicio.
2. Utilizar `HttpClient` y `IHttpClientFactory` para realizar llamadas HTTP tipadas.
3. Sustituir una implementación Fake por una implementación HTTP real sin modificar la lógica de aplicación (Patrón Adapter).

### Concepto Clave: Typed HttpClient

En lugar de crear `new HttpClient()` manualmente (lo cual puede agotar los sockets del servidor), utilizamos la inyección de dependencias de .NET.

- **Ventaja:** .NET gestiona el ciclo de vida de las conexiones subyacentes.
- **Tipado:** vinculamos una interfaz (`IBillingService`) a una implementación (`HttpBillingService`) y preconfiguramos su dirección base.

### Laboratorio 1: Lógica del Microservicio

Vamos a darle "inteligencia" al servicio de facturación para que no apruebe todo ciegamente.

**Archivo:** `B2BOrders.BillingService.Api/Program.cs`

```csharp
// "Base de datos" en memoria para persistir pagos mientras el servicio viva
var paymentsDatabase = new List<CreatePaymentResponse>();

app.MapPost("/payments", (CreatePaymentRequest request) =>
{
    // 1. Regla de Negocio: No se fía gratis
    if (request.Amount <= 0)
    {
        return Results.BadRequest(new { Error = "El monto debe ser positivo." });
    }

    Console.WriteLine($"[Billing] Procesando {request.Amount} {request.Currency} para Orden #{request.OrderId}");

    // 2. Simulación de proceso exitoso
    var response = new CreatePaymentResponse(
        PaymentId: Guid.NewGuid().ToString(),
        Status: "Approved"
    );

    // 3. Guardar en memoria
    paymentsDatabase.Add(response);

    return Results.Ok(response);
});
```

> **Nota:** recuerda mantener el microservicio ejecutándose en su propio puerto (ej: `http://localhost:5200`) para que el Monolito pueda llamarlo.

### Laboratorio 2: El Cliente HTTP (Monolito)

Ahora vamos al proyecto `B2BOrders.Infrastructure` para crear el adaptador real.

**Archivo:** `B2BOrders.Infrastructure/Billing/HttpBillingService.cs`

```csharp
using System.Net.Http.Json;
using B2BOrders.Application.Billing;

namespace B2BOrders.Infrastructure.Billing;

// DTOs internos para mapear la petición HTTP (Idénticos a los del Microservicio)
record ExternalPaymentRequest(int OrderId, decimal Amount, string Currency, string PaymentMethod);
record ExternalPaymentResponse(string PaymentId, string Status);

public class HttpBillingService : IBillingService
{
    private readonly HttpClient _httpClient;

    // El HttpClient ya viene configurado con la URL base gracias a Program.cs
    public HttpBillingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentResult> CreatePaymentAsync(int orderId, decimal amount, string currency)
    {
        var request = new ExternalPaymentRequest(orderId, amount, currency, "CreditCard");

        // Llamada síncrona HTTP
        var response = await _httpClient.PostAsJsonAsync("/payments", request);

        if (!response.IsSuccessStatusCode)
        {
            return new PaymentResult(false, null, $"Error HTTP: {response.StatusCode}");
        }

        var result = await response.Content.ReadFromJsonAsync<ExternalPaymentResponse>();

        return new PaymentResult(true, result?.PaymentId, null);
    }
}
```

### Laboratorio 3: Configuración (`Program.cs`)

Finalmente, conectamos los cables en el `B2BOrders.Api`.

**Archivo:** `B2BOrders.Api/Program.cs`

```csharp
// 1. Elimina o comenta la línea del Fake
// builder.Services.AddScoped<IBillingService, FakeBillingService>();

// 2. Registra el Cliente HTTP Tipado
builder.Services.AddHttpClient<IBillingService, HttpBillingService>(client =>
{
    // ¡IMPORTANTE!
    // Cambia este puerto por el que te salga al ejecutar B2BOrders.BillingService.Api
    client.BaseAddress = new Uri("http://localhost:5200");
});
```

### Checklist de Verificación

- [ ] El microservicio (`BillingService.Api`) tiene lógica para rechazar montos <= 0.
- [ ] He creado `HttpBillingService` en la capa de Infraestructura.
- [ ] En `Program.cs` (API), he sustituido `FakeBillingService` por `AddHttpClient<...>`.
- [ ] La URL en `BaseAddress` coincide con el puerto real de mi microservicio.

### Siguiente Paso

Ahora tenemos la infraestructura lista, pero aún no lo hemos probado todo junto.

En el Vídeo 5.3, haremos la Prueba de Integración End-to-End: levantaremos ambos proyectos a la vez y pagaremos un pedido real desde el Monolito.

---

## Cuadernillo 5.3: Orquestación End-to-End

**Bloque 5: Microservicios en .NET 10** · Duración: 10 min · Nivel: Avanzado

### Objetivos de la Lección

1. Orquestar un proceso de negocio complejo que involucra base de datos local y llamadas HTTP externas.
2. Refactorizar el Dominio para soportar tipos de datos reales (GUIDs como `string` en lugar de `int`).
3. Ejecutar una Arquitectura Distribuida (Monolito + Microservicio) en la máquina local.

### Laboratorio 1: Refactorización del Dominio (Realismo)

Durante la integración, descubrimos que los bancos devuelven IDs alfanuméricos (GUIDs), no enteros. Ajustamos el Dominio para soportarlo.

#### 1. El Evento de Dominio

**Archivo:** `B2BOrders.Domain/Orders/Events/OrderPaidDomainEvent.cs`

```csharp
namespace B2BOrders.Domain.Orders.Events;

// Cambio: PaymentId ahora es string
public record OrderPaidDomainEvent(int OrderId, string PaymentId, Money Amount) : IDomainEvent;
```

#### 2. La Entidad Order

**Archivo:** `B2BOrders.Domain/Orders/Order.cs`

```csharp
// Cambio en la firma del método
public void MarkAsPaid(string paymentId)
{
    if (Status == OrderStatus.Paid) return;

    // ... validaciones de importe ...
    Status = OrderStatus.Paid;

    // Pasamos el string sin convertir
    AddDomainEvent(new OrderPaidDomainEvent(Id, paymentId, TotalAmount));
}
```

### Laboratorio 2: Actualización de Infraestructura

Necesitamos capacidad para actualizar entidades existentes, no solo crearlas.

#### 1. La Interfaz

**Archivo:** `B2BOrders.Application/Orders/IOrderRepository.cs`

```csharp
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct);
    Task<Order?> GetByIdAsync(int id, CancellationToken ct);

    // Nuevo método necesario para el flujo de pago
    Task UpdateAsync(Order order, CancellationToken ct);
}
```

#### 2. La Implementación EF Core

**Archivo:** `B2BOrders.Infrastructure/Orders/EfCoreOrderRepository.cs`

```csharp
public async Task UpdateAsync(Order order, CancellationToken ct)
{
    _context.Orders.Update(order);
    await _context.SaveChangesAsync(ct);
}
```

### Laboratorio 3: La Orquestación (Handler)

El cerebro de la operación. Coordina la recuperación de datos, la llamada externa y la persistencia.

**Archivo:** `B2BOrders.Application/Orders/PayOrder/PayOrderHandler.cs`

```csharp
public class PayOrderHandler : IRequestHandler<PayOrderCommand, bool>
{
    private readonly IOrderRepository _repository;
    private readonly IBillingService _billingService; // Puerto de salida

    public PayOrderHandler(IOrderRepository repository, IBillingService billingService)
    {
        _repository = repository;
        _billingService = billingService;
    }

    public async Task<bool> Handle(PayOrderCommand request, CancellationToken ct)
    {
        // 1. Recuperar
        var order = await _repository.GetByIdAsync(request.OrderId, ct);
        if (order is null) return false;

        // 2. Llamar al Microservicio (HTTP)
        var paymentResult = await _billingService.CreatePaymentAsync(
            request.OrderId,
            order.TotalAmount.Amount,
            order.TotalAmount.Currency
        );

        // 3. Actualizar Estado
        if (paymentResult.Success)
        {
            // Usamos '!' porque confiamos en que si es Success, trae ID
            order.MarkAsPaid(paymentResult.PaymentId!);

            // 4. Guardar cambios
            await _repository.UpdateAsync(order, ct);
            return true;
        }

        return false;
    }
}
```

### Laboratorio 4: Ejecución Distribuida

Para probar esto, necesitamos levantar "toda la orquesta".

**Terminal 1: Microservicio (Billing)**

```powershell
# Puerto 5200 (o el que hayas configurado en el Monolito)
dotnet run --project B2BOrders.BillingService.Api --urls=http://localhost:5200
```

**Terminal 2: Monolito (API)**

```powershell
# Puerto 5000 (estándar)
dotnet run --project B2BOrders.Api --urls=http://localhost:5000
```

#### Prueba con Postman

1. **Insertar Pedido:** usa el endpoint `POST /customers` para crear un cliente y luego inserta un pedido manualmente en DB (o vía endpoint si lo tienes) con `Status = 0` (New) y `Amount = 100`.
2. **Pagar Pedido:**
   - `POST http://localhost:5000/orders/{ID}/pay`
   - Resultado esperado: `200 OK`
3. **Verificar:** revisa la base de datos. El pedido debe tener `Status = 1` (Paid).

### Resolución de Problemas (Troubleshooting)

| Error                                                              | Causa                                                                            | Solución                                                                                                |
| ------------------------------------------------------------------ | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `System.FormatException: Input string was not in a correct format` | Intentas convertir un GUID (texto) a `int` en el Handler.                        | Cambia `MarkAsPaid` para aceptar `string` como se indica en el Lab 1.                                   |
| `CS0535: ...does not implement interface member...`                | Añadiste `UpdateAsync` a la interfaz pero no a la clase `EfCoreOrderRepository`. | Implementa el método en el archivo de Infraestructura.                                                  |
| `Connection Refused` (al llamar a `/pay`)                          | El Monolito no encuentra al Microservicio.                                       | Asegúrate de que el Microservicio está corriendo y que el puerto en `Program.cs` del Monolito coincide. |
