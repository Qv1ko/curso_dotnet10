# Outbox de Inventario: resultado de las situaciones

**S1: sin colas vinculadas durante los primeros 27 s y un único mensaje pendiente.**
Con `mandatory: true` el broker devuelve el mensaje por no ser enrutable y la publicación falla en cada vuelta (a los 5, 10, 15, 20 y 25 s). Al quinto fallo queda `Intentos` = 5, `Fallido` = true (con un error en el log) y `ProcesadoEn` = null. A los 30 s, aunque ya haya colas, no se reintenta porque el mensaje está excluido: los consumidores no lo reciben hasta que alguien lo reactive a mano (`Fallido` = false, `Intentos` = 0).

**S2: el proceso cae tras la confirmación del broker para M1 y antes de guardar.**
El broker ya tiene M1, pero en la base sigue con `ProcesadoEn` = null, `Intentos` sin cambios y `Fallido` = false. Al reiniciar se publica de nuevo con el mismo `MessageId`: no se pierde, pero los consumidores pueden recibirlo dos veces (entrega al menos una vez), así que deben ser idempotentes y deduplicar por `MessageId`.

**S3: M1, M2 y M3 pendientes y solo falla M2 (`Intentos` = 0).**
M1 se publica y queda con `ProcesadoEn`. M2 pasa a `Intentos` = 1, sin `Fallido` y con un aviso en el log, y la vuelta termina: M3 no se intenta y sigue pendiente con `Intentos` = 0. Todo se guarda al final de la vuelta. Los consumidores reciben M1 ya; M2 y M3 llegan en la vuelta siguiente y en ese orden, si M2 se publica bien.
