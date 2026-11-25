# Tema 8: EF Core 10 Avanzado\*\*

## Índice

**Manual I: Modelado de Datos Complejos y Optimización de Consultas**

1. Modelado de Dominio Rico sin Normalización Excesiva
2. Integración de Columnas JSON para Estructuras Dinámicas
3. Técnicas Conceptuales de Optimización de Consultas
4. Gestión del Rastreo (Tracking) y Reducción de Roundtrips
5. Uso Adecuado de Filtros Globales

**Cuadernillos del Alumno (laboratorio)**

- 8.1 Mapeo Avanzado y Soft Delete
- 8.2 Consultas Complejas y Rendimiento
- 8.3 Tests de Integración con Docker

---

# Manual I: Modelado de Datos Complejos y Optimización de Consultas

## 1. Modelado de Dominio Rico sin Normalización Excesiva

El diseño de aplicaciones empresariales modernas requiere que el modelo de objetos refleje fielmente las reglas del negocio. En el pasado, las limitaciones de los ORM (Object-Relational Mappers) obligaban a los desarrolladores a adaptar su código a la estructura de la base de datos, resultando en modelos anémicos y una normalización excesiva que fragmentaba la información. Entity Framework Core 10 proporciona herramientas avanzadas para mapear modelos de dominio ricos directamente a esquemas de base de datos eficientes, permitiendo agrupar conceptos lógicos sin penalizar el rendimiento.

### 1.1. Tipos Complejos (Complex Types) en EF Core 10

Los Tipos Complejos (Complex Types) son una característica fundamental para implementar el patrón Value Object (Objeto de Valor) del Domain-Driven Design (DDD). Un Value Object es un objeto que se define por sus atributos en lugar de por una identidad única. Por ejemplo, una dirección postal o un importe monetario no necesitan un identificador (ID) propio; su valor reside en la combinación de sus propiedades.

A diferencia de las entidades tradicionales, los Tipos Complejos en EF Core 10 presentan las siguientes características conceptuales:

- **Ausencia de Identidad:** no poseen una clave primaria (`Id`). Dos instancias con los mismos valores se consideran conceptualmente idénticas.
- **Mapeo en Línea (Table Splitting) o en JSON:** por defecto, las propiedades del Tipo Complejo se mapean como columnas en la misma tabla que la entidad que lo contiene. Si una entidad `Usuario` tiene un Tipo Complejo `Direccion` con las propiedades `Calle` y `Ciudad`, la tabla `Usuarios` tendrá las columnas `Direccion_Calle` y `Direccion_Ciudad`. Desde EF Core 10 también pueden guardarse en una única columna JSON (apartado 2).
- **Inmutabilidad:** aunque EF Core permite que sean mutables, se recomienda diseñarlos como inmutables (por ejemplo, con `record`), reemplazando la instancia completa cuando un valor cambia (`factura.Direccion = factura.Direccion with { Ciudad = "Sevilla" }`). EF Core sigue detectando los cambios propiedad a propiedad y solo actualiza las columnas modificadas.
- **Semántica de valor:** la misma instancia puede asignarse a varias propiedades (por ejemplo, `pedido.DireccionFacturacion = pedido.DireccionEnvio`): EF Core copia los valores. Con las entidades propias (_owned_), que internamente sí tienen identidad, esa asignación falla. Por la misma razón, en LINQ dos tipos complejos se comparan por su contenido.
- **Novedades de EF Core 10:** tipos complejos opcionales (una propiedad `Direccion?` que puede ser null; el tipo debe tener al menos una propiedad obligatoria), tipos complejos definidos como `struct`, colecciones de tipos complejos y mapeo a JSON.
- **Configuración explícita:** a diferencia de las entidades, no se descubren por convención: se declaran con el atributo `[ComplexType]` o con `ComplexProperty(...)` en `OnModelCreating`.

El uso de Tipos Complejos evita la creación de tablas satélite innecesarias (desnormalización controlada), de modo que leer la entidad no requiere operaciones `JOIN`.

### 1.2. Colecciones Propias (Owned Collections) y Colecciones Complejas

Mientras que un Tipo Complejo simple resuelve relaciones uno a uno, muchos agregados contienen colecciones de elementos que no tienen sentido sin su padre. Si un `Pedido` tiene una lista de `LineasDePedido`, estas líneas no existen de forma independiente: su ciclo de vida está ligado al agregado (Aggregate Root). EF Core 10 ofrece dos formas de modelarlo:

- **Colecciones propias (`OwnsMany`):** los elementos son entidades propias (_owned entities_). Se guardan por defecto en una tabla separada, con una clave foránea hacia el padre y una clave propia que EF Core gestiona y oculta en el modelo de objetos. Al guardar el padre, EF Core inserta, actualiza o elimina las filas hijas, y al consultar el padre las carga siempre con él. Al estar en su propia tabla, sus filas se pueden indexar, filtrar y agregar con SQL como cualquier otra tabla.
- **Colecciones complejas (`ComplexCollection`, nuevas en EF Core 10):** los elementos son tipos complejos, sin identidad. En bases de datos relacionales solo pueden guardarse en una columna JSON (`ToJson()`), como un array dentro de la fila del padre; no pueden mapearse a otra tabla. Sus elementos deben ser clases o `record` (no `struct`).

**Criterio práctico:** si la colección es grande, crece con el tiempo o se consulta y agrega por separado en SQL, conviene una tabla (colección propia o, si los elementos necesitan identidad propia, entidades normales). Si es un conjunto acotado de valores que se leen y escriben siempre junto con el padre, una colección compleja en JSON evita la tabla y el `JOIN`.

### 1.3. Ejemplo Práctico: Implementación de Value Objects con Tipos Complejos

A continuación, se detalla cómo implementar un modelo de dominio rico utilizando Tipos Complejos para representar un importe monetario y una dirección, integrándolos en una entidad `Factura`.

Primero, definimos los Tipos Complejos como `record` inmutables:

```csharp
using System.ComponentModel.DataAnnotations.Schema;

// Definición del Value Object para Dinero
[ComplexType]
public record Dinero(decimal Importe, string Moneda)
{
    // Lógica de dominio encapsulada
    public Dinero Sumar(Dinero otro)
    {
        if (Moneda != otro.Moneda)
            throw new InvalidOperationException("No se pueden sumar monedas distintas.");
        return new Dinero(Importe + otro.Importe, Moneda);
    }
}

// Definición del Value Object para Dirección
[ComplexType]
public record DireccionPostal(string Calle, string Ciudad, string CodigoPostal, string Pais);
```

El atributo `[ComplexType]` está en el espacio de nombres `System.ComponentModel.DataAnnotations.Schema`. EF Core puede crear las instancias de estos `record` posicionales usando su constructor, porque los nombres de los parámetros coinciden con los de las propiedades.

A continuación, integramos estos Tipos Complejos en nuestra entidad principal (Aggregate Root). EF Core puede usar un constructor para crear entidades, pero solo enlaza parámetros que corresponden a propiedades escalares; no puede pasar tipos complejos ni navegaciones por constructor. Por eso la entidad declara además un constructor privado sin parámetros, que EF Core usa al materializar las filas:

```csharp
public class Factura
{
    public int Id { get; private set; }
    public string NumeroFactura { get; private set; } = null!;

    // Propiedades que utilizan los Tipos Complejos
    public Dinero Total { get; private set; } = null!;
    public DireccionPostal DireccionFacturacion { get; private set; } = null!;

    // Constructor para EF Core
    private Factura() { }

    // Constructor principal para el código de la aplicación
    public Factura(string numeroFactura, Dinero total, DireccionPostal direccionFacturacion)
    {
        NumeroFactura = numeroFactura;
        Total = total;
        DireccionFacturacion = direccionFacturacion;
    }

    // Método para actualizar la dirección (reemplazando el Value Object completo)
    public void ActualizarDireccion(DireccionPostal nuevaDireccion)
    {
        DireccionFacturacion = nuevaDireccion;
    }
}
```

Finalmente, configuramos el `DbContext`. Aunque hemos usado el atributo `[ComplexType]`, es una buena práctica conocer la configuración mediante Fluent API:

```csharp
using Microsoft.EntityFrameworkCore;

public class FacturacionDbContext(DbContextOptions<FacturacionDbContext> options) : DbContext(options)
{
    public DbSet<Factura> Facturas => Set<Factura>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Factura>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Configuración explícita del Tipo Complejo (opcional si se usa el atributo)
            entity.ComplexProperty(e => e.Total, dinero =>
            {
                dinero.Property(d => d.Importe).HasColumnName("Total_Importe").HasPrecision(18, 2);
                dinero.Property(d => d.Moneda).HasColumnName("Total_Moneda").HasMaxLength(3);
            });

            entity.ComplexProperty(e => e.DireccionFacturacion, direccion =>
            {
                direccion.Property(d => d.Calle).HasColumnName("Dir_Calle").HasMaxLength(200);
                direccion.Property(d => d.Ciudad).HasColumnName("Dir_Ciudad").HasMaxLength(100);
                direccion.Property(d => d.CodigoPostal).HasColumnName("Dir_CP").HasMaxLength(10);
                direccion.Property(d => d.Pais).HasColumnName("Dir_Pais").HasMaxLength(50);
            });
        });
    }
}
```

**Beneficios de este enfoque:** la tabla `Facturas` resultante tendrá columnas como `Total_Importe`, `Total_Moneda`, `Dir_Calle`, etc. (sin `HasColumnName`, EF Core las llamaría `Total_Importe`, `DireccionFacturacion_Calle`...). No hay tablas adicionales ni claves foráneas. El modelo de objetos encapsula reglas de negocio (como la validación al sumar monedas) y la lectura de una factura no necesita `JOIN`. Los miembros de un tipo complejo también se pueden usar en consultas: `context.Facturas.Where(f => f.Total.Moneda == "EUR")` filtra por la columna `Total_Moneda`.

## 2. Integración de Columnas JSON para Estructuras Dinámicas

Uno de los mayores desafíos en el diseño de bases de datos relacionales es el manejo de datos dinámicos o semiestructurados. Tradicionalmente, cuando los requisitos exigían almacenar atributos variables (por ejemplo, las especificaciones técnicas de diferentes tipos de productos), los arquitectos recurrían al antipatrón EAV (Entity-Attribute-Value). Este enfoque penaliza el rendimiento y complica las consultas, porque reconstruir una sola entidad exige múltiples `JOIN` o agrupaciones.

EF Core ofrece soporte para columnas JSON, que en EF Core 10 se amplía notablemente, permitiendo combinar la robustez del modelo relacional con la flexibilidad de las bases de datos documentales (NoSQL) dentro del mismo motor de base de datos.

### 2.1. Concepto y Casos de Uso de Columnas JSON

Una columna JSON permite almacenar un documento completo (un objeto o una matriz de objetos) dentro de una única celda de una tabla relacional. Los motores de bases de datos modernos (como SQL Server, PostgreSQL y MySQL) son capaces de indexar y consultar el interior de estos documentos JSON de forma nativa.

**Casos de uso ideales para columnas JSON:**

- **Catálogos de productos heterogéneos:** un ordenador portátil tiene atributos como "Memoria RAM" y "Procesador", mientras que una camiseta tiene "Talla" y "Color". Almacenar estos atributos específicos en una columna JSON evita crear muchas columnas casi siempre nulas o tablas satélite.
- **Configuraciones de usuario:** preferencias de la interfaz, notificaciones o ajustes específicos que cambian frecuentemente y no requieren integridad referencial estricta.
- **Datos de integraciones externas (Webhooks):** almacenar el payload original de un sistema de terceros (como la respuesta de una pasarela de pago) para auditoría o procesamiento diferido.
- **Formularios dinámicos:** respuestas a encuestas donde las preguntas pueden variar según el usuario.

**Cuándo NO usar columnas JSON:**

- Cuando los datos almacenados en el JSON son el objetivo principal de relaciones de clave foránea.
- Cuando se requiere actualizar frecuentemente propiedades individuales muy anidadas dentro del documento (las actualizaciones masivas de JSON pueden ser costosas).
- Cuando los documentos son muy grandes y la mayoría de las consultas solo necesitan una pequeña parte de ellos, porque leer la fila implica leer el documento completo.

### 2.2. Mapeo de Tipos Complejos a Documentos JSON

EF Core permite guardar una propiedad (un objeto o una colección) en una única columna JSON indicando `.ToJson()` en su configuración. En EF Core 10 la forma recomendada es hacerlo con tipos complejos:

```csharp
modelBuilder.Entity<Producto>(b =>
{
    b.ComplexProperty(p => p.Especificaciones, e => e.ToJson());   // objeto -> columna JSON
    b.ComplexCollection(p => p.Etiquetas, e => e.ToJson());        // colección -> array JSON
});
```

Hasta EF Core 9 se usaban entidades propias (`OwnsOne(...).ToJson()` y `OwnsMany(...).ToJson()`), que siguen funcionando. La documentación de EF Core 10 recomienda los tipos complejos para JSON porque tienen semántica de valor y porque solo con ellos funciona `ExecuteUpdateAsync` sobre propiedades de la columna JSON.

Al hacer esto, EF Core se encarga de:

1. **Serialización/Deserialización Automática:** convierte los objetos C# a JSON al guardar, y viceversa al leer, de forma transparente para el desarrollador. Los nombres de las propiedades del documento son, por defecto, los de las propiedades C#.
2. **Traducción de Consultas LINQ:** si escribes una consulta LINQ que filtra por una propiedad dentro del documento JSON, EF Core la traduce a las funciones JSON del motor (por ejemplo, `JSON_VALUE` en SQL Server). El filtrado se ejecuta en el servidor de base de datos, no en la memoria de la aplicación.
3. **Actualizaciones masivas dentro del JSON (EF Core 10):** `ExecuteUpdateAsync` puede modificar propiedades del documento sin cargar las entidades.

**Tipo `json` de SQL Server 2025 y Azure SQL.** Hasta ahora SQL Server guardaba el JSON en columnas `nvarchar(max)`. EF Core 10 usa automáticamente el tipo nativo `json` si el contexto se configura con `UseAzureSql` o con un nivel de compatibilidad 170 o superior (SQL Server 2025):

```csharp
options.UseSqlServer(cadena, o => o.UseCompatibilityLevel(170));
```

En versiones anteriores de SQL Server y en otros motores, el JSON se sigue guardando como texto. Si una aplicación existente ya usa columnas JSON en `nvarchar`, la primera migración con nivel 170 las convierte a `json`; puede evitarse fijando el tipo de columna con `HasColumnType("nvarchar(max)")`.

### 2.3. Ejemplo Práctico: Catálogo de Productos con Atributos Variables en JSON

Vamos a modelar un sistema de comercio electrónico donde los productos tienen especificaciones dinámicas. Paquete: `Microsoft.EntityFrameworkCore.SqlServer`.

```csharp
public class Producto
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = null!;
    public string Categoria { get; private set; } = null!;
    public decimal PrecioBase { get; private set; }

    // Tipo complejo que se almacenará como documento JSON
    public EspecificacionesTecnicas Especificaciones { get; private set; } = null!;

    // Colección compleja que se almacenará como array JSON en otra columna
    public List<EtiquetaPromocional> Etiquetas { get; private set; } = [];

    private Producto() { }   // para EF Core

    public Producto(string nombre, string categoria, decimal precioBase, EspecificacionesTecnicas especificaciones)
    {
        Nombre = nombre;
        Categoria = categoria;
        PrecioBase = precioBase;
        Especificaciones = especificaciones;
    }

    public void AplicarIncremento(decimal factor) => PrecioBase *= factor;
}

public class EspecificacionesTecnicas
{
    public string Fabricante { get; set; } = "";
    public string Modelo { get; set; } = "";
    public int GarantiaMeses { get; set; }
    public List<AtributoDinamico> AtributosDinamicos { get; set; } = [];
}

public record AtributoDinamico(string Clave, string Valor);
public record EtiquetaPromocional(string NombreCampaña, DateTime FechaExpiracion);

public class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTime FechaCreacion { get; set; }
    public string Mensaje { get; set; } = "";
}
```

A continuación, configuramos el mapeo en el `DbContext`:

```csharp
using Microsoft.EntityFrameworkCore;

public class CatalogoDbContext(DbContextOptions<CatalogoDbContext> options) : DbContext(options)
{
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<RegistroAuditoria> Registros => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Producto>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Categoria).HasMaxLength(50);
            entity.Property(e => e.PrecioBase).HasPrecision(18, 2);
            entity.HasIndex(e => e.Categoria);

            // Objeto -> columna JSON; su colección anidada se declara dentro
            entity.ComplexProperty(e => e.Especificaciones, esp =>
            {
                esp.ToJson();
                esp.ComplexCollection(e => e.AtributosDinamicos);
            });

            // Colección -> array JSON en su propia columna
            entity.ComplexCollection(e => e.Etiquetas, et => et.ToJson());
        });
    }
}
```

La tabla `Productos` tendrá, además de las columnas escalares, las columnas `Especificaciones` y `Etiquetas`, cada una con un documento JSON. Las especificaciones de un producto se guardarían así:

```json
{
  "Fabricante": "Fabrikam",
  "GarantiaMeses": 24,
  "Modelo": "X1",
  "AtributosDinamicos": [{ "Clave": "RAM", "Valor": "16 GB" }]
}
```

**Consultando dentro del JSON.** Supongamos que queremos buscar todos los productos del fabricante "Fabrikam" que tengan una garantía superior a 12 meses.

```csharp
public async Task<List<Producto>> BuscarProductosPorGarantiaAsync(CatalogoDbContext context)
{
    // EF Core traduce el acceso a propiedades del JSON a funciones JSON del motor
    return await context.Productos
        .Where(p => p.Especificaciones.Fabricante == "Fabrikam" &&
                    p.Especificaciones.GarantiaMeses > 12)
        .ToListAsync();
}
```

En SQL Server 2025, con el tipo `json`, la consulta generada tiene esta forma (simplificada; el texto exacto depende de la versión del proveedor y del motor):

```sql
SELECT [p].[Id], [p].[Categoria], [p].[Nombre], [p].[PrecioBase], [p].[Especificaciones], [p].[Etiquetas]
FROM [Productos] AS [p]
WHERE JSON_VALUE([p].[Especificaciones], '$.Fabricante' RETURNING nvarchar(max)) = N'Fabrikam'
  AND JSON_VALUE([p].[Especificaciones], '$.GarantiaMeses' RETURNING int) > 12
```

Y, desde EF Core 10, una propiedad del documento puede actualizarse en bloque sin cargar los productos:

```csharp
await context.Productos
    .Where(p => p.Especificaciones.Fabricante == "Fabrikam")
    .ExecuteUpdateAsync(s => s.SetProperty(
        p => p.Especificaciones.GarantiaMeses,
        p => p.Especificaciones.GarantiaMeses + 12));
```

Este enfoque elimina la necesidad de tablas adicionales y delega el filtrado al motor. Si una propiedad del JSON se usa constantemente como filtro, conviene valorar si debería ser una columna normal, más sencilla de indexar, en lugar de vivir dentro del documento.

## 3. Técnicas Conceptuales de Optimización de Consultas

Escribir código que funcione es solo el primer paso; escribir código que escale requiere una mentalidad orientada a la eficiencia. En el acceso a datos, el rendimiento se degrada a menudo por la acumulación de pequeñas ineficiencias (traer datos innecesarios o realizar demasiados viajes a la base de datos) que se notan sobre todo bajo carga.

### 3.1. Proyecciones Ligeras y Selección Específica de Datos

El error más común en el uso de ORMs es recuperar entidades completas cuando solo se necesita una fracción de su información. Utilizar métodos como `.Include()` para cargar relaciones enteras y luego enviar esos datos a una vista o API consume memoria, ancho de banda y ciclos de CPU de forma innecesaria.

Las Proyecciones Ligeras consisten en utilizar el operador `.Select()` de LINQ para transformar las entidades del dominio directamente en objetos de transferencia de datos (DTOs) o tipos anónimos en el momento de la consulta.

**Beneficios de las proyecciones:**

1. **Reducción del payload de red:** la base de datos solo devuelve las columnas explícitamente solicitadas.
2. **Sin rastreo para los DTO:** el Change Tracker solo rastrea instancias de entidades. Si la proyección devuelve un DTO o un tipo anónimo sin entidades dentro, no hay nada que rastrear, aunque no se use `AsNoTracking()`. (Si la proyección incluye una entidad completa, esa entidad sí se rastrea).
3. **Evita el sobre-fetching:** no se cargan propiedades pesadas (como columnas de texto largo o datos binarios) si no son necesarias para la operación actual.

### 3.2. Split Queries para Evitar la Explosión Cartesiana

Cuando se utiliza `.Include()` para cargar múltiples colecciones relacionadas (por ejemplo, un `Pedido` que incluye `LineasDePedido` y `ComentariosDelCliente`), las bases de datos relacionales resuelven esto mediante operaciones `JOIN`.

Si un pedido tiene 10 líneas y 5 comentarios, el resultado del `JOIN` generará 50 filas (10 × 5) en el conjunto de resultados devuelto por la base de datos. La información del pedido principal se duplicará 50 veces. A esto se le conoce como **Explosión Cartesiana**. A medida que las colecciones crecen, el volumen de datos redundantes se multiplica.

Para mitigar esto, EF Core introdujo las Split Queries (Consultas Divididas). Al añadir `.AsSplitQuery()` a una consulta LINQ, EF Core divide la operación en múltiples consultas SQL independientes (una para el padre y una por cada colección hija). Esto aumenta el número de viajes a la base de datos (_roundtrips_), pero evita el producto de filas: el total pasa a ser la suma de las filas de cada consulta. Es la opción preferida cuando se cargan colecciones grandes o varias colecciones a la vez.

**Precauciones de las split queries:**

- Las consultas se ejecutan por separado; si otro proceso modifica los datos entre ellas, el resultado puede ser incoherente, salvo que se ejecuten dentro de una transacción con el aislamiento adecuado.
- Con `Skip`/`Take` deben llevar un `OrderBy` sobre una clave única para que todas las consultas vean las mismas filas (EF Core 10 corrige además un caso en que la ordenación no era coherente entre consultas).
- Pueden activarse para todo el contexto con `UseSqlServer(cadena, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))` y, en ese caso, volver a una única consulta con `.AsSingleQuery()`.

### 3.3. El Problema N+1

El problema N+1 aparece cuando se ejecuta una consulta para obtener N elementos y después una consulta adicional por cada uno:

```csharp
// Antipatrón: 1 consulta para los pedidos + 1 consulta por pedido
var pedidos = await context.Pedidos.ToListAsync();
foreach (var pedido in pedidos)
{
    var articulos = await context.Lineas
        .Where(l => l.PedidoId == pedido.Id)
        .SumAsync(l => l.Cantidad);
}
```

Con 200 pedidos se ejecutan 201 consultas. El mismo efecto se produce, de forma menos visible, con la carga diferida (_lazy loading_), en la que cada acceso a una navegación no cargada lanza una consulta.

La solución es pedir todo en una sola consulta, con una proyección (apartado 3.4) o con `Include`. Para detectarlo, basta con registrar las consultas SQL en desarrollo (por ejemplo, `optionsBuilder.LogTo(Console.WriteLine)`) y contar cuántas se ejecutan por operación.

### 3.4. Ejemplo Práctico: Refactorización de una Consulta Pesada

Imaginemos un escenario donde necesitamos mostrar un listado de pedidos en un panel de control (dashboard). Solo necesitamos el número de pedido, el nombre del cliente y la cantidad total de artículos. El modelo de ventas es el siguiente:

```csharp
public class Cliente { public int Id { get; set; } public string Nombre { get; set; } = ""; }

public class Pedido
{
    public int Id { get; set; }
    public string NumeroPedido { get; set; } = "";
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public List<LineaPedido> Lineas { get; set; } = [];
    public List<CambioEstado> HistorialEstados { get; set; } = [];
    public List<Comentario> Comentarios { get; set; } = [];
}

public class LineaPedido { public int Id { get; set; } public int PedidoId { get; set; } public int Cantidad { get; set; } }
public class CambioEstado { public int Id { get; set; } public int PedidoId { get; set; } public string Estado { get; set; } = ""; }
public class Comentario { public int Id { get; set; } public int PedidoId { get; set; } public string Texto { get; set; } = ""; }

public class VentasDbContext(DbContextOptions<VentasDbContext> options) : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<LineaPedido> Lineas => Set<LineaPedido>();
}
```

**Antipatrón (consulta pesada e ineficiente):**

```csharp
// Antipatrón: carga entidades completas, usa Include innecesariamente y rastrea los objetos
public async Task<List<Pedido>> ObtenerPedidosDashboardMaloAsync(VentasDbContext context)
{
    return await context.Pedidos
        .Include(p => p.Cliente)
        .Include(p => p.Lineas) // Riesgo de explosión cartesiana si hubiera más includes
        .ToListAsync();
}
```

**Solución optimizada (proyección ligera).** Creamos un DTO específico para el caso de uso:

```csharp
public record PedidoDashboardDto(string NumeroPedido, string NombreCliente, int TotalArticulos);
```

Y refactorizamos la consulta utilizando `.Select()`:

```csharp
// Recomendado: solo recupera las columnas necesarias y no rastrea nada
public async Task<List<PedidoDashboardDto>> ObtenerPedidosDashboardOptimoAsync(VentasDbContext context)
{
    return await context.Pedidos
        .Select(p => new PedidoDashboardDto(
            p.NumeroPedido,
            p.Cliente.Nombre,              // EF Core hace el JOIN automáticamente solo para esta columna
            p.Lineas.Sum(l => l.Cantidad)  // Se traduce a un SUM() en SQL, no trae las líneas a memoria
        ))
        .ToListAsync();
}
```

**Ejemplo de uso de Split Queries.** Si realmente necesitamos cargar la entidad completa con sus colecciones para realizar operaciones de dominio complejas, debemos proteger la consulta contra la explosión cartesiana:

```csharp
public async Task<Pedido?> CargarPedidoParaProcesamientoAsync(VentasDbContext context, int pedidoId)
{
    return await context.Pedidos
        .Where(p => p.Id == pedidoId)
        .Include(p => p.Lineas)
        .Include(p => p.HistorialEstados)
        .Include(p => p.Comentarios)
        .AsSplitQuery() // divide la carga en 4 consultas SQL
        .FirstOrDefaultAsync();
}
```

En este último ejemplo, en lugar de un `JOIN` masivo, EF Core ejecutará una consulta para el `Pedido`, otra para las `Lineas`, otra para el `HistorialEstados` y otra para los `Comentarios`, y ensamblará el objeto final en memoria. Si el pedido tiene 10 líneas, 3 cambios de estado y 5 comentarios, la consulta única devolvería 10 × 3 × 5 = 150 filas; las cuatro consultas separadas devuelven 1 + 10 + 3 + 5 = 19.

## 4. Gestión del Rastreo (Tracking) y Reducción de Roundtrips

El Change Tracker (Rastreador de Cambios) es el componente central que permite a EF Core saber qué entidades han sido modificadas, añadidas o eliminadas, para generar las sentencias `UPDATE`, `INSERT` o `DELETE` correspondientes al llamar a `SaveChanges()`. Sin embargo, este mecanismo tiene un coste en memoria y CPU. Comprender cuándo activarlo y cómo evitar viajes innecesarios a la base de datos (roundtrips) es vital para el rendimiento a gran escala.

### 4.1. Consultas No Rastreadas (AsNoTracking) y su Impacto

Por defecto, cualquier consulta que devuelva tipos de entidad (clases mapeadas en el `DbContext`) es rastreada. EF Core toma una "instantánea" de los valores originales y mantiene referencias en memoria.

Cuando se construyen APIs RESTful o servicios que solo devuelven datos (operaciones de solo lectura), este rastreo consume recursos sin aportar nada. Utilizar el método `.AsNoTracking()` indica a EF Core que no almacene las entidades en el Change Tracker.

**Consideraciones importantes:**

- **Rendimiento:** las consultas `AsNoTracking` evitan el trabajo de registrar las entidades y guardar sus instantáneas, por lo que suelen ser más rápidas y consumir menos memoria. Las entidades devueltas no se guardan con `SaveChanges` salvo que se adjunten de nuevo (`Update`/`Attach`).
- **Por defecto en todo el contexto:** `optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)`; las consultas que necesiten rastreo usan entonces `.AsTracking()`.
- **Resolución de Identidad:** con `AsNoTracking`, si una consulta devuelve el mismo cliente asociado a cinco pedidos diferentes, EF Core creará cinco instancias distintas del objeto `Cliente` en memoria. Si esto es un problema (por consumo de memoria o lógica de presentación), se debe utilizar `.AsNoTrackingWithIdentityResolution()`, que desactiva el rastreo de cambios pero mantiene un mapa temporal para asegurar que la misma entidad se instancie solo una vez.

### 4.2. Estrategias para la Reducción de Viajes a la Base de Datos (Roundtrips)

Un "roundtrip" es el viaje completo de una solicitud desde la aplicación hasta el servidor de base de datos y su correspondiente respuesta. Cada roundtrip añade la latencia de red, por lo que reducir su número es una de las optimizaciones más rentables.

El patrón tradicional de actualización en EF Core requiere dos roundtrips:

1. `SELECT` para cargar la entidad en memoria.
2. Modificar las propiedades en C#.
3. `UPDATE` al llamar a `SaveChanges()`.

Si necesitamos actualizar 1.000 registros (por ejemplo, aplicar un descuento masivo), el enfoque tradicional cargaría 1.000 objetos en memoria y enviaría 1.000 sentencias `UPDATE` (agrupadas en lotes por EF Core), lo cual es ineficiente.

Desde EF Core 7 existen `ExecuteUpdateAsync` y `ExecuteDeleteAsync`, que ejecutan sentencias `UPDATE` y `DELETE` directamente en la base de datos sin cargar las entidades, en un único roundtrip. EF Core 10 añade dos mejoras: admiten propiedades dentro de columnas JSON (apartado 2.3) y `ExecuteUpdateAsync` acepta una lambda normal, lo que permite construir las asignaciones con condiciones.

Conviene recordar que estas operaciones no pasan por el Change Tracker ni por `SaveChanges`: no actualizan las entidades ya cargadas en el contexto, no comprueban tokens de concurrencia y no ejecutan la lógica que se haya añadido a `SaveChanges` (como el borrado lógico del apartado 5.3). Los filtros globales, en cambio, sí se aplican a su `WHERE`.

**Otras técnicas para reducir roundtrips:**

- Cargar en una sola consulta lo que se va a usar (proyecciones, `Include`), evitando el N+1.
- Agrupar varios cambios en una sola llamada a `SaveChanges`, que EF Core envía en lotes.
- Usar el operador `LeftJoin` de .NET 10, que EF Core 10 traduce a `LEFT JOIN`, en lugar de combinar datos en memoria:

```csharp
var informe = await context.Pedidos
    .LeftJoin(context.Clientes,
        pedido => pedido.ClienteId,
        cliente => cliente.Id,
        (pedido, cliente) => new { pedido.NumeroPedido, Cliente = cliente != null ? cliente.Nombre : "[sin cliente]" })
    .ToListAsync();
```

### 4.3. Consultas Compiladas y Pooling de DbContext

Cada vez que se ejecuta una consulta LINQ, EF Core debe comprobar si ya tiene su SQL en caché, lo que implica recorrer el árbol de expresión. Para consultas que se ejecutan con mucha frecuencia, las consultas compiladas hacen ese trabajo una sola vez:

```csharp
public record ProductoResumen(int Id, string Nombre, decimal Precio);

public static class ConsultasCatalogo
{
    // Se compila una vez; después se invoca como una función
    public static readonly Func<CatalogoDbContext, int, Task<ProductoResumen?>> ResumenPorId =
        EF.CompileAsyncQuery((CatalogoDbContext ctx, int id) =>
            ctx.Productos
               .Where(p => p.Id == id)
               .Select(p => new ProductoResumen(p.Id, p.Nombre, p.PrecioBase))
               .FirstOrDefault());
}

// Uso
var resumen = await ConsultasCatalogo.ResumenPorId(context, 42);
```

`EF.CompileAsyncQuery` recibe una lambda cuyo primer parámetro es el contexto y los siguientes, los parámetros de la consulta; dentro se usan los operadores síncronos (`FirstOrDefault`, `Where`...), y el delegado resultante es asíncrono. Para consultas que devuelven varias filas, el delegado devuelve un `IAsyncEnumerable<T>` que se recorre con `await foreach`. La ganancia solo se nota en rutas muy frecuentes y conviene medirla (tema 2).

**Pooling de DbContext.** Crear un `DbContext` es barato, pero en servicios con mucho tráfico se puede reutilizar un conjunto de instancias:

```csharp
builder.Services.AddDbContextPool<CatalogoDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Catalogo")));
```

Al devolver una instancia al pool, EF Core restablece su estado (Change Tracker incluido). La contrapartida es que el contexto no puede guardar estado propio que cambie entre peticiones: una instancia agrupada se construye una vez y se reutiliza, así que un valor recibido en el constructor (como el inquilino del apartado 5.3) quedaría fijado para siempre. Los contextos con ese tipo de estado no deben agruparse tal cual.

### 4.4. Ejemplo Práctico: Optimización de Operaciones Masivas

Veamos la diferencia entre el enfoque tradicional y el enfoque optimizado para aplicar un incremento de precio del 10% a todos los productos de una categoría específica.

**Antipatrón (múltiples roundtrips y consumo de memoria):**

```csharp
public async Task AplicarIncrementoTradicionalAsync(CatalogoDbContext context, string categoria)
{
    // Roundtrip 1: carga todas las entidades en memoria y las rastrea
    var productos = await context.Productos
        .Where(p => p.Categoria == categoria)
        .ToListAsync();

    foreach (var producto in productos)
        producto.AplicarIncremento(1.10m);

    // Roundtrip 2 (o más): envía un UPDATE por producto, agrupados en lotes
    await context.SaveChangesAsync();
}
```

**Solución optimizada (sin tracking, un solo roundtrip):**

```csharp
public async Task<int> AplicarIncrementoOptimoAsync(CatalogoDbContext context, string categoria)
{
    // Genera un único UPDATE: PrecioBase = PrecioBase * 1.10 para las filas de esa categoría
    return await context.Productos
        .Where(p => p.Categoria == categoria)
        .ExecuteUpdateAsync(s => s.SetProperty(p => p.PrecioBase, p => p.PrecioBase * 1.10m));
}
```

`ExecuteUpdateAsync` devuelve el número de filas afectadas. `SetProperty` recibe la propiedad que se asigna y el nuevo valor, que puede ser una constante o una expresión sobre la propia fila. Funciona aunque la propiedad tenga `private set`, porque no asigna en C#: genera SQL.

Con EF Core 10, las asignaciones pueden decidirse con código normal dentro de la lambda:

```csharp
public Task<int> ActualizarCategoriaAsync(
    CatalogoDbContext context, string categoria, decimal? factor, string? nuevaCategoria)
{
    return context.Productos
        .Where(p => p.Categoria == categoria)
        .ExecuteUpdateAsync(s =>
        {
            if (factor is not null)
                s.SetProperty(p => p.PrecioBase, p => p.PrecioBase * factor.Value);
            if (nuevaCategoria is not null)
                s.SetProperty(p => p.Categoria, nuevaCategoria);
        });
}
```

De manera similar, para eliminar registros antiguos sin cargarlos:

```csharp
public Task<int> PurgarRegistrosAntiguosAsync(CatalogoDbContext context, DateTime fechaLimite)
{
    // Genera un único DELETE para los registros anteriores a la fecha
    return context.Registros
        .Where(r => r.FechaCreacion < fechaLimite)
        .ExecuteDeleteAsync();
}
```

Estas técnicas son especialmente adecuadas cuando se procesan grandes volúmenes de datos o en procesos en segundo plano, siempre que no se necesite la lógica que se ejecuta en `SaveChanges`.

### 4.5. Concurrencia Optimista

Cuando dos usuarios cargan el mismo registro y lo guardan, el segundo `SaveChanges` sobrescribe en silencio los cambios del primero. La concurrencia optimista lo evita sin bloquear filas: cada fila lleva un token de versión y el `UPDATE` solo se aplica si el token no ha cambiado desde la lectura.

En SQL Server, lo habitual es una columna `rowversion`, que la base de datos cambia automáticamente en cada modificación:

```csharp
public class Contrato
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";

    [Timestamp]                       // rowversion en SQL Server; token de concurrencia
    public byte[] Version { get; set; } = [];
}
```

Equivalente con Fluent API: `entity.Property(c => c.Version).IsRowVersion();`. Para otras columnas se usa `[ConcurrencyCheck]` o `.IsConcurrencyToken()`, y entonces es la aplicación quien debe cambiar su valor en cada actualización. El atributo `[Timestamp]` está en `System.ComponentModel.DataAnnotations`.

EF Core añade el token al `WHERE` de cada `UPDATE` y `DELETE` (`... WHERE [Id] = @id AND [Version] = @version`). Si la fila ya cambió, la sentencia no afecta a ninguna fila y `SaveChanges` lanza `DbUpdateConcurrencyException`:

```csharp
try
{
    contrato.Titulo = nuevoTitulo;
    await context.SaveChangesAsync();
}
catch (DbUpdateConcurrencyException ex)
{
    var entrada = ex.Entries.Single();
    var valoresActuales = await entrada.GetDatabaseValuesAsync();   // null si la fila se eliminó

    // Decidir: informar al usuario (por ejemplo, 409 Conflict), reintentar
    // o fusionar valores; para reintentar: entrada.OriginalValues.SetValues(valoresActuales!)
}
```

En una API, el cliente suele recibir la versión junto con el recurso y devolverla al actualizar. El servidor la fija como valor original antes de guardar, con `context.Entry(contrato).Property(c => c.Version).OriginalValue = versionRecibida;`, de modo que el conflicto se detecta aunque la lectura y la escritura ocurran en peticiones distintas. `ExecuteUpdateAsync` no comprueba el token automáticamente: si se usa, hay que incluir la condición en el `Where`.

## 5. Uso Adecuado de Filtros Globales

En aplicaciones empresariales complejas, existen reglas de negocio que deben aplicarse a casi todas las consultas del sistema. Los ejemplos más comunes son el borrado lógico (Soft Delete), donde los registros no se eliminan físicamente sino que se marcan como inactivos, y las arquitecturas multi-inquilino (Multi-tenant), donde una sola base de datos sirve a múltiples clientes y es vital garantizar que un cliente nunca vea los datos de otro.

Aplicar estas reglas manualmente en cada consulta LINQ (`.Where(e => !e.IsDeleted)`) es propenso a errores: basta con olvidarlo una vez para abrir una brecha de seguridad. EF Core proporciona los Filtros de Consulta Globales (Global Query Filters) para automatizar este proceso.

### 5.1. Propósito y Funcionamiento de los Query Filters

Un Filtro de Consulta Global es una expresión LINQ que se define en la configuración del modelo (`OnModelCreating`) y que EF Core inyecta automáticamente en la cláusula `WHERE` de cada consulta que involucre a esa entidad.

**Características principales:**

- **Transparencia:** el desarrollador escribe consultas normales (`context.Usuarios.ToList()`) y EF Core añade el filtro por debajo (`WHERE IsDeleted = 0`).
- **Aplicación en relaciones:** los filtros se aplican también cuando las entidades se cargan a través de propiedades de navegación (por ejemplo, al usar `.Include()`). Cuidado con las relaciones obligatorias: si el padre queda filtrado pero el hijo no, el resultado puede ser incoherente; EF Core avisa de esta situación al construir el modelo.
- **Seguridad por diseño:** en escenarios multi-tenant, aplican el aislamiento de datos en la capa de acceso a datos, reduciendo el riesgo de fugas de información.
- **Filtros con nombre (EF Core 10):** hasta EF Core 9 cada entidad admitía un solo filtro, e `IgnoreQueryFilters()` los desactivaba todos. EF Core 10 permite varios filtros con nombre en la misma entidad (`HasQueryFilter("Nombre", expresión)`) y desactivar solo algunos con `IgnoreQueryFilters(["Nombre"])`. `IgnoreQueryFilters()` sin argumentos sigue desactivando todos.

### 5.2. Implicaciones de Rendimiento y Cuándo Desactivarlos

Aunque los filtros globales son extremadamente útiles, tienen implicaciones de rendimiento que deben gestionarse:

1. **Necesidad de Índices:** si todas las consultas a la tabla `Pedidos` incluyen automáticamente `WHERE TenantId = @id AND IsDeleted = 0`, es imprescindible que existan índices en la base de datos que cubran estas columnas. De lo contrario, cada consulta resultará en un escaneo completo de la tabla (_Table Scan_), degradando el rendimiento. A menudo, estas columnas deben formar parte de índices compuestos junto con los criterios de búsqueda habituales. En EF Core los índices se declaran con `HasIndex(d => new { d.TenantId, d.EstaEliminado })`; si además deben impedir valores repetidos, se añade `.IsUnique()` (por ejemplo, `HasIndex(p => p.Sku).IsUnique()`).
2. **Desactivación controlada:** existen escenarios donde es necesario ignorar estos filtros. Por ejemplo, un administrador del sistema podría necesitar ver los registros "eliminados" para restaurarlos, o un proceso de facturación global podría necesitar procesar datos de todos los inquilinos. Para estos casos, EF Core proporciona el método `.IgnoreQueryFilters()`, que en EF Core 10 puede recibir los nombres de los filtros que se quieren desactivar.

### 5.3. Ejemplo Práctico: Implementación de Multi-tenant y Soft Delete

Vamos a implementar un contexto de base de datos que soporta tanto borrado lógico como aislamiento multi-tenant.

Primero, definimos una interfaz para estandarizar nuestras entidades:

```csharp
public interface IEntidadAuditable
{
    bool EstaEliminado { get; set; }
    string TenantId { get; set; }
}

public interface ITenantProvider
{
    string ObtenerTenantActual();
}

public class Documento : IEntidadAuditable
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Contenido { get; set; } = "";

    // Propiedades de la interfaz
    public bool EstaEliminado { get; set; }
    public string TenantId { get; set; } = "";
}
```

A continuación, configuramos el `DbContext`. Necesitaremos inyectar un servicio que nos proporcione el `TenantId` del usuario actual (por ejemplo, extrayéndolo del token JWT en una petición HTTP).

```csharp
using Microsoft.EntityFrameworkCore;

public class MultiTenantDbContext : DbContext
{
    private readonly string _currentTenantId;

    // Inyectamos el ID del tenant actual a través del constructor
    public MultiTenantDbContext(
        DbContextOptions<MultiTenantDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _currentTenantId = tenantProvider.ObtenerTenantActual();
    }

    public DbSet<Documento> Documentos => Set<Documento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Filtros globales con nombre (EF Core 10)
        modelBuilder.Entity<Documento>()
            .HasQueryFilter("SoftDelete", d => !d.EstaEliminado)
            .HasQueryFilter("Tenant", d => d.TenantId == _currentTenantId);

        // Índice compuesto para las columnas de los filtros
        modelBuilder.Entity<Documento>()
            .HasIndex(d => new { d.TenantId, d.EstaEliminado });
    }

    // Todas las sobrecargas de SaveChanges acaban en estas dos
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarReglas();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarReglas();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AplicarReglas()
    {
        foreach (var entry in ChangeTracker.Entries<IEntidadAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.TenantId = _currentTenantId;
                    entry.Entity.EstaEliminado = false;
                    break;

                case EntityState.Deleted:
                    // Se intercepta el borrado físico y se convierte en lógico
                    entry.State = EntityState.Modified;
                    entry.Entity.EstaEliminado = true;
                    break;
            }
        }
    }
}
```

**Detalles importantes del ejemplo:**

- El filtro usa el campo `_currentTenantId` del propio contexto. Cuando un filtro hace referencia a un miembro (campo o propiedad) del `DbContext`, EF Core no fija su valor al construir el modelo (que se crea una sola vez y se comparte), sino que lo lee de la instancia del contexto en cada consulta, como un parámetro. Si el filtro usara una variable local o una constante, el valor quedaría fijado en el modelo para todas las instancias.
- `SaveChanges()` y `SaveChangesAsync(CancellationToken)` llaman internamente a las sobrecargas con `acceptAllChangesOnSuccess`; sobrescribiendo esas dos se cubren las llamadas síncronas y asíncronas. Si solo se sobrescribiera `SaveChangesAsync`, un `SaveChanges()` síncrono borraría físicamente. Una alternativa reutilizable entre contextos es un interceptor (`SaveChangesInterceptor`).
- `ExecuteDeleteAsync` no pasa por `SaveChanges`: borraría físicamente. Para un borrado lógico masivo se usa `ExecuteUpdateAsync(s => s.SetProperty(d => d.EstaEliminado, true))`.
- Como el contexto recibe el inquilino en el constructor, no debe registrarse con `AddDbContextPool` (apartado 4.3); se registra con `AddDbContext`, junto con la implementación de `ITenantProvider`.

**Uso en la aplicación.** Cuando un usuario normal consulta los documentos, el filtro se aplica automáticamente:

```csharp
public async Task<List<Documento>> ObtenerMisDocumentosAsync(MultiTenantDbContext context)
{
    // EF Core añade automáticamente: WHERE EstaEliminado = 0 AND TenantId = @currentTenant
    return await context.Documentos.ToListAsync();
}
```

Cuando un administrador del sistema necesita ver la papelera de reciclaje (registros eliminados lógicamente), desactiva los filtros explícitamente:

```csharp
public async Task<List<Documento>> ObtenerPapeleraReciclajeAsync(MultiTenantDbContext context)
{
    // IgnoreQueryFilters(["NombreFiltro"]) desactiva solo el filtro especificado
    return await context.Documentos
        .IgnoreQueryFilters(["SoftDelete"])
        .Where(d => d.EstaEliminado == true)
        .ToListAsync();
}
```

En EF Core 10, es recomendable usar filtros de consulta con nombre (_Named Query Filters_). Esto permite desactivar específicamente el filtro de borrado lógico usando `.IgnoreQueryFilters(["SoftDelete"])`, manteniendo activo el filtro de `TenantId` sin necesidad de reaplicarlo manualmente. Si se llamara a `.IgnoreQueryFilters()` sin argumentos, el administrador vería también los documentos eliminados de todos los inquilinos: es una consideración de seguridad crítica.

---

# Cuadernillos del Alumno (laboratorio)

En esta parte se aplica la teoría de la Parte I sobre el proyecto de ejemplo **B2BOrders**: mapeo de objetos de valor y borrado lógico, consultas optimizadas y tests de integración contra una base de datos real en Docker.

---

## Cuadernillo 8.1: Mapeo Avanzado y Soft Delete

**Bloque 8: EF Core 10 Avanzado** · Duración: 10 min · Nivel: Experto

### Objetivos de la Lección

1. **Owned Types (Objetos de Valor):** mapear objetos complejos (`Money`) dentro de la tabla principal sin crear relaciones ni tablas extra.
2. **Soft Delete (Borrado Lógico):** implementar un sistema donde los datos nunca se borran físicamente, sino que se ocultan.
3. **Global Query Filters:** automatizar el filtrado de datos borrados en toda la aplicación.
4. **Consistencia Arquitectónica:** leer datos usando MediatR y Repositorios en lugar de inyectar el `DbContext` en la API.

### Concepto Clave: Aplanamiento y Filtros

- **Flattening (Aplanamiento):** EF Core toma las propiedades de `Money` (`Amount`, `Currency`) y las guarda como columnas en la tabla `Orders` (`TotalAmount_Amount`, `TotalAmount_Currency`).
- **Query Filters:** es como añadir un `WHERE` invisible a todas las consultas SQL que hace tu aplicación.

### Laboratorio 1: Preparar el Dominio

Modificamos la entidad para soportar el borrado y asegurar la inicialización correcta (evitando warnings de nulos).

**Archivo:** `B2BOrders.Domain/Orders/Order.cs`

```csharp
public class Order
{
    // ... propiedades
    public Money TotalAmount { get; private set; }
    public bool IsDeleted { get; private set; } // 1. Propiedad de estado

    // 2. Constructor Privado para EF Core (CRÍTICO para evitar CS8602)
    private Order()
    {
        // Inicializamos valores por defecto para que el compilador sepa que no son nulos
        TotalAmount = Money.Zero("EUR");
        _orderLines = new List<OrderLine>();
    }

    public Order(int customerId)
    {
        // ... lógica de constructor público
        CustomerId = customerId;
        IsDeleted = false;
    }

    // 3. Método de comportamiento
    public void Delete()
    {
        IsDeleted = true;
    }
}
```

### Laboratorio 2: Configuración de EF Core (Infrastructure)

Configuramos el mapeo exacto para que coincida con las convenciones de la base de datos y activamos el filtro.

**Archivo:** `B2BOrders.Infrastructure/Orders/OrdersDbContext.cs`

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Order>(b =>
    {
        // ... otras configuraciones

        // 1. MAPEO DE OWNED TYPE (Ajustado a nombres reales de BD)
        b.OwnsOne(o => o.TotalAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalAmount_Amount") // Coincide con SQL
                .HasPrecision(18, 2);

            money.Property(m => m.Currency)
                .HasColumnName("TotalAmount_Currency")
                .HasMaxLength(3);
        });

        // 2. FILTRO GLOBAL (Soft Delete)
        // Automáticamente añade "WHERE IsDeleted = 0"
        b.HasQueryFilter(o => !o.IsDeleted);
    });
}
```

### Laboratorio 3: Lectura Limpia (Application & API)

En lugar de "ensuciar" el controlador con EF Core, usamos el patrón CQRS con MediatR.

#### Paso A: Query y Handler

**Ruta:** `B2BOrders.Application/Orders/GetOrder/`

```csharp
// 1. La Petición
public record GetOrderByIdQuery(int Id) : IRequest<Order?>;

// 2. El Manejador
public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Order?>
{
    private readonly IOrderRepository _repository; // Inyección de dependencia

    public GetOrderByIdQueryHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Order?> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        // El repositorio usa EF Core, por lo que el Filtro Global se aplica aquí
        return await _repository.GetByIdAsync(request.Id, ct);
    }
}
```

#### Paso B: El Endpoint

**Ruta:** `B2BOrders.Api/OrdersEndpoints.cs`

```csharp
app.MapGet("/orders/{id}", async (int id, IMediator mediator) =>
{
    var order = await mediator.Send(new GetOrderByIdQuery(id));

    if (order is null)
        return Results.NotFound(new { Msg = "Pedido no encontrado o eliminado." });

    return Results.Ok(order);
});
```

### Guía de Resolución de Problemas (Troubleshooting)

Errores que hemos resuelto en esta práctica:

| Síntoma                                                     | Causa                                                                                                         | Solución                                                                                                                                  |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| Warning `CS8602` (Dereference of a possibly null reference) | El constructor `private Order() {}` dejaba propiedades no-anulables (`TotalAmount`) sin inicializar.          | Inicializar las propiedades con valores vacíos/cero dentro del constructor privado.                                                       |
| SQL Error: Invalid column name `'TotalAmount'`              | `HasColumnName("TotalAmount")` buscaba una columna simple, pero EF crea `Propiedad_SubPropiedad` por defecto. | Cambiar el mapeo en `DbContext` a `TotalAmount_Amount` o usar Migrations para renombrar la columna en SQL.                                |
| Error 404 pero el registro existe en SQL                    | El filtro global `HasQueryFilter` está funcionando correctamente y el registro tiene `IsDeleted = 1`.         | Es el comportamiento esperado. Para ver borrados, se necesitaría usar `.IgnoreQueryFilters()` en una consulta especial de administración. |
| No se ven los logs de SQL                                   | `appsettings.json` tiene el nivel de log muy alto.                                                            | Añadir `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` en la configuración de `Logging`.                                |

### Siguiente Paso

Tu base de datos ahora es más inteligente: oculta lo que no debe verse y estructura los datos complejos correctamente.

---

## Cuadernillo 8.2: Consultas Complejas y Rendimiento

**Bloque 8: EF Core 10 Avanzado** · Duración: 10-15 min · Nivel: Experto

**Objetivo:** transformar una consulta lenta y pesada en una operación de alto rendimiento.

### Conceptos Clave

1. **Explosión Cartesiana:** cuando haces un `JOIN` de 1 Pedido con 50 Líneas, SQL devuelve 50 filas repitiendo los datos del pedido (Cliente, Fecha, Total) en cada una. Esto desperdicia memoria y ancho de banda.
2. **Split Query (`AsSplitQuery`):** divide esa consulta gigante en dos pequeñas: una para cargar los pedidos y otra para las líneas. EF Core las une en memoria. Es ideal para relaciones 1:N grandes.
3. **Proyecciones (`.Select`):** nunca devuelvas Entidades de Dominio (`Order`) directamente a la API. Mapea a un DTO plano solo con los datos necesarios.
4. **Índices Compuestos:** un índice en `(CustomerId, CreatedAt)` permite filtrar y ordenar instantáneamente sin que SQL tenga que leer toda la tabla.

### Laboratorio 1: El Contrato de Datos (DTO Eficiente)

Optamos por la eficacia: un DTO plano que facilita el pintado en tablas (_grids_) del Frontend y reduce el tamaño del JSON.

**Archivo:** `B2BOrders.Application/Orders/OrderSummaryDto.cs`

```csharp
namespace B2BOrders.Application.Orders;

// DTO anidado para las líneas
public record OrderLineDto(
    string Product,
    int Quantity,
    decimal UnitPrice, // Primitivo (sin objeto Money)
    string Currency
);

// DTO principal
public record OrderSummaryDto(
    int OrderId,
    DateTime Date,
    decimal TotalAmount, // Primitivo
    string Currency,
    List<OrderLineDto> Lines
);
```

### Laboratorio 2: Implementación Optimizada (Infrastructure)

Implementamos la consulta usando las 3 reglas de oro del rendimiento de lectura: `AsNoTracking`, `AsSplitQuery` y `Select`.

**Archivo:** `B2BOrders.Infrastructure/Orders/EfCoreOrderRepository.cs`

```csharp
public async Task<List<OrderSummaryDto>> GetOrdersForCustomerAsync(
    int customerId, DateTime from, DateTime to, int page, int pageSize, CancellationToken ct)
{
    var query = _context.Orders
        .AsNoTracking() // 1. Solo Lectura
        .Where(o => o.CustomerId == customerId &&
                    o.CreatedAt >= from &&
                    o.CreatedAt <= to);

    return await query
        .OrderByDescending(o => o.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .AsSplitQuery() // 2. Evita explosión cartesiana
        .Select(o => new OrderSummaryDto( // 3. Proyección
            o.Id,
            o.CreatedAt,
            o.TotalAmount.Amount,
            o.TotalAmount.Currency,
            o.OrderLines.Select(ol => new OrderLineDto(
                $"Producto {ol.ProductId}",
                ol.Quantity,
                ol.UnitPrice.Amount,
                ol.UnitPrice.Currency // ¡No olvidar mapear todos los campos!
            )).ToList()
        ))
        .ToListAsync(ct);
}
```

### Laboratorio 3: Arquitectura Limpia (CQRS)

Para no ensuciar la API inyectando repositorios, encapsulamos la petición.

1. **Query:** `GetOrdersForCustomerQuery(CustomerId, From, To...)`
2. **Handler:** recibe la Query, llama al Repositorio y devuelve `List<OrderSummaryDto>`.
3. **Endpoint:**

```csharp
app.MapGet("/orders/customer/{customerId}", async (
    int customerId,
    IMediator mediator,
    [AsParameters] DateRangeParams dates) =>
{
    var from = dates.From ?? DateTime.UtcNow.AddYears(-1);
    var to = dates.To ?? DateTime.UtcNow;

    // Enviamos el mensaje a MediatR
    var result = await mediator.Send(
        new GetOrdersForCustomerQuery(customerId, from, to, 1, 10));

    return Results.Ok(result);
});

// Record definido FUERA del método MapGet
public record DateRangeParams(DateTime? From, DateTime? To);
```

### Laboratorio 4: Base de Datos (Índices y Seeding)

**1. Índice compuesto.** Para acelerar el `WHERE CustomerId = X AND CreatedAt = Y`.

```csharp
// OrdersDbContext.cs
b.HasIndex(o => new { o.CustomerId, o.CreatedAt })
 .HasDatabaseName("IX_Orders_Customer_Date");
```

**2. Datos de prueba (Seeding).** Creamos un endpoint `/seed` para inyectar 1.000 pedidos y probar que el rendimiento se mantiene estable con carga real.

### Guía de Resolución de Problemas (Troubleshooting)

Errores comunes que resolvimos durante la sesión:

| Síntoma                                 | Causa                                                                                | Solución                                                                         |
| --------------------------------------- | ------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------- |
| Error `CS7036` (No argument given...)   | El constructor del `record` espera 4 parámetros pero en el `.Select` solo pasamos 3. | Asegurarse de mapear `ol.UnitPrice.Currency` dentro del `new OrderLineDto(...)`. |
| Error `CS1513` (`}` expected)           | Se definió un `public record` dentro de un método (`MapOrdersEndpoints`).            | Mover la definición del `record` fuera del método o al final del archivo.        |
| Error 405 Method Not Allowed en `/seed` | El endpoint se definió como `POST` pero se llamó desde el navegador (`GET`).         | Cambiar a `app.MapGet("/seed", ...)` o usar Postman/PowerShell.                  |
| IntelliSense lento / Errores falsos     | VS Code perdió el contexto del proyecto.                                             | `Ctrl+Shift+P` → `Developer: Reload Window` o `OmniSharp: Restart`.              |

---

## Cuadernillo 8.3: Tests de Integración con Docker

**Bloque 8: EF Core 10 Avanzado** · Duración: 15 min · Nivel: Experto

**Objetivo:** crear un entorno de CI/CD local donde los tests se ejecutan contra una base de datos real y aislada, garantizando que el código funciona en producción.

### Conceptos Clave

1. **Unit vs. Integration:** los tests unitarios prueban lógica aislada (mockeando la BD). Los de integración prueban que tu código habla correctamente con SQL Server (restricciones, tipos de datos, transacciones).
2. **Aislamiento de Entornos:**
   - **Puerto 1433 (Dev):** tu base de datos de desarrollo "sucia" con datos manuales.
   - **Puerto 1435 (Test):** una base de datos "efímera" que se borra y recrea en cada ejecución de tests.
3. **`ChangeTracker.Clear()`:** el truco vital en tests de EF Core. Si no limpias el rastreador, EF Core te devuelve la entidad que tiene en memoria RAM en lugar de hacer una `SELECT` real a la base de datos, falseando la prueba.
4. **Fake Dependencies:** cuando probamos la capa de persistencia (Repositorio), a veces necesitamos "falsificar" dependencias externas (como el EventBus) para que el código compile sin complicar el test.

### Laboratorio 1: Infraestructura (Docker)

Levantamos un contenedor dedicado para no romper nuestra base de datos de desarrollo.

**Comando (PowerShell/Terminal):**

```powershell
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=TestPassword123!" -p 1435:1433 --name sql_test -d mcr.microsoft.com/azure-sql-edge
```

### Laboratorio 2: Configuración del Test (Fixture)

Creamos la clase que configura el contexto una sola vez por suite de pruebas.

**Archivo:** `B2BOrders.IntegrationTests/DbFixture.cs`

```csharp
using B2BOrders.Infrastructure.Orders;
using Microsoft.EntityFrameworkCore;

namespace B2BOrders.IntegrationTests;

public class DbFixture : IDisposable
{
    public OrdersDbContext Context { get; private set; }

    public DbFixture()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            // Conectamos al puerto 1435 (Docker de Test)
            .UseSqlServer("Server=localhost,1435;Database=B2BOrders_TestDb;User Id=sa;Password=TestPassword123!;TrustServerCertificate=True;")
            .Options;

        Context = new OrdersDbContext(options);

        // Limpieza agresiva: Borrar y crear de cero
        Context.Database.EnsureDeleted();
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Database.EnsureDeleted();
        Context.Dispose();
    }
}
```

### Laboratorio 3: El Test de Ciclo Completo

Probamos inserción, tipos complejos (`Money`) y borrado lógico (Soft Delete).

**Archivo:** `B2BOrders.IntegrationTests/OrderRepositoryTests.cs`

```csharp
using B2BOrders.Domain.Orders;
using B2BOrders.Infrastructure.Orders;
using B2BOrders.Domain.Common; // Para IEventBus
using Xunit;

namespace B2BOrders.IntegrationTests;

// 1. Fake para satisfacer el constructor del repositorio
public class FakeEventBus : IEventBus
{
    public Task PublishAsync(object domainEvent, CancellationToken ct) => Task.CompletedTask;
}

public class OrderRepositoryTests : IClassFixture<DbFixture>
{
    private readonly DbFixture _fixture;
    private readonly EfCoreOrderRepository _repository;

    public OrderRepositoryTests(DbFixture fixture)
    {
        _fixture = fixture;
        // Inyectamos el contexto y el bus falso
        _repository = new EfCoreOrderRepository(_fixture.Context, new FakeEventBus());
    }

    [Fact]
    public async Task FullCycle_Should_Work_Correctly()
    {
        // 1. ARRANGE
        var order = new Order(customerId: 99);
        order.AddLine(1, 5, Money.Of(50, "EUR")); // Value Object correcto

        // 2. ACT: Guardar
        await _repository.AddAsync(order, CancellationToken.None);

        // CRÍTICO: Limpiar memoria para forzar lectura de BD
        _fixture.Context.ChangeTracker.Clear();

        // 3. ASSERT: Lectura
        var saved = await _repository.GetByIdAsync(order.Id, CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(250, saved.TotalAmount.Amount); // Validación de lógica de dominio y persistencia

        // 4. SOFT DELETE TEST
        saved.Delete();
        await _repository.UpdateAsync(saved, CancellationToken.None);

        _fixture.Context.ChangeTracker.Clear(); // Limpiar de nuevo

        var deleted = await _repository.GetByIdAsync(order.Id, CancellationToken.None);

        Assert.Null(deleted); // El filtro global funcionó
    }
}
```

### Guía de Resolución de Problemas (Troubleshooting)

Errores que encontramos y corregimos durante la sesión:

| Error / Síntoma                          | Causa                                                                             | Solución                                                                       |
| ---------------------------------------- | --------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| `CS7036` (No argument given... EventBus) | El constructor de `EfCoreOrderRepository` pedía `IEventBus` y no se lo pasábamos. | Crear una clase `FakeEventBus` e inyectarla (`new FakeEventBus()`).            |
| `CS0305` (OrderedDictionary)             | Autocompletado incorrecto al escribir `new Order`.                                | Corregir manualmente a `new Order(...)`.                                       |
| `CS0117` (SqlMoney)                      | Autocompletado incorrecto al escribir `Money.`.                                   | Corregir manualmente a `Money.Of(...)` (usando el namespace del Dominio).      |
| Test pasa pero no guarda en BD           | No se usó `ChangeTracker.Clear()`.                                                | Siempre limpiar el tracker antes de hacer `GetById` en un test de integración. |
| Connection Refused                       | El puerto en `DbFixture` no coincide con el de Docker.                            | Verificar si Docker corre en 1433 o 1435 y ajustar la cadena de conexión.      |
