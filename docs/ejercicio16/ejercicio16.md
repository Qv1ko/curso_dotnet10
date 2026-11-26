Ejercicio: Modelo y consultas del servicio de reservas con EF Core 10

1. OnModelCreating

```
protected override void OnModelCreating(ModelBuilder modelBuilder) {
    modelBuilder.Entity<Reserva>(b => {
        b.ComplexProperty(r => r.Total, t => {
            t.Property(x => x.Importe)
                .HasColumnName("Total_Importe")
                .HasPrecision(18, 2);

            t.Property(x => x.Moneda)
                .HasColumnName("Total_Moneda")
                .HasMaxLength(3);
        });

        b.OwnsMany(r => r.Huespedes, h => h.ToJson());

        b.HasQueryFilter("SoftDelete", r => !r.Eliminada);
        b.HasQueryFilter("Hotel", r => r.HotelId == HotelId);

        b.HasIndex(r => new { r.HotelId, r.Eliminada });
    });
}
```

2. RejillaAsync

```
public static Task<List<FilaRejilla>> RejillaAsync(ReservasDbContext db) {
    return db.Reservas
        .OrderByDescending(r => r.CreadaEn)
        .Take(50)
        .Select(r => new FilaRejilla(
            r.Codigo,
            r.Estado,
            r.Total.Importe,
            r.Total.Moneda,
            r.Lineas.Count))
        .ToListAsync();
}
```

3. CaducarAsync

```
public static Task<int> CaducarAsync(ReservasDbContext db, DateTime limite) {
    return db.Reservas
        .Where(r => r.Estado == "Pendiente" &&
                    r.CreadaEn < limite)
        .ExecuteUpdateAsync(s =>
            s.SetProperty(r => r.Estado, "Caducada"));
}
```

4. PapeleraAsync

```
public static Task<List<FilaRejilla>> PapeleraAsync(ReservasDbContext db) {
    return db.Reservas
        .IgnoreQueryFilters(["SoftDelete"])
        .Where(r => r.Eliminada)
        .Select(r => new FilaRejilla(
            r.Codigo,
            r.Estado,
            r.Total.Importe,
            r.Total.Moneda,
            r.Lineas.Count))
        .ToListAsync();
}
```

5. Predicciones

   a) ¿Puede CaducarAsync modificar reservas de otro hotel o eliminadas?

   No, porque los filtros globales de hotel y SoftDelete se aplican automáticamente.

   b) ¿Qué devolvería la papelera usando IgnoreQueryFilters() sin argumentos?

   Devolvería las reservas eliminadas de todos los hoteles si no se añade un filtro explícito por HotelId, rompiendo el aislamiento entre hoteles.

6. Namespace necesario

```
using Microsoft.EntityFrameworkCore;
```
