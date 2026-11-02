using System.Globalization;
using Coworking.Aplicacion;
using Coworking.Dominio;
using Xunit;

namespace Coworking.Tests;

public class ReservaTests {
    private static DateTime D(int hora) => new(2026, 10, 5, hora, 0, 0);

    private static Reserva Nueva(int inicio, int fin) =>
        new(Guid.NewGuid(), Guid.NewGuid(), new FranjaHoraria(D(inicio), D(fin)), new Dinero(25m, "EUR"));

    [Fact]
    public void Confirmar_10_a_13_calcula_75_y_registra_un_evento() {
        var reserva = Nueva(10, 13);
        reserva.Confirmar();

        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
        Assert.Equal(75m, reserva.CalcularImporte().Cantidad);
        var evento = Assert.IsType<ReservaConfirmadaDomainEvent>(Assert.Single(reserva.Eventos));
        Assert.Equal(75m, evento.Importe);
        Assert.Equal("EUR", evento.Moneda);
        Assert.Equal(reserva.Id, evento.ReservaId);
    }

    [Theory]
    [InlineData("2026-10-05T10:30", "2026-10-05T12:00")]
    [InlineData("2026-10-05T09:00", "2026-10-05T18:00")]
    [InlineData("2026-10-05T22:00", "2026-10-06T02:00")]
    public void Franja_invalida_lanza_ArgumentException(string inicio, string fin) {
        var i = DateTime.Parse(inicio, CultureInfo.InvariantCulture);
        var f = DateTime.Parse(fin, CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentException>(() => new FranjaHoraria(i, f));
    }

    [Fact]
    public void Confirmar_dos_veces_lanza_InvalidOperationException() {
        var reserva = Nueva(10, 13);
        reserva.Confirmar();
        Assert.Throws<InvalidOperationException>(reserva.Confirmar);
    }

    [Fact]
    public void Cancelar_confirmada_antes_de_empezar_la_cancela() {
        var reserva = Nueva(10, 13);
        reserva.Confirmar();
        reserva.Cancelar(D(9));
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    public void Cancelar_a_la_hora_de_inicio_lanza_InvalidOperationException() {
        var reserva = Nueva(10, 13);
        Assert.Throws<InvalidOperationException>(() => reserva.Cancelar(D(10)));
    }

    [Fact]
    public async Task Handler_registra_el_cargo_con_el_concepto_esperado() {
        var facturacion = new FacturacionFalsa();
        var evento = new ReservaConfirmadaDomainEvent(Guid.NewGuid(), Guid.NewGuid(), 75m, "EUR");

        await new ReservaConfirmadaHandler(facturacion).HandleAsync(evento, CancellationToken.None);

        Assert.Equal(evento.ClienteId, facturacion.ClienteId);
        Assert.Equal(new Dinero(75m, "EUR"), facturacion.Importe);
        Assert.Equal($"Reserva {evento.ReservaId}", facturacion.Concepto);
    }

    private sealed class FacturacionFalsa : IFacturacionService {
        public Guid ClienteId { get; private set; }
        public Dinero? Importe { get; private set; }
        public string? Concepto { get; private set; }

        public Task RegistrarCargoAsync(Guid clienteId, Dinero importe, string concepto, CancellationToken cancellationToken) {
            (ClienteId, Importe, Concepto) = (clienteId, importe, concepto);
            return Task.CompletedTask;
        }
    }
}