# Tema 2 — Rendimiento y Escalabilidad en .NET 10

## Contenido

- [Resumen del Tema 2](#resumen-del-tema-2)
- [Rendimiento en .NET 10, Métricas y Cuellos de Botella](#rendimiento-en-net-10-métricas-y-cuellos-de-botella)
- [Laboratorio: medir y optimizar `/orders`](#laboratorio-medir-y-optimizar-orders)

---

## Resumen del Tema 2

### Idea central

El rendimiento no se optimiza por intuición. Primero se definen **objetivos medibles** (SLI/SLO/SLA), después se **mide** (percentiles, throughput, memoria), se **localiza el cuello de botella** con un método (USE) y solo entonces se aplica el patrón que ataca esa causa. La regla que guía todo: **medir, cambiar una sola cosa y volver a medir**.

### 1. Objetivos y métricas

| Concepto | Qué es                                                                                   |
| -------- | ---------------------------------------------------------------------------------------- |
| **SLI**  | La métrica real que se mide (p. ej. latencia de respuestas 200 OK, en ms)                |
| **SLO**  | El objetivo interno para un SLI (p. ej. "p99 < 200 ms durante 30 días")                  |
| **SLA**  | El contrato con el cliente, con penalizaciones. El SLO suele ser más estricto que el SLA |

- **La media engaña.** Se miden **percentiles**: p50 (usuario típico), p95 y p99 (la "cola larga"). Una página con 50 peticiones tiene ≈ 39,5 % de probabilidad de topar con al menos una petición más lenta que el p99.
- **Throughput** (RPS/TPS) y **Ley de Little:** `Concurrencia = Throughput × Latencia media`. El objetivo es hallar el punto de inflexión donde el throughput se estanca y la latencia se dispara.
- **Memoria:** Working Set (crecimiento sostenido = fuga), tasa de asignación (MB/s) y % de tiempo en pausas de GC. Se obtienen del medidor `System.Runtime` (desde .NET 9).

### 2. El runtime de .NET 10

- **GC generacional** (gen0, gen1, gen2 + LOH para objetos ≥ 85.000 bytes). Asignar es barato; **el coste se paga después** en el GC. El objetivo es que los objetos **mueran jóvenes (gen0)**. Gen2 bloqueante = _Stop-The-World_ = picos en el p99.
- **Modos del GC:** Workstation, Server (por defecto en ASP.NET Core), Background y DATAS (ajuste dinámico de heaps, por defecto con Server GC desde .NET 9). Cambiar de modo **no reduce las asignaciones**; si el problema es la tasa de asignación hay que corregir el código.
- **JIT:** compilación por niveles (Tier 0 → Tier 1) con Dynamic PGO y OSR. Por eso los benchmarks necesitan _warmup_.
- **SIMD:** se aprovecha sobre todo a través de las APIs de biblioteca (`IndexOf`, `SearchValues<T>`, `Span<T>`). AVX10.2 está desactivado por defecto: es preparación para hardware futuro.
- **Pila y Span:** `struct` locales, `stackalloc` (bloques pequeños y acotados) y análisis de escape del JIT. `AsSpan`/slicing evita copias (no es asignación en pila).

### 3. Cuellos de botella (método USE: Utilización, Saturación, Errores)

| Categoría             | Síntomas                                                                               | Causas típicas                                                                                    | Herramienta                        |
| --------------------- | -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- | ---------------------------------- |
| **CPU**               | Throughput estancado, latencia sube de forma uniforme                                  | Serialización masiva, regex con backtracking catastrófico, excepciones como control de flujo      | `dotnet-trace`                     |
| **Fuga de memoria**   | Working Set crece durante horas/días hasta OOM                                         | Eventos estáticos sin desuscribir, cachés sin expiración, diccionarios estáticos                  | `dotnet-gcdump`                    |
| **Presión del GC**    | RAM oscila sin crecer, alta tasa de asignación y % de pausas, p99 errático             | Concatenar strings en bucles, búferes grandes por petición, listas completas filtradas en memoria | `dotnet-counters` + `dotnet-trace` |
| **BD y red**          | CPU/RAM bajas pero timeouts                                                            | N+1, agotamiento del pool de conexiones, agotamiento de puertos (`HttpClient` nuevo por petición) | métricas de BD, `dotnet-counters`  |
| **Thread Starvation** | CPU baja, errores 503, latencia de decenas de segundos, cola del Thread Pool creciente | **Sync-over-Async** (`.Result`, `.Wait()`)                                                        | `dotnet-counters`                  |

### 4. Medición y patrones

- **BenchmarkDotNet** (siempre en Release, `[MemoryDiagnoser]`, `Baseline = true`) para fragmentos de código; la **prueba de carga** valida el SLO del servicio completo.
- **Herramientas:** `dotnet-counters` (primer vistazo), `dotnet-trace` (CPU y asignaciones), `dotnet-gcdump` (fugas).
- **Patrones:** `SearchValues<T>`, `ArrayPool<T>`, `StringBuilder`/`string.Join`, métodos `TryXxx` en lugar de excepciones, `async`/`await` de extremo a extremo.

### 5. Laboratorio `/orders` (resultados reales)

Prueba con `hey -n 5000 -c 50` antes y después de aplicar paginación, `AsNoTracking()` y proyección con `.Select`:

| Métrica        | Antes   | Después | Mejora        |
| -------------- | ------- | ------- | ------------- |
| Requests/sec   | ~626    | ~1.382  | ×2,2          |
| Latencia media | 0,068 s | 0,033 s | 2× más rápida |
| p99            | 1,30 s  | 0,06 s  | ×20           |
| Pausa de GC    | ~1,13 s | ~0,37 s | −67 %         |

**Causa raíz detectada:** _Allocation Storm_. El endpoint traía 1000 filas, creaba 1000 objetos y los serializaba a JSON, 5000 veces (≈ 6,67 GB asignados), lo que provocaba pausas del GC que disparaban el p99.

**Reglas de oro de EF Core:** `AsNoTracking()` en lecturas, nunca `ToList()` sin paginación previa y proyectar siempre con `.Select`.

### 6. Notas sobre los materiales

- **Dos informes de `hey` distintos:** el primero (475 req/s, p99 = 1,1 s) es un **ejemplo ilustrativo** para explicar la anatomía del reporte. Los resultados reales del experimento son los del apartado 5 (~626 req/s y p99 = 1,30 s).
- **Fórmula del diagnóstico:** `p99 ≈ procesamiento + pausa de GC` es una **aproximación didáctica**. La pausa de 1,13 s es el acumulado de toda la prueba (como indica el Manual, `dotnet.gc.pause.time` es una métrica acumulada), no la pausa de una sola petición. Lo importante es la correlación entre p99 alto y pausas del GC.
- **Contadores `gen0`/`gen2`:** el laboratorio los interpreta como objetos. Comprueba la unidad real que muestra tu herramienta (en el Manual, `dotnet.gc.collections` cuenta _recolecciones_ por generación).
- **Corrección en el ejemplo del percentil:** el Manual dice que "nueve de cada diez peticiones tardan 18 ms o menos", pero con los datos del ejemplo son **ocho** de diez (la novena tarda 240 ms). En este documento está corregido.

---

## Rendimiento en .NET 10, Métricas y Cuellos de Botella

### 1. Establecimiento de Objetivos de Rendimiento y Métricas

El análisis de rendimiento en aplicaciones modernas desarrolladas con .NET 10 no puede basarse en intuiciones o percepciones subjetivas. Para optimizar un sistema, primero es estrictamente necesario definir qué significa "rendimiento aceptable" en términos matemáticos y objetivos. Esta sección detalla cómo establecer metas realistas y cómo interpretar las métricas fundamentales que guiarán cualquier esfuerzo de optimización.

#### 1.1. Traducción de Necesidades del Negocio a Indicadores Medibles

El principal error en la ingeniería de rendimiento es comenzar a optimizar sin un objetivo claro. Cuando el negocio comunica que "la aplicación va lenta" o exige que "el sistema debe ser rápido", el equipo técnico debe traducir estas afirmaciones ambiguas a un lenguaje cuantitativo.

Para lograr esta traducción, se utilizan tres conceptos fundamentales en la ingeniería de fiabilidad (SRE):

- **SLI (Service Level Indicator):** es la métrica real y objetiva que se está midiendo. Por ejemplo, la latencia de las respuestas HTTP con código 200 OK, medida en milisegundos.
- **SLO (Service Level Objective):** es el objetivo interno que el equipo de ingeniería se compromete a cumplir para un SLI específico. Por ejemplo: "El 99 % de las peticiones de inicio de sesión deben completarse en menos de 200 milisegundos durante un periodo de 30 días".
- **SLA (Service Level Agreement):** es el contrato comercial o legal con el cliente, que incluye penalizaciones si no se cumple el SLO. Generalmente, el SLO interno es más estricto que el SLA para tener un margen de maniobra.

Traducir las necesidades del negocio implica sentarse con los responsables del producto y determinar el coste del bajo rendimiento. Si un proceso de pago tarda 5 segundos, ¿cuántos usuarios abandonan la compra? A partir de esa respuesta, se establece un SLO que proteja los ingresos y la experiencia del usuario, proporcionando al equipo técnico un límite claro: si el sistema cumple el SLO, no es necesario invertir más tiempo y costes en optimización; si no lo cumple, la optimización es prioritaria.

#### 1.2. Métricas de Latencia: Percentiles p95, p99 y Distribución

La latencia es el tiempo que tarda un sistema en procesar una única petición, desde que la recibe hasta que devuelve la respuesta. Históricamente, los equipos técnicos han utilizado el "tiempo medio de respuesta" (la media aritmética) para evaluar la latencia. Sin embargo, en sistemas distribuidos, la media es una métrica engañosa y peligrosa.

Si un servidor procesa 9 peticiones en 10 milisegundos y 1 petición en 1.000 milisegundos (1 segundo), la latencia media es de 109 milisegundos. Un ingeniero que observe una media de 109 ms podría concluir que el sistema funciona perfectamente. No obstante, el 10 % de los usuarios está experimentando una lentitud inaceptable.

Para evitar este sesgo, el rendimiento en .NET 10 se mide utilizando **percentiles**, que representan la distribución real de los tiempos de respuesta:

- **Percentil 50 (p50 o mediana):** el 50 % de las peticiones son más rápidas que este valor. Representa la experiencia del usuario típico.
- **Percentil 90 (p90):** el 90 % de las peticiones son más rápidas que este valor. El 10 % restante experimenta tiempos peores.
- **Percentil 95 (p95):** el 95 % de las peticiones se resuelven por debajo de este umbral. Es uno de los percentiles más usados para fijar objetivos de rendimiento general.
- **Percentil 99 (p99):** el 99 % de las peticiones son más rápidas que este valor. Representa la "cola" de la distribución (_tail latency_). Optimizar el p99 es crucial porque una página que realiza muchas peticiones para cargarse acumula la probabilidad de toparse con la cola. Si una página lanza 50 peticiones independientes y cada una tiene un 1 % de probabilidad de superar el p99, la probabilidad de que al menos una lo supere es 1 − 0,99^50 ≈ 0,395: casi cuatro de cada diez cargas de página sufren al menos una petición lenta.

Al establecer objetivos, siempre se debe exigir el cumplimiento en los percentiles altos (p95 y p99) para garantizar que la inmensa mayoría de los usuarios disfruten de un sistema rápido, sin que los valores atípicos (_outliers_) causados por pausas del recolector de basura o latencias de red momentáneas queden escondidos en una media.

**Cómo se calcula un percentil sobre una muestra.** Existen varios métodos (con o sin interpolación) y las herramientas no siempre usan el mismo, por eso un contrato de rendimiento debe fijar cuál se aplica. El más sencillo es el método del **rango más cercano** (_nearest rank_): se ordenan los _n_ valores de menor a mayor y el percentil _p_ es el valor que ocupa la posición ⌈p/100 × n⌉ (redondeo hacia arriba).

Ejemplo con 10 latencias en milisegundos: 12, 15, 11, 240, 14, 13, 16, 12, 18, 900.

- Ordenadas: 11, 12, 12, 13, 14, 15, 16, 18, 240, 900.
- Media: 1.251 / 10 = 125,1 ms.
- p50: posición ⌈0,50 × 10⌉ = 5 → 14 ms.
- p90: posición ⌈0,90 × 10⌉ = 9 → 240 ms.

La media (125,1 ms) no describe a ningún usuario real: ocho de cada diez peticiones tardan 18 ms o menos, una tarda 240 ms y otra 900 ms. Con muestras pequeñas, los percentiles altos dependen de muy pocos valores; en producción se calculan sobre miles de peticiones.

#### 1.3. Throughput (Rendimiento) y Capacidad del Sistema

Mientras que la latencia mide el tiempo de una petición individual, el **Throughput** (rendimiento o caudal) mide la cantidad de trabajo que el sistema puede procesar en un periodo de tiempo determinado. En aplicaciones web y APIs, se mide habitualmente en Peticiones Por Segundo (RPS, _Requests Per Second_) o Transacciones Por Segundo (TPS).

Es vital comprender la relación matemática entre la concurrencia (usuarios simultáneos), la latencia y el throughput, descrita por la **Ley de Little**:

```text
Concurrencia = Throughput × Latencia media
```

Las unidades deben ser coherentes: si el throughput está en peticiones por segundo, la latencia media se expresa en segundos. Si un sistema procesa de forma sostenida 1.000 RPS con una latencia media de 0,1 segundos (100 ms), en cualquier instante hay de media 1.000 × 0,1 = 100 peticiones en vuelo. Usar la latencia en milisegundos sin convertirla daría 100.000, un resultado absurdo.

El objetivo al medir el throughput no es simplemente saber "cuánto soporta el servidor", sino identificar el **punto de inflexión** donde el sistema se degrada. A medida que aumenta la carga, el throughput sube linealmente hasta alcanzar la capacidad máxima de un recurso (CPU, hilos, conexiones a base de datos). A partir de ese punto, el throughput se estanca, pero la latencia crece muy deprisa debido a que las peticiones comienzan a encolarse esperando recursos libres. Establecer un objetivo de throughput implica definir la carga máxima esperada y asegurar que la latencia p95 se mantenga dentro del SLO bajo esa carga específica.

#### 1.4. Métricas de Memoria: Consumo, Allocations y Eficiencia del GC

En plataformas gestionadas como .NET 10, la memoria no es solo un recurso de almacenamiento estático; es un recurso dinámico cuyo ciclo de vida afecta directamente a la latencia de la CPU. Las métricas de memoria más importantes no se limitan a "cuánta RAM consume el proceso", sino a cómo se gestiona esa memoria.

Las métricas clave a monitorizar son:

- **Working Set (conjunto de trabajo):** la cantidad total de memoria física (RAM) que el proceso de .NET está utilizando en el sistema operativo. Un crecimiento constante e infinito de esta métrica indica una fuga de memoria (_Memory Leak_).
- **Allocation Rate (tasa de asignación):** medida en bytes o megabytes por segundo (MB/s). Indica a qué velocidad la aplicación está creando nuevos objetos en la memoria. Una tasa de asignación alta es una de las causas más habituales de mal rendimiento en .NET, ya que obliga al recolector a trabajar constantemente.
- **Porcentaje de tiempo en pausas de GC:** qué fracción del tiempo transcurrido tiene la aplicación detenida por el recolector de basura en lugar de ejecutando el código del negocio. Un valor alto y sostenido indica asignación excesiva de objetos y suele traducirse en picos en la latencia p99. No existe un umbral universal: el valor aceptable se fija en el SLO de cada servicio y se compara con su propia línea base.

**Cómo se obtienen estas métricas en .NET 10.** Desde .NET 9, el runtime publica el medidor (`Meter`) `System.Runtime` con métricas acumuladas desde el arranque del proceso. Las tasas se calculan como diferencia entre dos lecturas dividida por el intervalo:

| Métrica                             | Unidad                                    | Qué permite calcular                                                    |
| ----------------------------------- | ----------------------------------------- | ----------------------------------------------------------------------- |
| `dotnet.process.memory.working_set` | bytes                                     | Working Set actual                                                      |
| `dotnet.gc.heap.total_allocated`    | bytes (acumulado)                         | Tasa de asignación = incremento / segundos del intervalo                |
| `dotnet.gc.pause.time`              | segundos (acumulado)                      | % de tiempo en pausas de GC = incremento / segundos del intervalo × 100 |
| `dotnet.gc.collections`             | recolecciones (acumulado, por generación) | Frecuencia de recolecciones de gen0, gen1 y gen2                        |
| `dotnet.thread_pool.queue.length`   | elementos                                 | Trabajo encolado esperando un hilo del Thread Pool                      |
| `dotnet.exceptions`                 | excepciones (acumulado)                   | Excepciones lanzadas por segundo                                        |

**Ejemplo:** si en una ventana de 20 segundos `dotnet.gc.pause.time` pasa de 12,4 s a 13,0 s y `dotnet.gc.heap.total_allocated` pasa de 8.000 MB a 9.000 MB, la aplicación ha estado en pausa de GC 0,6 / 20 = 3 % del tiempo y ha asignado 1.000 / 20 = 50 MB/s. (En aplicaciones con .NET 8 o anterior, las herramientas muestran en su lugar los contadores antiguos, como `% Time in GC since last GC` y `Allocation Rate`.)

Establecer objetivos de memoria implica definir límites estrictos para la tasa de asignación en las rutas críticas del código (_hot paths_), asegurando que el recolector de basura intervenga lo menos posible.

#### 1.5. Ejemplo Práctico: Definición de un SLA para un API de Pagos

**Contexto:** una empresa de comercio electrónico está rediseñando su pasarela de pagos utilizando .NET 10. El equipo de negocio indica que "el pago debe ser instantáneo y nunca fallar, especialmente durante el Black Friday".

**Problema:** la afirmación del negocio es subjetiva y técnicamente imposible (nada es instantáneo y los fallos de red existen). El equipo de ingeniería debe traducir esto a métricas objetivas para poder diseñar la arquitectura y planificar las pruebas de carga.

**Análisis y traducción a métricas:** el equipo técnico se reúne con negocio y extrae los siguientes datos reales:

1. El Black Friday genera un pico máximo histórico de 4.500 compras por minuto.
2. Si el pago tarda más de 2 segundos, el 15 % de los usuarios pulsa el botón de "Pagar" por segunda vez, generando transacciones duplicadas y quejas en atención al cliente.

**Solución (el contrato de rendimiento):** el equipo de ingeniería define los siguientes objetivos medibles (SLOs) para el API de pagos:

- **Objetivo de throughput:** el sistema debe soportar de forma sostenida 100 TPS, lo que equivale a 6.000 compras por minuto, dejando un margen de seguridad del 33 % sobre el pico histórico.
- **Objetivo de latencia (SLO):** bajo una carga de 100 TPS, la latencia p95 debe ser inferior a 500 milisegundos, y la latencia p99 debe ser estrictamente inferior a 1.500 milisegundos (1,5 segundos). Así, al menos el 99 % de los pagos queda por debajo de 1,5 segundos, con margen respecto a la barrera de los 2 segundos que provoca clics duplicados.
- **Objetivo de memoria:** el porcentaje de tiempo en pausas de GC debe mantenerse por debajo del 3 % durante la prueba de carga máxima (valor que el equipo fija a partir de su línea base), asegurando que las pausas del recolector de basura no disparen la latencia p99 por encima del límite establecido.

Con este documento formalizado, el equipo ya no trabaja con suposiciones. Cualquier cambio en el código que eleve el p99 por encima de 1,5 segundos en las pruebas de carga previas al despliegue se rechaza antes de llegar a producción.

### 2. El Runtime de .NET 10 y su Impacto en el Rendimiento

Para optimizar una aplicación, es imprescindible comprender cómo el motor subyacente ejecuta el código. El runtime de .NET 10 es una pieza de ingeniería altamente sofisticada que gestiona la memoria, compila el código en tiempo real y optimiza las instrucciones para la CPU. Entender su comportamiento interno permite a los arquitectos y desarrolladores tomar decisiones de diseño que colaboren con el runtime en lugar de luchar contra él.

#### 2.1. Arquitectura y Tipos del Garbage Collector (GC) en .NET 10

El Recolector de Basura (_Garbage Collector_ o GC) es el componente del runtime responsable de asignar y liberar memoria automáticamente. En .NET 10, el GC es **generacional**, dividiendo los objetos en tres generaciones (0, 1 y 2) según su tiempo de vida. Los objetos nuevos nacen en la Generación 0. Si sobreviven a una recolección, pasan a la Generación 1, y posteriormente a la Generación 2 (objetos de larga duración).

El rendimiento de una aplicación depende críticamente del tipo de GC que el runtime esté utilizando. .NET 10 ofrece diferentes modalidades adaptadas a distintos entornos:

- **Workstation GC:** es el modo por defecto de las aplicaciones de consola y escritorio. Usa un único heap administrado y la recolección se ejecuta en el hilo que la desencadena. Está pensado para que la interfaz responda y para consumir poca memoria, no para maximizar el throughput de un servidor con muchos núcleos.
- **Server GC:** diseñado para aplicaciones backend. Por defecto crea un heap y un hilo de GC dedicado por cada procesador lógico, de modo que la recolección se hace en paralelo y el throughput es mayor, a cambio de consumir más memoria. Las aplicaciones ASP.NET Core lo usan por defecto; se controla con la propiedad MSBuild `ServerGarbageCollection` (o `System.GC.Server` en `runtimeconfig.json`).
- **GC en segundo plano (Background GC):** activado por defecto (`ConcurrentGarbageCollection`). Permite que las recolecciones de generación 2 se hagan mayoritariamente mientras la aplicación sigue ejecutándose, con pausas breves al principio y al final, en lugar de una única pausa larga.
- **DATAS (Dynamic Adaptation To Application Sizes):** apareció en .NET 8 y está activado por defecto con Server GC desde .NET 9, así que también en .NET 10. Tradicionalmente, el número de heaps de Server GC era fijo. DATAS permite al runtime ajustar dinámicamente el número de heaps y el comportamiento del GC basándose en la carga de trabajo actual y las restricciones de memoria del entorno (por ejemplo, dentro de un contenedor Docker con límites estrictos). Si la aplicación tiene baja concurrencia, DATAS reduce los heaps para ahorrar memoria; si la concurrencia aumenta, escala el comportamiento del GC para maximizar el throughput.

Comprender esto es vital: un backend en una máquina con muchos núcleos que se haya configurado con Workstation GC recolecta con un solo heap y un solo hilo, así que suele obtener menos throughput que con Server GC. Cambiar de modo, en cualquier caso, **no reduce la cantidad de objetos que se asignan**: si el problema es la tasa de asignación, hay que corregir el código.

```xml
<!-- Fragmento de un .csproj: opciones del GC (los valores mostrados son los habituales de ASP.NET Core) -->
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
  <!-- 1 = DATAS activado (valor por defecto con Server GC desde .NET 9), 0 = desactivado -->
  <GarbageCollectionAdaptationMode>1</GarbageCollectionAdaptationMode>
</PropertyGroup>
```

#### 2.2. El Impacto de las Asignaciones (Allocations) en el Rendimiento Global

El principio fundamental del rendimiento en .NET 10 es que asignar memoria en el Heap (montículo) tiene un **coste diferido**. Crear un objeto (usando la palabra clave `new` para clases) es una operación extremadamente rápida, casi equivalente a incrementar un puntero. Sin embargo, el coste real se paga más tarde, cuando el GC debe detener la ejecución de la aplicación para buscar objetos huérfanos y compactar la memoria.

El impacto en el rendimiento varía mucho según la generación en la que se recolecte el objeto:

- **Recolección de Generación 0:** es extremadamente rápida (fracciones de milisegundo). Ocurre frecuentemente y tiene un impacto mínimo en la latencia p95.
- **Recolección de Generación 1:** sirve como un búfer entre los objetos efímeros y los permanentes. También es relativamente rápida.
- **Recolección de Generación 2 (Full GC):** es la más costosa, porque debe revisar todos los objetos vivos de la aplicación. Si se hace en modo bloqueante (por ejemplo, cuando hay que compactar el heap o la memoria disponible es escasa), detiene todos los hilos que ejecutan código administrado (_Stop-The-World_) durante toda la recolección, y todas las peticiones en curso suman esa pausa a su latencia, lo que se nota en el p99. Mientras la memoria no escasea, el GC prefiere hacer las recolecciones de generación 2 en segundo plano, en paralelo con la aplicación y con pausas breves.
- **Montículo de objetos grandes (LOH):** los objetos de 85.000 bytes o más (típicamente arrays y búferes grandes) se asignan directamente en el LOH, que solo se recolecta junto con la generación 2. Crear búferes grandes en cada petición es, por tanto, especialmente dañino.

El objetivo arquitectónico no es eliminar todas las asignaciones, sino asegurar que los objetos **mueran jóvenes** (en la Generación 0). Los problemas de rendimiento surgen cuando se asignan grandes volúmenes de objetos que sobreviven lo suficiente para ser promovidos a la Generación 2 (por ejemplo, porque se acumulan en una lista hasta procesar un lote completo), pero luego quedan huérfanos, forzando al sistema a realizar recolecciones de generación 2 con frecuencia.

#### 2.3. Optimizaciones Avanzadas del Compilador JIT (Just-In-Time)

El código .NET no se compila directamente a lenguaje máquina, sino a un Lenguaje Intermedio (IL). Cuando la aplicación se ejecuta, el compilador JIT traduce ese IL a instrucciones nativas específicas para la CPU del servidor. En .NET 10, el JIT utiliza un enfoque de **Compilación por Niveles** (_Tiered Compilation_) combinado con **Dynamic PGO** (_Profile-Guided Optimization_), activado por defecto desde .NET 8.

El proceso funciona de la siguiente manera:

1. **Tier 0 (Nivel 0):** cuando un método se llama por primera vez, el JIT lo compila lo más rápido posible, sin aplicar optimizaciones. El objetivo es que la aplicación arranque inmediatamente.
2. **Instrumentación:** mientras el código Tier 0 se ejecuta, el runtime inyecta contadores invisibles para observar cómo se comporta el código en la realidad (qué ramas de los condicionales `if` se toman más, qué tipos exactos se pasan a las interfaces).
3. **Tier 1 (Nivel 1) y Dynamic PGO:** si un método se llama con mucha frecuencia (se convierte en un _hot path_), el JIT lo recompila en un hilo en segundo plano. Esta vez, utiliza los datos recopilados (PGO) para optimizar de forma agresiva. Por ejemplo, si observa que una llamada a través de una interfaz recibe casi siempre la misma implementación, el JIT añade una comprobación rápida del tipo y, en ese caso, llama directamente al método concreto (_devirtualization_) e inserta su código en el llamador (_inlining_), eliminando la sobrecarga de la llamada virtual.
4. **OSR (On-Stack Replacement):** disponible y activo por defecto desde .NET 7 en x64 y Arm64. Si un método tiene un bucle muy largo (como un procesador de mensajes), el JIT puede reemplazar el código no optimizado (Tier 0) por el código optimizado (Tier 1) mientras el bucle se está ejecutando, sin esperar a que el método termine.

Conocer este comportamiento explica por qué las pruebas de rendimiento (benchmarks) nunca deben medir los primeros segundos de ejecución de una aplicación .NET: el código aún no ha sido optimizado por el JIT al Nivel 1. Por eso las herramientas de benchmarking ejecutan primero unas iteraciones de calentamiento (_warmup_) que no cuentan en el resultado (sección 4.1).

**Novedades del JIT en .NET 10.** Según las notas oficiales de la versión, el JIT de .NET 10 reduce el coste de las abstracciones: puede desvirtualizar y hacer inlining de los métodos de interfaz de los arrays (por ejemplo, recorrer un `int[]` a través de `IEnumerable<int>`), mejora el inlining (incluidos algunos métodos con `try-finally`), reorganiza el código para agrupar las rutas calientes y amplía la asignación en pila de objetos que no escapan del método (sección 2.5). Son mejoras que se obtienen al actualizar a .NET 10 sin cambiar el código.

#### 2.4. Vectorización (SIMD) y AVX10.2

Los procesadores modernos incluyen instrucciones SIMD (_Single Instruction, Multiple Data_): en lugar de comparar o sumar un valor cada vez, cargan varios valores en registros anchos (128, 256 o 512 bits) y aplican la misma operación a todos a la vez.

En .NET, la vectorización llega al código de negocio sobre todo a través de las bibliotecas, no porque el JIT transforme automáticamente los bucles escritos a mano:

- Las APIs de búsqueda y comparación sobre `Span<T>` y cadenas (`IndexOf`, `IndexOfAny`, `SequenceEqual`, `Contains`...) y el tipo `SearchValues<T>` usan internamente `Vector128`, `Vector256` o `Vector512` cuando el procesador lo permite.
- Buena parte de la biblioteca base se apoya en esas APIs; por ejemplo, `System.Text.Json` las usa para localizar caracteres especiales al leer y escribir JSON.
- Quien necesite vectorizar su propio algoritmo puede usar directamente los tipos `Vector128<T>`, `Vector256<T>` y `Vector512<T>` (este último desde .NET 8) o los intrínsecos de `System.Runtime.Intrinsics`.

Por tanto, la forma práctica de beneficiarse de SIMD en un backend es trabajar sobre memoria contigua (arrays, `Span<T>`) y usar las APIs de búsqueda de la biblioteca en lugar de recorrer caracteres uno a uno (ejemplo en la sección 4.3). El cifrado del tráfico HTTPS no depende de esto: lo realizan las bibliotecas criptográficas del sistema operativo (SChannel en Windows, OpenSSL en Linux), que ya usan las instrucciones específicas del procesador.

**AVX10.2 en .NET 10.** .NET 10 añade la clase `System.Runtime.Intrinsics.X86.Avx10v2` con los intrínsecos de AVX10.2 para x64. Según la documentación oficial, como aún no hay hardware disponible con AVX10.2, el soporte del JIT está **desactivado por defecto**. Es una preparación para procesadores futuros, no una mejora que hoy se note en producción.

#### 2.5. Asignación en Pila (Stack Allocation) y Slicing con Span

Para reducir el trabajo del recolector, .NET ofrece mecanismos para que ciertos datos vivan en la pila (_stack_) en lugar de en el montículo (_heap_). La memoria de la pila se reserva y se libera automáticamente al entrar y salir del método, sin intervención del GC. Conviene distinguir tres técnicas que a menudo se confunden:

1. **Tipos por valor (`struct`) como variables locales.** Un `struct` local vive en la pila (o en registros del procesador) y no genera trabajo para el GC.
2. **`stackalloc`.** Reserva explícitamente un bloque de memoria en la pila y lo expone como `Span<T>`. Solo es adecuado para bloques pequeños y de tamaño acotado: la pila es limitada y un tamaño que dependa de la entrada sin control puede provocar un desbordamiento de pila.

   ```csharp
   public static bool EmpiezaPorEs(ReadOnlySpan<char> codigo)
   {
       if (codigo.Length is 0 or > 64) return false;        // tamaño acotado antes de usar la pila
       Span<char> normalizado = stackalloc char[codigo.Length]; // en la pila: sin trabajo para el GC
       codigo.ToUpperInvariant(normalizado);
       return normalizado.Length >= 2 && normalizado[0] == 'E' && normalizado[1] == 'S';
   }
   ```

3. **Asignación en pila automática del JIT (análisis de escape).** Si el JIT demuestra que un objeto no sale del método (no "escapa"), puede colocarlo en la pila aunque el código use `new`. .NET 10 amplía esta capacidad a arrays pequeños de tamaño fijo, tanto de tipos por valor como de referencias, a objetos referenciados desde campos de structs locales y a delegados que no escapan. No requiere cambiar el código.

**Slicing con `Span<T>` y `ReadOnlySpan<T>`: no es asignación en pila, pero evita copias.** Un `ReadOnlySpan<char>` es una "ventana" sobre memoria que ya existe (por ejemplo, un `string`). Si un sistema necesita los primeros 5 caracteres de un texto de 1.000 caracteres, `texto.Substring(0, 5)` crea un nuevo `string` en el heap; `texto.AsSpan(0, 5)` solo crea una pequeña estructura (referencia más longitud) que apunta a esos 5 caracteres del texto original. El texto sigue estando en el heap, pero no se crea ningún objeto nuevo. Muchas APIs aceptan `ReadOnlySpan<char>` directamente (por ejemplo, `int.Parse`, `decimal.TryParse` o `IndexOf`), así que el dato nunca llega a convertirse en `string`. Una restricción importante: `Span<T>` es un `ref struct`, de modo que no puede guardarse en campos de clases.

#### 2.6. Ejemplo Práctico: Análisis de una Mejora de Rendimiento

**Contexto:** un microservicio en .NET 10 procesa archivos CSV con millones de líneas de transacciones financieras. El servicio extrae el importe de cada línea, lo valida y lo suma a un total.

**Problema:** bajo carga alta, el servicio sufre picos de latencia. La monitorización muestra CPU moderada, una tasa de asignación muy alta y un porcentaje de tiempo en pausas de GC muy superior a su línea base.

**Análisis del comportamiento interno:** por cada línea, el código hace `linea.Split(',')`, que crea un array nuevo y un `string` por cada columna, aunque solo necesita una. Además, acumula las líneas leídas en una lista y las procesa por lotes grandes, así que muchos de esos objetos sobreviven a varias recolecciones y acaban promovidos a generación 2, que hay que recolectar con frecuencia.

**Solución (aplicando el conocimiento del runtime):**

1. Procesar cada línea en cuanto se lee, sin acumularlas, para que los objetos temporales mueran en la generación 0.
2. Localizar los separadores con `IndexOf` sobre un `ReadOnlySpan<char>` (API vectorizada por la biblioteca) y trocear con slicing, sin crear `string` intermedios.
3. Convertir el importe directamente desde el span con `decimal.TryParse`, sin excepciones en las líneas mal formadas.

```csharp
using System.Globalization;

public static class ImporteCsv
{
    // Devuelve el valor de la columna 'indice' (base 0) sin crear strings intermedios.
    public static bool TryLeerImporte(ReadOnlySpan<char> linea, int indice, out decimal importe)
    {
        for (int i = 0; i < indice; i++)
        {
            int coma = linea.IndexOf(',');
            if (coma < 0) { importe = 0; return false; }
            linea = linea[(coma + 1)..];
        }
        int fin = linea.IndexOf(',');
        ReadOnlySpan<char> campo = fin >= 0 ? linea[..fin] : linea;
        return decimal.TryParse(campo, NumberStyles.Number, CultureInfo.InvariantCulture, out importe);
    }
}
```

**Resultado:** la tasa de asignación por línea cae prácticamente a cero, el tiempo en pausas de GC vuelve a su línea base y la latencia p99 se estabiliza. La magnitud exacta de la mejora no se supone: se mide antes y después con las herramientas de la sección 4, y esa medición es la que justifica el cambio.

### 3. Detección y Clasificación de Cuellos de Botella

Incluso con una arquitectura sólida y un código optimizado, los sistemas en producción inevitablemente enfrentarán problemas de rendimiento bajo condiciones imprevistas. La habilidad más crítica en la ingeniería de rendimiento no es saber usar una herramienta de perfilado (_profiler_), sino poseer un marco mental estructurado para deducir dónde está el problema antes de mirar el código. Esta sección detalla cómo clasificar y diagnosticar cuellos de botella en sistemas backend.

#### 3.1. Marco Conceptual para el Diagnóstico de Sistemas Backend

Cuando ocurre un incidente de rendimiento, la reacción instintiva suele ser revisar los logs o adivinar qué parte del código está fallando. Este enfoque desestructurado consume tiempo y a menudo lleva a conclusiones erróneas.

Para diagnosticar problemas en .NET 10, se debe aplicar un marco conceptual estandarizado. El más efectivo para el análisis de recursos es el **Método USE** (_Utilization, Saturation, Errors_), creado por el experto en rendimiento Brendan Gregg. Para cada recurso del sistema (CPU, memoria, discos, red, hilos), se deben verificar tres métricas en orden estricto:

1. **Utilización (Utilization):** ¿qué porcentaje del tiempo el recurso estuvo ocupado realizando trabajo? (Por ejemplo, CPU al 85 %).
2. **Saturación (Saturation):** ¿existe trabajo adicional encolado que no puede ser procesado porque el recurso está al 100 %? La saturación es el verdadero indicador de un cuello de botella. Un recurso puede estar al 99 % de utilización y funcionar perfectamente, pero si hay saturación, la latencia se disparará.
3. **Errores (Errors):** ¿el recurso está registrando fallos? (Por ejemplo, errores de conexión de red o excepciones de falta de memoria).

Aplicando el método USE, el ingeniero revisa sistemáticamente cada componente de la infraestructura y del runtime de .NET hasta encontrar el recurso que presenta alta saturación. Una vez identificado el recurso estrangulado, se clasifica el cuello de botella en una de las siguientes categorías.

#### 3.2. Cuellos de Botella de CPU: Síntomas y Causas Típicas

Un cuello de botella de CPU ocurre cuando los procesadores del servidor están al 100 % de utilización y las peticiones entrantes comienzan a encolarse (saturación). El síntoma principal es que el throughput se estanca y la latencia de todas las peticiones aumenta de manera uniforme.

En aplicaciones backend modernas, rara vez el código de negocio puro (sumas, restas, condicionales) agota la CPU. Las causas típicas en .NET 10 suelen ser:

- **Serialización y deserialización masiva:** convertir grandes grafos de objetos a JSON o XML repetidamente. Serializar más datos de los necesarios o usar serializadores basados en reflexión en rutas muy calientes consume mucha CPU; `System.Text.Json` con generación de código en compilación (_source generation_) reduce ese coste.
- **Expresiones regulares (Regex) mal diseñadas:** una expresión regular compleja aplicada a textos largos puede sufrir de "backtracking catastrófico", donde el motor de Regex explora un número combinatorio de caminos y mantiene un núcleo de la CPU ocupado durante segundos o minutos. Se mitiga fijando un tiempo máximo de coincidencia (`matchTimeout`) o usando `RegexOptions.NonBacktracking` (disponible desde .NET 7).
- **Excepciones frecuentes:** lanzar una excepción en .NET (usar `throw`) es una operación extremadamente costosa para la CPU, ya que el runtime debe capturar el estado de la pila (_stack trace_). Si se utilizan excepciones para controlar el flujo normal del negocio (por ejemplo, lanzar una excepción cuando un usuario introduce una contraseña incorrecta), bajo carga alta una parte apreciable de la CPU se dedica solo a crear y capturar excepciones. La alternativa son los métodos `TryXxx` (`int.TryParse`, `Dictionary.TryGetValue`) o devolver un resultado que indique el error. La métrica `dotnet.exceptions` permite detectar este patrón.

#### 3.3. Cuellos de Botella de Memoria: Fugas y Presión del GC

Como se analizó en la sección del runtime, los problemas de memoria en .NET se dividen en dos categorías conceptualmente distintas, con síntomas y soluciones completamente diferentes:

1. **Fugas de memoria (Memory Leaks):** ocurren cuando la aplicación retiene referencias a objetos que ya no necesita, impidiendo que el Recolector de Basura los elimine.
   - **Síntoma:** la métrica del Working Set (memoria RAM total) crece de forma sostenida a lo largo de horas o días, sin volver a bajar tras las recolecciones, hasta que el contenedor o servidor agota la memoria física y el sistema operativo mata el proceso (OOM, _Out Of Memory_).
   - **Causas típicas:** suscripciones a eventos estáticos que nunca se desuscriben, cachés en memoria sin políticas de expiración (_eviction_), o diccionarios estáticos que crecen infinitamente.
2. **Presión de memoria (GC Pressure):** ocurre cuando la aplicación crea y destruye objetos a una velocidad vertiginosa. No hay fuga (la memoria no crece infinitamente), pero el sistema se ahoga limpiándola.
   - **Síntoma:** el uso de RAM oscila pero no crece de forma sostenida; la tasa de asignación y el porcentaje de tiempo en pausas de GC son altos, y la CPU muestra picos de uso intermitentes. La latencia p99 es errática.
   - **Causas típicas:** concatenación de cadenas de texto en bucles (mejor `StringBuilder` o `string.Create`), búferes grandes creados en cada petición (mejor `ArrayPool<T>`), devolver listas completas de base de datos para luego filtrarlas en memoria, o instanciar objetos pesados dentro de bucles de alta frecuencia.

#### 3.4. Cuellos de Botella de Acceso a Base de Datos y Red

En arquitecturas distribuidas y microservicios, el cuello de botella más común no está en el servidor de aplicaciones, sino en las dependencias externas (bases de datos, APIs de terceros, cachés distribuidas).

- **Latencia de red y Chatty I/O:** si un servicio necesita recuperar datos de un cliente y realiza 50 consultas a la base de datos de 2 milisegundos cada una, la latencia total será de 100 ms solo en tiempo de tránsito de red. Este patrón, conocido como problema de las **consultas N+1** cuando se lanza una consulta por cada elemento de una lista, degrada mucho el rendimiento; se corrige obteniendo los datos en una sola consulta o en lotes.
- **Agotamiento del pool de conexiones (Connection Pool Starvation):** .NET mantiene un grupo de conexiones abiertas hacia la base de datos para reutilizarlas. Si las consultas son lentas o si los desarrolladores olvidan cerrar las conexiones, el pool se agota. El síntoma es que la CPU y la memoria del servidor web están casi al 0 %, pero las peticiones fallan por "Timeout" esperando obtener una conexión al servidor de datos.
- **Agotamiento de puertos (SNAT Port Exhaustion):** al realizar muchas llamadas HTTP salientes creando un `HttpClient` nuevo por petición en lugar de reutilizarlo (con `IHttpClientFactory` o una instancia compartida), el servidor agota los puertos TCP disponibles en el sistema operativo, provocando fallos de red intermitentes bajo carga.

#### 3.5. Saturación del Sistema y Agotamiento de Hilos (Thread Starvation)

Este es uno de los cuellos de botella más difíciles de diagnosticar en aplicaciones .NET. El _Thread Pool_ (grupo de hilos) de .NET es un gestor que asigna hilos de trabajo (_worker threads_) para procesar las peticiones entrantes.

El **Agotamiento de Hilos (Thread Starvation)** ocurre cuando todos los hilos disponibles en el Thread Pool están ocupados, pero no están ejecutando código; están bloqueados esperando que termine una operación externa (como una consulta a base de datos o una llamada HTTP).

La causa principal de este desastre es el antipatrón conocido como **Sync-over-Async**. Esto sucede cuando un desarrollador llama a un método asíncrono, pero en lugar de usar la palabra clave `await` para liberar el hilo, utiliza `.Result` o `.Wait()`. Al hacer esto, el hilo actual se congela por completo hasta que la operación de red termina. Si llegan muchas peticiones concurrentes y todas bloquean su hilo esperando a la base de datos, el Thread Pool se queda sin hilos libres: crea hilos nuevos de forma gradual, más despacio de lo que llegan las peticiones, y la cola (`dotnet.thread_pool.queue.length`) crece.

El síntoma clásico y paradójico del Thread Starvation es:

1. El servidor web deja de responder o devuelve errores 503 (_Service Unavailable_).
2. La latencia se dispara a decenas de segundos.
3. El uso de CPU es bajo.

Para un ingeniero inexperto, ver un servidor caído con la CPU inactiva es incomprensible. Aplicando el marco conceptual, se entiende que el recurso saturado no es la CPU, sino la capacidad del Thread Pool para aceptar nuevo trabajo.

#### 3.6. Ejemplo Práctico: Clasificación de un Incidente de Rendimiento en Producción

**Contexto:** una plataforma de reservas de vuelos desarrollada en .NET 10 sufre una caída de rendimiento todos los viernes por la tarde, coincidiendo con el pico de tráfico semanal. Los usuarios indican que la web se queda cargando indefinidamente.

**Problema:** el equipo de operaciones reinicia los servidores para mitigar el problema temporalmente, pero necesitan una solución definitiva. Se requiere clasificar el cuello de botella utilizando el método USE.

**Análisis y diagnóstico paso a paso:**

1. **Paso 1: CPU (utilización y saturación).** Durante el incidente, la utilización de CPU de los servidores web es del 12 %. No hay saturación de CPU. Se descartan problemas de serialización masiva o bucles infinitos.
2. **Paso 2: memoria.** El Working Set se mantiene estable en 800 MB. No hay fugas de memoria. El porcentaje de tiempo en pausas de GC es del 2 %, igual que su línea base. El recolector de basura está sano. Se descartan problemas de asignación excesiva.
3. **Paso 3: base de datos y red.** La CPU de la base de datos está al 15 % y las consultas se resuelven en menos de 5 milisegundos. La base de datos no es el cuello de botella.
4. **Paso 4: hilos y saturación del sistema.** La longitud de la cola del Thread Pool (`dotnet.thread_pool.queue.length`) pasa de 0 a miles de elementos en espera durante el incidente. El recurso saturado es el Thread Pool.

**Clasificación del cuello de botella:** Thread Starvation (agotamiento de hilos).

**Solución conceptual:** sabiendo que la causa típica del Thread Starvation con baja CPU es el bloqueo de hilos, el equipo audita el código buscando el antipatrón Sync-over-Async. Descubren que un componente heredado que valida tarjetas de crédito realiza una llamada HTTP externa utilizando `.Result` en lugar de `await`.

```csharp
// Antes: el hilo del Thread Pool queda bloqueado mientras dura la llamada de red
public bool Validar(Tarjeta tarjeta)
{
    var respuesta = _http.PostAsJsonAsync("validaciones", tarjeta).Result;
    return respuesta.IsSuccessStatusCode;
}

// Después: asíncrono de extremo a extremo; el hilo se libera durante la espera
public async Task<bool> ValidarAsync(Tarjeta tarjeta, CancellationToken ct)
{
    var respuesta = await _http.PostAsJsonAsync("validaciones", tarjeta, ct);
    return respuesta.IsSuccessStatusCode;
}
```

El cambio obliga a que los llamadores también sean asíncronos (`await ValidarAsync(...)`) hasta llegar al endpoint; sustituir `.Result` por `.GetAwaiter().GetResult()` no resuelve nada, porque sigue bloqueando el hilo. Bajo tráfico normal, el Thread Pool inyecta suficientes hilos para compensar el bloqueo. Sin embargo, durante el pico de los viernes, la cantidad de bloqueos simultáneos supera la capacidad del Thread Pool de crear nuevos hilos, colapsando el servidor. Al refactorizar esa llamada para utilizar programación asíncrona correcta (`await`), los hilos se liberan mientras esperan la respuesta de la pasarela de pago, de modo que el pico de los viernes deja de agotar el Thread Pool. La corrección se confirma repitiendo la prueba de carga y comprobando que la cola del Thread Pool se mantiene cerca de cero.

### 4. Medición y Optimización en la Práctica

Las secciones anteriores explican qué medir y cómo razonar. Esta sección muestra las herramientas oficiales del ecosistema para medir en un equipo de desarrollo y los patrones de código que corrigen las causas más habituales. La regla que guía todo el proceso es: **medir, cambiar una sola cosa y volver a medir.**

#### 4.1. Microbenchmarks con BenchmarkDotNet

BenchmarkDotNet es la biblioteca de referencia para medir el rendimiento de fragmentos de código .NET (la usa el propio equipo de .NET). Se encarga de lo que un `Stopwatch` hace mal: ejecuta iteraciones de calentamiento para que el JIT optimice el código (sección 2.3), repite las mediciones, calcula estadísticos y, con el atributo `[MemoryDiagnoser]`, informa de la memoria asignada por operación.

```bash
dotnet new console -n Perf.Benchmarks
cd Perf.Benchmarks
dotnet add package BenchmarkDotNet
```

```csharp
// Perf.Benchmarks/Program.cs
using System.Globalization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<ImporteBenchmarks>();

[MemoryDiagnoser]
public class ImporteBenchmarks
{
    private const string Linea = "2026-03-01,ES-0042,125.40,EUR";

    [Benchmark(Baseline = true)]
    public decimal ConSplit() =>
        decimal.Parse(Linea.Split(',')[2], CultureInfo.InvariantCulture);

    [Benchmark]
    public decimal ConSpan() =>
        ImporteCsv.TryLeerImporte(Linea, 2, out var importe) ? importe : 0m;
}
```

(`ImporteCsv` es la clase de la sección 2.6, copiada en el mismo proyecto.)

**Reglas de uso:**

- Siempre en Release: `dotnet run -c Release`. En Debug el JIT no optimiza y BenchmarkDotNet avisa de que los resultados no son válidos.
- Cada método `[Benchmark]` debe ser `public` y la clase también; el método puede devolver el resultado para que el JIT no elimine el trabajo como código muerto.
- `Baseline = true` marca la referencia: la columna `Ratio` expresa cada método en relación con ella.
- `[Params(...)]` sobre una propiedad pública ejecuta el benchmark con varios valores (por ejemplo, tamaños de entrada), y `[GlobalSetup]` marca un método que prepara los datos una sola vez antes de medir.

**Columnas del informe que se usan para decidir:** `Mean` (tiempo medio por operación), `Error` y `StdDev` (dispersión), `Ratio` (respecto a la línea base), `Gen0` (recolecciones de generación 0 por cada 1.000 operaciones) y `Allocated` (bytes asignados por operación). Si `Allocated` baja a `-` significa que la operación no asigna memoria en el heap.

Un microbenchmark mide un fragmento aislado; no sustituye a la prueba de carga del servicio completo, que es la que valida el SLO.

#### 4.2. Herramientas de Diagnóstico: dotnet-counters, dotnet-trace y dotnet-gcdump

Las tres herramientas de diagnóstico de línea de comandos se conectan a un proceso .NET en ejecución. Se pueden instalar como herramientas globales o, con el SDK de .NET 10, ejecutarse sin instalación permanente mediante `dnx`:

```bash
dotnet tool install --global dotnet-counters
dotnet tool install --global dotnet-trace
dotnet tool install --global dotnet-gcdump

# Alternativa con el SDK de .NET 10, sin instalar la herramienta
dnx dotnet-counters ps
```

| Herramienta       | Para qué sirve                                                                | Comando básico                                                        |
| ----------------- | ----------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| `dotnet-counters` | Primer vistazo: métricas en vivo (CPU, memoria, GC, Thread Pool, excepciones) | `dotnet-counters monitor --processid <PID> --counters System.Runtime` |
| `dotnet-trace`    | Averiguar en qué métodos se gasta la CPU o dónde se producen las asignaciones | `dotnet-trace collect --process-id <PID>`                             |
| `dotnet-gcdump`   | Ver qué tipos ocupan el heap y quién los retiene (fugas de memoria)           | `dotnet-gcdump collect --process-id <PID>`                            |

- `dotnet-counters ps` lista los procesos .NET a los que se puede conectar. Con `--counters System.Runtime[dotnet.gc.pause.time,dotnet.gc.heap.total_allocated]` se muestran solo las métricas indicadas; las acumuladas (como `dotnet.gc.pause.time`) aparecen ya como tasa por segundo, así que su valor es directamente la fracción de tiempo en pausa. `dotnet-counters collect ... --format csv` las guarda en un archivo para compararlas antes y después de un cambio.
- `dotnet-trace collect` genera un archivo `.nettrace` que se abre con Visual Studio o PerfView para ver las rutas de código más costosas.
- `dotnet-gcdump collect` genera un archivo `.gcdump` con el grafo de objetos vivos. En Windows se abre con Visual Studio o PerfView; en cualquier sistema, `dotnet-gcdump report <archivo>` muestra en consola el tamaño y el número de objetos por tipo. Para capturarlo se fuerza una recolección de generación 2, que puede detener la aplicación bastante tiempo si el heap es grande, así que no debe usarse a la ligera en producción. Para confirmar una fuga se comparan dos capturas separadas en el tiempo: los tipos que crecen entre ambas son los sospechosos.

**Correspondencia entre síntomas y herramienta:**

- CPU alta → `dotnet-trace`.
- Tasa de asignación o pausas de GC altas → `dotnet-counters` para confirmarlo y `dotnet-trace` para localizar quién asigna.
- Working Set creciente → `dotnet-gcdump`.
- Cola del Thread Pool creciente con CPU baja → `dotnet-counters` y revisión de llamadas bloqueantes (sección 3.5).

#### 4.3. Patrones de Código para Reducir Asignaciones y CPU

**1. Búsqueda de varios caracteres con `SearchValues<T>`.** Cuando se busca repetidamente cualquiera de un conjunto de caracteres, `SearchValues.Create` precalcula una vez la estructura de búsqueda más eficiente para el procesador (incluidas instrucciones SIMD) y se reutiliza en cada llamada. Se guarda en un campo `static readonly`:

```csharp
using System.Buffers;

public static class Separadores
{
    private static readonly SearchValues<char> Validos = SearchValues.Create(",;\t");

    public static int ContarCampos(ReadOnlySpan<char> linea)
    {
        int campos = 1;
        int pos;
        while ((pos = linea.IndexOfAny(Validos)) >= 0)
        {
            campos++;
            linea = linea[(pos + 1)..];
        }
        return campos;
    }
}
```

**2. Búferes reutilizables con `ArrayPool<T>`.** En lugar de crear un `byte[]` grande en cada petición (que además acabaría en el LOH si llega a 85.000 bytes), se alquila uno del pool compartido y se devuelve en un bloque `finally`. `Rent` puede devolver un array mayor que el solicitado, así que hay que trabajar con la longitud que se necesita, no con `buffer.Length`, cuando importa el tamaño exacto; y el array devuelto no debe usarse después de `Return`.

```csharp
using System.Buffers;

public static class Flujos
{
    public static async Task<long> ContarBytesAsync(Stream origen, CancellationToken ct)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64 * 1024); // 65.536 bytes: por debajo del umbral del LOH
        try
        {
            long total = 0;
            int leidos;
            while ((leidos = await origen.ReadAsync(buffer.AsMemory(), ct)) > 0)
            {
                total += leidos;
            }
            return total;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
```

**3. Construcción de cadenas sin concatenar en bucles.** Cada `+=` sobre un `string` crea una cadena nueva con todo el contenido anterior; dentro de un bucle se usa `StringBuilder`, que reutiliza su búfer interno, o `string.Join` si se unen elementos de una colección.

**4. Errores esperados sin excepciones.** Los métodos `TryXxx` devuelven `false` en lugar de lanzar:

```csharp
// Dentro de un endpoint de API mínima.
// En vez de: try { cantidad = int.Parse(texto); } catch (FormatException) { ... }
if (!int.TryParse(texto, out int cantidad))
{
    return Results.BadRequest("Cantidad no válida.");
}
```

**Cómo elegir el patrón.** Primero se identifica la categoría del cuello de botella (sección 3) y la línea responsable con las herramientas de la sección 4.2; después se aplica el patrón que ataca esa causa y se confirma la mejora con BenchmarkDotNet (fragmento) y con la prueba de carga (servicio). Un patrón aplicado donde no hay problema añade complejidad sin beneficio.

---

## Laboratorio: medir y optimizar `/orders`

Se mide un endpoint "ingenuo" (`GET /orders`, que devuelve 1000 objetos), se diagnostica la causa del p99 alto y se aplica la optimización, comparando antes y después con las mismas condiciones de prueba.

### 1. Herramienta de carga: `hey` (Windows)

`hey` no siempre está en los gestores de paquetes estándar, por lo que se usa la instalación manual:

1. Descarga `hey_windows_amd64.exe` (Windows 64-bit; alternativa: la página de _Releases_ del repositorio oficial en GitHub).
2. Cópialo a la raíz del proyecto (por ejemplo `C:\dev3\B2BOrders`) y **renómbralo a `hey.exe`**.
3. Comprueba el tamaño (clic derecho → Propiedades): debe pesar **~8 MB**. Si pesa ~200 KB, la descarga falló: bórralo y vuelve a descargarlo.
4. En PowerShell hay que indicar la ruta explícita para ejecutables locales (`.\` delante):

```powershell
.\hey.exe -n 5000 -c 50 http://localhost:5144/orders
```

`-n` es el total de peticiones y `-c` los usuarios concurrentes. Cambia `5144` por el puerto real de tu API.

### 2. Anatomía del reporte de `hey`

- **Resumen (Throughput):** `Requests/sec` es la velocidad bruta: cuántas peticiones atiende el servidor por segundo. Cuanto más alto, mejor.
- **Histograma de latencia:** muestra la consistencia. Si se aplana hacia la derecha (cola larga), hay problemas de rendimiento esporádicos.
- **Percentiles (la verdad):** la sección `Latency distribution` muestra p50, p75, p90, p95 y p99.

Ejemplo ilustrativo de un reporte:

```text
Summary:
  Requests/sec: 475.5422        <-- Throughput

Latency distribution:
  50% in 0.0800 secs   (la mayoría de usuarios)
  95% in 0.4500 secs   (usuarios lentos)
  99% in 1.1000 secs   (el peor caso: p99)
```

> **p99 (la cola larga):** el 1 % de los usuarios sufre esa espera. En sistemas a escala, el 1 % pueden ser miles de personas. Causas comunes de un p99 alto: bloqueos en base de datos, pausas del Garbage Collector y serialización JSON excesiva.

### 3. El experimento: baseline con 5000 peticiones

Resultados reales contra `/orders` (5000 peticiones, 50 concurrentes):

```text
Latency distribution:
  50% in 0.0324 secs   (todo va bien...)
  75% in 0.0422 secs
  90% in 0.0529 secs
  95% in 0.0659 secs
  99% in 1.3049 secs   <-- ¡EL PRECIPICIO!
```

**Pregunta clave:** ¿por qué la media (Average: 0,06 s) no refleja este problema?
**Respuesta:** porque el 99 % de datos rápidos "diluyen" a ese 1 % extremadamente lento. Por eso siempre se mira el p99.

**Salud del runtime con `dotnet-counters`** (mientras corría la prueba; `dotnet-counters monitor --processid <PID> --counters System.Runtime`):

| Contador          | Valor observado | Significado                                                             |
| ----------------- | --------------- | ----------------------------------------------------------------------- |
| `gen0`            | ~919.000        | Objetos de vida corta: casi un millón                                   |
| `gen2`            | ~3.000          | Objetos que sobrevivieron y pasaron a larga duración (caros de limpiar) |
| `gc.pause.time`   | 1,131 s         | Tiempo que la CPU dejó de trabajar para limpiar RAM                     |
| `total_allocated` | 6,67 GB         | Memoria total solicitada durante el test                                |

### 4. Diagnóstico: "Allocation Storm"

Existe una correlación directa entre el p99 y las pausas del GC:

```text
Latencia p99 ≈ Tiempo de procesamiento + Pausa del GC
1,30 s       ≈ 0,03 s                  + 1,13 s
```

**Causa raíz:** el endpoint `/orders` sufre asignaciones de memoria excesivas.

1. Trae 1000 filas de la base de datos.
2. Crea 1000 objetos `Order` en el heap.
3. Serializa esos 1000 objetos a JSON (creando más strings en el heap).

Esto ocurre 5000 veces y genera unos 6 GB de basura en segundos. El Garbage Collector congela la aplicación (_Stop-The-World_) para limpiar, y las peticiones de ese momento tardan 1,3 s.

### 5. La optimización

Se transforma el endpoint "ingenuo" en uno profesional con tres técnicas: **paginación** en el servidor (evita el _over-fetching_), **`AsNoTracking()`** (lecturas ultrarrápidas en EF Core) y **proyección con `.Select`** (menos tráfico y menos objetos).

```csharp
// Program.cs
// 1. Aceptamos parámetros de paginación (con valores por defecto seguros)
app.MapGet("/orders", async (OrdersDbContext db, int page = 1, int pageSize = 20) =>
{
    return await db.Orders
        // 2. AsNoTracking: vital para lecturas. Evita que EF vigile cambios (ahorra CPU/RAM).
        .AsNoTracking()
        .OrderByDescending(o => o.CreatedAt)
        // 3. Paginación eficiente en SQL (OFFSET/FETCH)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        // 4. Proyección: solo las columnas necesarias, no el objeto entero
        .Select(o => new
        {
            o.Id,
            Date = o.CreatedAt,
            o.Total
        })
        .ToListAsync();
});
```

**¿Por qué funciona?**

- **Antes:** SQL Server enviaba 1000 filas → .NET creaba 1000 objetos → se serializaban 1000 objetos a JSON.
- **Ahora:** SQL Server envía 20 filas → .NET crea 20 objetos. La presión sobre la memoria (_allocations_) baja drásticamente.

### 6. Resultados (baseline)

Escenario: `hey -n 5000 -c 50`.

| Métrica           | Antes      | Después (optimizado) | Análisis                                 |
| ----------------- | ---------- | -------------------- | ---------------------------------------- |
| Requests/sec      | ~626 req/s | ~1.382 req/s         | ×2,2: se duplica la capacidad            |
| Latencia media    | 0,068 s    | 0,033 s              | Respuesta el doble de rápida             |
| **p99 (crítico)** | **1,30 s** | **0,06 s**           | **×20:** se eliminan los bloqueos del GC |
| Pausa de GC       | ~1,13 s    | ~0,37 s              | El servidor trabaja más fluido           |

**Interpretación:** el cambio más espectacular es el p99. Antes, 1 de cada 100 usuarios sufría una espera de 1,3 s (por el GC limpiando 6 GB de basura). Ahora el peor caso es 0,06 s y la experiencia es consistentemente rápida.

**Documentación del proyecto:** como buena práctica, crea `docs/performance_log.md` en la solución y pega la tabla anterior. Demuestra el valor del trabajo técnico en términos de negocio: _"hemos duplicado la capacidad del servidor sin gastar más en hardware"_.

### 7. Reglas de oro de EF Core (cheat sheet)

1. **Lecturas:** usa siempre `AsNoTracking()` si no vas a modificar los datos.
2. **Listas:** jamás uses `ToList()` sin un `Take()` o paginación previa.
3. **Select:** proyecta siempre a un DTO o tipo anónimo (`.Select(x => new { ... })`). Evita traer columnas innecesarias (especialmente BLOBs o textos largos).

### 8. Verificación

- [ ] Tengo `hey.exe` (~8 MB) en la carpeta del proyecto y lo ejecuto con `.\` delante.
- [ ] He identificado mi throughput (Req/sec) y mi p99, y entiendo que el p99 representa a los usuarios con peor experiencia.
- [ ] He ejecutado `dotnet-counters monitor` y he visto subir las asignaciones.
- [ ] He comprobado que mi p99 es mucho más alto que mi p50.
- [ ] Tras aplicar paginación, `AsNoTracking()` y `.Select`, he vuelto a medir con `hey` y he documentado la mejora.
