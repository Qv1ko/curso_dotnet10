namespace Coworking.Dominio;

public enum EstadoReserva { Pendiente, Confirmada, Cancelada }

public sealed record FranjaHoraria {
    public DateTime Inicio { get; private init; }
    public DateTime Fin { get; private init; }
    public int Horas => (int)(Fin - Inicio).TotalHours;

    public FranjaHoraria(DateTime inicio, DateTime fin) {
        if (inicio.Ticks % TimeSpan.TicksPerHour != 0 || fin.Ticks % TimeSpan.TicksPerHour != 0) {
            throw new ArgumentException("La franja debe empezar y terminar en punto.");
        }
        if (inicio.Date != fin.Date || fin <= inicio || (fin - inicio).TotalHours > 8) {
            throw new ArgumentException("Mismo día, fin posterior al inicio y máximo 8 horas.");
        }
        (Inicio, Fin) = (inicio, fin);
    }
}

public sealed record ReservaConfirmadaDomainEvent(Guid ReservaId, Guid ClienteId, decimal Importe, string Moneda) : IDomainEvent;

public sealed class Reserva : Entidad {
    public Guid SalaId { get; private set; }
    public Guid ClienteId { get; private set; }
    public FranjaHoraria Franja { get; private set; } = default!;
    public Dinero PrecioPorHora { get; private set; } = default!;
    public EstadoReserva Estado { get; private set; }
    private Reserva() { }

    public Reserva(Guid salaId, Guid clienteId, FranjaHoraria franja, Dinero precioPorHora) {
        Id = Guid.NewGuid();
        (SalaId, ClienteId) = (salaId, clienteId);
        (Franja, PrecioPorHora) = (franja, precioPorHora);
        Estado = EstadoReserva.Pendiente;
    }

    public Dinero CalcularImporte() => new(PrecioPorHora.Cantidad * Franja.Horas, PrecioPorHora.Moneda);

    public void Confirmar() {
        if (Estado != EstadoReserva.Pendiente) {
            throw new InvalidOperationException("Solo se puede confirmar una reserva pendiente.");
        }
        Estado = EstadoReserva.Confirmada;
        var importe = CalcularImporte();
        Registrar(new ReservaConfirmadaDomainEvent(Id, ClienteId, importe.Cantidad, importe.Moneda));
    }

    public void Cancelar(DateTime ahora) {
        if (Estado == EstadoReserva.Cancelada || ahora >= Franja.Inicio) {
            throw new InvalidOperationException("La reserva ya está cancelada o ya ha empezado.");
        }
        Estado = EstadoReserva.Cancelada;
    }
}
