using Microsoft.EntityFrameworkCore;

public class Producto { public string Id { get; set; } = ""; public int Stock { get; set; } }

public class Reserva {
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public string ProductoId { get; set; } = "";
    public int Unidades { get; set; }
}

public class MensajeOutbox {
    public Guid Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Contenido { get; set; } = "";
    public DateTime OcurridoEn { get; set; }
    public DateTime? ProcesadoEn { get; set; }
    public int Intentos { get; set; }
    public bool Fallido { get; set; }
}

public record SolicitudReserva(Guid PedidoId, string ProductoId, int Unidades);
public record StockReservado(Guid PedidoId, string ProductoId, int Unidades, int StockRestante);

public class InventarioDbContext(DbContextOptions<InventarioDbContext> o) : DbContext(o) {
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<MensajeOutbox> Outbox => Set<MensajeOutbox>();
}