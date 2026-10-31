# Tema 3 — Domain-Driven Design (DDD) Táctico en .NET 10

> Documento unificado: resumen, teoría (Manual I: Estructura y Elementos del DDD) y laboratorio práctico en `B2BOrders` (dominio, capa de aplicación y tests unitarios).

## Contenido

- [Resumen del Tema 3](#resumen-del-tema-3)
- [Estructura y Elementos del DDD en .NET 10](#estructura-y-elementos-del-ddd-en-net-10)
- [Laboratorio B2BOrders: dominio, aplicación y tests](#laboratorio-b2borders-dominio-aplicación-y-tests)

---

## Resumen del Tema 3

### Idea central

DDD centra el diseño en el **núcleo del negocio**: la estructura y el lenguaje del código deben coincidir con los del dominio, no con tablas, JSON o pantallas. Tiene un nivel **estratégico** (subdominios, Bounded Contexts, Lenguaje Ubicuo) y otro **táctico** (Entidades, Objetos de Valor, Agregados, Eventos de Dominio). El modelo debe ser **rico** (datos + comportamiento), proteger sus propias reglas y no depender de ninguna tecnología externa.

### 1. Diseño estratégico

| Concepto                  | Qué es                                                                                                                                                                                               |
| ------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Subdominio Core**       | Lo que diferencia al negocio; ahí va el mayor esfuerzo y el DDD táctico más riguroso                                                                                                                 |
| **Subdominio Supporting** | Necesario, pero sin ventaja competitiva; se desarrolla a medida con menos complejidad                                                                                                                |
| **Subdominio Generic**    | Estándar (identidad, pagos); mejor comprar o integrar                                                                                                                                                |
| **Bounded Context**       | Límite explícito donde un modelo es válido. Un proyecto/assembly, un módulo de un monolito modular o un microservicio. Evita "modelos monstruosos" (p. ej. `Cliente` distinto en Ventas y en Envíos) |
| **Lenguaje Ubicuo**       | Lenguaje común entre negocio y desarrolladores, usado también en el código (`pedido.Confirmar(...)` en vez de `UpdateOrderStatus(...)`)                                                              |

### 2. Bloques tácticos

| Elemento              | Definición                                                                  | Claves                                                                                     |
| --------------------- | --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| **Entidad**           | Se define por su identidad única, que persiste en el tiempo                 | Mutable, ciclo de vida, igualdad por `Id` (clase base `Entidad<TId>`)                      |
| **Objeto de Valor**   | Se define por sus atributos, sin identidad                                  | Inmutable, igualdad estructural, encapsula validación y comportamiento. Ideal con `record` |
| **Agregado**          | Clúster de entidades y objetos de valor tratado como unidad de consistencia | Una única **Raíz** controla todos los cambios                                              |
| **Evento de Dominio** | Registro de algo significativo que ya ocurrió (tiempo pasado)               | Desacopla efectos secundarios, permite consistencia eventual                               |

**Trampa de `with` en los records:** la expresión `with` copia asignando directamente las propiedades `init`, sin pasar por el constructor. Si la validación solo está en el constructor, `with` permite crear objetos de valor inválidos. Soluciones: `private set` o validar dentro del accesor `init` (C# 14, palabra clave `field`).

### 3. Agregados e invariantes

- Fuera del agregado solo se referencia a la **Raíz**, y a otros agregados **solo por su `Id`**.
- Una transacción debería modificar **un solo agregado**; para tocar varios, usar consistencia eventual con eventos.
- Los hijos se crean con constructores `internal`; la colección interna es privada y se expone como `IReadOnlyCollection<T>`.
- La Raíz valida **todas** las invariantes antes de cambiar el estado: si alguna falla, el agregado queda como estaba.
- Persistencia con EF Core 10: objetos de valor como **tipos complejos** (`ComplexProperty`), constructor privado sin parámetros, `ValueGeneratedNever()` cuando el dominio genera los `Guid`.

### 4. Eventos de Dominio

- Interfaz `IDomainEvent` propia (sin depender de librerías), eventos como `record` inmutables en tiempo pasado, y una lista de eventos pendientes en la entidad: **el agregado registra, no publica**.
- La infraestructura los **despacha al guardar**, recogiéndolos del `ChangeTracker` en `SaveChangesAsync`.
- Despachar **después** de guardar evita actuar sobre cambios que se deshacen (pero un fallo del manejador pierde el efecto). Despachar **antes** permite que los manejadores guarden en la misma transacción. Para garantías fuertes: patrón **Outbox** (tema 7).
- **Evento de Dominio** (dentro del contexto) ≠ **Evento de Integración** (bus de mensajes entre microservicios).
- MediatR (desde v13 con licencia comercial) puede usarse, pero no debe contaminar el Dominio; un despachador propio de pocas líneas evita ambos problemas.

### 5. Lectura y monolito DDD

- **Modelos de lectura independientes** (sin CQRS completo): escrituras cargando el agregado; lecturas con proyecciones (`.Select`, `.AsNoTracking()`) a DTOs planos, o Dapper. Si la vista necesita datos de otro contexto, ese contexto guarda su propia copia.
- **Monolito modular preparado para microservicios:** sin referencias directas de proyecto entre Bounded Contexts, comunicación por interfaces o eventos, y esquema + `DbContext` propios sin claves foráneas entre contextos.

### 6. Antipatrones

- **Modelo anémico:** entidades con `get`/`set` públicos y la lógica en `PedidoService`/`Manager`. Solución: `private set` y métodos de negocio en la entidad.
- **Falta de aislamiento:** atributos de EF Core o JSON (`[Column]`, `[JsonIgnore]`) en el dominio. Solución: dominio POCO puro y configuración con Fluent API (`IEntityTypeConfiguration<T>`) en Infraestructura.

### 7. Laboratorio `B2BOrders`

| Pieza                                     | Capa                     | Descripción                                                                                              |
| ----------------------------------------- | ------------------------ | -------------------------------------------------------------------------------------------------------- |
| `Money`                                   | Domain/Shared            | Objeto de valor (`record`) con `Of`, operador `+` y `Zero`                                               |
| `OrderLine`                               | Domain/Orders            | Entidad hija; valida cantidad positiva; `LineTotal` calculado                                            |
| `Order`                                   | Domain/Orders            | **Raíz del agregado**; lista privada de líneas, invariantes, `AddLine`, `MarkAsPaid`, `PullDomainEvents` |
| `IDomainEvent`, `OrderPaidDomainEvent`    | Domain                   | Contrato base y evento "pedido pagado"                                                                   |
| `IOrderRepository`                        | Domain                   | Contrato del repositorio (inversión de dependencias)                                                     |
| `PlaceOrderCommand` + `PlaceOrderHandler` | Application              | Command/Handler con MediatR; orquesta, sin reglas de negocio                                             |
| `OrderTests`                              | `B2BOrders.Domain.Tests` | xUnit, patrón AAA; verifica total y evento sin base de datos                                             |

### 8. Notas sobre los materiales

- **Español vs. inglés:** el Manual usa nombres en español y el laboratorio en inglés. Equivalencias: `Pedido` = `Order`, `LineaPedido` = `OrderLine`, `Dinero` = `Money`, `AñadirProducto` = `AddLine`. El laboratorio no usa la clase base `Entidad<TId>` del Manual.
- **`Money` del laboratorio y la trampa de `with`:** `Money` es un `record` posicional cuya validación solo está en el método `Of`. Por eso `new Money(-5, "EUR")` y `dinero with { Amount = -5 }` saltan la validación (el propio dominio usa `new Money(...)` en `LineTotal` y en el operador `+`). El Manual (sección 2.2) propone `private set` o validar en el accesor `init` si se quiere blindar el objeto de valor.
- **Extracción de eventos:** el laboratorio usa `PullDomainEvents()` (devuelve y vacía la lista); el Manual usa `DomainEvents` + `LimpiarEventosDominio()` con `IConEventosDominio` para que el `DbContext` los recoja del `ChangeTracker`. Es la misma idea con dos formas.
- **MediatR:** el laboratorio lo usa solo en Application para los comandos (el Dominio no depende de él, como recomienda el Manual). Desde la v13 tiene licencia comercial: revisa sus condiciones.

---

## Estructura y Elementos del DDD en .NET 10

### 1. Fundamentos Conceptuales de Domain-Driven Design (DDD)

Domain-Driven Design (DDD) es un enfoque de desarrollo de software que centra la atención en el núcleo del negocio (el dominio) y en su lógica. En lugar de comenzar el diseño del software pensando en tablas de bases de datos, esquemas JSON o interfaces de usuario, DDD propone que la estructura y el lenguaje del código fuente deben coincidir con el dominio del negocio. En el ecosistema de .NET 10, las características de C# (records, propiedades con setters privados, colecciones de solo lectura) y los tipos complejos de EF Core 10 permiten expresar estos conceptos con claridad y persistirlos sin contaminar el modelo.

#### 1.1. El Espacio del Problema: Dominios y Subdominios

Para aplicar DDD correctamente, primero debemos entender el "Espacio del Problema". El dominio principal de una empresa es su razón de ser, la actividad principal que genera valor. Sin embargo, un dominio completo (por ejemplo, "Comercio Electrónico") es demasiado grande y complejo para ser modelado como un todo único. Por ello, se divide en partes más pequeñas y manejables llamadas **Subdominios**.

Existen tres tipos principales de subdominios:

1. **Subdominio Core (Núcleo):** es el corazón del negocio. Es lo que diferencia a la empresa de sus competidores y donde se genera el mayor valor. Aquí es donde debemos invertir el mayor esfuerzo de desarrollo y aplicar los patrones tácticos de DDD con mayor rigor. En un sistema de comercio electrónico, el motor de recomendaciones o el algoritmo de precios dinámicos podría ser el Core.
2. **Subdominio Supporting (de Soporte):** es necesario para que el negocio funcione, pero no proporciona una ventaja competitiva directa. No existe una solución estándar en el mercado que se adapte perfectamente, por lo que debe desarrollarse a medida, pero sin la complejidad extrema del Core. Un ejemplo sería el sistema de gestión de inventario interno.
3. **Subdominio Generic (Genérico):** son funcionalidades necesarias pero completamente estándar. No aportan diferenciación y existen múltiples soluciones comerciales disponibles. La gestión de identidad de usuarios (autenticación y autorización) o la pasarela de pagos son ejemplos clásicos. Lo ideal es comprar o integrar soluciones existentes (como Microsoft Entra ID o Stripe) en lugar de construirlas desde cero.

#### 1.2. El Espacio de la Solución: Bounded Contexts en .NET 10

Mientras que los subdominios pertenecen al espacio del problema (cómo el negocio ve el mundo), los **Bounded Contexts** (Contextos Delimitados) pertenecen al "Espacio de la Solución" (cómo los desarrolladores estructuran el software).

Un Bounded Context es un límite explícito dentro del cual un modelo de dominio específico es válido y consistente. Fuera de este límite, el modelo puede cambiar de significado o no tener sentido. La regla de oro en DDD es que un modelo de dominio solo debe existir dentro de un Bounded Context.

En soluciones modernas desarrolladas con .NET 10, un Bounded Context suele implementarse físicamente de las siguientes maneras:

- **Como un proyecto independiente (Assembly):** por ejemplo, `Ventas.Domain.dll` y `Envios.Domain.dll`.
- **Como un módulo dentro de un Monolito Modular:** utilizando espacios de nombres (_namespaces_) raíz distintos, como `MiApp.Modulos.Ventas` y `MiApp.Modulos.Envios`, asegurando mediante herramientas de análisis estático que no existan dependencias circulares entre ellos.
- **Como un Microservicio independiente:** donde cada Bounded Context se despliega y escala por separado (este enfoque arquitectónico se profundizará en el tema 5 del curso).

La separación en Bounded Contexts evita la creación de "modelos monstruosos". Por ejemplo, el concepto de `Cliente` en el contexto de "Ventas" puede requerir propiedades como `LimiteDeCredito` e `HistorialDeCompras`. Sin embargo, en el contexto de "Envíos", el `Cliente` solo necesita una `DireccionDeEntrega` y un `NumeroDeContacto`. Intentar unificar ambos en una sola clase `Cliente` global genera acoplamiento, conflictos entre equipos y un código difícil de mantener.

#### 1.3. El Lenguaje Ubicuo: Alineación entre Negocio y Tecnología

El Lenguaje Ubicuo es, posiblemente, el concepto más importante del diseño estratégico en DDD. Se trata de un lenguaje común, riguroso y compartido, construido conjuntamente por los expertos del dominio (el negocio) y los desarrolladores (la tecnología).

Este lenguaje debe utilizarse en todas partes: en las reuniones, en la documentación, en los diagramas y, lo más importante, en el código fuente.

Si un experto del dominio dice: _"Cuando un cliente confirma un pedido, debemos verificar si el importe total supera su límite de crédito"_, el código en C# no debería verse así:

```csharp
// MAL: Lenguaje técnico, centrado en datos (CRUD)
public void UpdateOrderStatus(int orderId, int statusId)
{
    var order = db.Orders.Find(orderId);
    order.StatusId = statusId;
    db.SaveChanges();
}
```

El código anterior no refleja la intención del negocio. Un enfoque basado en el Lenguaje Ubicuo en .NET 10 se vería así:

```csharp
// BIEN: Lenguaje Ubicuo, centrado en el comportamiento
public void ConfirmarPedido(PedidoId id)
{
    var pedido = _repositorioPedidos.ObtenerPorId(id);
    var cliente = _repositorioClientes.ObtenerPorId(pedido.ClienteId);

    pedido.Confirmar(cliente.LimiteDeCredito);

    _repositorioPedidos.Guardar(pedido);
}
```

Al leer el segundo bloque de código, cualquier persona, incluso sin conocimientos profundos de programación, puede entender qué regla de negocio se está aplicando. Esta alineación reduce los errores de traducción entre los requisitos del negocio y la implementación técnica.

### 2. Elementos Estructurales Básicos del Modelo de Dominio

Una vez definidos los límites estratégicos (Bounded Contexts) y el lenguaje (Lenguaje Ubicuo), pasamos al diseño táctico. El diseño táctico proporciona los bloques de construcción para crear el modelo de dominio en código. Los dos elementos más fundamentales son las Entidades y los Objetos de Valor.

#### 2.1. Entidades (Entities): Identidad y Ciclo de Vida

Una **Entidad** es un objeto de dominio que se define fundamentalmente por su identidad única, no por sus atributos. La identidad de una entidad se mantiene constante a lo largo del tiempo, incluso si todos sus demás atributos cambian.

Características clave de las Entidades:

- **Identidad persistente:** tienen un identificador único (ID) que las distingue de cualquier otra instancia, incluso si tienen los mismos datos.
- **Mutabilidad:** sus propiedades y estado pueden cambiar a lo largo de su ciclo de vida (por ejemplo, un `Pedido` pasa de "Pendiente" a "Enviado").
- **Ciclo de vida:** nacen (se crean), cambian de estado y eventualmente pueden morir (eliminarse o archivarse).

Es una buena práctica crear una clase base abstracta para todas las entidades, lo que centraliza la lógica de comparación de identidades: dos entidades son iguales si son del mismo tipo y tienen el mismo identificador.

#### 2.2. Objetos de Valor (Value Objects): Inmutabilidad y Encapsulación

Un **Objeto de Valor** (_Value Object_) es un objeto que representa un concepto descriptivo del dominio, pero que no tiene identidad conceptual. Se definen exclusivamente por el valor de sus atributos. Si dos Objetos de Valor tienen exactamente los mismos datos en todos sus campos, se consideran el mismo objeto.

Características clave de los Objetos de Valor:

- **Sin identidad:** no tienen un campo `Id`.
- **Inmutabilidad:** una vez creados, su estado no puede cambiar. Si se necesita un valor diferente, se debe crear una nueva instancia. Esto los hace inherentemente seguros para subprocesos (_thread-safe_) y predecibles.
- **Igualdad estructural:** se comparan por el valor de sus propiedades, no por su referencia en memoria.
- **Encapsulación de lógica:** no son solo contenedores de datos; deben contener la lógica de validación y comportamiento relacionada con ese valor (por ejemplo, un objeto `Dinero` debe saber cómo sumarse a otro objeto `Dinero` de la misma moneda).

Desde C# 9, el tipo `record` es una herramienta muy adecuada para implementar Objetos de Valor: el compilador genera la igualdad estructural (`Equals`, `GetHashCode`, `==`) a partir de sus propiedades. La inmutabilidad, en cambio, depende de cómo se declaren las propiedades, y hay una trampa importante: la expresión `with` (`otro = dinero with { Importe = -5 }`) crea una copia sin pasar por el constructor, asignando directamente las propiedades `init`. Si la validación solo está en el constructor, `with` permite crear objetos de valor inválidos. Hay dos formas de evitarlo:

- Declarar las propiedades con `private set` (o solo `get`): fuera del tipo, `with` no puede asignarlas y el código no compila.
- Validar dentro del propio accesor `init`. C# 14 (incluido en .NET 10) facilita esto con la palabra clave contextual `field`, que da acceso al campo de respaldo generado por el compilador sin tener que declararlo.

#### 2.3. Ejemplo Práctico: Implementación de Entidades y Objetos de Valor en C#

A continuación, veremos cómo implementar estos conceptos en .NET 10, asegurando que el modelo de dominio sea rico y proteja sus propias reglas. Los ejemplos asumen un proyecto con `<Nullable>enable</Nullable>` y los `using` implícitos habituales (`ImplicitUsings`).

Primero, definimos un Objeto de Valor utilizando un `record` de C#. Representaremos el concepto de `Dinero` (_Money_), un clásico en DDD.

```csharp
// Objeto de Valor: la validación vive en los accesores 'init' (C# 14, palabra clave 'field'),
// así que se aplica tanto al construirlo como al copiarlo con 'with'.
public sealed record Dinero
{
    public decimal Importe
    {
        get;
        init => field = value >= 0
            ? value
            : throw new ArgumentException("El importe no puede ser negativo.", nameof(Importe));
    }

    public string Moneda
    {
        get;
        init => field = !string.IsNullOrWhiteSpace(value) && value.Length == 3
            ? value.ToUpperInvariant()
            : throw new ArgumentException("La moneda debe ser un código ISO de 3 letras.", nameof(Moneda));
    }

    public Dinero(decimal importe, string moneda)
    {
        Importe = importe;
        Moneda = moneda;
    }

    // Comportamiento encapsulado en el Objeto de Valor
    public Dinero Sumar(Dinero otro)
    {
        if (Moneda != otro.Moneda)
            throw new InvalidOperationException("No se pueden sumar importes de distintas monedas.");

        return new Dinero(Importe + otro.Importe, Moneda);
    }

    public Dinero Multiplicar(int cantidad) => new(Importe * cantidad, Moneda);
}
```

En este ejemplo, `Dinero` es inmutable: fuera del constructor y de los inicializadores no se puede asignar `Importe` (`miDinero.Importe = 50;` no compila). Y como la validación está en el accesor `init`, `dinero with { Importe = -5 }` lanza la misma excepción que el constructor. Si no se quiere usar `field`, la alternativa equivalente es declarar las propiedades con `{ get; private set; }` y validar en el constructor: entonces `with` ni siquiera compila fuera del tipo.

Ahora, implementaremos una Entidad `Producto` que utiliza este Objeto de Valor.

```csharp
// Clase base para estandarizar la identidad de las Entidades
public abstract class Entidad<TId> where TId : IEquatable<TId>
{
    public TId Id { get; protected set; } = default!;

    protected Entidad(TId id)
    {
        Id = id;
    }

    // Constructor sin parámetros para EF Core (ver explicación más abajo)
    protected Entidad() { }

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false; // mismo tipo concreto
        if (ReferenceEquals(this, obj)) return true;
        return Id.Equals(((Entidad<TId>)obj).Id);
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

// Entidad Producto
public class Producto : Entidad<Guid>
{
    // Propiedades con setters privados para evitar modificaciones externas incontroladas
    public string Nombre { get; private set; } = string.Empty;
    public Dinero Precio { get; private set; } = null!;
    public bool EstaActivo { get; private set; }

    // Solo para EF Core: no puede pasar objetos de valor (tipos complejos) por el constructor
    private Producto() { }

    // Constructor diseñado para crear un estado inicial válido
    public Producto(Guid id, string nombre, Dinero precioInicial) : base(id)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del producto es obligatorio.", nameof(nombre));

        Nombre = nombre;
        Precio = precioInicial ?? throw new ArgumentNullException(nameof(precioInicial));
        EstaActivo = true; // Estado inicial por defecto
    }

    // Métodos que expresan el Lenguaje Ubicuo y modifican el estado
    public void ActualizarPrecio(Dinero nuevoPrecio)
    {
        if (!EstaActivo)
            throw new InvalidOperationException("No se puede cambiar el precio de un producto descatalogado.");

        Precio = nuevoPrecio ?? throw new ArgumentNullException(nameof(nuevoPrecio));
    }

    public void Descatalogar()
    {
        EstaActivo = false;
    }
}
```

En la entidad `Producto`, observamos que las propiedades tienen `private set`. Esto significa que el estado de la entidad no puede ser modificado directamente desde fuera (por ejemplo, `producto.EstaActivo = false` no compilará). En su lugar, exponemos métodos con nombres que reflejan el negocio, como `Descatalogar()`. Esto centraliza la lógica y evita que el modelo de dominio se convierta en un simple contenedor de datos anémico.

**Por qué el constructor privado sin parámetros.** Al leer de la base de datos, EF Core puede llamar a un constructor con parámetros solo si todos ellos corresponden a propiedades escalares mapeadas (por nombre y tipo). No puede pasar por constructor navegaciones ni objetos de valor mapeados como tipos complejos, como `Precio`. Por eso las entidades que contienen objetos de valor o colecciones declaran un constructor privado sin parámetros: EF Core puede usar constructores privados, y el resto del código sigue obligado a usar el constructor público que valida. EF Core trata una propiedad con `private set` como de lectura y escritura y la mapea por convención; una propiedad sin setter, en cambio, no se mapea por convención.

### 3. Agregados y Reglas de Consistencia (Invariantes)

A medida que el modelo de dominio crece, las relaciones entre Entidades y Objetos de Valor se vuelven más complejas. Si permitimos que cualquier parte del sistema modifique cualquier entidad individualmente, es muy probable que los datos queden en un estado inconsistente. Para resolver esto, DDD introduce el concepto de **Agregado** (_Aggregate_).

#### 3.1. El Patrón Agregado (Aggregate) y la Raíz del Agregado (Aggregate Root)

Un Agregado es un clúster o agrupación lógica de Entidades y Objetos de Valor que se tratan como una única unidad a efectos de modificación de datos. El propósito principal de un Agregado es garantizar la consistencia de los cambios.

Cada Agregado tiene una única entidad principal llamada **Raíz del Agregado** (_Aggregate Root_). La regla fundamental de este patrón es: _cualquier modificación al estado de los objetos dentro del Agregado debe realizarse exclusivamente a través de la Raíz del Agregado._

Reglas de diseño de Agregados en .NET 10:

1. **Referencia externa:** los objetos externos al Agregado solo pueden mantener referencias a la Raíz del Agregado, nunca a las entidades internas.
2. **Límites transaccionales:** una transacción de base de datos debe modificar, idealmente, un solo Agregado por operación. Si necesitas modificar múltiples Agregados, suele ser un indicador de que los límites del Agregado están mal definidos o de que deberías usar consistencia eventual (mediante Eventos de Dominio).
3. **Borrado en cascada:** si se elimina la Raíz del Agregado, todas las entidades y objetos de valor internos deben eliminarse con ella.

#### 3.2. Protección de Invariantes y Reglas de Negocio

Las **Invariantes** son reglas de negocio que deben cumplirse en todo momento para que el sistema se considere válido. El Agregado es el responsable absoluto de proteger sus invariantes.

Por ejemplo, en un sistema de ventas, una invariante podría ser: _"Un pedido no puede superar los 10.000 euros de importe total sin aprobación manual"_. Si permitiéramos que un desarrollador añadiera líneas de pedido directamente a la base de datos o a una lista pública dentro de la clase `Pedido`, esta regla podría violarse fácilmente. Al obligar a que la adición de líneas pase por un método `AñadirLinea` en la Raíz del Agregado `Pedido`, garantizamos que la regla se evalúe siempre.

#### 3.3. Ejemplo Práctico: Diseño de un Agregado Transaccional

Vamos a modelar un Agregado `Pedido` (_Order_) que actúa como Raíz, y que contiene una colección de `LineaPedido` (_OrderItem_) que son entidades internas. Las reglas de negocio son: un pedido no admite más de 50 líneas, cada producto aparece en una sola línea, todos los precios van en euros y el total no puede superar 10.000 EUR.

```csharp
// Entidad interna del Agregado. No debe ser accesible directamente desde fuera.
public class LineaPedido : Entidad<Guid>
{
    public Guid ProductoId { get; private set; }
    public string NombreProducto { get; private set; } = string.Empty;
    public Dinero PrecioUnitario { get; private set; } = null!;
    public int Cantidad { get; private set; }

    private LineaPedido() { } // Para EF Core

    // Internal asegura que solo el Agregado (en el mismo assembly) pueda instanciarla
    internal LineaPedido(Guid id, Guid productoId, string nombreProducto, Dinero precioUnitario, int cantidad)
        : base(id)
    {
        if (cantidad <= 0) throw new ArgumentException("La cantidad debe ser mayor que cero.", nameof(cantidad));

        ProductoId = productoId;
        NombreProducto = nombreProducto;
        PrecioUnitario = precioUnitario;
        Cantidad = cantidad;
    }

    public Dinero CalcularSubtotal() => PrecioUnitario.Multiplicar(Cantidad);
}

// Raíz del Agregado (Aggregate Root)
public class Pedido : Entidad<Guid>
{
    private const string Moneda = "EUR";
    private const int MaximoLineas = 50;
    private static readonly Dinero ImporteMaximo = new(10_000m, Moneda);

    public Guid ClienteId { get; private set; } // referencia a otro agregado solo por su Id
    public DateTime FechaCreacion { get; private set; }

    // Encapsulación de la colección. Exponemos IReadOnlyCollection para evitar modificaciones externas.
    private readonly List<LineaPedido> _lineas = new();
    public IReadOnlyCollection<LineaPedido> Lineas => _lineas.AsReadOnly();

    private Pedido() { } // Para EF Core

    public Pedido(Guid id, Guid clienteId) : base(id)
    {
        ClienteId = clienteId;
        FechaCreacion = DateTime.UtcNow;
    }

    // Método que protege las invariantes del Agregado
    public void AñadirProducto(Guid productoId, string nombre, Dinero precio, int cantidad)
    {
        if (precio.Moneda != Moneda)
            throw new InvalidOperationException($"Los precios del pedido deben estar en {Moneda}.");

        // Invariante 1: No se pueden añadir más de 50 líneas distintas
        if (_lineas.Count >= MaximoLineas)
            throw new InvalidOperationException($"Un pedido no puede tener más de {MaximoLineas} líneas diferentes.");

        // Invariante 2: cada producto aparece en una sola línea
        if (_lineas.Any(l => l.ProductoId == productoId))
            throw new InvalidOperationException("El producto ya existe en el pedido. Actualice la cantidad.");

        var nuevaLinea = new LineaPedido(Guid.NewGuid(), productoId, nombre, precio, cantidad);

        // Invariante 3: el total del pedido no puede superar 10.000 EUR
        var totalResultante = CalcularTotal().Sumar(nuevaLinea.CalcularSubtotal());
        if (totalResultante.Importe > ImporteMaximo.Importe)
            throw new InvalidOperationException("El pedido excede el límite máximo permitido de 10.000 EUR.");

        _lineas.Add(nuevaLinea);
    }

    public Dinero CalcularTotal() =>
        _lineas.Aggregate(new Dinero(0m, Moneda), (total, linea) => total.Sumar(linea.CalcularSubtotal()));
}
```

En este diseño, la colección `_lineas` es privada. Hacia el exterior, solo exponemos `IReadOnlyCollection<LineaPedido>`. Esto impide que cualquier código externo haga `pedido.Lineas.Add(...)` saltándose las reglas de negocio. La única forma de añadir un producto es a través del método `AñadirProducto`, el cual verifica todas las invariantes antes de modificar el estado interno: si alguna falla, el agregado queda exactamente como estaba. Observa también que `Pedido` referencia al cliente solo por su identificador (`ClienteId`), no con una propiedad de tipo `Cliente`: los agregados se referencian entre sí por Id.

**Persistencia del agregado con EF Core 10 (en Infraestructura).** Los objetos de valor se mapean como **tipos complejos** (`ComplexProperty`), disponibles desde EF Core 8 y ampliados en EF Core 10 (tipos complejos opcionales, es decir, propiedades anulables como `Dinero?`, y asignación a una columna JSON con `ToJson()`). Pueden declararse como clases, records o `struct`. La documentación oficial los recomienda frente a los tipos de entidad en propiedad (_owned entities_) para modelar objetos de valor, porque se comparan por valor y no tienen identidad oculta. Por defecto, sus propiedades se guardan como columnas de la tabla de la entidad (por ejemplo, `PrecioUnitario_Importe` y `PrecioUnitario_Moneda`).

```csharp
// Proyecto de Infraestructura (paquete Microsoft.EntityFrameworkCore.SqlServer)
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("Pedidos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever(); // el Id lo asigna el dominio

        // EF Core usa el campo _lineas (convención de nombres) para rellenar la colección
        builder.HasMany(p => p.Lineas).WithOne().HasForeignKey("PedidoId").IsRequired();
    }
}

internal sealed class LineaPedidoConfiguration : IEntityTypeConfiguration<LineaPedido>
{
    public void Configure(EntityTypeBuilder<LineaPedido> builder)
    {
        builder.ToTable("LineasPedido");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.NombreProducto).HasMaxLength(200);

        builder.ComplexProperty(l => l.PrecioUnitario, precio =>
        {
            precio.Property(d => d.Importe).HasPrecision(18, 2);
            precio.Property(d => d.Moneda).HasMaxLength(3);
        });
    }
}
```

`ValueGeneratedNever()` es importante cuando el dominio genera los `Guid`: por convención, EF Core considera que una clave `Guid` se genera al insertar, y una entidad nueva con la clave ya asignada que se añade a la colección de un agregado cargado se interpretaría como existente en lugar de como nueva. `PedidoId` es una propiedad _shadow_: existe en la tabla, pero no en la clase `LineaPedido`.

### 4. Eventos de Dominio: Expresando Cambios Significativos

Hasta ahora, hemos visto cómo los Agregados protegen la consistencia de los datos dentro de sus propios límites. Sin embargo, en sistemas complejos, un cambio en un Agregado a menudo requiere que otros componentes del sistema reaccionen. Aquí es donde entran en juego los Eventos de Dominio.

#### 4.1. Concepto y Propósito de los Eventos de Dominio

Un **Evento de Dominio** (_Domain Event_) es un registro de algo significativo que ya ha ocurrido en el dominio del negocio. Se nombran siempre en tiempo pasado (por ejemplo, `PedidoConfirmado`, `ClienteRegistrado`, `StockAgotado`) y transportan los datos que necesitan quienes reaccionan (identificadores y valores relevantes), no el agregado completo.

Propósitos principales:

1. **Desacoplamiento:** permiten que un Agregado notifique al resto del sistema que algo ha sucedido sin necesidad de conocer quién está escuchando ni qué acciones se tomarán.
2. **Consistencia eventual:** si la regla de que "una transacción modifica un solo Agregado" se mantiene, los Eventos de Dominio son el mecanismo para actualizar otros Agregados en un paso posterior, logrando consistencia eventual.
3. **Trazabilidad:** proporcionan un historial claro de los cambios de estado críticos en el sistema.

Es importante distinguir los Eventos de Dominio (que ocurren y se manejan dentro del mismo Bounded Context o aplicación) de los **Eventos de Integración** (que se publican en un bus de mensajes como RabbitMQ o Azure Service Bus para comunicarse con otros microservicios, tema que se abordará en el tema 7 del curso).

#### 4.2. Implementación y Despacho de Eventos en .NET 10

La implementación habitual de Eventos de Dominio en .NET consiste en:

1. Definir una interfaz marcadora propia, `IDomainEvent`, en el proyecto de Dominio.
2. Crear los eventos como `records` inmutables.
3. Añadir una colección de eventos pendientes en la clase base `Entidad`: el agregado registra el evento, pero no lo publica.
4. Despachar los eventos desde la infraestructura, al guardar los cambios, hacia los manejadores (_handlers_) registrados en el contenedor de inyección de dependencias.

Para el paso 4 puede usarse una biblioteca de mediador en memoria, como MediatR. Desde su versión 13 (2025), MediatR se distribuye con licencia comercial, por lo que conviene revisar sus condiciones antes de adoptarlo. Además, si la interfaz `IDomainEvent` hereda de un tipo de la biblioteca (como `INotification`), el Dominio pasa a depender de un paquete externo, lo que contradice el aislamiento que se explica en la sección 6.2. Un despachador propio de pocas líneas, como el del siguiente ejemplo, evita ambos problemas.

#### 4.3. Ejemplo Práctico: Publicación y Consumo de Eventos de Dominio

Primero, definimos en el proyecto de Dominio las abstracciones básicas, sin ninguna dependencia externa.

```csharp
// Proyecto de Dominio
public interface IDomainEvent
{
    DateTime FechaOcurrencia { get; }
}

// Contrato que implementan los manejadores (en la capa de Aplicación)
public interface IDomainEventHandler<in TEvento> where TEvento : IDomainEvent
{
    Task HandleAsync(TEvento evento, CancellationToken ct);
}

// Interfaz no genérica para que la infraestructura encuentre entidades con eventos
public interface IConEventosDominio
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void LimpiarEventosDominio();
}

// Clase base Entidad ampliada para registrar eventos
public abstract class Entidad<TId> : IConEventosDominio where TId : IEquatable<TId>
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public TId Id { get; protected set; } = default!;
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected Entidad(TId id) { Id = id; }
    protected Entidad() { }

    protected void AñadirEventoDominio(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void LimpiarEventosDominio() => _domainEvents.Clear();

    // ... (Equals y GetHashCode como en la sección 2.3)
}
```

A continuación, creamos un evento específico y modificamos nuestro Agregado `Pedido` para que lo registre cuando ocurra una acción importante.

```csharp
// El evento de dominio, inmutable y en tiempo pasado
public sealed record PedidoConfirmadoDomainEvent(Guid PedidoId, Guid ClienteId, decimal ImporteTotal) : IDomainEvent
{
    public DateTime FechaOcurrencia { get; } = DateTime.UtcNow;
}

// Dentro de la clase Pedido (Agregado)
public class Pedido : Entidad<Guid>
{
    // ... (código anterior omitido) ...
    public bool EstaConfirmado { get; private set; }

    public void AñadirProducto(Guid productoId, string nombre, Dinero precio, int cantidad)
    {
        if (EstaConfirmado)
            throw new InvalidOperationException("No se pueden añadir productos a un pedido confirmado.");
        // ... (resto de invariantes de la sección 3.3)
    }

    public void Confirmar()
    {
        if (EstaConfirmado)
            throw new InvalidOperationException("El pedido ya está confirmado.");
        if (_lineas.Count == 0)
            throw new InvalidOperationException("No se puede confirmar un pedido vacío.");

        EstaConfirmado = true;

        // Registramos el evento de dominio. Aún no se despacha, solo se almacena.
        AñadirEventoDominio(new PedidoConfirmadoDomainEvent(Id, ClienteId, CalcularTotal().Importe));
    }
}
```

Finalmente, creamos un manejador (_Handler_) que reaccionará a este evento. Este manejador vive en la capa de Aplicación, logrando el desacoplamiento deseado: `Pedido` no sabe que existe.

```csharp
public sealed class EnviarEmailConfirmacionHandler(IEmailService emailService)
    : IDomainEventHandler<PedidoConfirmadoDomainEvent>
{
    public async Task HandleAsync(PedidoConfirmadoDomainEvent evento, CancellationToken ct)
    {
        string mensaje = $"Su pedido {evento.PedidoId} por un importe de {evento.ImporteTotal} EUR ha sido confirmado.";
        await emailService.EnviarEmailAsync(evento.ClienteId, "Pedido confirmado", mensaje, ct);
    }
}

// IEmailService es una interfaz de la aplicación (su implementación está en Infraestructura)
public interface IEmailService
{
    Task EnviarEmailAsync(Guid clienteId, string asunto, string cuerpo, CancellationToken ct);
}
```

**Despacho al guardar.** En Infraestructura, el despachador busca en el contenedor todos los manejadores registrados para el tipo concreto de cada evento, y el `DbContext` sobrescribe `SaveChangesAsync` para recoger los eventos de las entidades seguidas por el `ChangeTracker`.

```csharp
// Proyecto de Infraestructura
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

public interface IDomainEventDispatcher
{
    Task DespacharAsync(IReadOnlyList<IDomainEvent> eventos, CancellationToken ct);
}

public sealed class DomainEventDispatcher(IServiceProvider provider) : IDomainEventDispatcher
{
    public async Task DespacharAsync(IReadOnlyList<IDomainEvent> eventos, CancellationToken ct)
    {
        foreach (var evento in eventos)
        {
            // IDomainEventHandler<TipoConcretoDelEvento>
            var tipoHandler = typeof(IDomainEventHandler<>).MakeGenericType(evento.GetType());
            var metodo = tipoHandler.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

            foreach (var handler in provider.GetServices(tipoHandler))
            {
                await (Task)metodo.Invoke(handler, [evento, ct])!;
            }
        }
    }
}

public sealed class VentasDbContext(DbContextOptions<VentasDbContext> options, IDomainEventDispatcher dispatcher)
    : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ventas");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VentasDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Recoger y vaciar los eventos pendientes de las entidades seguidas
        var entidades = ChangeTracker.Entries<IConEventosDominio>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();

        var eventos = entidades.SelectMany(e => e.DomainEvents).ToList();
        entidades.ForEach(e => e.LimpiarEventosDominio());

        // 2. Guardar los cambios del agregado
        int filas = await base.SaveChangesAsync(cancellationToken);

        // 3. Despachar solo si el guardado ha tenido éxito: el hecho ya ha ocurrido
        await dispatcher.DespacharAsync(eventos, cancellationToken);

        return filas;
    }
}
```

Dos detalles de configuración: la propiedad `DomainEvents` no se guarda en la base de datos. Como no tiene setter, EF Core ya no la mapea por convención, pero conviene excluirla de forma explícita en cada configuración de entidad con `builder.Ignore(p => p.DomainEvents);` (por ejemplo, en `PedidoConfiguration` y `LineaPedidoConfiguration`) para dejar clara la intención. Y todos los componentes se registran en el contenedor:

```csharp
// Program.cs del Host
builder.Services.AddDbContext<VentasDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Ventas")));
builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
builder.Services.AddScoped<IDomainEventHandler<PedidoConfirmadoDomainEvent>, EnviarEmailConfirmacionHandler>();
```

**Antes o después de guardar.** En este ejemplo los eventos se despachan después de confirmar la transacción, así que un manejador nunca actúa sobre un cambio que luego se deshace; a cambio, si el manejador falla, el pedido ya está guardado y el efecto secundario (el correo) se pierde. Otra opción habitual es despacharlos antes de `base.SaveChangesAsync`, de modo que los cambios que hagan los manejadores sobre el mismo `DbContext` se guarden en la misma transacción. Cuando la reacción debe ejecutarse con garantías aunque haya fallos, se usa el patrón **Outbox** (el evento se guarda en una tabla dentro de la misma transacción y un proceso aparte lo entrega), que se trata en el tema 7.

### 5. Modelos de Lectura y el Monolito DDD Cohesionado

Uno de los desafíos más comunes al implementar DDD es la fricción entre el modelo de dominio (optimizado para escribir datos y aplicar reglas de negocio) y las necesidades de la interfaz de usuario (optimizada para leer datos de forma rápida y estructurada).

#### 5.1. Modelos de Lectura Independientes (Sin CQRS Completo)

El modelo de dominio, con sus Agregados fuertemente encapsulados, es excelente para procesar comandos (acciones que cambian el estado). Sin embargo, usar estos mismos Agregados para realizar consultas (_queries_) para mostrar en pantalla suele ser ineficiente. Cargar un Agregado completo con todas sus entidades hijas solo para mostrar un listado en una tabla consume memoria y ciclos de CPU innecesarios.

La solución dentro de un enfoque DDD pragmático es utilizar **Modelos de Lectura Independientes**. Esto no requiere implementar el patrón CQRS (_Command Query Responsibility Segregation_) en su forma más compleja con bases de datos separadas y sincronización por eventos (lo cual se estudiará en el tema 10).

En su lugar, mantenemos una única base de datos, pero separamos el código que lee del código que escribe:

- **Para escrituras (Commands):** utilizamos Entity Framework Core, cargamos el Agregado completo, ejecutamos el método de dominio (ej. `pedido.Confirmar()`) y guardamos los cambios.
- **Para lecturas (Queries):** evitamos cargar el Agregado. En su lugar, ejecutamos consultas directas a la base de datos que devuelven DTOs (_Data Transfer Objects_) planos, diseñados exactamente para lo que la vista necesita. Esto se puede hacer con EF Core usando proyecciones (`.Select()`) y `.AsNoTracking()`, o utilizando micro-ORMs como Dapper para un rendimiento extremo.

```csharp
// Modelo de lectura (DTO): solo datos, con la forma que necesita la pantalla
public sealed record ResumenPedidoDto(Guid PedidoId, Guid ClienteId, int NumeroLineas, decimal Total, string Estado);

// Consulta de lectura: no carga el agregado ni pasa por sus métodos
public sealed class ConsultasPedidos(VentasDbContext dbContext)
{
    public async Task<IReadOnlyList<ResumenPedidoDto>> ObtenerPedidosDeClienteAsync(Guid clienteId, CancellationToken ct)
    {
        return await dbContext.Pedidos
            .AsNoTracking()
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.FechaCreacion)
            .Select(p => new ResumenPedidoDto(
                p.Id,
                p.ClienteId,
                p.Lineas.Count(),
                p.Lineas.Sum(l => l.PrecioUnitario.Importe * l.Cantidad),
                p.EstaConfirmado ? "Confirmado" : "Pendiente"))
            .ToListAsync(ct);
    }
}
```

La proyección con `Select` hace que la base de datos devuelva solo las columnas necesarias (el recuento y la suma se calculan en SQL); como el resultado no son entidades, EF Core no las sigue en el `ChangeTracker`, y `AsNoTracking()` deja explícita esa intención. Fíjate en que el DTO usa `ClienteId`: el agregado `Pedido` no tiene una navegación a `Cliente`. Si la pantalla necesita el nombre del cliente, el contexto de Ventas guarda su propia copia de los datos que usa (por ejemplo, una entidad `ClienteVentas` con `Id` y `Nombre`, expuesta en `VentasDbContext` como `DbSet<ClienteVentas> ClientesVentas`) y la consulta la combina. En .NET 10, LINQ incorpora el operador `LeftJoin`, que EF Core 10 traduce a `LEFT JOIN`:

```csharp
var filas = await dbContext.Pedidos
    .AsNoTracking()
    .LeftJoin(dbContext.ClientesVentas,
        p => p.ClienteId,
        c => c.Id,
        (p, c) => new { p.Id, NombreCliente = c != null ? c.Nombre : "(desconocido)" })
    .ToListAsync(ct);
```

Este enfoque mantiene el modelo de dominio puro y libre de propiedades añadidas únicamente para satisfacer las necesidades de la interfaz de usuario.

#### 5.2. Criterios para un Monolito DDD Preparado para Microservicios

Salvo que haya razones claras para distribuir el sistema desde el principio (tema 1), es recomendable comenzar el desarrollo de sistemas complejos utilizando un **Monolito Modular** basado en DDD, en lugar de saltar directamente a microservicios. Un monolito modular bien diseñado ofrece la simplicidad de despliegue de un monolito tradicional, pero con la separación lógica estricta necesaria para extraer microservicios en el futuro si la escalabilidad lo requiere.

Para que un monolito DDD esté verdaderamente preparado para una futura extracción, debe cumplir estos criterios:

1. **Aislamiento de Bounded Contexts:** un Bounded Context (ej. Ventas) no debe tener referencias directas de proyecto al modelo de dominio de otro Bounded Context (ej. Inventario).
2. **Comunicación por interfaces o eventos:** si Ventas necesita datos de Inventario, debe hacerlo a través de una interfaz de servicio abstracta o reaccionando a Eventos de Dominio, nunca consultando directamente las tablas de base de datos del otro contexto.
3. **Bases de datos lógicas separadas:** aunque físicamente compartan el mismo servidor SQL Server o PostgreSQL, cada Bounded Context debe tener su propio esquema (_schema_) y su propio `DbContext` en EF Core. No deben existir claves foráneas (_Foreign Keys_) duras entre tablas de distintos Bounded Contexts.

Si se respetan estas reglas, extraer el módulo de "Ventas" a un microservicio independiente en el futuro consistirá sobre todo en cambios de infraestructura (despliegue, comunicación por red, migración de datos), no en reescribir la lógica de negocio.

### 6. Antipatrones y Errores Comunes en la Implementación de DDD

La adopción de Domain-Driven Design conlleva una curva de aprendizaje pronunciada. Es habitual que los equipos que transicionan desde arquitecturas tradicionales centradas en datos cometan ciertos errores estructurales. Identificar y corregir estos antipatrones es vital para el éxito del proyecto.

#### 6.1. El Antipatrón del Modelo de Dominio Anémico

El **Modelo de Dominio Anémico** es el error más frecuente al intentar aplicar DDD. Ocurre cuando las Entidades y los Objetos de Valor se diseñan como simples contenedores de datos (clases con propiedades públicas que tienen `get` y `set`), careciendo por completo de comportamiento y lógica de negocio.

En un modelo anémico, toda la lógica de negocio se extrae y se coloca en clases externas, típicamente llamadas "Servicios" o "Managers" (ej. `PedidoService`, `ClienteManager`).

**Por qué es un problema:**

- Rompe el principio de encapsulación de la Programación Orientada a Objetos.
- Las reglas de negocio (invariantes) no están protegidas por la entidad; cualquier desarrollador puede modificar el estado de la entidad directamente, saltándose las validaciones del servicio.
- El código se vuelve procedimental en lugar de orientado a objetos, dificultando la comprensión del Lenguaje Ubicuo.

**Cómo solucionarlo:** como vimos en las secciones 2 y 3, la solución es mover el comportamiento hacia las entidades. Las propiedades deben tener setters privados (`private set`) o, en objetos de valor, accesores `init` que validen (sección 2.2), y el estado solo debe modificarse a través de métodos públicos en la entidad que representen acciones reales del negocio (ej. `pedido.AplicarDescuento(descuento)` en lugar de `pedidoService.AplicarDescuento(pedido, descuento)`).

#### 6.2. Falta de Aislamiento del Dominio

El núcleo de DDD dicta que el Modelo de Dominio debe ser el corazón de la aplicación y no debe depender de ninguna tecnología externa, framework de base de datos, ni librerías de interfaz de usuario. Esto se conoce como la regla de dependencia de la Arquitectura Limpia (_Clean Architecture_, que se explorará en el tema 4).

Un error común en .NET es contaminar las clases del dominio con atributos de Entity Framework Core o de serialización JSON.

```csharp
// MAL: El dominio está acoplado a la infraestructura (Entity Framework y JSON)
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

public class Cliente : Entidad<Guid>
{
    [Column("nombre_completo", TypeName = "varchar(100)")] // Acoplamiento a BD
    public string Nombre { get; private set; } = string.Empty;

    [JsonIgnore] // Acoplamiento a la capa de presentación/API
    public string CodigoSecreto { get; private set; } = string.Empty;
}
```

**Por qué es un problema:** si el día de mañana el equipo decide cambiar el ORM (por ejemplo, pasar de EF Core a Dapper, o migrar a una base de datos NoSQL), tendrá que modificar las clases del dominio, lo cual viola el Principio de Responsabilidad Única. El dominio debe cambiar solo cuando cambian las reglas del negocio, no cuando cambia la tecnología de persistencia.

**Cómo solucionarlo:** el Modelo de Dominio debe ser código C# puro (POCO, _Plain Old CLR Objects_). La configuración de la base de datos debe realizarse en la capa de Infraestructura utilizando la API Fluida (_Fluent API_) de Entity Framework Core, mediante clases que implementen `IEntityTypeConfiguration<T>`.

```csharp
// Proyecto de Dominio - Cliente.cs
// BIEN: El dominio es puro y no sabe nada de bases de datos
public class Cliente : Entidad<Guid>
{
    public string Nombre { get; private set; } = string.Empty;
    public string CodigoSecreto { get; private set; } = string.Empty;
    // Constructores y métodos de negocio...
}

// Proyecto de Infraestructura (separado del Dominio) - ClienteConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nombre).HasColumnName("nombre_completo").HasMaxLength(100);
        builder.Ignore(c => c.DomainEvents);
        // La configuración de persistencia se mantiene fuera del dominio
    }
}
```

Manteniendo este nivel de aislamiento, garantizamos que el modelo de dominio en .NET 10 sea altamente testeable mediante pruebas unitarias rápidas, expresivo, y verdaderamente alineado con las necesidades y el lenguaje del negocio.

---

## Laboratorio B2BOrders: dominio, aplicación y tests

Se aplica DDD táctico sobre la solución `B2BOrders`: se abandona el modelo anémico por un modelo rico, se protegen las invariantes dentro del agregado, se registran eventos de dominio, se orquesta con la capa de Aplicación (Command/Handler) y se verifica todo con tests unitarios, sin base de datos.

### 1. Conceptos clave

| Concepto              | Definición                                                                                                                                     | Ejemplo                                              |
| --------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- |
| **Value Object**      | Definido por su valor, no por su identidad. Es inmutable                                                                                       | Dinero, Dirección, Email                             |
| **Entidad**           | Definida por un ID único que perdura en el tiempo                                                                                              | Pedido, Cliente, Producto                            |
| **Agregado**          | Conjunto de entidades y objetos de valor tratados como una unidad                                                                              | Un `Order` con sus `OrderLine`                       |
| **Raíz del Agregado** | La única entidad accesible desde fuera                                                                                                         | `Order` (controla a las líneas)                      |
| **Invariante**        | Regla de negocio que siempre debe ser verdadera. Se implementa con cláusulas de guarda (`if (...) throw`) al inicio de los métodos públicos    | "No se puede añadir una línea con cantidad negativa" |
| **Evento de Dominio** | Mensaje de que "algo ha sucedido" en el pasado; desacopla el efecto secundario (email, envíos) de la lógica principal. Nombre: verbo en pasado | `OrderPaid`, `OrderShipped`                          |

### 2. Value Object: `Money`

En `Domain/Shared` (o `Domain/ValueObjects`). Se usa `record` para obtener igualdad por valor.

```csharp
namespace B2BOrders.Domain.Shared;

public record Money(decimal Amount, string Currency)
{
    // Factory method: validación al crear
    public static Money Of(decimal amount, string currency)
    {
        if (amount < 0) throw new ArgumentException("El monto no puede ser negativo.");
        return new Money(amount, currency);
    }

    // Operador de suma: lógica de negocio encapsulada
    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException("No se pueden sumar divisas distintas.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    // Helper estático (¡ojo! se usa sin 'new')
    public static Money Zero(string currency) => new Money(0, currency);
}
```

### 3. Estados y entidad hija

En `Domain/Orders`:

```csharp
namespace B2BOrders.Domain.Orders;

public enum OrderStatus
{
    New,
    Paid,
    Shipped,
    Cancelled
}
```

```csharp
using B2BOrders.Domain.Shared;

namespace B2BOrders.Domain.Orders;

public class OrderLine
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }

    // Propiedad calculada (no se guarda el total, se calcula)
    public Money LineTotal => new Money(UnitPrice.Amount * Quantity, UnitPrice.Currency);

    private OrderLine() { } // Requerido por EF Core

    public OrderLine(int productId, int quantity, Money unitPrice)
    {
        if (quantity <= 0) throw new ArgumentException("La cantidad debe ser positiva.");

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
```

### 4. Infraestructura de eventos

Contrato base para cualquier evento del sistema (`Domain/Shared/IDomainEvent.cs`):

```csharp
namespace B2BOrders.Domain.Shared;

public interface IDomainEvent
{
    // Momento exacto en que ocurrió el hecho
    DateTime OccurredOn { get; }
}
```

Evento concreto (`Domain/Orders/Events/OrderPaidDomainEvent.cs`). Se usa `record` porque es un dato inmutable, y solo se guardan IDs y datos relevantes, no el objeto entero:

```csharp
using B2BOrders.Domain.Shared;

namespace B2BOrders.Domain.Orders.Events;

public record OrderPaidDomainEvent(int OrderId, int PaymentId, Money Amount) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
```

### 5. La Raíz del Agregado: `Order`

`Order` protege la lista de líneas (`_orderLines`), valida las invariantes en cada método público y registra los eventos en una "mochila" privada (`_domainEvents`). Versión final, con todo integrado:

```csharp
using B2BOrders.Domain.Orders.Events;
using B2BOrders.Domain.Shared;

namespace B2BOrders.Domain.Orders;

public class Order
{
    public int Id { get; private set; }
    public int CustomerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money TotalAmount { get; private set; }

    // Encapsulamiento: lista privada, exposición de solo lectura
    private readonly List<OrderLine> _orderLines = new();
    public IReadOnlyCollection<OrderLine> OrderLines => _orderLines.AsReadOnly();

    // 1. Lista privada para guardar los eventos que van ocurriendo
    private readonly List<IDomainEvent> _domainEvents = new();

    private Order() { } // Constructor vacío para EF Core

    public Order(int customerId)
    {
        CustomerId = customerId;
        CreatedAt = DateTime.UtcNow;
        Status = OrderStatus.New;
        // Money.Zero es un método estático, no un constructor
        TotalAmount = Money.Zero("EUR");
    }

    // Comportamiento de negocio: añadir líneas con validación
    public void AddLine(int productId, int quantity, Money unitPrice)
    {
        // Invariante: cantidad positiva
        if (quantity <= 0)
            throw new ArgumentException("La cantidad debe ser positiva.", nameof(quantity));

        // Invariante: estado correcto
        if (Status != OrderStatus.New)
            throw new InvalidOperationException("No se pueden modificar pedidos que no sean Nuevos.");

        var line = new OrderLine(productId, quantity, unitPrice);
        _orderLines.Add(line);

        RecalculateTotal();
    }

    // Comportamiento de negocio: procesar el pago
    public void MarkAsPaid(int paymentId)
    {
        // Invariante: idempotencia (si ya está pagado, no hacemos nada)
        if (Status == OrderStatus.Paid) return;

        // Invariante: integridad de datos
        if (TotalAmount.Amount <= 0)
            throw new InvalidOperationException("No se puede pagar un pedido con total 0.");

        // 1. Cambio de estado
        Status = OrderStatus.Paid;

        // 2. Registro del evento (efecto secundario): notificamos que esto ha ocurrido
        AddDomainEvent(new OrderPaidDomainEvent(Id, paymentId, TotalAmount));
    }

    // 2. Método para que EF Core o el repositorio extraigan los eventos al guardar
    public IReadOnlyCollection<IDomainEvent> PullDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear(); // ¡Vital! Limpiamos para no publicarlos dos veces
        return events;
    }

    // 3. Helper privado para uso interno
    private void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    private void RecalculateTotal()
    {
        // Usamos el operador + definido en Money
        TotalAmount = _orderLines.Aggregate(
            Money.Zero("EUR"),
            (current, line) => current + line.LineTotal
        );
    }
}
```

**Ciclo de vida del evento:**

1. **Llamada:** el código cliente llama a `order.MarkAsPaid(123)`.
2. **Validación:** se comprueban las reglas (invariantes).
3. **Cambio:** se actualiza `Status = Paid`.
4. **Registro:** se añade `OrderPaidDomainEvent` a la lista `_domainEvents`.
5. **(Futuro):** cuando se haga `SaveChanges`, se leerá esa lista y se publicará.

### 6. Capa de Aplicación: Command/Handler

La capa de aplicación es el **"guardia de tráfico"**: no tiene reglas de negocio (eso es cosa del Dominio), pero sabe a quién llamar.

- **Input:** DTOs o comandos (ej. `PlaceOrderCommand`).
- **Proceso:** carga entidades, ejecuta métodos del dominio, guarda cambios.
- **Output:** DTOs o IDs.

**Paso 1: dependencias.** Para usar el patrón CQRS de forma limpia se instala MediatR en la capa de aplicación (desde la raíz de la solución):

```bash
dotnet add B2BOrders.Application package MediatR
```

**Paso 2: contrato del repositorio (Domain).** El dominio define _qué_ necesita (guardar un pedido), pero no _cómo_ (SQL, archivo, API). Archivo: `B2BOrders.Domain/Orders/IOrderRepository.cs`.

```csharp
namespace B2BOrders.Domain.Orders;

public interface IOrderRepository
{
    // Cuidado con el nombre: AddAsync (con A)
    Task AddAsync(Order order, CancellationToken cancellationToken);

    // Contrato para leer por ID (se usará más adelante)
    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken);
}
```

**Paso 3: el comando (Application).** Objeto de datos (DTO) que representa la intención del usuario. Archivo: `B2BOrders.Application/Orders/PlaceOrder/PlaceOrderCommand.cs`.

```csharp
using MediatR; // Requiere el paquete instalado en el paso 1

namespace B2BOrders.Application.Orders.PlaceOrder;

// DTO simple para las líneas dentro del comando
public record OrderItemDto(int ProductId, int Quantity, decimal UnitPrice, string Currency);

// El comando devuelve un 'int' (el ID del pedido creado)
public record PlaceOrderCommand(
    int CustomerId,
    List<OrderItemDto> Items
) : IRequest<int>;
```

**Paso 4: el handler (Application).** El cerebro que coordina la operación; traduce el DTO a objetos de dominio. Archivo: `B2BOrders.Application/Orders/PlaceOrder/PlaceOrderHandler.cs`.

```csharp
using MediatR;
using B2BOrders.Domain.Orders;
using B2BOrders.Domain.Shared;

namespace B2BOrders.Application.Orders.PlaceOrder;

public class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, int>
{
    private readonly IOrderRepository _repository;

    public PlaceOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        // 1. Crear el agregado (entidad raíz)
        var order = new Order(command.CustomerId);

        // 2. Ejecutar lógica de negocio (añadir líneas)
        // Convertimos los DTOs planos a Value Objects ricos (Money)
        foreach (var item in command.Items)
        {
            var price = Money.Of(item.UnitPrice, item.Currency);
            order.AddLine(item.ProductId, item.Quantity, price);
        }

        // 3. Persistir: el repositorio guardará el Order y sus eventos en una transacción
        await _repository.AddAsync(order, cancellationToken);

        // 4. Retornar
        return order.Id;
    }
}
```

### 7. Tests unitarios con xUnit

Se verifica que el Dominio funciona correctamente, aislado de la base de datos y la API.

```bash
# 1. Crear proyecto de tests
dotnet new xunit -n B2BOrders.Domain.Tests

# 2. Añadir a la solución
dotnet sln add B2BOrders.Domain.Tests/B2BOrders.Domain.Tests.csproj

# 3. Referenciar SOLAMENTE al Dominio (test puro)
dotnet add B2BOrders.Domain.Tests reference B2BOrders.Domain
```

Archivo `OrderTests.cs` en `B2BOrders.Domain.Tests`, con el patrón **AAA** (Arrange, Act, Assert):

```csharp
using Xunit;
using B2BOrders.Domain.Orders;
using B2BOrders.Domain.Shared;
using B2BOrders.Domain.Orders.Events;

namespace B2BOrders.Domain.Tests;

public class OrderTests
{
    [Fact]
    public void AddLine_Should_CalculateTotalCorrectly()
    {
        // ARRANGE: preparamos el escenario
        var order = new Order(1);

        // ACT: ejecutamos la acción a probar
        order.AddLine(1, 2, Money.Of(10, "EUR")); // 2 unidades a 10 EUR = 20
        order.AddLine(2, 1, Money.Of(50, "EUR")); // 1 unidad a 50 EUR = 50

        // ASSERT: esperamos 70 EUR en total
        Assert.Equal(70m, order.TotalAmount.Amount);
        Assert.Equal("EUR", order.TotalAmount.Currency);
    }

    [Fact]
    public void MarkAsPaid_Should_Raise_DomainEvent()
    {
        // ARRANGE
        var order = new Order(1);
        order.AddLine(1, 1, Money.Of(100, "EUR")); // necesario para que Total > 0

        // ACT
        order.MarkAsPaid(paymentId: 123);

        // ASSERT: recuperamos los eventos generados
        var events = order.PullDomainEvents();

        Assert.Single(events); // debe haber 1 solo evento
        var domainEvent = Assert.IsType<OrderPaidDomainEvent>(events.First());
        Assert.Equal(100m, domainEvent.Amount.Amount);
    }
}
```

```bash
dotnet test
# Resultado esperado: Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2
```

### 8. Resolución de problemas comunes

| Error                                                                  | Causa probable                     | Solución                                                                                                          |
| ---------------------------------------------------------------------- | ---------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `CS0246` The type `IRequest` could not be found                        | Falta el paquete NuGet             | `dotnet add B2BOrders.Application package MediatR`                                                                |
| `CS1061` `IOrderRepository` does not contain definition for `AddAsync` | Nombre mal escrito en la interfaz  | Verifica que pone `AddAsync` y no `AddSync`                                                                       |
| `CS8618` Non-nullable property `TotalAmount` must contain value        | Constructor privado de EF Core     | Inicializa la propiedad a `null!` en el constructor privado: `TotalAmount = null!;`                               |
| `CS0103` The name `price` does not exist                               | Confusión de nombres de parámetros | En `Order.AddLine`, usa el nombre del argumento (`unitPrice`) al crear la línea                                   |
| `Money does not contain a definition for Zero`                         | `Zero` no es público/estático      | Debe ser `public static` dentro del record `Money`                                                                |
| `new Money.Zero(...)`                                                  | `new` con un método estático       | Usa `Money.Zero(...)`, sin `new`                                                                                  |
| Referencias o tipos no encontrados                                     | Namespace o `using` incorrectos    | Todos los archivos deben usar `B2BOrders.Domain.Orders` (o `.Shared` para `Money`) y los `using` correspondientes |

### 9. Verificación

- [ ] Existen `Money`, `OrderStatus`, `OrderLine`, `Order`, `IDomainEvent` y `OrderPaidDomainEvent`.
- [ ] `Order` tiene la lista privada `_domainEvents` y el método `PullDomainEvents`.
- [ ] `AddLine` lanza excepción si `Status != New`, y `MarkAsPaid` cambia el estado **y** registra un evento.
- [ ] `dotnet build` compila toda la solución sin errores.
- [ ] `dotnet test` pasa los 2 tests en verde.
- [ ] Entiendo que el Handler **orquesta pero no contiene reglas de negocio** (como validar negativos).
- [ ] Entiendo que el test unitario verifica el Dominio **sin usar base de datos**.
