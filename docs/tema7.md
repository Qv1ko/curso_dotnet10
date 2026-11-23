# Tema 7 - Comunicación Asíncrona y Mensajería

## Índice

**Presentación: De la comunicación síncrona a la asíncrona**

1. Comparación entre comunicación síncrona y asíncrona
2. Limitaciones del modelo síncrono en aplicaciones web
3. Introducción a la comunicación asíncrona y eventos
4. Arquitectura basada en Message Broker, colas y topics
5. Conclusión

**Manual I: Colas, Topics y Retos de la Comunicación Asíncrona**

1. Fundamentos de la Comunicación Asíncrona y Desacoplamiento
2. Patrones de Mensajería: Colas, Topics y Pub/Sub
3. Retos Inherentes de la Comunicación Asíncrona
4. Estrategias de Resiliencia y Manejo de Errores
5. Estrategias de Consistencia en Sistemas Distribuidos

**Cuadernillos del Alumno (laboratorio)**

- 7.1 Introducción a la Mensajería
- 7.2 Implementando el EventBus
- 7.3 El Worker Service

---

# Presentación: De la comunicación síncrona a la asíncrona

_Arquitectura de mensajería moderna._

**Agenda:**

- Comparación entre comunicación síncrona y asíncrona
- Limitaciones del modelo síncrono en web
- Introducción a comunicación asíncrona y eventos
- Arquitectura con Message Broker, colas y topics

## 1. Comparación entre comunicación síncrona y asíncrona

### Diferencias fundamentales: llamada telefónica vs. correo electrónico

|                     | Síncrona                                                        | Asíncrona                                                                             |
| ------------------- | --------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| **Características** | Interacción en tiempo real, requiere disponibilidad simultánea. | Mensajes enviados y recibidos en distintos momentos, no requiere respuesta inmediata. |
| **Ventajas**        | Respuestas rápidas, mayor claridad en la comunicación.          | Flexibilidad, permite organizar mejor el tiempo y recursos.                           |
| **Desventajas**     | Menor flexibilidad, interrupciones posibles.                    | Retrasos en la respuesta, posible falta de inmediatez.                                |
| **Ejemplos**        | Llamada telefónica, videollamada.                               | Correo electrónico, mensajería instantánea sin respuesta inmediata.                   |

### Ventajas y desventajas de cada enfoque

**Comunicación síncrona: pros y contras.** Asegura respuestas rápidas e interacción dinámica, ideal para decisiones inmediatas y coordinación en tiempo real. Sin embargo, puede generar interrupciones frecuentes y depende de la disponibilidad simultánea, lo que dificulta el trabajo concentrado.

**Comunicación asíncrona: pros y contras.** Ofrece mayor flexibilidad, mejor gestión del tiempo y trazabilidad de decisiones. Puede provocar demoras y malentendidos por falta de feedback instantáneo; aun así, el 63% de profesionales la prefiere para reducir interrupciones.

### Implicaciones para la experiencia del usuario

- **Interfaces reactivas y no bloqueantes:** la comunicación asíncrona reduce esperas visibles y evita frustraciones.
- **Reducción de abandonos por espera:** estudios señalan hasta un 40% de abandonos por respuestas lentas.
- **Continuidad y satisfacción del uso:** los procesos complejos siguen en segundo plano mientras el usuario avanza.

## 2. Limitaciones del modelo de comunicación síncrona en aplicaciones web

> **20%** de reducción de retención cuando la respuesta supera 3 segundos.

### Ejemplo práctico: escenario de pago

1. **Iniciar pago:** el usuario confirma el método y envía la solicitud.
2. **Procesamiento síncrono:** la interfaz muestra "procesando" y se bloquea.
3. **Umbral de espera crítica:** tras 5 s, el 25% abandona por incertidumbre.
4. **Alternativa asíncrona:** la confirmación diferida libera la UI y mantiene el flujo.

### Problemas de escalabilidad y eficiencia

- **Limitaciones de la comunicación síncrona:** cada solicitud requiere respuesta inmediata, lo que consume recursos y genera cuellos de botella bajo alta demanda.
- **Impacto en la escalabilidad:** las arquitecturas síncronas sufren caídas de eficiencia superiores al 30% cuando aumenta el número de usuarios concurrentes.
- **Ventajas de modelos asíncronos:** permiten procesar solicitudes sin bloquear recursos, mejorando la eficiencia y adaptándose mejor a cargas variables.
- **Factores clave para la eficiencia:** gestión de recursos, tolerancia a la latencia y capacidad de adaptación son esenciales para sistemas escalables y eficientes.

## 3. Introducción a la comunicación asíncrona y eventos

### Concepto de eventos y mensajería diferida

- **Comunicación asíncrona:** emisión y recepción de eventos sin esperar respuestas inmediatas.
- **Desacoplamiento y resiliencia:** el emisor no bloquea; los componentes evolucionan y fallan de forma aislada.
- **Cola de eventos:** buffer intermedio que almacena mensajes hasta su procesamiento.
- **Impacto según Gartner:** las arquitecturas basadas en eventos mejoran escalabilidad y flexibilidad hasta un 50%.

> _"La comunicación asíncrona es como dejar una carta en el buzón: depositas tu mensaje y sigues adelante, confiando en que será recibido cuando corresponda."_ — Analogía del buzón

### Beneficios para la arquitectura y el rendimiento

- **Rendimiento:** los procesos no bloquean recursos esperando respuestas, lo que permite un uso más eficiente de la infraestructura y una mejora significativa en el tiempo de respuesta.
- **Escalabilidad:** la arquitectura desacoplada facilita la adición de nuevos servicios o módulos sin afectar el sistema global, permitiendo crecer según la demanda.
- **Resiliencia:** sistemas tolerantes a fallos, capaces de aislar y recuperar componentes individuales sin afectar la operación general.
- **Mantenimiento:** facilita actualizaciones y cambios en componentes específicos sin interrumpir el funcionamiento del sistema completo.

## 4. Arquitectura basada en Message Broker, colas y topics

### El Message Broker como centro neurálgico

- **Comunicación desacoplada:** intermedia entre productores y consumidores, recibiendo, almacenando y distribuyendo mensajes para reducir dependencias directas y simplificar la integración.
- **Entrega confiable y resiliencia:** garantiza confirmaciones, reintentos y persistencia de mensajes, manteniendo la integridad de datos incluso ante fallos o latencias temporales.
- **Escalabilidad e impacto medible:** balancea carga y absorbe picos de tráfico de forma eficiente. Bien implementado, mejora la disponibilidad hasta un 40% y reduce la pérdida de datos en entornos distribuidos.

### Patrón Pub/Sub: publicación y suscripción de eventos

- **Productores (Publishers):** generan y envían eventos a un canal o topic sin conocer a los consumidores finales, permitiendo desacoplamiento y flexibilidad.
- **Consumidores (Subscribers):** se suscriben a topics específicos para recibir solo los eventos relevantes, facilitando la personalización y el filtrado de información.
- **Publicación y suscripción:** los eventos se publican en canales centralizados; los consumidores eligen los canales de interés, optimizando el flujo de datos en tiempo real.
- **Beneficios: escalabilidad y flexibilidad:** el patrón Pub/Sub permite crecer horizontalmente y adaptarse a nuevos requisitos sin modificar el núcleo de la arquitectura.

### Elementos clave: Publisher, Topic/Exchange, Queue y Subscriber

Arquitectura modular de eventos que desacopla componentes para escalar y ser resiliente; implementaciones comunes incluyen RabbitMQ, Kafka y AWS SNS/SQS.

- **Publisher: origen de eventos.** Servicio que emite eventos sin conocer a los consumidores, promoviendo bajo acoplamiento.
- **Topic/Exchange: enrutamiento.** Clasifica y enruta mensajes según reglas o suscripciones, optimizando la distribución.
- **Queue: almacenamiento confiable.** Mantiene mensajes hasta ser consumidos, suavizando picos y mejorando resiliencia.
- **Subscriber: procesamiento.** Consume eventos y ejecuta lógica de negocio, escalando horizontalmente según demanda.

## 5. Conclusión: comunicación más eficiente y escalable

- **Eficiencia operativa:** la adopción de comunicación asíncrona reduce bloqueos y mejora el flujo de procesos.
- **Mejor UX:** interfaces más responsivas al desacoplar tareas lentas del hilo de interacción.
- **Escalabilidad:** mayor capacidad de crecimiento mediante desacoplamiento y procesamiento distribuido.
- **Arquitectura moderna:** eventos, Message Brokers y Pub/Sub habilitan sistemas resilientes y flexibles.

---

# Manual I: Colas, Topics y Retos de la Comunicación Asíncrona

## 1. Fundamentos de la Comunicación Asíncrona y Desacoplamiento

### 1.1. Limitaciones de la Comunicación Síncrona en Sistemas Distribuidos

En el diseño de arquitecturas de microservicios, la comunicación síncrona (típicamente mediante HTTP/REST o gRPC) es intuitiva y directa. Cuando el Servicio A necesita que el Servicio B realice una acción, realiza una petición de red y espera la respuesta. Sin embargo, este enfoque presenta limitaciones severas cuando el sistema crece en complejidad y volumen de tráfico.

La principal limitación es el **acoplamiento temporal**. En una llamada síncrona, tanto el emisor como el receptor deben estar operativos y disponibles exactamente en el mismo instante. Si el Servicio B está caído por mantenimiento, experimenta un pico de latencia o sufre un reinicio, la petición del Servicio A fallará o se quedará bloqueada esperando. Esto provoca un efecto dominó: la indisponibilidad de un servicio secundario puede hacer caer la operación principal de cara al usuario.

Además, la comunicación síncrona dificulta la escalabilidad frente a picos de carga repentinos. Si un sistema de comercio electrónico recibe miles de peticiones de compra por segundo durante una promoción, y el servicio de facturación (llamado de forma síncrona) solo puede procesar cien por segundo, el sistema entero colapsará. Los recursos del Servicio A se agotarán esperando las respuestas del Servicio B, consumiendo hilos de ejecución, memoria y conexiones de red.

### 1.2. Beneficios del Desacoplamiento mediante Mensajería

La comunicación asíncrona mediante un intermediario (broker de mensajes) resuelve el acoplamiento temporal introduciendo un búfer entre los servicios. El Servicio A (productor) envía un mensaje al broker y recibe una confirmación inmediata de que el mensaje ha sido almacenado de forma segura. A partir de ese momento, el Servicio A puede continuar con su trabajo, delegando la responsabilidad de la entrega al broker.

El Servicio B (consumidor) se conectará al broker y procesará los mensajes a su propio ritmo. Este modelo aporta beneficios arquitectónicos fundamentales:

1. **Independencia temporal:** el productor y el consumidor no necesitan estar activos simultáneamente. Si el consumidor está caído, los mensajes se acumulan de forma segura en el broker hasta que el servicio se recupere, momento en el cual procesará el trabajo atrasado sin pérdida de datos.
2. **Nivelación de carga (Load Leveling):** el broker actúa como un amortiguador ante picos de tráfico. Por ejemplo, el productor puede generar 10.000 mensajes por segundo durante un pico mientras el consumidor procesa 500 por segundo. El sistema no colapsa; simplemente la cola crece temporalmente y se vacía cuando el pico de tráfico disminuye.
3. **Aislamiento de fallos:** un error o lentitud en un servicio consumidor no afecta al rendimiento ni a la disponibilidad del servicio productor. La experiencia del usuario final se mantiene rápida y fluida, ya que la petición inicial se responde en cuanto el mensaje entra al broker.

## 2. Patrones de Mensajería: Colas, Topics y Pub/Sub

Para implementar la comunicación asíncrona, los brokers de mensajes ofrecen diferentes estructuras lógicas. Comprender la diferencia entre colas y topics es esencial para diseñar correctamente la topología de red de nuestros microservicios.

### 2.1. Colas de Mensajes (Procesamiento Punto a Punto)

#### 2.1.1. Concepto y Casos de Uso de las Colas

Una cola (Queue) implementa un patrón de comunicación punto a punto (Point-to-Point). En este modelo, el productor envía un mensaje a una cola específica, y ese mensaje será entregado a **un único consumidor**, independientemente de cuántos consumidores estén conectados a dicha cola.

Este patrón se utiliza para distribuir trabajo. Cuando múltiples instancias de un mismo microservicio (consumidores competidores o _competing consumers_) se conectan a la misma cola, el broker distribuye los mensajes entre ellos, típicamente usando un algoritmo de Round-Robin. Si el mensaje 1 es procesado por la Instancia A, la Instancia B nunca verá ese mensaje.

Las colas son la elección correcta cuando se trata de **comandos**: instrucciones imperativas que indican que una acción específica debe llevarse a cabo ("GenerarFactura", "EnviarCorreo", "ProcesarPago"). El objetivo es que la acción se ejecute una sola vez, repartiendo la carga de trabajo entre todos los trabajadores disponibles.

#### 2.1.2. Ejemplo Práctico: Nivelación de Carga en Procesamiento de Imágenes

Imaginemos una aplicación donde los usuarios suben imágenes de alta resolución que deben ser redimensionadas y convertidas a diferentes formatos. Este es un proceso intensivo en CPU. Si se hiciera de forma síncrona, el servidor web se bloquearía rápidamente.

**Diseño con Colas:**

1. El usuario sube la imagen. La API web (Productor) guarda el archivo original en un almacenamiento en la nube (como Azure Blob Storage o AWS S3).
2. La API web crea un mensaje JSON con el formato: `{"ImageId": "12345", "Path": "/raw/12345.jpg", "TargetFormat": "webp"}`.
3. La API envía este mensaje a una cola llamada `image-processing-queue`.
4. La API responde inmediatamente al usuario con un código HTTP 202 (Accepted), indicando que el proceso ha comenzado.
5. En el backend, tenemos tres instancias de un microservicio de procesamiento de imágenes (Worker 1, Worker 2, Worker 3) escuchando la misma cola.
6. El broker entrega el mensaje al Worker 1. El Worker 1 descarga la imagen, la procesa, guarda el resultado y confirma (ACK) al broker que ha terminado.
7. Si llegan 100 imágenes de golpe, los mensajes se encolan. Los tres workers irán tomando mensajes hasta vaciar la cola. En funcionamiento normal cada mensaje se entrega a un solo worker; sin embargo, si un worker cae después de procesar una imagen y antes de confirmarla, el broker la entregará de nuevo a otro worker (entrega "al menos una vez", apartado 3.2). Por eso el procesamiento debe poder repetirse sin efectos indeseados, por ejemplo sobrescribiendo el archivo de destino en lugar de crear uno nuevo.

### 2.2. Topics y Difusión a Múltiples Consumidores

#### 2.2.1. Concepto y Casos de Uso de los Topics

Un Topic (Tema) implementa un patrón de comunicación de difusión (Publish/Subscribe o Pub/Sub). A diferencia de las colas, cuando un productor envía un mensaje a un topic, el broker crea una copia lógica de ese mensaje para **cada una** de las suscripciones conectadas a dicho topic.

Este patrón se utiliza para notificar a múltiples partes interesadas que algo ha ocurrido, sin que el productor necesite saber quiénes son esas partes. Es el mecanismo fundamental para la arquitectura dirigida por eventos (Event-Driven Architecture).

Los topics son la elección correcta cuando se trata de **eventos**: hechos que ya han ocurrido en el pasado ("UsuarioRegistrado", "PedidoPagado", "InventarioActualizado"). El productor simplemente anuncia el hecho al mundo (al topic), y cualquier microservicio al que le interese esa información puede crear una suscripción para recibir su propia copia del mensaje.

#### 2.2.2. Ejemplo Práctico: Notificaciones Multicanal tras Registro de Usuario

Supongamos que en nuestra plataforma, cuando un nuevo usuario se registra, deben ocurrir tres cosas completamente independientes:

- Enviar un correo electrónico de bienvenida.
- Registrar los datos del usuario en el sistema CRM de ventas.
- Asignar un cupón de descuento inicial en el sistema de promociones.

Hacer esto de forma síncrona acoplaría el servicio de identidad con el servicio de correos, el CRM y el motor de promociones.

**Diseño con Topics:**

1. El usuario completa el formulario de registro en el Servicio de Identidad.
2. El Servicio de Identidad guarda el usuario en su base de datos.
3. El Servicio de Identidad publica un evento en el topic `user-events`. El mensaje es: `{"EventType": "UserRegistered", "UserId": "987", "Email": "nuevo@example.com"}`.
4. En el broker, existen tres suscripciones configuradas para este topic: `sub-email`, `sub-crm` y `sub-promotions`.
5. El broker recibe el único mensaje del productor y lo clona, depositando una copia en cada una de las tres suscripciones.
6. El Microservicio de Correos lee de `sub-email` y envía el correo.
7. El Microservicio CRM lee de `sub-crm` y actualiza el CRM de ventas.
8. El Microservicio de Promociones lee de `sub-promotions` y genera el cupón.
9. Si el Microservicio CRM está caído, su copia del mensaje permanecerá segura en la suscripción `sub-crm`. Los servicios de correo y promociones procesarán sus copias sin interrupción. El Servicio de Identidad no sabe ni le importa cuántos sistemas reaccionan al registro.

### 2.3. Modelos Pub/Sub Basados en Eventos

#### 2.3.1. Diferencia Arquitectónica entre Comandos y Eventos

Para diseñar sistemas distribuidos robustos, es imperativo distinguir conceptualmente entre un Comando y un Evento, ya que determinan si usaremos una Cola o un Topic.

- **Comando (Cola):** expresa una intención. Es una orden directa de un sistema a otro. El emisor espera que el receptor ejecute una acción y, a menudo, el emisor sabe exactamente quién es el receptor. Ejemplos: `CobrarTarjetaCredito`, `GenerarPDF`. Si el comando falla, el proceso de negocio se detiene. Se enrutan a través de Colas (Punto a Punto).
- **Evento (Topic):** expresa un hecho consumado. Es una notificación de que algo ya ha sucedido en el dominio del emisor. El emisor no tiene expectativas sobre lo que sucederá después ni sabe quién está escuchando. Ejemplos: `TarjetaCreditoCobrada`, `PDFGenerado`. Se enrutan a través de Topics (Pub/Sub).

Mezclar estos conceptos es un antipatrón común. Si el Servicio de Pedidos publica un evento llamado `EnviarCorreoConfirmacion` en un topic, está dictando el comportamiento de otro servicio, violando la autonomía de los microservicios. Lo correcto es que el Servicio de Pedidos publique el evento `PedidoConfirmado`, y sea el Servicio de Correos quien decida, de forma autónoma, suscribirse a ese evento y enviar un correo como reacción.

### 2.4. Ejemplo Práctico: Topología y Productor con RabbitMQ en .NET 10

RabbitMQ, que el alumno puede levantar en un contenedor local, no tiene un objeto llamado "topic" como tal. Sus piezas son:

- **Cola (queue):** almacena mensajes; con varios consumidores conectados, cada mensaje se entrega a uno solo (consumidores competidores).
- **Exchange:** recibe los mensajes del productor y los copia a las colas vinculadas (_bindings_) según su tipo: `fanout` copia a todas las colas vinculadas; `direct` y `topic` filtran por la clave de enrutamiento (_routing key_). El exchange predeterminado (nombre vacío) entrega directamente en la cola cuyo nombre coincide con la routing key.

El "topic" conceptual de este tema se construye con un exchange (por ejemplo, `fanout`) y una cola por suscriptor vinculada a él; cada cola tiene sus propios consumidores competidores. En Azure Service Bus, en cambio, colas, topics y suscripciones son objetos nativos del servicio.

Se usa el cliente oficial, paquete NuGet `RabbitMQ.Client` (versión 7), cuya API es completamente asíncrona:

```powershell
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:4-management

dotnet add package RabbitMQ.Client
```

**Topología.** Cada servicio declara lo que le pertenece: el productor, el exchange donde publica; el suscriptor, su cola, su DLQ y el vínculo con el exchange. Las declaraciones son idempotentes: repetirlas con los mismos parámetros no cambia nada (con parámetros distintos, el broker devuelve un error).

```csharp
using RabbitMQ.Client;

public static class Topologia
{
    public const string ExchangeEventos = "pedidos.eventos";          // "topic" de eventos de pedidos
    public const string ColaFacturacion = "facturacion.pedidos";      // suscripción de Facturación
    public const string ExchangeFallidos = "facturacion.dlx";
    public const string ColaFallidos = "facturacion.pedidos.dlq";

    // Lo declara el productor (Pedidos)
    public static Task DeclararEventosAsync(IChannel canal, CancellationToken ct = default) =>
        canal.ExchangeDeclareAsync(ExchangeEventos, ExchangeType.Fanout, durable: true, cancellationToken: ct);

    // Lo declara el suscriptor (Facturación)
    public static async Task DeclararFacturacionAsync(IChannel canal, CancellationToken ct = default)
    {
        await DeclararEventosAsync(canal, ct);

        // Destino de los mensajes fallidos (apartado 4.3)
        await canal.ExchangeDeclareAsync(ExchangeFallidos, ExchangeType.Fanout, durable: true, cancellationToken: ct);
        await canal.QueueDeclareAsync(ColaFallidos, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);
        await canal.QueueBindAsync(ColaFallidos, ExchangeFallidos, routingKey: string.Empty, cancellationToken: ct);

        await canal.QueueDeclareAsync(ColaFacturacion, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",                    // cola quorum (replicada); sin este argumento, la cola es "clásica"
                ["x-dead-letter-exchange"] = ExchangeFallidos,  // adónde van los mensajes rechazados
                ["x-delivery-limit"] = 5                        // entregas fallidas máximas (apartado 4.3.1)
            }, cancellationToken: ct);
        await canal.QueueBindAsync(ColaFacturacion, ExchangeEventos, routingKey: string.Empty, cancellationToken: ct);
    }
}
```

**Productor con confirmaciones del broker (_publisher confirms_).** Sin confirmaciones, el productor no sabe si el broker guardó el mensaje. En RabbitMQ.Client 7 se activan al crear el canal; a partir de ahí, `await BasicPublishAsync(...)` no termina hasta que el broker confirma, y lanza una excepción si el broker rechaza el mensaje (nack) o lo devuelve por no poder enrutarlo (con `mandatory: true`, por ejemplo si ninguna cola está vinculada todavía al exchange).

```csharp
using System.Text.Json;
using RabbitMQ.Client;

public record PedidoCreado(Guid PedidoId, string ClienteId, decimal Importe);

public static class Productor
{
    public static async Task PublicarAsync(IConnection conexion, PedidoCreado evento, CancellationToken ct = default)
    {
        await using var canal = await conexion.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                     publisherConfirmationTrackingEnabled: true), ct);
        await Topologia.DeclararEventosAsync(canal, ct);

        var propiedades = new BasicProperties
        {
            Persistent = true,                         // el broker lo guarda en disco
            MessageId = Guid.NewGuid().ToString(),     // identificador para la idempotencia (4.1.1)
            ContentType = "application/json",
            Type = nameof(PedidoCreado)
        };

        await canal.BasicPublishAsync(Topologia.ExchangeEventos, routingKey: string.Empty,
            mandatory: true, basicProperties: propiedades,
            body: JsonSerializer.SerializeToUtf8Bytes(evento), cancellationToken: ct);
    }
}
```

La conexión (`IConnection`) es costosa y está pensada para durar toda la vida de la aplicación; se crea con `await new ConnectionFactory { HostName = "localhost" }.CreateConnectionAsync()`. Los canales (`IChannel`) son ligeros y no deben compartirse entre hilos que publican a la vez.

Publicar así, justo después de guardar en la base de datos, tiene un defecto: si el broker no está disponible, el pedido queda guardado y el evento se pierde. El apartado 5.2 explica cómo evitarlo.

## 3. Retos Inherentes de la Comunicación Asíncrona

Adoptar mensajería asíncrona resuelve el acoplamiento temporal, pero introduce una nueva categoría de problemas derivados de la naturaleza de los sistemas distribuidos. Ignorar estos retos conduce a sistemas frágiles y datos inconsistentes.

### 3.1. El Mito del Broker de Mensajes Perfecto

Un error fundamental en el diseño de arquitecturas distribuidas es asumir que la red es fiable y que el broker de mensajes funcionará siempre de manera impecable. Las falacias de la computación distribuida nos advierten que las particiones de red ocurrirán, los tiempos de espera (timeouts) se agotarán y los nodos se reiniciarán.

Cuando un productor envía un mensaje al broker, o cuando un consumidor confirma (ACK) el procesamiento de un mensaje, esa comunicación viaja por la red. Si la red falla en el milisegundo exacto en que viaja la confirmación, el estado entre el servicio y el broker quedará desincronizado. Para construir sistemas robustos, debemos diseñar nuestros microservicios asumiendo que el broker cometerá errores en la entrega.

### 3.2. El Problema de los Mensajes Duplicados (At-Least-Once Delivery)

Los brokers de mensajes más usados (RabbitMQ, Azure Service Bus, Amazon SQS) ofrecen, con confirmaciones activadas, una garantía de entrega **At-Least-Once** (al menos una vez): el mensaje confirmado no se pierde, pero a cambio es posible que se entregue más de una vez.

Apache Kafka también trabaja por defecto con esta garantía y, además, ofrece semántica _exactly-once_ mediante productores idempotentes y transacciones. Esa garantía cubre los flujos que leen y escriben en el propio Kafka; en cuanto el consumidor produce efectos fuera de él (escribir en una base de datos, llamar a una API), vuelve a necesitar las técnicas de este tema.

¿Por qué ocurre esto? Analicemos el ciclo de vida de consumo:

1. El consumidor recibe el mensaje del broker. En brokers tradicionales basados en colas, el mensaje queda "invisible" para otros consumidores temporalmente.
2. El consumidor procesa el mensaje (por ejemplo, inserta un registro en su base de datos).
3. El consumidor envía una señal de reconocimiento (ACK) al broker para que elimine el mensaje definitivamente. (Nota: Apache Kafka utiliza un modelo diferente basado en un registro inmutable y offsets, donde los mensajes no se eliminan al consumirlos; el equivalente al ACK es confirmar el offset).

Si ocurre un fallo de red, un reinicio del contenedor o un timeout justo en el paso 3 (después de procesar, pero antes de que el broker reciba el ACK), el broker asumirá que el consumidor falló y volverá a entregar el mensaje a otro consumidor (o al mismo tras reiniciarse). En Azure Service Bus o Amazon SQS esto ocurre cuando expira el bloqueo o el tiempo de invisibilidad; en RabbitMQ, cuando se cierra el canal o la conexión con el mensaje sin confirmar.

El productor también puede duplicar: si publica, el broker guarda el mensaje y la confirmación se pierde por la red, el productor lo reintentará y el mensaje quedará dos veces en la cola.

El resultado: el consumidor procesará el mismo mensaje dos veces. Si el mensaje era "Sumar 100 € al saldo", el usuario recibirá 200 €. Este es un fallo crítico de negocio que no puede permitirse.

### 3.3. El Problema de los Mensajes Fuera de Orden

Otro reto significativo es la pérdida del orden estricto. Aunque enviemos los mensajes A, B y C en ese orden exacto, no hay garantía absoluta de que se procesen en el orden A, B, C. Existen múltiples razones para esto:

- **Consumidores concurrentes:** si tenemos tres instancias leyendo de la misma cola, la Instancia 1 toma el mensaje A, la Instancia 2 toma el B. Si la Instancia 1 es ligeramente más lenta (por ejemplo, por una recolección de basura en memoria o lentitud en su conexión a base de datos), la Instancia 2 terminará de procesar el mensaje B antes de que la Instancia 1 termine con el A.
- **Reintentos:** si el mensaje A falla en su primer intento y es devuelto a la cola, el mensaje B será procesado con éxito. Segundos después, el mensaje A se reintentará y tendrá éxito. El orden final de procesamiento habrá sido B, luego A.

Si los mensajes representan cambios de estado dependientes (por ejemplo, A = "Crear Cliente", B = "Actualizar Dirección del Cliente"), procesar B antes que A resultará en un error, ya que el cliente aún no existe en la base de datos del consumidor.

## 4. Estrategias de Resiliencia y Manejo de Errores

Para mitigar los retos descritos anteriormente, la responsabilidad de la consistencia debe recaer en los extremos (los microservicios) y no en el canal de comunicación (el broker).

### 4.1. Idempotencia en el Consumidor

La solución al problema de los mensajes duplicados es diseñar consumidores idempotentes. Una operación es idempotente si aplicarla una vez tiene exactamente el mismo efecto en el estado del sistema que aplicarla múltiples veces.

En matemáticas, multiplicar por 1 es una operación idempotente. En bases de datos, un `UPDATE` absoluto (`UPDATE Saldo SET Cantidad = 100`) es idempotente, mientras que un `UPDATE` relativo (`UPDATE Saldo SET Cantidad = Cantidad + 100`) no lo es.

Dado que no siempre podemos transformar la lógica de negocio en operaciones naturalmente idempotentes, la técnica estándar en sistemas distribuidos es implementar la idempotencia artificialmente mediante el seguimiento de identificadores únicos.

#### 4.1.1. Implementación Práctica de Idempotencia con Claves Únicas

Para lograr esto, cada mensaje que viaja por el sistema debe incluir un identificador único inmutable, típicamente un UUID, conocido como `MessageId` o `IdempotencyKey`.

El consumidor mantendrá una tabla en su base de datos dedicada exclusivamente a registrar los IDs de los mensajes que ya ha procesado con éxito.

**Flujo de ejecución paso a paso:**

1. El consumidor recibe el mensaje con `MessageId = "550e8400-e29b-41d4-a716-446655440000"`.
2. El consumidor inicia una transacción de base de datos.
3. El consumidor consulta la tabla `ProcessedMessages` buscando ese ID.
4. **Si el ID ya existe:** significa que es un mensaje duplicado. El consumidor ignora la lógica de negocio, confirma (ACK) el mensaje al broker inmediatamente y termina.
5. **Si el ID no existe:**
   - Ejecuta la lógica de negocio (ej. actualiza el saldo).
   - Inserta el `MessageId` en la tabla `ProcessedMessages`.
   - Hace un `COMMIT` de la transacción de base de datos.
6. Envía el ACK al broker.

Si el sistema falla antes del paso 6, el broker reenviará el mensaje. En el segundo intento, el paso 4 detectará el ID en la base de datos y detendrá el procesamiento, evitando el doble cobro. Es crucial que la inserción en `ProcessedMessages` y la lógica de negocio ocurran dentro de la **misma transacción** de base de datos para garantizar la atomicidad.

Hay un tercer requisito que la consulta del paso 3 no cubre por sí sola: la columna `MessageId` debe tener una **restricción de unicidad** (clave primaria o índice único). Si dos copias del mismo mensaje llegan a la vez a dos instancias, ambas pueden consultar la tabla antes de que la otra confirme, no encontrar el ID y ejecutar la lógica. Con la restricción, el `COMMIT` de la segunda falla por clave duplicada, su transacción se deshace entera (incluida la lógica de negocio) y el consumidor la trata como un duplicado: confirma el mensaje sin más. La consulta previa sigue siendo útil como atajo para el caso habitual, pero la garantía la da la restricción.

Otra consecuencia del flujo: el ACK se envía siempre **después** del `COMMIT`. Si se confirmara antes (o con confirmación automática), un fallo durante el procesamiento haría que el broker ya hubiera eliminado un mensaje que nunca se procesó.

### 4.2. Políticas de Reintento (Retry Policies)

Cuando un consumidor intenta procesar un mensaje y ocurre una excepción, no debemos descartar el mensaje inmediatamente. Los errores en sistemas distribuidos se dividen en dos categorías:

- **Errores Transitorios:** son fallos temporales que probablemente se resuelvan por sí solos si esperamos un poco. Ejemplos: un microcorte de red, un bloqueo temporal en la base de datos (deadlock), o una API externa devolviendo un error HTTP 503 (Service Unavailable) o 429 (Too Many Requests).
- **Errores Permanentes:** son fallos lógicos que nunca se resolverán por mucho que reintentemos. Ejemplos: un mensaje con un JSON mal formado que no se puede deserializar, una violación de clave foránea en la base de datos, o una regla de negocio no cumplida (ej. saldo insuficiente).

Para los errores transitorios, la estrategia correcta es devolver el mensaje a la cola para que sea reintentado.

#### 4.2.1. Estrategias de Backoff Exponencial y Jitter

Reintentar un mensaje inmediatamente y en bucle cerrado es peligroso. Si la base de datos está sufriendo por alta carga, bombardearla con reintentos inmediatos solo empeorará la situación y puede prolongar la caída.

La mejor práctica es implementar un **Backoff Exponencial** (retroceso exponencial). Esto significa que el tiempo de espera entre reintentos aumenta exponencialmente.

- Intento 1 falla → esperar 2 segundos.
- Intento 2 falla → esperar 4 segundos.
- Intento 3 falla → esperar 8 segundos.
- Intento 4 falla → esperar 16 segundos.

Esto da tiempo a que el sistema subyacente se recupere. Además, para evitar el problema de la "manada en estampida" (_thundering herd_) —donde muchos mensajes encolados se reintentan a la vez tras una caída— se debe añadir **Jitter** (aleatoriedad) al tiempo de espera. En lugar de esperar exactamente 8 segundos, el sistema espera 8 segundos más un valor aleatorio entre 0 y 2 segundos. Esto dispersa la carga de los reintentos a lo largo del tiempo. En .NET, estas políticas se implementan con Polly y `Microsoft.Extensions.Resilience`, que se estudian en el tema 13; algunos brokers ofrecen además reentregas diferidas (por ejemplo, Azure Service Bus permite programar un mensaje para más tarde), mientras que RabbitMQ devuelve inmediatamente a la cola el mensaje rechazado con reencolado.

### 4.3. Colas de Mensajes Fallidos (Dead Letter Queues, DLQ)

¿Qué ocurre cuando un mensaje sufre un error permanente, o cuando ha agotado su número máximo de reintentos transitorios (por ejemplo, ha fallado 5 veces seguidas)?

Si dejamos el mensaje en la cola principal, se convertirá en un "mensaje envenenado" (_Poison Pill_). El consumidor lo leerá, fallará, lo devolverá a la cola, lo volverá a leer, volverá a fallar, creando un bucle infinito que consume recursos y bloquea el procesamiento de mensajes válidos que vienen detrás.

Para evitar esto, los brokers de mensajes implementan el concepto de **Dead Letter Queue (DLQ)** o Cola de Mensajes Muertos. Es una cola secundaria asociada a la cola principal, destinada exclusivamente a almacenar mensajes que no pudieron ser procesados.

#### 4.3.1. Criterios de Enrutamiento a DLQ y Estrategias de Recuperación

Un mensaje debe ser movido a la DLQ bajo dos circunstancias principales:

1. **Exceso de entregas (Max Delivery Count):** el broker lleva la cuenta de cuántas veces ha fallado la entrega de un mensaje. Si este contador supera un umbral configurado (ej. 5 intentos), el broker mueve automáticamente el mensaje a la DLQ.
2. **Rechazo explícito:** si el consumidor detecta un error permanente (ej. error de validación de esquema), no debe reintentar y debe enviar el mensaje directamente a la DLQ con el mecanismo del broker.

**Cómo se concreta en cada broker:**

- **RabbitMQ:** la DLQ no existe por defecto. Hay que configurar en la cola un _dead-letter exchange_ (argumento `x-dead-letter-exchange` o, preferiblemente en producción, una política del servidor) y vincularle una cola. Un mensaje se "manda a la DLQ" cuando el consumidor lo rechaza con `BasicRejectAsync` o `BasicNackAsync` indicando `requeue: false`; si la cola no tiene dead-letter exchange, ese rechazo simplemente descarta el mensaje. Para el exceso de entregas, las colas quorum admiten `x-delivery-limit` (desde RabbitMQ 4.0 su valor por defecto es 20): cuando un mensaje supera el límite, se envía al dead-letter exchange o, si no hay, se elimina. Cuentan como entregas fallidas el rechazo con `BasicRejectAsync` y reencolado y el cierre del canal con el mensaje pendiente; en las versiones recientes, un `BasicNackAsync` con reencolado no cuenta para el límite, por eso los ejemplos de este tema usan `BasicRejectAsync`.
- **Azure Service Bus:** cada cola y suscripción tiene una DLQ integrada. El mensaje pasa a ella al superar `MaxDeliveryCount` (10 por defecto) o cuando el consumidor llama a `DeadLetterMessageAsync`.
- **Amazon SQS:** se configura una cola de destino con una _redrive policy_; el mensaje se mueve al superar `maxReceiveCount`.

**Operativa sobre la DLQ.** La DLQ no es un cementerio donde los mensajes se olvidan; es una sala de emergencias. Debe existir un proceso operativo claro:

1. **Monitorización:** cualquier mensaje que entre en una DLQ debe disparar una alerta al equipo de operaciones o desarrollo.
2. **Análisis:** un ingeniero debe inspeccionar el mensaje en la DLQ y los logs asociados para entender por qué falló. ¿Fue un bug en el código? ¿Faltaban datos en una tabla de referencia?
3. **Resolución y Re-procesamiento (Replay):** una vez solucionado el problema subyacente (por ejemplo, desplegando un parche en el código o insertando el dato faltante en la base de datos), los mensajes de la DLQ se "reproducen" (_replay_), moviéndolos de vuelta a la cola principal para que sean procesados con éxito.

### 4.4. Ejemplo Práctico: Consumidor Idempotente con BackgroundService

Este consumidor de Facturación reúne los apartados 4.1 a 4.3 sobre la topología del apartado 2.4. Se ejecuta como servicio hospedado: una clase que hereda de `BackgroundService` y se registra con `builder.Services.AddHostedService<T>()`, de modo que arranca y se detiene con la aplicación. Como un `BackgroundService` es singleton y el `DbContext` es de ámbito (_scoped_), cada mensaje crea su propio ámbito con `IServiceScopeFactory`.

Paquetes: `RabbitMQ.Client` y `Microsoft.EntityFrameworkCore.SqlServer` (o el proveedor que se use).

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public class Factura
{
    public int Id { get; set; }
    public Guid PedidoId { get; set; }
    public decimal Importe { get; set; }
}

public class MensajeProcesado
{
    public string MessageId { get; set; } = "";   // clave primaria: garantiza la unicidad
    public DateTime ProcesadoEn { get; set; }
}

public class FacturacionDbContext(DbContextOptions<FacturacionDbContext> options) : DbContext(options)
{
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<MensajeProcesado> MensajesProcesados => Set<MensajeProcesado>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MensajeProcesado>().HasKey(m => m.MessageId);
        modelBuilder.Entity<Factura>().Property(f => f.Importe).HasPrecision(18, 2);
    }
}

public sealed class ConsumidorPedidos(IServiceScopeFactory scopes, ILogger<ConsumidorPedidos> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fabrica = new ConnectionFactory { HostName = "localhost" };
        await using var conexion = await fabrica.CreateConnectionAsync(stoppingToken);
        await using var canal = await conexion.CreateChannelAsync(cancellationToken: stoppingToken);
        await Topologia.DeclararFacturacionAsync(canal, stoppingToken);

        // Como máximo 10 mensajes sin confirmar a la vez en este consumidor
        await canal.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumidor = new AsyncEventingBasicConsumer(canal);
        consumidor.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var messageId = ea.BasicProperties.MessageId
                    ?? throw new JsonException("Mensaje sin MessageId");
                var evento = JsonSerializer.Deserialize<PedidoCreado>(ea.Body.Span)
                    ?? throw new JsonException("Cuerpo vacío");

                await ProcesarAsync(messageId, evento);
                await canal.BasicAckAsync(ea.DeliveryTag, multiple: false);        // después del COMMIT
            }
            catch (JsonException ex)                                               // error permanente
            {
                logger.LogError(ex, "Mensaje no válido; se envía a la DLQ");
                await canal.BasicRejectAsync(ea.DeliveryTag, requeue: false);
            }
            catch (Exception ex)                                                   // se trata como transitorio
            {
                logger.LogWarning(ex, "Error al procesar; se devuelve a la cola");
                await canal.BasicRejectAsync(ea.DeliveryTag, requeue: true);      // cuenta para x-delivery-limit
            }
        };

        await canal.BasicConsumeAsync(Topologia.ColaFacturacion, autoAck: false,
            consumer: consumidor, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);                            // hasta que se detenga la app
    }

    private async Task ProcesarAsync(string messageId, PedidoCreado evento)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FacturacionDbContext>();

        if (await db.MensajesProcesados.AnyAsync(m => m.MessageId == messageId))
            return;                                                                   // duplicado: no se repite

        db.Facturas.Add(new Factura { PedidoId = evento.PedidoId, Importe = evento.Importe });
        db.MensajesProcesados.Add(new MensajeProcesado { MessageId = messageId, ProcesadoEn = DateTime.UtcNow });

        try
        {
            await db.SaveChangesAsync();          // una sola llamada: factura e ID en la misma transacción
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (!await db.MensajesProcesados.AnyAsync(m => m.MessageId == messageId))
                throw;                            // no era un duplicado: se reintentará
            // duplicado concurrente: otra instancia ya lo procesó; se confirma sin repetir
        }
    }
}
```

Registro en `Program.cs` (proyecto `dotnet new worker` o una API):

```csharp
builder.Services.AddDbContext<FacturacionDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Facturacion")));
builder.Services.AddHostedService<ConsumidorPedidos>();
```

**Puntos clave:**

- `autoAck: false`: el mensaje solo se elimina cuando el código llama a `BasicAckAsync`.
- `SaveChangesAsync` ejecuta todas las inserciones pendientes en una única transacción, así que la factura y el `MessageId` se guardan juntos o no se guarda ninguno.
- La clave primaria de `MensajeProcesado` hace fallar al duplicado concurrente; el `catch` comprueba que el fallo se debe a eso antes de darlo por procesado.
- Los errores permanentes (el mensaje no se puede interpretar) se rechazan sin reencolar y acaban en `facturacion.pedidos.dlq`. Los demás se devuelven a la cola; tras 5 entregas fallidas (`x-delivery-limit`), RabbitMQ los envía también a la DLQ.
- `ea.Body` solo es válido durante la ejecución del manejador; si hay que guardarlo para después, se copia con `ea.Body.ToArray()`.

## 5. Estrategias de Consistencia en Sistemas Distribuidos

Al abandonar las transacciones ACID monolíticas y distribuir los datos entre múltiples microservicios, mantener la consistencia de la información se convierte en el mayor desafío arquitectónico.

### 5.1. Consistencia Eventual frente a Transacciones Distribuidas

En un monolito, si necesitamos guardar un pedido y actualizar el inventario, lo hacemos dentro de una única transacción de base de datos. Si algo falla, se hace un rollback y nada cambia.

En microservicios, el Pedido vive en la base de datos del Servicio de Pedidos, y el Inventario en la base de datos del Servicio de Inventario. No podemos usar una transacción de base de datos tradicional que abarque ambos servicios.

Históricamente, se intentó resolver esto con protocolos de transacciones distribuidas como Two-Phase Commit (2PC). Sin embargo, estos protocolos requieren bloqueos sincrónicos a través de la red, lo que limita la escalabilidad, aumenta la latencia y hace depender a todos los participantes del coordinador. Además, muchos brokers y bases de datos en la nube no lo admiten. Por eso, en microservicios se evita.

La alternativa habitual es la **Consistencia Eventual**. Aceptamos que, durante un breve periodo de tiempo, el sistema estará en un estado inconsistente. El Servicio de Pedidos guardará el pedido y, milisegundos o segundos después, el Servicio de Inventario se actualizará. El sistema garantiza que, si no hay nuevas entradas, eventualmente todos los nodos convergerán a un estado consistente.

Para implementar la consistencia eventual de forma segura, dependemos de dos patrones fundamentales: el Patrón Outbox y el Patrón Saga.

### 5.2. El Patrón Outbox (Bandeja de Salida Transaccional)

El problema más crítico al publicar eventos es el **problema de la doble escritura** (_Dual Write Problem_).

Supongamos que el Servicio de Pedidos debe hacer dos cosas:

1. Guardar el pedido en su base de datos relacional.
2. Publicar el evento `PedidoCreado` en el broker de mensajes.

Si guardamos en la base de datos primero y luego intentamos publicar en el broker, el broker podría estar caído. Tendremos un pedido en base de datos, pero el resto del sistema nunca se enterará. Si publicamos en el broker primero y luego intentamos guardar en la base de datos, la base de datos podría fallar. El resto del sistema procesará un pedido que, para el Servicio de Pedidos, nunca existió.

No podemos envolver la base de datos y el broker en una sola transacción. La solución es el Patrón Outbox.

**Implementación del Patrón Outbox:**

1. En la base de datos del microservicio, creamos una tabla adicional llamada `OutboxMessages` (bandeja de salida).
2. Cuando el servicio necesita crear un pedido, abre una transacción de base de datos local.
3. Inserta el registro en la tabla `Pedidos`.
4. En la misma transacción, inserta el evento serializado (JSON) en la tabla `OutboxMessages`.
5. Hace el `COMMIT` de la transacción. Como ambas tablas están en la misma base de datos, la operación es atómica: o se guardan ambas cosas, o ninguna.
6. Un proceso se encarga de llevar los mensajes de `OutboxMessages` al broker. Hay dos variantes:
   - **Sondeo (polling):** un worker consulta periódicamente la tabla buscando mensajes no enviados.
   - **Change Data Capture (CDC):** una herramienta (por ejemplo, Debezium) lee el log de transacciones de la base de datos, no la tabla, y publica cada inserción en `OutboxMessages` según se produce.
7. En la variante de sondeo, el worker publica cada mensaje pendiente y, tras recibir la confirmación del broker, lo marca como procesado (o lo elimina).

Este patrón garantiza que todo evento derivado de un cambio de estado confirmado en la base de datos acabará publicado en el broker, sin transacciones distribuidas. Lo que no garantiza es que se publique una sola vez: si el worker publica un mensaje y cae antes de marcarlo como procesado, lo publicará de nuevo al reiniciarse. El Outbox ofrece entrega "al menos una vez", y por eso se combina siempre con consumidores idempotentes (apartado 4.1), usando el identificador del mensaje del Outbox como `MessageId`.

#### 5.2.1. Ejemplo Práctico: Outbox Mínimo con EF Core 10

El Servicio de Pedidos guarda el pedido y el evento con una sola llamada a `SaveChangesAsync`, y un `BackgroundService` publica los pendientes. Paquetes: `Microsoft.EntityFrameworkCore.SqlServer` y `RabbitMQ.Client`.

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public class Pedido
{
    public Guid Id { get; set; }
    public string ClienteId { get; set; } = "";
    public decimal Importe { get; set; }
}

public class MensajeOutbox
{
    public Guid Id { get; set; }                  // se publicará como MessageId
    public string Tipo { get; set; } = "";
    public string Contenido { get; set; } = "";   // evento serializado en JSON
    public DateTime OcurridoEn { get; set; }
    public DateTime? ProcesadoEn { get; set; }    // null = pendiente
}

public class PedidosDbContext(DbContextOptions<PedidosDbContext> options) : DbContext(options)
{
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<MensajeOutbox> Outbox => Set<MensajeOutbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>().Property(p => p.Importe).HasPrecision(18, 2);
        modelBuilder.Entity<MensajeOutbox>().HasIndex(m => new { m.ProcesadoEn, m.OcurridoEn });
    }
}
```

Escritura en `Program.cs`: pedido y evento en la misma transacción.

```csharp
app.MapPost("/api/pedidos", async (Pedido pedido, PedidosDbContext db) =>
{
    pedido.Id = Guid.NewGuid();
    db.Pedidos.Add(pedido);
    db.Outbox.Add(new MensajeOutbox
    {
        Id = Guid.NewGuid(),
        Tipo = nameof(PedidoCreado),
        Contenido = JsonSerializer.Serialize(new PedidoCreado(pedido.Id, pedido.ClienteId, pedido.Importe)),
        OcurridoEn = DateTime.UtcNow
    });
    await db.SaveChangesAsync();          // atómico: las dos inserciones o ninguna
    return TypedResults.Created($"/api/pedidos/{pedido.Id}", pedido);
});
```

Publicación: un servicio hospedado sondea periódicamente los mensajes pendientes.

```csharp
using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public sealed class PublicadorOutbox(IServiceScopeFactory scopes, ILogger<PublicadorOutbox> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var fabrica = new ConnectionFactory { HostName = "localhost" };
        await using var conexion = await fabrica.CreateConnectionAsync(stoppingToken);
        await using var canal = await conexion.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                     publisherConfirmationTrackingEnabled: true), stoppingToken);
        await Topologia.DeclararEventosAsync(canal, stoppingToken);

        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PedidosDbContext>();

            var pendientes = await db.Outbox
                .Where(m => m.ProcesadoEn == null)
                .OrderBy(m => m.OcurridoEn)
                .Take(50)
                .ToListAsync(stoppingToken);

            foreach (var mensaje in pendientes)
            {
                var propiedades = new BasicProperties
                {
                    Persistent = true,
                    MessageId = mensaje.Id.ToString(),
                    Type = mensaje.Tipo,
                    ContentType = "application/json"
                };
                try
                {
                    await canal.BasicPublishAsync(Topologia.ExchangeEventos, routingKey: string.Empty,
                        mandatory: true, basicProperties: propiedades,
                        body: Encoding.UTF8.GetBytes(mensaje.Contenido),
                        cancellationToken: stoppingToken);
                    mensaje.ProcesadoEn = DateTime.UtcNow;      // solo tras la confirmación del broker
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "No se pudo publicar {Id}; se reintentará", mensaje.Id);
                    break;                                     // no adelanta mensajes posteriores
                }
            }

            await db.SaveChangesAsync(stoppingToken);          // guarda las marcas de los publicados
        }
    }
}
```

Registro en `Program.cs`: `builder.Services.AddDbContext<PedidosDbContext>(...)` y `builder.Services.AddHostedService<PublicadorOutbox>();`.

**Observaciones:**

- `PeriodicTimer.WaitForNextTickAsync` espera al siguiente intervalo y devuelve `false` (o lanza `OperationCanceledException`) cuando se detiene la aplicación.
- El `break` ante un fallo conserva el orden de publicación dentro de este publicador: los mensajes posteriores esperan a la siguiente vuelta.
- Si el proceso cae entre la confirmación del broker y el `SaveChangesAsync` final, esos mensajes se publicarán otra vez: el consumidor debe ser idempotente.
- Con varias instancias del servicio, dos publicadores podrían leer los mismos pendientes. Las soluciones habituales son ejecutar un único publicador o bloquear las filas leídas; las técnicas de bloqueo y concurrencia se tratan en el tema 8. Una vez publicados, los mensajes antiguos se eliminan periódicamente para que la tabla no crezca sin límite.

### 5.3. El Patrón Saga para Procesos de Negocio de Larga Duración

Cuando un proceso de negocio abarca múltiples microservicios (ej. Crear Pedido → Reservar Inventario → Cobrar Tarjeta → Preparar Envío), necesitamos un mecanismo para coordinar estos pasos y, lo más importante, para revertirlos si un paso intermedio falla.

Dado que no podemos hacer un rollback tradicional, utilizamos el Patrón Saga. Una Saga es una secuencia de transacciones locales. Cada servicio ejecuta su transacción local y publica un evento que dispara el siguiente paso de la Saga.

Si una transacción local falla (por ejemplo, la tarjeta de crédito es rechazada), la Saga ejecuta una serie de **transacciones compensatorias**. Estas son operaciones explícitas diseñadas para deshacer el trabajo de los pasos anteriores que ya habían tenido éxito.

**Ejemplo de Transacciones Compensatorias:**

- **Paso 1:** el Servicio de Pedidos crea el pedido en estado "Pendiente". (Éxito)
- **Paso 2:** el Servicio de Inventario reserva el stock. (Éxito)
- **Paso 3:** el Servicio de Pagos intenta cobrar. (Fallo: fondos insuficientes).
- **Compensación 2:** se envía un comando al Servicio de Inventario para liberar el stock reservado.
- **Compensación 1:** se envía un comando al Servicio de Pedidos para cambiar el estado a "Cancelado".

Las Sagas pueden implementarse de dos formas conceptuales:

- **Coreografía:** no hay un coordinador central. Cada servicio escucha los eventos de los demás y sabe cómo reaccionar. Encaja bien en flujos sencillos con pocos servicios, pero se vuelve difícil de rastrear en procesos complejos, con una maraña de eventos cruzados.
- **Orquestación:** existe un componente central (el Orquestador de la Saga) que mantiene el estado global del proceso. El orquestador envía comandos a los servicios y espera sus eventos de respuesta, decidiendo qué paso dar a continuación o cuándo iniciar las compensaciones. Es el enfoque recomendado para procesos de negocio complejos y críticos. (La persistencia del estado de estos orquestadores suele apoyarse en patrones que se explorarán en el tema de Event Sourcing).

### 5.4. Bibliotecas de Mensajería en .NET

Los ejemplos de este tema usan el cliente de RabbitMQ directamente para que se vea cada mecanismo. En proyectos reales es frecuente apoyarse en una biblioteca que ya resuelve la topología, los reintentos, las DLQ, el Outbox y las sagas sobre varios brokers:

- **MassTransit:** muy extendida; a partir de la versión 9 se distribuye con licencia comercial (las versiones anteriores eran de código abierto).
- **Wolverine:** biblioteca de código abierto de mensajería y mediación, con Outbox integrado.
- **NServiceBus:** producto comercial de Particular Software, con larga trayectoria en sistemas empresariales.

Antes de elegir una conviene revisar su licencia y su coste, y entender los mecanismos que automatiza: son los mismos que se han visto en este tema.

---

# Cuadernillos del Alumno (laboratorio)

En esta parte se aplica la teoría de las partes anteriores sobre el proyecto de ejemplo **B2BOrders**: el Monolito publica el evento de "pedido pagado" en RabbitMQ y un Worker lo consume.

---

## Cuadernillo 7.1: Introducción a la Mensajería

**Bloque 7: Comunicación Asíncrona y Mensajería** · Duración: 9 min · Nivel: Avanzado

### Objetivos de la Lección

1. Comprender la diferencia fundamental entre comunicación Síncrona (HTTP) y Asíncrona (Mensajería).
2. Asimilar los conceptos clave del patrón Pub/Sub: Publisher, Message Broker, Topic, Queue y Subscriber.
3. Diseñar el flujo teórico para procesar el pago de un pedido en segundo plano.
4. Preparar el entorno local desplegando RabbitMQ usando Docker.

### Conceptos Clave

#### 1. Síncrono vs. Asíncrono

| Característica   | Síncrono (HTTP/REST)                               | Asíncrono (Mensajería)                                         |
| ---------------- | -------------------------------------------------- | -------------------------------------------------------------- |
| **Analogía**     | Llamada telefónica                                 | Correo electrónico                                             |
| **Acoplamiento** | Alto (temporal). Ambas partes deben estar activas. | Bajo (temporal). El receptor puede estar apagado.              |
| **Bloqueante**   | Sí. El emisor espera respuesta.                    | No. El emisor "dispara y olvida" (_fire-and-forget_).          |
| **Uso ideal**    | Consultas de datos en tiempo real.                 | Procesos largos, notificaciones, integraciones entre sistemas. |

#### 2. El Patrón Pub/Sub (Publicación-Suscripción)

Es un estilo de mensajería donde los emisores (Publishers) no envían mensajes a receptores específicos. En su lugar:

- **Publisher (Publicador):** grita un evento al "mundo" (al Broker). No sabe quién escucha. Ej: Monolito B2BOrders.
- **Message Broker (Intermediario):** la "oficina de correos" central que recibe, almacena y enruta mensajes. Ej: RabbitMQ.
- **Topic/Exchange:** el "canal" o categoría donde se publica el mensaje.
- **Queue (Cola):** el "buzón" donde se acumulan los mensajes para un consumidor específico hasta que puede procesarlos.
- **Subscriber (Suscriptor/Worker):** el servicio que escucha una cola y procesa los mensajes. Ej: Worker de Notificaciones.

#### 3. Eventos de Dominio vs. Integración

- **Evento de Dominio (Domain Event):** algo que pasó dentro de un mismo microservicio/contexto delimitado. Vive en memoria.
- **Evento de Integración (Integration Event):** un mensaje público diseñado para ser consumido por otros sistemas externos. Viaja por la red (serializado, ej. JSON).

### Laboratorio: Diseño y Preparación

#### Paso 1: Diseño del Flujo "Pedido Pagado"

En este bloque, transformaremos nuestro sistema para que, cuando se confirme un pago, ocurra lo siguiente sin bloquear al usuario:

1. **Monolito (API):** el `PayOrderHandler` confirma el pago en DB y publica un mensaje `OrderPaidIntegrationEvent` al Bus. Devuelve "OK" al usuario inmediatamente.
2. **Broker (RabbitMQ):** recibe el evento y lo guarda en una cola segura.
3. **Worker (nuevo servicio):** un proceso en segundo plano recoge el mensaje de la cola y ejecuta tareas pesadas (ej: simular envío de email, avisar a almacén).

#### Paso 2: Despliegue de Infraestructura (RabbitMQ en Docker)

Necesitamos un Message Broker real para implementar esto de forma profesional. Usaremos RabbitMQ corriendo en un contenedor.

Ejecuta el siguiente comando en tu terminal (PowerShell, CMD o Bash):

```powershell
docker run -d --hostname my-rabbit --name some-rabbit -p 15672:15672 -p 5672:5672 rabbitmq:3-management
```

**Desglose del comando:**

- `docker run`: crea e inicia un contenedor.
- `-d`: (_detached_) ejecuta en segundo plano.
- `--name some-rabbit`: nombre amigable para el contenedor.
- `-p 15672:15672`: mapea el puerto de la Interfaz Web de Gestión.
- `-p 5672:5672`: mapea el puerto del Protocolo de Mensajería (AMQP), el que usarán nuestras apps .NET.
- `rabbitmq:3-management`: la imagen a usar (versión 3 con el plugin de gestión preinstalado).

#### Paso 3: Verificación

Abre tu navegador web y accede a: `http://localhost:15672`

- **Usuario:** `guest`
- **Contraseña:** `guest`

Si ves el panel de control verde de RabbitMQ, ¡tu infraestructura está lista!

### Guía de Resolución de Problemas (Troubleshooting)

| Error / Síntoma                                                       | Causa Probable                                                                       | Solución                                                                                                            |
| --------------------------------------------------------------------- | ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------- |
| `docker: 'docker run' requires at least 1 argument`                   | Se te olvidó poner el nombre de la imagen al final del comando.                      | Asegúrate de que el comando termina en `rabbitmq:3-management`.                                                     |
| `Error: Ports are not available (e.g., bind: address already in use)` | Ya tienes otro servicio (u otro RabbitMQ mal cerrado) usando el puerto 5672 o 15672. | Ejecuta `docker ps` para ver qué corre. Usa `docker stop <id>` y `docker rm <id>` para limpiar contenedores viejos. |
| No puedo acceder a `localhost:15672`                                  | El contenedor no arrancó bien o Docker Desktop no está funcionando.                  | Verifica que Docker Desktop esté en verde. Mira los logs del contenedor: `docker logs some-rabbit`.                 |

### Siguiente Paso

Ya tenemos la "oficina de correos" (RabbitMQ) funcionando.

---

## Cuadernillo 7.2: Implementando el EventBus

**Bloque 7: Comunicación Asíncrona y Mensajería** · Duración: 8 min · Nivel: Avanzado

### Objetivos de la Lección

1. Desacoplar el dominio de la tecnología de mensajería mediante una abstracción (`IEventBus`).
2. Implementar la conexión real a RabbitMQ utilizando MassTransit (versión estable 8.x).
3. Configurar la aplicación de forma robusta, leyendo credenciales desde `appsettings.json` y manejando valores por defecto.
4. Interceptar el guardado de datos en el Repositorio para publicar automáticamente los eventos de dominio.

### Concepto Clave: Clean Architecture & Messaging

El Dominio emite hechos ("Ha pasado esto"), pero no debe saber cómo enviarlos ni a quién.

- **Capa Application:** define el contrato QUÉ se necesita (`IEventBus`).
- **Capa Infrastructure:** define CÓMO se hace (MassTransit conectado a RabbitMQ).

### Laboratorio 1: La Abstracción (Contrato)

Definimos la interfaz en la capa que orquesta los casos de uso.

**Archivo:** `B2BOrders.Application/Common/Interfaces/IEventBus.cs`

```csharp
namespace B2BOrders.Application.Common.Interfaces;

public interface IEventBus
{
    // Método genérico para publicar cualquier tipo de evento (clase/record)
    Task PublishAsync(object message, CancellationToken ct = default);
}
```

### Laboratorio 2: La Implementación (MassTransit)

Creamos la "tubería" que conecta nuestra interfaz con RabbitMQ.

> **Nota de versión:** usamos MassTransit versión 8.3.0 (o 8.x) para garantizar compatibilidad Open Source sin requerir licencias comerciales.

**Archivo:** `B2BOrders.Infrastructure/Services/MassTransitEventBus.cs`

```csharp
using MassTransit;
using Microsoft.Extensions.Logging;
using B2BOrders.Application.Common.Interfaces;

namespace B2BOrders.Infrastructure.Services;

public class MassTransitEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitEventBus> _logger;

    public MassTransitEventBus(IPublishEndpoint publishEndpoint, ILogger<MassTransitEventBus> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync(object message, CancellationToken ct = default)
    {
        // Este log es vital para saber que el mensaje salió de nuestra app
        _logger.LogInformation("Publicando evento al Bus: {EventType}", message.GetType().Name);

        await _publishEndpoint.Publish(message, ct);
    }
}
```

### Laboratorio 3: Configuración Blindada

Configuramos la inyección de dependencias y la lectura segura de `appsettings.json`.

**Archivo:** `B2BOrders.Api/appsettings.json`

(Asegúrate de que esta sección esté en la raíz del JSON, no dentro de otra llave).

```json
"EventBus": {
  "Host": "localhost",
  "VirtualHost": "/",
  "Username": "guest",
  "Password": "guest"
}
```

**Archivo:** `B2BOrders.Api/Program.cs`

```csharp
// Configuración MassTransit con lectura segura y valores por defecto
builder.Services.AddMassTransit(x => {
    x.UsingRabbitMq((context, cfg) => {
        var eventBusOptions = builder.Configuration.GetSection("EventBus");

        var host = eventBusOptions["Host"];
        // Validación para evitar errores silenciosos
        if (string.IsNullOrEmpty(host))
            throw new InvalidOperationException("Configuración EventBus no encontrada");

        cfg.Host(host, eventBusOptions["VirtualHost"] ?? "/", h => {
            h.Username(eventBusOptions["Username"] ?? "guest");
            h.Password(eventBusOptions["Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

// Registrar nuestra implementación
builder.Services.AddScoped<IEventBus, MassTransitEventBus>();
```

### Laboratorio 4: Integración en Repositorio

Modificamos el repositorio para disparar los eventos justo después de persistir en SQL Server.

**Archivo:** `B2BOrders.Infrastructure/Orders/EfCoreOrderRepository.cs`

```csharp
public class EfCoreOrderRepository : IOrderRepository {
    private readonly OrdersDbContext _context;
    private readonly IEventBus _eventBus; // 1. Declarar campo

    // 2. IMPORTANTE: Inyectar en el constructor y ASIGNAR al campo privado
    public EfCoreOrderRepository(OrdersDbContext context, IEventBus eventBus) {
        _context = context;
        _eventBus = eventBus; // Si olvidas esto, tendrás NullReferenceException
    }

    public async Task UpdateAsync(Order order, CancellationToken ct) {
        // 3. Persistencia (SQL)
        _context.Orders.Update(order);
        await _context.SaveChangesAsync(ct);

        // 4. Publicación (RabbitMQ)
        // Usamos PullDomainEvents() para obtener y limpiar la lista en un solo paso
        var events = order.PullDomainEvents();

        foreach (var @event in events) {
            await _eventBus.PublishAsync(@event, ct);
        }
    }
}
```

### Guía de Resolución de Problemas (Troubleshooting Real)

Estos son los errores que hemos resuelto durante la práctica:

| Síntoma / Error                      | Causa Probable                                       | Solución                                                                                                                |
| ------------------------------------ | ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------- |
| `MassTransit License Exception`      | Se instaló la versión 9.0.0 o un paquete Enterprise. | En el `.csproj`, bajar la versión de `MassTransit.RabbitMQ` a 8.3.0.                                                    |
| `NullReferenceException` al publicar | El campo `_eventBus` es nulo en el Repositorio.      | Verificar que en el constructor haces `_eventBus = eventBus;`.                                                          |
| No aparece el log de "Publicando..." | El pedido ya estaba pagado (idempotencia).           | Probar con un ID de pedido nuevo o que esté en estado `New`.                                                            |
| RabbitMQ no recibe mensajes          | No hay ninguna cola escuchando (aún).                | Verificar en `http://localhost:15672` pestaña _Exchanges_. El mensaje llega pero se descarta al no haber ruta (Worker). |

### Siguiente Paso

Tu Monolito ya sabe hablar (Publicar). Pero de momento, le habla a la pared (RabbitMQ recibe el mensaje y lo tira porque nadie lo reclama).

---

## Cuadernillo 7.3: El Worker Service

**Bloque 7: Comunicación Asíncrona y Mensajería** · Duración: 8 min · Nivel: Avanzado

### Objetivos de la Lección

1. Crear un Worker Service en .NET (una aplicación de consola de larga duración).
2. Implementar un Consumidor de MassTransit para procesar mensajes `OrderPaidDomainEvent`.
3. Configurar el Worker leyendo del `appsettings.json` (igual que la API) para evitar código hardcoded.
4. Ejecutar el sistema completo: API (Publicador) → RabbitMQ (Broker) → Worker (Consumidor).

### Concepto Clave: Worker Service

Un Worker es un microservicio sin interfaz gráfica (ni HTML ni JSON). Su trabajo es quedarse residente en memoria escuchando una cola o ejecutando tareas programadas.

- **API:** responde a peticiones del usuario (síncrono).
- **Worker:** responde a eventos del sistema (asíncrono).

### Laboratorio 1: Creación del Proyecto

Creamos el esqueleto y añadimos las dependencias. Es vital mantener las versiones alineadas con la API para evitar conflictos.

**Terminal:**

```powershell
# 1. Crear proyecto
dotnet new worker -n B2BOrders.Workers -o B2BOrders.Workers
dotnet sln add B2BOrders.Workers/B2BOrders.Workers.csproj

# 2. Instalar dependencias (Versiones Específicas para evitar errores)
cd B2BOrders.Workers
dotnet add package MassTransit.RabbitMQ --version 8.3.0
dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.2

# 3. Referenciar Dominio (para ver el evento) e Infraestructura (opcional, si usas DB)
dotnet add reference ../B2BOrders.Domain/B2BOrders.Domain.csproj
dotnet add reference ../B2BOrders.Infrastructure/B2BOrders.Infrastructure.csproj
```

### Laboratorio 2: El Consumidor (Consumer)

Definimos la lógica: "¿Qué hago cuando llega un mensaje?".

**Archivo:** `B2BOrders.Workers/OrderPaidConsumer.cs`

```csharp
using MassTransit;
using B2BOrders.Domain.Orders;

namespace B2BOrders.Workers;

// IConsumer<T> es la interfaz mágica de MassTransit
public class OrderPaidConsumer : IConsumer<OrderPaidDomainEvent> {
    private readonly ILogger<OrderPaidConsumer> _logger;

    public OrderPaidConsumer(ILogger<OrderPaidConsumer> logger) {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderPaidDomainEvent> context) {
        var msg = context.Message;

        _logger.LogInformation(
            "ENVIANDO EMAIL: Pedido {OrderId} pagado. ID Pago: {PaymentId}. Importe: {Amount}",
            msg.OrderId,
            msg.PaymentId,
            msg.Amount.Amount);

        // Aquí iría la llamada real a SendGrid, SMTP, etc.
        return Task.CompletedTask;
    }
}
```

### Laboratorio 3: Configuración (El punto crítico)

Aquí es donde configuramos la conexión a RabbitMQ leyendo del JSON.

**Archivo:** `B2BOrders.Workers/appsettings.json`

(¡OJO! `EventBus` debe estar fuera de `Logging`, al mismo nivel raíz).

```jsonc
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
    },
  }, // <--- COMA IMPORTANTE AQUÍ
  "EventBus": {
    "Host": "localhost",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest",
  },
}
```

**Archivo:** `B2BOrders.Workers/Program.cs`

```csharp
using B2BOrders.Workers;
using MassTransit;

var builder = Host.CreateApplicationBuilder(args);

// Configuración de MassTransit
builder.Services.AddMassTransit(x => {
    // 1. Registramos el Consumidor
    x.AddConsumer<OrderPaidConsumer>();

    x.UsingRabbitMq((context, cfg) => {
        // 2. Leemos configuración
        var busConfig = builder.Configuration.GetSection("EventBus");
        var host = busConfig["Host"];

        // Validación de seguridad
        if (string.IsNullOrEmpty(host))
            throw new InvalidOperationException("Configuración 'EventBus:Host' no encontrada.");

        cfg.Host(host, busConfig["VirtualHost"] ?? "/", h => {
            h.Username(busConfig["Username"] ?? "guest");
            h.Password(busConfig["Password"] ?? "guest");
        });

        // 3. Auto-configuración de colas (Exchange -> Queue)
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
```

### Guía de Resolución de Problemas (Troubleshooting)

Errores reales que hemos solucionado en esta sesión:

| Error / Síntoma                                         | Causa                                                                            | Solución                                                                                                                                             |
| ------------------------------------------------------- | -------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `InvalidOperationException: ERROR CRÍTICO...`           | La sección `EventBus` está anidada dentro de `Logging` en el `appsettings.json`. | Mover `EventBus` fuera de las llaves de `Logging`, a la raíz del JSON.                                                                               |
| `FileNotFoundException: Could not load ...SqlServer...` | Conflicto de versiones. La API usa v10.0.1 y la Infraestructura v10.0.2.         | Ejecutar `dotnet add package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.2` en todos los proyectos (Api, Infrastructure, Workers, Tests). |
| `NU1605: Detected package downgrade`                    | Una dependencia pide una versión mayor de la que tienes instalada.               | Igualar todas las versiones de los paquetes NuGet en toda la solución.                                                                               |
| `MassTransit License Error`                             | Se instaló la versión 9.0.0 (Preview/Commercial).                                | Usar siempre `MassTransit.RabbitMQ` versión 8.3.0.                                                                                                   |
