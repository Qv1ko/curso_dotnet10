namespace Coworking.Dominio;

public interface IDomainEvent { }

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent {
    Task HandleAsync(TEvent evento, CancellationToken cancellationToken);
}

public record Dinero(decimal Cantidad, string Moneda);

public abstract class Entidad {
    private readonly List<IDomainEvent> _eventos = new();
    public Guid Id { get; protected set; }
    public IReadOnlyCollection<IDomainEvent> Eventos => _eventos;
    protected void Registrar(IDomainEvent evento) => _eventos.Add(evento);
}
