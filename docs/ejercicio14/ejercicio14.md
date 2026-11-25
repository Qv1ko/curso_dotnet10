# Revisión de canales y resiliencia en Butaquia

## Tarea 1: tipo de mensaje y canal

| Mensaje                 | Tipo                                                | Canal actual                    | ¿Adecuado? | Corrección y justificación                                                                                                                                                                                        |
| ----------------------- | --------------------------------------------------- | ------------------------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ReservarButacas`       | Comando (ordena una acción a un único destinatario) | Topic con una suscripción       | **No**     | Cola punto a punto (por ejemplo `seats-queue`): un comando debe ejecutarlo un solo receptor, y un topic admite nuevas suscripciones que duplicarían la reserva.                                                   |
| `PagoConfirmado`        | Evento (hecho ya ocurrido, varios interesados)      | Cola compartida por 3 servicios | **No**     | Topic `payment-topic` con una suscripción (cola propia) por servicio: en una cola compartida los consumidores compiten y cada mensaje llega solo a uno, así que dos de los tres servicios no se enteran del pago. |
| `GenerarEntradaPDF`     | Comando                                             | Cola con 4 instancias           | **Sí**     | Consumidores competidores: cada mensaje lo procesa una sola instancia y la carga se reparte.                                                                                                                      |
| `EnviarSmsRecordatorio` | Comando                                             | Topic con una suscripción       | **No**     | Cola `sms-queue`, por el mismo motivo que `ReservarButacas`. Si otros servicios necesitan saber que se envió, el servicio SMS publica después un evento (por ejemplo `SmsEnviado`) en un topic.                   |

## Tarea 2: estrategia por incidencia

1. **INC-1: doble factura tras reiniciar Facturación.**
   - **Estrategia:** consumidor idempotente, es decir, deduplicar por `MessageId`.
   - **Tipo de error:** no procede. No es un fallo del mensaje, sino una reentrega propia de la entrega "al menos una vez", y no se evita reintentando menos sino haciendo que procesar dos veces el mismo mensaje no tenga efecto.
   - **Cómo:** ver las tareas 3 y 4.

2. **INC-2: 503 y 429 de la pasarela y reintentos cada 100 ms.**
   - **Estrategia:** reintentos con backoff exponencial y jitter, con un máximo de intentos, más un circuit breaker que deje de llamar a la pasarela mientras esté caída y la sondee de vez en cuando.
   - **Complementos:** respetar la cabecera `Retry-After` en los 429 y limitar el prefetch y la concurrencia para que 3.000 mensajes no golpeen a la vez.
   - **Tipo de error:** transitorio, porque 503 y 429 desaparecen solos.

3. **INC-3: mensaje sin `OrderId` entregado 812 veces.**
   - **Tipo de error:** permanente (mensaje venenoso). Reintentarlo no lo arregla.
   - **Estrategia:** no reintentar y enviarlo a una dead-letter queue.
   - **Cambio en el consumidor:** distinguir los errores permanentes (deserialización o validación) de los transitorios. Ante un permanente, rechaza con `requeue = false` y registra el `MessageId` y el motivo. Solo los transitorios entran en la política de reintentos de INC-2.
   - **Cambio en la cola:** asociarle un dead-letter exchange con una cola DLQ enlazada, vigilada con una alerta y con reproceso manual cuando se corrija al productor. Los argumentos de una cola ya declarada no se pueden modificar, así que o se aplica el `dead-letter-exchange` mediante una policy de RabbitMQ (no obliga a recrear la cola) o se declara de nuevo la cola con `x-dead-letter-exchange`. Como red de seguridad opcional, una quorum queue permite fijar un `delivery-limit`.
   - **Resultado:** el mensaje sale de la cola tras el primer rechazo y desaparece el retraso de los mensajes posteriores.

## Tarea 3: defectos del flujo C

1. **ACK inmediato antes de procesar:** si la instancia cae después del ACK y antes de T1, el mensaje se pierde y la factura no se emite nunca.
2. **Consulta previa sin restricción única, con 3 instancias en paralelo:** dos instancias pueden leer "no está" a la vez y emitir dos facturas, y la base de datos no lo impide porque `MessageId` no tiene clave primaria ni índice único.
3. **Factura (T1) y marca de procesado (T2) en transacciones distintas:** si el proceso cae o T2 falla entre ambas, la factura existe sin registrar el `MessageId` y la reentrega factura otra vez, que es el caso de INC-1.

## Tarea 4: flujo corregido

**Cambio previo en la tabla** (primero hay que eliminar los duplicados existentes y asegurar que la columna no admite nulos):

```sql
ALTER TABLE ProcessedMessages ADD CONSTRAINT PK_ProcessedMessages PRIMARY KEY (MessageId);
```

**Flujo:**

1. Recibir el mensaje con confirmación manual (sin autoack) y no enviar el ACK todavía.
2. Deserializar y validar. Si el error es permanente, rechazar sin reencolar (va a la DLQ) y terminar.
3. Abrir una única transacción T.
4. Insertar el `MessageId` en `ProcessedMessages`. Si salta la violación de clave, ya se procesó o lo está procesando otra instancia: hacer rollback y saltar al paso 7 sin facturar. La segunda instancia espera al bloqueo de la clave y, cuando la primera confirma, recibe la violación.
5. Insertar la factura dentro de la misma T.
6. Hacer COMMIT de T: factura y marca de procesado se confirman juntas o ninguna.
7. Enviar el ACK al broker, solo después del COMMIT (o tras detectar el duplicado en el paso 4).
8. Si algo falla antes del COMMIT, hacer rollback sin ACK. Si el error es transitorio, se reintenta con backoff. Si el proceso cae entre el COMMIT y el ACK, el broker reentrega el mensaje y el paso 4 lo reconoce como duplicado.

Con esto, la entrega "al menos una vez" del broker más un consumidor idempotente da un efecto equivalente a procesar cada mensaje exactamente una vez.
