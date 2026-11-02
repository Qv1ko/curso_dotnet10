using Coworking.Dominio;
using Microsoft.Extensions.DependencyInjection;

namespace Coworking.Aplicacion;

public sealed class ReservaConfirmadaHandler(IFacturacionService facturacion): IDomainEventHandler<ReservaConfirmadaDomainEvent> {
    public Task HandleAsync(ReservaConfirmadaDomainEvent evento, CancellationToken cancellationToken) =>
        facturacion.RegistrarCargoAsync(evento.ClienteId, new Dinero(evento.Importe, evento.Moneda), $"Reserva {evento.ReservaId}", cancellationToken);
}

public static class ReservasAplicacionExtensions {
    public static IServiceCollection AddReservasAplicacion(this IServiceCollection services) =>
        services.AddScoped<IDomainEventHandler<ReservaConfirmadaDomainEvent>, ReservaConfirmadaHandler>();
}
