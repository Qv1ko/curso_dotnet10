# Consultas de envíos con Dapper, réplica de lectura y MongoDB en Flotaris

## Supuesto sobre `IDbConnectionFactory`

El enunciado remite a la factoría del manual (apartado 3.2), que no tengo a la vista. El código asume dos métodos que devuelven una conexión de `System.Data`: `CreateReadConnection()` (réplica, `ApplicationIntent=ReadOnly`) y `CreateWriteConnection()` (nodo principal). Si en tu manual se llaman de otra forma, solo cambian esas dos llamadas. Dapper abre la conexión si llega cerrada.

## Código de los tres métodos (dentro de `ShipmentQueries`)

```csharp
private static readonly HashSet<string> ValidStatuses = ["Pendiente", "EnRuta", "Entregado"];

public async Task<ShipmentDto?> GetShipmentAsync(int shipmentId)
{
    const string sql = """
        SELECT s.Id, s.Reference, s.Status, st.StopId, st.Sequence, st.City
        FROM Shipments s
        LEFT JOIN ShipmentStops st ON st.ShipmentId = s.Id
        WHERE s.Id = @shipmentId
        ORDER BY st.Sequence
        """;
    using var connection = factory.CreateReadConnection();
    ShipmentDto? shipment = null;
    await connection.QueryAsync<ShipmentDto, StopDto, ShipmentDto>(
        sql,
        (s, stop) =>
        {
            shipment ??= s;
            if (stop is not null) shipment.Stops.Add(stop);
            return shipment;
        },
        new { shipmentId },
        splitOn: "StopId");
    return shipment;
}

public async Task<bool> ChangeStatusAsync(int shipmentId, string newStatus)
{
    if (!ValidStatuses.Contains(newStatus))
        throw new ArgumentException($"Estado no válido: {newStatus}", nameof(newStatus));

    using var connection = factory.CreateWriteConnection();
    var rows = await connection.ExecuteAsync(
        "UPDATE Shipments SET Status = @newStatus WHERE Id = @shipmentId",
        new { shipmentId, newStatus });
    return rows > 0;
}

public async Task<List<VehiclePosition>> GetLastPositionsAsync(
    string vehicleId, int max, CancellationToken ct)
{
    if (max is < 1 or > 50)
        throw new ArgumentOutOfRangeException(nameof(max), max, "max debe estar entre 1 y 50.");

    return await _positions.Find(p => p.VehicleId == vehicleId)
        .SortByDescending(p => p.RecordedAt)
        .Limit(max)
        .ToListAsync(ct);
}
```

### Cómo cumple cada requisito

**Tarea 1: `GetShipmentAsync`**

- Una sola consulta con `LEFT JOIN` y multimapeo (`splitOn: "StopId"`), sobre la conexión de lectura.
- El `LEFT JOIN` hace que un envío sin paradas devuelva una fila con las columnas de la parada a `NULL`. Dapper entrega entonces `stop` como `null`, que no se añade, así que `Stops` queda vacía y sin elementos nulos (envío 8).
- Si el envío no existe no hay filas y la variable `shipment` sigue en `null`.
- `ORDER BY st.Sequence` ordena las paradas del envío 7 como Madrid (1), Zaragoza (2) y Barcelona (3).

**Tarea 2: `ChangeStatusAsync`**

- Valida el estado (comparación exacta, con distinción de mayúsculas) antes de crear la conexión, así que con un estado inválido no se toca la base de datos.
- `UPDATE` sobre la conexión de escritura: devuelve `true` si afectó a una fila y `false` si el envío no existe.

## Tarea 3: respuesta con `V-12` y `max` = 2

La llamada devuelve **dos posiciones: la de las 10:10 y después la de las 10:05**, de la más nueva a la más antigua. La de las 10:00 queda fuera por el límite, y la de las 10:07 no aparece porque pertenece a `V-99`. Con `max` = 0 o 51 lanza `ArgumentOutOfRangeException` sin consultar MongoDB.

## Tarea 4: `ChangeStatusAsync(7, "Entregado")` seguido, 200 ms después, de `GetShipmentAsync(7)`

El cliente puede ver todavía el estado anterior, **`EnRuta`**, o ya `Entregado`, según lo que haya tardado la réplica en aplicar el cambio. La escritura se hace en el nodo principal y la lectura va a la réplica de lectura (`ApplicationIntent=ReadOnly`), cuya replicación es asíncrona, así que 200 ms pueden no bastar y la lectura puede devolver un dato desfasado. La medida que lo evita es la consistencia de lectura tras escritura: las lecturas que necesitan ver lo que el propio cliente acaba de escribir deben ir al nodo principal (la conexión de escritura) y no a la réplica, y esta se reserva para consultas que toleran un pequeño retraso. No puedo confirmar con qué nombre figura esa medida en el apartado correspondiente de tu manual, porque no lo tengo.
