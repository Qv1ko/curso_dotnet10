# Tema 1 — Arquitecturas modernas de backend en .NET 10

## Contenido

- [Resumen del Tema 1](#resumen-del-tema-1)
- [Evolución y Selección de Arquitecturas en .NET 10](#evolución-y-selección-de-arquitecturas-en-net-10)
- [Laboratorio B2BOrders](#laboratorio-b2borders)

---

## Resumen del Tema 1

### Idea central

Cada arquitectura de backend nació para resolver los problemas de la anterior, y la elección no debe hacerse por moda sino por criterios objetivos (tráfico, equipo, despliegue, dominios, costes). Para la mayoría de proyectos nuevos, el punto de partida recomendado es un **monolito modular** con límites de dominio claros (_Monolith First_), extrayendo microservicios solo cuando exista un dolor real y medible.

### 1. Evolución histórica

| Etapa                                           | Qué resolvió                                                                                                          | Qué problemas trajo                                                                        |
| ----------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| **Monolito tradicional acoplado**               | Simplicidad inicial: una unidad, un despliegue, sin latencia de red                                                   | Código espagueti, despliegues "todo o nada", escalado unidimensional, alta carga cognitiva |
| **Modularidad interna**                         | Recuperó mantenibilidad con límites lógicos e interfaces públicas (Inversión de Dependencias), aún en un solo proceso | El despliegue y el escalado siguen siendo unitarios                                        |
| **Arquitecturas distribuidas / microservicios** | Escalado y despliegue independientes, heterogeneidad tecnológica                                                      | Latencia, fallos transitorios, consistencia eventual, observabilidad y seguridad complejas |

### 2. Capas clásicas vs. modelos modernos

- **N-Tier (Presentación / BLL / DAL):** agrupa por función técnica, no por significado de negocio. Una funcionalidad nueva toca las tres capas y la lógica tiende a filtrarse entre ellas.
- **Monolito modular:** divide **verticalmente** por módulos de negocio dentro de un único proceso. Cada módulo tiene su propia presentación, lógica y datos. Regla de oro: un módulo no accede a la BD de otro ni instancia sus clases internas. Beneficios: aislamiento, refactorización segura, rendimiento (llamadas en memoria).
- **Monolito orientado a dominio (DDD):** los módulos se corresponden con _Bounded Contexts_ (lenguaje ubicuo propio). Da gran parte de los beneficios lógicos de los microservicios sin su coste operativo, y facilita extraer un contexto más adelante (aunque no es gratis: migrar datos, llamadas de red, latencia, consistencia eventual).

### 3. Criterios para elegir estilo

| Criterio                  | A favor del monolito modular              | A favor de microservicios                              |
| ------------------------- | ----------------------------------------- | ------------------------------------------------------ |
| Tráfico                   | Moderado o uniforme                       | Muy alto y asimétrico                                  |
| Equipo                    | Un solo equipo, sin perfiles de operación | Varios equipos autónomos que chocan en el mismo código |
| Ritmo de despliegue       | Entregas periódicas coordinadas           | Despliegue independiente varias veces al día           |
| Independencia de dominios | Datos compartidos y transacciones ACID    | Datos propios y pocas interacciones                    |
| Tolerancia a fallos       | Una caída total es asumible               | Una parte debe seguir funcionando si otra cae          |
| Costes operativos         | Sin orquestador ni presupuesto            | Plataforma y personas de operación disponibles         |

Conceptos clave: **Ley de Conway** (el sistema copia la estructura de comunicación de la organización), **"Premium de los Microservicios"** (Kubernetes, service mesh, múltiples BD, CI/CD por servicio, observabilidad distribuida, DevOps), y **patrón Saga** si se separan dominios que requieren transacciones ACID.

### 4. Estructuración práctica en .NET 10

- Solución con un proyecto **Host** (API, punto de entrada) y un proyecto de biblioteca por **módulo** (`Catalog`, `Orders`, `Customers`).
- **Estructura interna de módulo:** `Contracts/` (público), `Core/`, `Data/`, `Endpoints/` y `XxxModuleExtensions.cs` (registro de DI).
- **`internal` como barrera:** entidades, `DbContext` y repositorios son `internal`; solo el contrato público (`IOrderService`, `ICustomerVerificationService`) es visible. Se puede sortear con reflexión o `InternalsVisibleTo`, por lo que se refuerza con pruebas de arquitectura (tema 4).
- **Cada módulo, su esquema de BD** (`HasDefaultSchema("orders")`), y solo guarda el `Id` de entidades de otros módulos (nada de propiedades de navegación).
- **Comunicación entre módulos** solo por interfaces públicas en memoria: `Orders` consume `ICustomerVerificationService`. Si `Customers` pasa a microservicio, solo cambia la implementación registrada (cliente HTTP/gRPC); `Orders` no cambia, pero habrá que tratar latencia y fallos (temas 6 y 13).
- **Host:** solo llama a `AddXxxModule(...)` y `MapXxxEndpoints()`; nunca registra clases internas.
- La plantilla `webapi` de .NET 10 usa `AddOpenApi()` / `MapOpenApi()` (sin Swashbuckle).

### 5. Antipatrones

- **Hype-Driven Development → Monolito Distribuido:** servicios separados físicamente pero acoplados lógicamente (despliegues coordinados, cadenas de llamadas HTTP síncronas, caída en cascada). Todos los inconvenientes de lo distribuido, ninguno de sus beneficios. _Solución: Monolith First._
- **Falso monolito modular:** proyectos `.csproj` separados pero con la misma BD y `JOIN`/`.Include()` entre tablas de distintos módulos. _Solución: integrar solo en código (en memoria) mediante interfaces públicas, o guardar una copia de los datos necesarios; cada módulo es dueño único de sus datos._

### 6. Laboratorio `B2BOrders`

**Arquitectura:** Clean Architecture / Hexagonal (Puertos y Adaptadores), con una matriz **capas × módulos**.

| Proyecto                   | Tipo     | Rol                                                   | Referencias                              |
| -------------------------- | -------- | ----------------------------------------------------- | ---------------------------------------- |
| `B2BOrders.Domain`         | classlib | Núcleo: entidades y reglas puras                      | Ninguna                                  |
| `B2BOrders.Application`    | classlib | Casos de uso, puertos (interfaces), DTOs              | → Domain                                 |
| `B2BOrders.Infrastructure` | classlib | EF Core, APIs externas, emails (adaptador secundario) | → Domain, → Application                  |
| `B2BOrders.Api`            | webapi   | Endpoints HTTP (adaptador primario)                   | → Application (**nunca** Infrastructure) |

**Pasos:** crear la solución y los 4 proyectos → añadirlos a la solución y borrar las plantillas (`Class1.cs`, `WeatherForecast*.cs`) → configurar las referencias con `dotnet add reference` → crear las carpetas `Customers`, `Orders` y `Billing` en cada capa → añadir `/health/architecture` en `Program.cs` y ejecutar `dotnet run --project B2BOrders.Api`.

**Regla de oro:** el Dominio no sabe que existen una base de datos ni una API web; todas las dependencias apuntan hacia adentro.

### 7. Notas de coherencia entre los materiales

- **Formato de solución:** el Manual indica que en .NET 10 `dotnet new sln` genera por defecto `.slnx` (el clásico se pide con `--format sln`). El laboratorio usa `.sln`, así que con el SDK de .NET 10 crea la solución con `--format sln`.
- **Dos formas de modularizar:** el Manual organiza el monolito modular con **un proyecto por módulo**; el laboratorio usa **un proyecto por capa con carpetas por módulo**. Ambas buscan límites claros, pero separar por proyecto permite que `internal` y las referencias de proyecto hagan cumplir esos límites.

---

## Evolución y Selección de Arquitecturas en .NET 10

### 1. Evolución Histórica de las Arquitecturas de Backend

Para comprender las decisiones arquitectónicas en el desarrollo moderno con .NET 10, es imprescindible analizar la evolución histórica de las arquitecturas de backend. Cada paradigma arquitectónico no surgió por capricho, sino como respuesta directa a los problemas de escalabilidad, mantenibilidad, despliegue y complejidad operativa que presentaba el modelo anterior.

#### 1.1. El Monolito Tradicional Acoplado: Orígenes y Limitaciones

En los albores del desarrollo web y empresarial, la arquitectura predominante era el monolito tradicional acoplado. En este modelo, toda la aplicación (interfaz de usuario, lógica de negocio, acceso a datos y tareas en segundo plano) se compilaba y desplegaba como una única unidad indivisible. En el ecosistema .NET temprano, esto se traducía frecuentemente en una única solución con un proyecto masivo o, en el mejor de los casos, una separación física muy tenue.

**¿Qué problemas resolvía en su momento?** El monolito tradicional era excelente para la simplicidad inicial. Permitía a equipos pequeños iterar rápidamente sin preocuparse por la latencia de red entre componentes, la serialización de datos o la consistencia eventual. El despliegue era trivial: copiar los binarios compilados a un servidor IIS (Internet Information Services) y apuntar a una única base de datos.

**Limitaciones y problemas emergentes:** A medida que las aplicaciones crecían en tamaño y los equipos de desarrollo se expandían, el monolito acoplado comenzó a mostrar deficiencias críticas:

- **Acoplamiento extremo (Spaghetti Code):** La falta de límites estrictos permitía que cualquier parte del código llamara a cualquier otra. Un cambio en el módulo de facturación podía romper inadvertidamente el módulo de registro de usuarios.
- **Cuellos de botella en el despliegue:** Al ser una única unidad, cualquier cambio, por minúsculo que fuera, requería redesplegar toda la aplicación. Esto generaba miedo al despliegue, reduciendo el ritmo de entregas a ciclos mensuales o trimestrales.
- **Escalabilidad unidimensional:** Si el módulo de generación de informes era el que concentraba casi todo el consumo de CPU, la única forma de escalar era replicar todo el monolito en múltiples servidores, desperdiciando recursos en módulos que no requerían escalamiento.
- **Complejidad cognitiva:** Para los nuevos desarrolladores, entender el sistema completo se volvía una tarea titánica, aumentando los costes de incorporación y el riesgo de introducir errores.

#### 1.2. La Transición hacia la Modularidad

Para mitigar los problemas del monolito acoplado sin incurrir en la complejidad de los sistemas distribuidos, la industria evolucionó hacia la modularidad interna. Esta etapa se centró en establecer límites lógicos firmes dentro del mismo proceso de ejecución.

**¿Por qué surgió esta aproximación?** La modularidad buscaba recuperar la mantenibilidad perdida. Al dividir el sistema en módulos cohesivos (por ejemplo, Catálogo, Pedidos, Facturación) que solo se comunicaban a través de interfaces públicas bien definidas, se redujo drásticamente el acoplamiento.

En esta fase, los arquitectos comenzaron a aplicar principios de diseño más rigurosos, como la Inversión de Dependencias (SOLID), para asegurar que los módulos no dependieran de implementaciones concretas de otros módulos. Aunque el despliegue seguía siendo unitario (un solo proceso), la organización del código permitía a diferentes equipos trabajar en distintos módulos con un riesgo mucho menor de conflictos. Esta evolución mejoró la mantenibilidad y la trazabilidad, preparando el terreno para arquitecturas más avanzadas.

#### 1.3. Arquitecturas Distribuidas y Basadas en Servicios

Cuando la modularidad interna no fue suficiente para satisfacer los requisitos extremos de escalabilidad y la necesidad de despliegues independientes por parte de múltiples equipos autónomos, surgieron las arquitecturas distribuidas, culminando en el modelo de microservicios.

**¿Qué problemas resuelven?**

- **Escalabilidad independiente:** Permiten escalar únicamente los servicios que sufren alta carga. Si el servicio de procesamiento de imágenes requiere más CPU, se escalan sus instancias sin afectar al servicio de gestión de perfiles.
- **Despliegue independiente:** Un equipo puede desplegar una nueva versión de su servicio varias veces al día sin coordinarse con otros equipos, acelerando drásticamente el Time to Market.
- **Heterogeneidad tecnológica:** Diferentes servicios pueden estar escritos en diferentes lenguajes o usar diferentes bases de datos según el problema a resolver (por ejemplo, .NET 10 con SQL Server para transacciones financieras y Python con MongoDB para análisis de datos).

**La nueva complejidad operativa:** Sin embargo, esta evolución introdujo una complejidad operativa sin precedentes. La comunicación pasó de ser llamadas a métodos en memoria a llamadas de red, varios órdenes de magnitud más lentas, introduciendo latencia, fallos transitorios, problemas de serialización y la necesidad de gestionar la consistencia eventual. La monitorización, el despliegue y la seguridad se volvieron mucho más complejos, requiriendo infraestructuras avanzadas (que se abordarán en los temas 11, 12 y 14 de este curso).

---

### 2. Arquitectura en Capas Clásica vs. Modelos Modernos

Para tomar decisiones arquitectónicas fundamentadas en .NET 10, es vital contrastar el enfoque clásico con los modelos modernos que dominan el panorama actual. La forma en que organizamos el código dicta cómo evolucionará el sistema a lo largo de los años.

#### 2.1. Principios de la Arquitectura en Capas Clásica (N-Tier)

La arquitectura en capas clásica, a menudo conocida como arquitectura de 3 capas (Presentación, Lógica de Negocio y Acceso a Datos), ha sido el estándar de facto durante décadas.

**Organización del código y responsabilidades:** En este modelo, el código se organiza horizontalmente basándose en preocupaciones técnicas:

- **Capa de Presentación (UI / API):** Responsable de recibir las peticiones HTTP, validar la entrada básica y devolver las respuestas.
- **Capa de Lógica de Negocio (BLL):** Contiene las reglas del negocio, cálculos y orquestación de procesos.
- **Capa de Acceso a Datos (DAL):** Gestiona la comunicación con la base de datos, ejecutando consultas y comandos.

**Limitaciones del modelo clásico:** El principal problema de la arquitectura en capas clásica es que agrupa el código por su función técnica, no por su significado de negocio. Si un desarrollador necesita añadir una nueva funcionalidad (por ejemplo, "Aplicar descuento a un pedido"), debe modificar código en las tres capas. Esto provoca que las capas se conviertan en monolitos horizontales. Además, fomenta un acoplamiento temporal y espacial donde la lógica de negocio a menudo se filtra hacia la capa de presentación o se entierra en procedimientos almacenados en la capa de datos.

#### 2.2. El Monolito Modular: Modularización en un Único Proceso

El monolito modular representa una evolución significativa sobre la arquitectura en capas clásica. En lugar de dividir el sistema horizontalmente por capas técnicas, lo divide verticalmente por áreas funcionales o módulos de negocio.

**¿Qué significa modularizar dentro de un único proceso?** Significa que la aplicación sigue compilándose y desplegándose como un único ejecutable (un solo proceso en memoria, un solo sitio web en IIS o un solo contenedor Docker), pero internamente está estrictamente dividida en módulos independientes.

Cada módulo (por ejemplo, `ModuloVentas`, `ModuloInventario`) contiene su propia presentación (controladores de API), su propia lógica de negocio y su propio acceso a datos. La regla de oro del monolito modular es que un módulo no puede acceder directamente a la base de datos de otro módulo ni instanciar directamente sus clases internas.

**Beneficios claros:**

- **Aislamiento:** Los cambios en el módulo de inventario no afectan al módulo de ventas.
- **Facilidad de refactorización:** Al estar todo en el mismo proceso, las refactorizaciones globales soportadas por el IDE (como renombrar una interfaz compartida) siguen siendo seguras y sencillas.
- **Rendimiento:** La comunicación entre módulos se realiza mediante llamadas a métodos en memoria, evitando la latencia de red y la sobrecarga de serialización inherente a los microservicios.

#### 2.3. Monolito Orientado a Dominio (Domain-Driven Monolith)

El Monolito Orientado a Dominio es una especialización del monolito modular que utiliza los principios del Diseño Guiado por el Dominio (DDD, que se explorará a fondo en el tema 3) para definir los límites de los módulos.

En este enfoque, los módulos no se definen arbitrariamente, sino que se corresponden con los _Bounded Contexts_ (Contextos Delimitados) del negocio. Cada contexto tiene su propio modelo de dominio, su propio lenguaje ubicuo y sus propias reglas.

**Mejora de la claridad y trazabilidad del negocio:** Este modelo mejora radicalmente la claridad porque la estructura del código refleja exactamente la estructura del negocio. Si un experto del dominio habla de "Proceso de Facturación", el desarrollador encontrará un módulo llamado exactamente así, con entidades que coinciden con el vocabulario del experto. La trazabilidad es directa: un requisito de negocio se traduce en cambios dentro de un único contexto delimitado.

**Por qué evita convertirse automáticamente en microservicios:** El monolito orientado a dominio ofrece buena parte de los beneficios lógicos de los microservicios (independencia lógica, claridad de límites, equipos alineados con el negocio) sin su complejidad operativa: un solo proceso, un solo despliegue y llamadas en memoria. Permite a las organizaciones madurar sus límites de dominio en un entorno seguro (un solo proceso) antes de asumir los enormes costes operativos de una arquitectura distribuida. Si en el futuro un contexto delimitado específico requiere escalabilidad independiente, extraerlo a un microservicio será mucho más sencillo, porque sus límites lógicos ya están definidos. No será gratuito: habrá que migrar sus datos, sustituir las llamadas en memoria por llamadas de red y gestionar la latencia, los fallos parciales y la consistencia eventual.

---

### 3. Criterios Objetivos para la Selección del Estilo Arquitectónico

La elección entre un monolito tradicional, un monolito modular o una arquitectura de microservicios no debe basarse en modas tecnológicas, sino en un análisis riguroso de las necesidades del negocio y las capacidades del equipo. A continuación, se detallan los criterios objetivos para tomar esta decisión en proyectos .NET 10.

#### 3.1. Volumen de Tráfico, Escalamiento y Tolerancia a Fallos

El comportamiento del sistema bajo carga es uno de los factores más determinantes.

**Volumen de Tráfico y Estrategias de Escalamiento:**

- **Monolito (Tradicional o Modular):** Adecuado para volúmenes de tráfico bajos a moderados, o cuando el tráfico es uniforme en toda la aplicación. La estrategia de escalamiento es horizontal (clonar la aplicación entera detrás de un balanceador de carga). Si la carga se reparte de forma uniforme entre las funcionalidades, clonar el proceso completo no desperdicia recursos y un monolito bien optimizado en .NET 10 es muy eficiente.
- **Microservicios:** Indispensable cuando el volumen de tráfico es masivo y, crucialmente, asimétrico. Si la inmensa mayoría del tráfico se dirige a la "Búsqueda de Productos" y una parte pequeña al "Carrito de Compras", los microservicios permiten escalar el servicio de búsqueda de forma independiente, optimizando drásticamente los costes de infraestructura.

**Tolerancia a Fallos:**

- En un monolito, un error fatal (como un desbordamiento de memoria o un bucle infinito que agota los hilos) en un módulo derribará todo el proceso, afectando a todos los usuarios.
- En una arquitectura distribuida, la tolerancia a fallos se incrementa mediante el aislamiento de procesos. Si el servicio de recomendaciones cae, la tienda online puede seguir procesando ventas, simplemente ocultando la sección de recomendaciones (degradación elegante).

#### 3.2. Tamaño del Equipo, Ritmo de Despliegue y Complejidad Organizativa

La Ley de Conway establece que las organizaciones diseñan sistemas que copian su estructura de comunicación. La arquitectura debe alinearse con el equipo humano.

**Tamaño del Equipo y Complejidad Organizativa:**

- **Un solo equipo pequeño:** Un monolito modular suele ser la mejor opción. Introducir microservicios en un equipo pequeño, y más aún sin perfiles de operación, genera una sobrecarga operativa (pipelines, observabilidad, despliegues coordinados) que frena el desarrollo de nuevas funcionalidades.
- **Varios equipos autónomos:** Cuando múltiples equipos (squads) trabajan en el mismo código, los conflictos de fusión (_merge conflicts_) y la coordinación se vuelven insostenibles. Aquí, los microservicios permiten que cada equipo sea dueño de su servicio, desde el código hasta el despliegue, reduciendo la complejidad organizativa inter-equipos.

**Ritmo de Despliegue:** Si el negocio requiere que diferentes partes de la aplicación se desplieguen de forma completamente independiente varias veces al día sin coordinar ventanas de mantenimiento, las arquitecturas distribuidas brillan. Si un despliegue semanal o quincenal es aceptable, el monolito modular ofrece un proceso de CI/CD mucho más sencillo y menos propenso a errores de integración.

#### 3.3. Independencia de Dominios y Costes Operativos

La evaluación financiera y lógica del sistema es el último pilar de la decisión.

**Independencia de Dominios:** Se debe evaluar qué tan acoplados están los conceptos de negocio. Si dos dominios comparten constantemente los mismos datos y requieren transacciones ACID (Atomicidad, Consistencia, Aislamiento, Durabilidad) que abarquen ambos, separarlos en microservicios requerirá implementar complejos patrones de transacciones distribuidas (como el patrón Saga, que se verá en temas posteriores). En estos casos, mantenerlos en el mismo proceso (un monolito modular sobre la misma base de datos) suele ser la opción más sensata para garantizar la consistencia sin disparar la complejidad.

**Costes Operativos:** Este es el criterio más subestimado. Las arquitecturas distribuidas conllevan lo que se conoce como el "Premium de los Microservicios".

- **Costes de un Monolito:** Requiere un servidor de aplicaciones, una base de datos, un pipeline de CI/CD simple y monitorización básica.
- **Costes de Microservicios:** Suele requerir orquestadores de contenedores (Kubernetes), a menudo mallas de servicios (_Service Mesh_), múltiples bases de datos, un pipeline de CI/CD por servicio, sistemas de observabilidad distribuida (trazabilidad, agregación de logs) y personas dedicadas a la operación de la plataforma (DevOps).

La decisión arquitectónica debe garantizar que el valor aportado por la escalabilidad y el despliegue independiente supere con creces estos costes operativos adicionales.

#### 3.4. Resumen: señales de cada estilo

La tabla resume cómo leer cada criterio. Ninguna señal decide por sí sola: la recomendación sale de ponderar todas con los datos reales del proyecto.

| Criterio                  | Señal a favor del monolito modular                        | Señal a favor de microservicios                        |
| ------------------------- | --------------------------------------------------------- | ------------------------------------------------------ |
| Tráfico                   | Moderado o repartido de forma uniforme                    | Muy alto y asimétrico entre funcionalidades            |
| Equipo                    | Un solo equipo, sin perfiles de operación                 | Varios equipos autónomos que chocan en el mismo código |
| Ritmo de despliegue       | Entregas periódicas coordinadas son aceptables            | Cada área necesita desplegar sola, varias veces al día |
| Independencia de dominios | Dominios que comparten datos y transacciones ACID         | Dominios con datos propios y pocas interacciones       |
| Tolerancia a fallos       | Una caída total es asumible durante la recuperación       | Una parte debe seguir funcionando si otra cae          |
| Costes operativos         | Sin orquestador ni presupuesto para ampliar la plataforma | Plataforma y personas de operación disponibles         |

El monolito tradicional acoplado no aparece en la tabla porque no es una opción recomendable para un sistema nuevo que vaya a crecer: sus problemas (sección 1.1) aparecen en cuanto aumentan el código y el equipo.

---

### 4. Ejemplos Prácticos de Estructuración en .NET 10

Para consolidar los conceptos teóricos, analizaremos cómo se implementan estas arquitecturas a nivel de código y estructura de proyectos en una solución moderna de .NET 10.

#### 4.1. Ejemplo Práctico: De Capas Clásicas a Monolito Modular en .NET 10

Supongamos que tenemos una aplicación de comercio electrónico. En una arquitectura de capas clásica, la solución en Visual Studio se vería así:

```text
ECommerce.sln
├── ECommerce.UI (Capa de Presentación - Controladores API)
├── ECommerce.BLL (Capa de Lógica de Negocio - Servicios)
└── ECommerce.DAL (Capa de Acceso a Datos - Repositorios y DbContext)
```

**El problema:** El desarrollador que trabaja en el catálogo de productos tiene que tocar los tres proyectos. Además, el servicio de `Pedidos` en la BLL puede acceder fácilmente al repositorio de `Usuarios` en la DAL, creando un acoplamiento indeseado.

**La Solución: Refactorización a Monolito Modular en .NET 10**

Transformaremos esta estructura agrupando por módulos de negocio. Cada módulo será un proyecto de biblioteca de clases (`.csproj`), y existirá un proyecto anfitrión (Host) que los unificará.

```text
ECommerce.Modular.slnx
├── ECommerce.Host (API Web .NET 10 - Punto de entrada)
├── ECommerce.Modules.Catalog (Módulo de Catálogo)
├── ECommerce.Modules.Orders (Módulo de Pedidos)
└── ECommerce.Modules.Customers (Módulo de Clientes)
```

##### Paso 0: Crear la solución con la CLI de .NET 10

En .NET 10, `dotnet new sln` genera por defecto una solución en el formato XML SLNX (`.slnx`), más sencillo de leer y de fusionar en Git que el `.sln` clásico. Si se necesita el formato antiguo, se usa `dotnet new sln --format sln`.

```bash
dotnet new sln -n ECommerce.Modular            # crea ECommerce.Modular.slnx
dotnet new webapi -n ECommerce.Host             # incluye Microsoft.AspNetCore.OpenApi
dotnet new classlib -n ECommerce.Modules.Customers
dotnet new classlib -n ECommerce.Modules.Orders

dotnet sln add ECommerce.Host/ECommerce.Host.csproj ECommerce.Modules.Customers/ECommerce.Modules.Customers.csproj ECommerce.Modules.Orders/ECommerce.Modules.Orders.csproj

# El Host referencia a todos los módulos
dotnet add ECommerce.Host/ECommerce.Host.csproj reference ECommerce.Modules.Customers/ECommerce.Modules.Customers.csproj ECommerce.Modules.Orders/ECommerce.Modules.Orders.csproj
# Pedidos consume el contrato público de Clientes
dotnet add ECommerce.Modules.Orders/ECommerce.Modules.Orders.csproj reference ECommerce.Modules.Customers/ECommerce.Modules.Customers.csproj
# Cada módulo con base de datos necesita el proveedor de EF Core
dotnet add ECommerce.Modules.Orders/ECommerce.Modules.Orders.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add ECommerce.Modules.Customers/ECommerce.Modules.Customers.csproj package Microsoft.EntityFrameworkCore.SqlServer
```

**Reglas de dependencias entre proyectos.** Las referencias de proyecto son la primera barrera del monolito modular:

| Proyecto | Puede referenciar                                            | No debe referenciar                                                                           |
| -------- | ------------------------------------------------------------ | --------------------------------------------------------------------------------------------- |
| Host     | Todos los módulos (para registrarlos y mapear sus endpoints) | Nada lo referencia a él                                                                       |
| Módulo   | Los módulos cuyo contrato público consume                    | Al Host; a módulos que no necesita                                                            |
| Módulo   | Sus propios paquetes (EF Core, etc.)                         | Módulos que a su vez lo referencian: las referencias circulares entre proyectos no se admiten |

El archivo de proyecto del módulo de Pedidos queda así. La `FrameworkReference` a `Microsoft.AspNetCore.App` es necesaria porque el módulo define sus propios endpoints HTTP y una biblioteca de clases no la incluye por defecto:

```xml
<!-- ECommerce.Modules.Orders/ECommerce.Modules.Orders.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <ItemGroup>
    <!-- La misma versión 10.0.x en todos los módulos (la que añadió dotnet add package); si Pedidos
         pide una versión menor que la que le llega a través de Clientes, la restauración falla con NU1605 -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ECommerce.Modules.Customers\ECommerce.Modules.Customers.csproj" />
  </ItemGroup>

</Project>
```

##### Paso 1: Estructura interna de un módulo

Dentro del proyecto `ECommerce.Modules.Orders`, encapsularemos todo lo necesario para los pedidos:

```text
ECommerce.Modules.Orders
├── Contracts/
│   └── IOrderService.cs (Contrato público: interfaz y tipos que viajan por ella)
├── Core/
│   ├── Order.cs (Entidades de dominio, internas)
│   └── OrderService.cs (Implementación interna del contrato)
├── Data/
│   ├── OrdersDbContext.cs (Contexto de base de datos exclusivo del módulo)
│   └── OrderRepository.cs (Implementación de acceso a datos)
├── Endpoints/
│   └── OrdersEndpoints.cs (Endpoints HTTP específicos de pedidos)
└── OrdersModuleExtensions.cs (Clase de registro de dependencias)
```

El contrato público es lo único que otros módulos y el Host pueden usar:

```csharp
// ECommerce.Modules.Orders/Contracts/IOrderService.cs
namespace ECommerce.Modules.Orders.Contracts;

public record OrderItem(string Sku, int Quantity);

public record OrderResult(bool Succeeded, Guid? OrderId, string? Error)
{
    public static OrderResult Success(Guid orderId) => new(true, orderId, null);
    public static OrderResult Failed(string error) => new(false, null, error);
}

public interface IOrderService
{
    Task<OrderResult> PlaceOrderAsync(Guid customerId, IReadOnlyList<OrderItem> items);
}
```

##### Paso 2: Uso del modificador `internal` para garantizar el aislamiento

Una de las características más potentes de C# para el monolito modular es el modificador de acceso `internal`. Asegura que las clases solo sean accesibles dentro de su propio ensamblado (proyecto).

En `ECommerce.Modules.Orders`, definimos las entidades, el repositorio y el DbContext como `internal`:

```csharp
// ECommerce.Modules.Orders/Core/Order.cs
namespace ECommerce.Modules.Orders.Core;

internal class Order
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }   // solo el identificador: nada de navegación a Cliente
    public DateTime CreatedAtUtc { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

internal class OrderLine
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
```

```csharp
// ECommerce.Modules.Orders/Data/OrdersDbContext.cs
using ECommerce.Modules.Orders.Core;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Orders.Data;

internal class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Las tablas del módulo viven en su propio esquema de la base de datos
        modelBuilder.HasDefaultSchema("orders");
    }
}
```

```csharp
// ECommerce.Modules.Orders/Data/OrderRepository.cs
using ECommerce.Modules.Orders.Core;

namespace ECommerce.Modules.Orders.Data;

internal interface IOrderRepository
{
    Task AddAsync(Order order);
}

internal class OrderRepository(OrdersDbContext context) : IOrderRepository
{
    public async Task AddAsync(Order order)
    {
        context.Orders.Add(order);
        await context.SaveChangesAsync();
    }
}
```

Al hacer esto, el compilador impide que el módulo de Catálogo (o cualquier otro proyecto) use `OrdersDbContext`, `Order` u `OrderRepository`: el intento produce un error de compilación por nivel de acceso. La barrera no es absoluta, porque se puede sortear con reflexión o si el módulo declara el atributo `InternalsVisibleTo` para otro ensamblado, pero convierte el acoplamiento accidental en un error visible. Para reforzarla se usan pruebas de arquitectura, que se tratan en el tema 4.

##### Paso 3: Exposición de contratos públicos e Inyección de Dependencias

El módulo solo expone lo estrictamente necesario mediante interfaces públicas. Para registrar sus servicios en el contenedor de inyección de dependencias de .NET 10, creamos un método de extensión público. Puede registrar tipos `internal` porque el método está en el mismo ensamblado; quien lo llama no necesita verlos:

```csharp
// ECommerce.Modules.Orders/OrdersModuleExtensions.cs
using ECommerce.Modules.Orders.Contracts;
using ECommerce.Modules.Orders.Core;
using ECommerce.Modules.Orders.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Modules.Orders;

public static class OrdersModuleExtensions
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, string connectionString)
    {
        // Registro interno del DbContext (UseSqlServer: paquete Microsoft.EntityFrameworkCore.SqlServer)
        services.AddDbContext<OrdersDbContext>(options =>
            options.UseSqlServer(connectionString));

        // Registro de servicios: la interfaz pública apunta a la implementación interna
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        return services;
    }
}
```

`AddDbContext` registra el contexto con ciclo de vida _scoped_ (uno por petición HTTP), por eso los servicios que lo usan se registran también como `AddScoped`.

Los endpoints del módulo se agrupan en otro método de extensión, para que el Host solo tenga que llamarlo. El endpoint trabaja únicamente con el contrato público:

```csharp
// ECommerce.Modules.Orders/Endpoints/OrdersEndpoints.cs
using ECommerce.Modules.Orders.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ECommerce.Modules.Orders.Endpoints;

public record PlaceOrderRequest(Guid CustomerId, List<OrderItem> Items);

public static class OrdersEndpoints
{
    public static IEndpointRouteBuilder MapOrdersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders");

        group.MapPost("/", async (PlaceOrderRequest request, IOrderService orders) =>
        {
            var result = await orders.PlaceOrderAsync(request.CustomerId, request.Items);
            return result.Succeeded
                ? Results.Created($"/orders/{result.OrderId}", result)
                : Results.BadRequest(result.Error);
        });

        return app;
    }
}
```

##### Paso 4: El proyecto Host (Punto de entrada)

El proyecto `ECommerce.Host` es una API mínima de .NET 10 que referencia a todos los módulos y los inicializa. Cada módulo ofrece su propio método `AddXxxModule` (y, si tiene endpoints, `MapXxxEndpoints`), análogo a los de Pedidos. El ejemplo registra los dos módulos que hemos escrito; el de Catálogo se crearía y registraría igual (`AddCatalogModule`). La plantilla `webapi` de .NET 10 usa `AddOpenApi()` y `MapOpenApi()` (paquete `Microsoft.AspNetCore.OpenApi`) para publicar el documento OpenAPI; ya no incluye Swashbuckle, y Swagger UI o Scalar se añaden como paquetes aparte si se quiere una interfaz visual.

```csharp
// ECommerce.Host/Program.cs
using ECommerce.Modules.Customers;
using ECommerce.Modules.Orders;
using ECommerce.Modules.Orders.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta la cadena de conexión 'DefaultConnection'.");

builder.Services.AddOpenApi();

// Registrar los módulos independientes
builder.Services.AddCustomersModule(connectionString);
builder.Services.AddOrdersModule(connectionString);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapOrdersEndpoints();

app.Run();
```

El Host no registra ni una sola clase interna de los módulos: si un módulo cambia su implementación, `Program.cs` no se toca.

**Resultado:** Hemos logrado una modularización estricta. Si en el futuro el módulo de Pedidos necesita escalar independientemente, extraer el proyecto `ECommerce.Modules.Orders` a su propio microservicio será un proceso mucho más directo, ya que no comparte tablas ni lógica interna con el resto del sistema (aunque seguirá habiendo que resolver la comunicación por red y la migración de sus datos).

#### 4.2. Ejemplo Práctico: Estructuración de un Monolito Orientado a Dominio

El Monolito Orientado a Dominio lleva el ejemplo anterior un paso más allá, aplicando rigor conceptual a la comunicación entre módulos.

**El escenario:** El módulo de `Pedidos` necesita verificar si un cliente es válido antes de procesar una compra. En un monolito acoplado, `Pedidos` consultaría directamente la tabla de `Clientes`. En un monolito orientado a dominio, esto está prohibido.

**La Solución: Comunicación mediante Contratos e Interfaces**

El módulo de `Clientes` (Customer Context) debe definir un contrato público (una interfaz o un evento) que otros módulos puedan consumir.

```csharp
// ECommerce.Modules.Customers/Contracts/ICustomerVerificationService.cs
namespace ECommerce.Modules.Customers.Contracts;

// Esta interfaz es PÚBLICA. Es el contrato que el módulo ofrece al exterior.
public interface ICustomerVerificationService
{
    Task<bool> IsCustomerEligibleForPurchaseAsync(Guid customerId);
}
```

La implementación de esta interfaz reside dentro del módulo de clientes y usa clases internas (`internal`) para realizar la validación.

```csharp
// ECommerce.Modules.Customers/Data/CustomersDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Modules.Customers.Data;

internal class Customer
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
    public bool HasOutstandingDebt { get; set; }
}

internal class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.HasDefaultSchema("customers");
}
```

```csharp
// ECommerce.Modules.Customers/Services/CustomerVerificationService.cs
using ECommerce.Modules.Customers.Contracts;
using ECommerce.Modules.Customers.Data;

namespace ECommerce.Modules.Customers.Services;

internal class CustomerVerificationService : ICustomerVerificationService
{
    private readonly CustomersDbContext _context; // Uso de base de datos interna

    public CustomerVerificationService(CustomersDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsCustomerEligibleForPurchaseAsync(Guid customerId)
    {
        var customer = await _context.Customers.FindAsync(customerId);
        return customer != null && customer.IsActive && !customer.HasOutstandingDebt;
    }
}
```

```csharp
// ECommerce.Modules.Customers/CustomersModuleExtensions.cs
using ECommerce.Modules.Customers.Contracts;
using ECommerce.Modules.Customers.Data;
using ECommerce.Modules.Customers.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Modules.Customers;

public static class CustomersModuleExtensions
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<CustomersDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<ICustomerVerificationService, CustomerVerificationService>();
        return services;
    }
}
```

Ahora, el módulo de `Pedidos` (Orders Context) necesita utilizar este servicio. Para mantener la independencia, `Pedidos` referenciará el contrato público de `Clientes`, pero nunca su implementación interna ni su base de datos (que, al ser `internal`, tampoco podría usar).

```csharp
// ECommerce.Modules.Orders/Core/OrderService.cs
using ECommerce.Modules.Customers.Contracts;
using ECommerce.Modules.Orders.Contracts;
using ECommerce.Modules.Orders.Data;

namespace ECommerce.Modules.Orders.Core;

internal class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;
    private readonly ICustomerVerificationService _customerVerification;

    // Inyectamos el contrato del otro módulo
    public OrderService(
        IOrderRepository orderRepository,
        ICustomerVerificationService customerVerification)
    {
        _orderRepository = orderRepository;
        _customerVerification = customerVerification;
    }

    public async Task<OrderResult> PlaceOrderAsync(Guid customerId, IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
        {
            return OrderResult.Failed("El pedido no tiene líneas.");
        }

        // Comunicación síncrona entre módulos a través de interfaces públicas (en memoria)
        bool isEligible = await _customerVerification.IsCustomerEligibleForPurchaseAsync(customerId);

        if (!isEligible)
        {
            return OrderResult.Failed("El cliente no es elegible para realizar compras.");
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            CreatedAtUtc = DateTime.UtcNow,
            Lines = items.Select(i => new OrderLine { Sku = i.Sku, Quantity = i.Quantity }).ToList()
        };

        await _orderRepository.AddAsync(order);
        return OrderResult.Success(order.Id);
    }
}
```

**Beneficios de este enfoque:**

- **Trazabilidad:** Queda explícitamente documentado en el código que `Pedidos` depende de una validación de `Clientes`.
- **Preparación para la distribución:** Si mañana separamos `Clientes` en un microservicio, solo tendremos que cambiar qué implementación de `ICustomerVerificationService` se registra en el contenedor. En lugar de la clase local `CustomerVerificationService`, se registrará un cliente (HTTP o gRPC) que implemente la misma interfaz y llame al nuevo microservicio. El código del módulo de `Pedidos` no cambia, aunque la llamada pasa a tener latencia y puede fallar, algo que habrá que tratar con las técnicas de los temas 6 y 13.

---

### 5. Antipatrones y Errores Comunes en la Decisión Arquitectónica

Elegir la arquitectura incorrecta puede condenar un proyecto desde su inicio. Es fundamental reconocer y evitar los antipatrones más destructivos en el diseño de backend.

#### 5.1. Hype-Driven Development y la Adopción Prematura de Microservicios

El error más común en la industria actual es el _Hype-Driven Development_ (Desarrollo impulsado por modas). Consiste en adoptar microservicios simplemente porque grandes plataformas de streaming o de comercio electrónico los utilizan, ignorando que el contexto, el presupuesto y los problemas de esas empresas no son los mismos que los de un proyecto estándar.

**El Antipatrón: El Monolito Distribuido**

Cuando un equipo sin la madurez operativa necesaria intenta construir microservicios, a menudo termina creando un "Monolito Distribuido". Esto ocurre cuando los servicios se separan físicamente (diferentes contenedores, diferentes repositorios) pero permanecen lógicamente acoplados.

**Síntomas del Monolito Distribuido:**

- Para desplegar una nueva funcionalidad, el equipo tiene que coordinar el despliegue simultáneo de 5 microservicios diferentes en un orden específico.
- Un servicio no puede responder a una petición sin hacer llamadas HTTP síncronas a otros 3 servicios, creando una cadena de dependencias frágil.
- Si un servicio cae, todo el sistema deja de funcionar.

**Consecuencias:** El equipo sufre todos los inconvenientes de los sistemas distribuidos (latencia de red, fallos de comunicación, complejidad de despliegue, dificultad para depurar) sin obtener ninguno de sus beneficios (despliegue independiente, escalabilidad aislada, resiliencia). Los costes operativos se disparan mientras la velocidad de desarrollo se desploma.

**La Solución Conceptual:** Comenzar siempre con un Monolito Modular (_Monolith First_). Solo se debe extraer un microservicio cuando exista un dolor real y medible que justifique el coste: una necesidad imperiosa de escalamiento asimétrico o la necesidad de que un equipo grande trabaje de forma totalmente aislada.

#### 5.2. El Falso Monolito Modular y el Acoplamiento Oculto

Otro error grave ocurre cuando los equipos intentan implementar un monolito modular, pero fallan en mantener la disciplina de los límites, creando un falso monolito modular.

**El Antipatrón: Compartir la Base de Datos a Nivel de Tablas**

El equipo separa el código en proyectos `.csproj` distintos (Módulo A y Módulo B), pero ambos módulos se conectan a la misma base de datos y, lo que es peor, realizan uniones (JOINs) SQL directas entre las tablas de diferentes módulos.

**Ejemplo del error:** El módulo de `Facturación` ejecuta una consulta Entity Framework Core que hace un `.Include()` hacia la tabla de `Usuarios` que pertenece al módulo de `Identidad`.

```csharp
// ANTIPATRÓN: Acoplamiento oculto en base de datos
var factura = await _dbContext.Facturas
    .Include(f => f.Usuario) // Cruzando límites de módulos en base de datos
    .FirstOrDefaultAsync(f => f.Id == id);
```

**Por qué es destructivo:** Aunque el código C# parezca modular, la base de datos está fuertemente acoplada. Si el equipo de `Identidad` decide cambiar la estructura de la tabla `Usuarios` (por ejemplo, dividiendo el nombre en `Nombre` y `Apellidos`), romperá inmediatamente las consultas del módulo de `Facturación`. Este acoplamiento oculto destruye la independencia de los módulos.

**La Solución Conceptual:** La regla inquebrantable del monolito modular es que la integración entre módulos se hace en el código (en memoria), nunca en la base de datos. Si el módulo de `Facturación` necesita datos del usuario, debe almacenar una copia de los datos que necesita (por ejemplo, `NombreCliente` y `DireccionFacturacion`) en su propia tabla de facturas en el momento de la creación, o debe solicitar esos datos al módulo de `Identidad` a través de una interfaz pública en memoria, como se demostró en el ejemplo práctico de la sección 4.2. Cada módulo debe ser el único dueño y guardián de sus propios datos.

---

## Laboratorio B2BOrders

Solución modular con Clean Architecture / Arquitectura Hexagonal en .NET 10.

### 1. Arquitectura y reglas de dependencia

Las dependencias apuntan hacia adentro (Domain no depende de nada).

| Capa / Proyecto     | Tipo     | Responsabilidad                                                                    | Referencia a        |
| ------------------- | -------- | ---------------------------------------------------------------------------------- | ------------------- |
| **Domain** (núcleo) | classlib | Entidades, Value Objects, reglas de negocio puras (C# puro)                        | Ninguno             |
| **Application**     | classlib | Casos de uso, interfaces (puertos), DTOs, orquestación                             | Domain              |
| **Infrastructure**  | classlib | EF Core, APIs externas, emails, archivos. Implementa las interfaces de Application | Domain, Application |
| **Api**             | webapi   | Punto de entrada HTTP, endpoints, middlewares                                      | Application         |

> **Regla de oro:** el Dominio no debe saber que existe una base de datos ni una API web.
> **Prohibido:** que `Api` referencie directamente a `Infrastructure`. El controlador no debe saber si se usa SQL Server o un archivo de texto.

**Lectura hexagonal (Puertos y Adaptadores):**

| Proyecto                   | Rol                                                                                      | Tipo de adaptador |
| -------------------------- | ---------------------------------------------------------------------------------------- | ----------------- |
| `B2BOrders.Domain`         | Núcleo: reglas de negocio, no conoce a nadie                                             | N/A               |
| `B2BOrders.Application`    | Anillo interno: casos de uso, rodea al dominio                                           | N/A               |
| `B2BOrders.Api`            | _Driving_ (conductor): recibe HTTP y llama a la aplicación                               | Primario          |
| `B2BOrders.Infrastructure` | _Driven_ (conducido): ejecuta lo que ordena la aplicación (guardar datos, enviar emails) | Secundario        |

```text
            ┌───────────── PUERTOS Y ADAPTADORES ─────────────┐
            │         ┌──────── APPLICATION ────────┐         │
  [ API ] ──┼──────►  │  ┌────── DOMAIN ──────┐     │  ──────►┼── [ INFRASTRUCTURE ]
  Driving   │         │  │ Reglas de negocio  │     │         │   Driven
  (Primario)│         │  │ No conoce a nadie  │     │         │   (Secundario)
            │         │  └────────────────────┘     │         │
            │         │  Casos de uso               │         │
            │         └─────────────────────────────┘         │
            └─────────────────────────────────────────────────┘
```

### 2. Crear la solución y los proyectos

```bash
mkdir B2BOrders
cd B2BOrders

# Solución vacía (.sln clásico; en .NET 10 el valor por defecto es .slnx)
dotnet new sln -n B2BOrders --format sln

# Proyectos
dotnet new classlib -n B2BOrders.Domain
dotnet new classlib -n B2BOrders.Application
dotnet new classlib -n B2BOrders.Infrastructure
dotnet new webapi   -n B2BOrders.Api

# Añadirlos a la solución
dotnet sln add B2BOrders.Domain/B2BOrders.Domain.csproj
dotnet sln add B2BOrders.Application/B2BOrders.Application.csproj
dotnet sln add B2BOrders.Infrastructure/B2BOrders.Infrastructure.csproj
dotnet sln add B2BOrders.Api/B2BOrders.Api.csproj
```

**Limpieza:** borrar `Class1.cs` (en Domain, Application e Infrastructure) y `WeatherForecast.cs` y `WeatherForecastController.cs` (en Api, si la plantilla los incluye).

**`.sln` vs `.slnx`:** `.slnx` es el formato nuevo de .NET 10 (XML limpio, fácil de leer y con menos conflictos en Git). `.sln` es el formato clásico, más compatible con herramientas de terceros.

### 3. Referencias entre proyectos

```bash
# Application ve a Domain
dotnet add B2BOrders.Application reference B2BOrders.Domain

# Infrastructure ve a Domain y Application
dotnet add B2BOrders.Infrastructure reference B2BOrders.Domain
dotnet add B2BOrders.Infrastructure reference B2BOrders.Application

# Api ve a Application (no a Infrastructure)
dotnet add B2BOrders.Api reference B2BOrders.Application
```

### 4. Estructura modular (Customers, Orders, Billing)

Se crean los mismos tres módulos en cada capa de librería con PowerShell (`pwsh` en Linux/Mac):

```powershell
foreach ($capa in "Domain", "Application", "Infrastructure") {
    "Customers", "Orders", "Billing" | % { mkdir "B2BOrders.$capa/$_" }
}
```

(En el original son tres líneas, una por capa. `|` pasa la lista al siguiente comando, `%` es alias de `ForEach-Object` y `$_` es el elemento actual.)

Resultado, una matriz **capas × módulos**:

```text
B2BOrders
├── B2BOrders.Api
├── B2BOrders.Application
│   ├── Billing
│   ├── Customers
│   └── Orders
├── B2BOrders.Domain
│   ├── Billing
│   ├── Customers
│   └── Orders
└── B2BOrders.Infrastructure
    ├── Billing
    ├── Customers
    └── Orders
```

Así, si en el futuro hay que extraer `Billing` a un microservicio, todo su código está localizado y no disperso.

### 5. Endpoint de diagnóstico (Health Check)

En `B2BOrders.Api/Program.cs`, **antes** de `app.Run()`:

```csharp
// Endpoint de diagnóstico arquitectónico
app.MapGet("/health/architecture", () =>
{
    return new
    {
        Status = "Healthy",
        Architecture = "Modular Monolith / Hexagonal",
        Modules = new[] { "Customers", "Orders", "Billing" },
        Version = ".NET 10"
    };
});
```

ASP.NET Core serializa el objeto anónimo a JSON automáticamente. Al haber varios proyectos hay que indicar cuál arrancar:

```bash
dotnet run --project B2BOrders.Api
# Now listening on: http://localhost:5xxx
```

Navega a la URL indicada + `/health/architecture`. Respuesta esperada:

```json
{
  "status": "Healthy",
  "architecture": "Modular Monolith / Hexagonal",
  "modules": ["Customers", "Orders", "Billing"],
  "version": ".NET 10"
}
```

Si hay errores de referencias faltantes, añade la que falte con `dotnet add [Origen] reference [Destino]` siguiendo la tabla de la sección 1.

### 6. Verificación

- [ ] Existe la carpeta raíz `B2BOrders` y el archivo de solución.
- [ ] Existen los 4 proyectos (`.Domain`, `.Application`, `.Infrastructure`, `.Api`) y se eliminaron los archivos de plantilla.
- [ ] `B2BOrders.Api.csproj` **no** referencia a Infrastructure.
- [ ] Existen `Customers`, `Orders` y `Billing` dentro de Domain, Application e Infrastructure.
- [ ] `dotnet build` en la raíz muestra **Build succeeded**.
- [ ] `/health/architecture` devuelve el JSON esperado.
