using Coworking.Dominio;

namespace Coworking.Aplicacion;

public interface IFacturacionService {
    Task RegistrarCargoAsync(Guid clienteId, Dinero importe, string concepto, CancellationToken cancellationToken);
}
