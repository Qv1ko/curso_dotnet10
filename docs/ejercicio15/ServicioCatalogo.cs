
using Microsoft.EntityFrameworkCore;

namespace Ejercicio15;

public record FichaDto(string Sku, string Nombre, decimal Precio, string Marca, int GarantiaMeses, byte[] Version);

public enum ResultadoCambio {
    Actualizado, NoEncontrado, Conflicto, PrecioNoValido
}

public class ServicioCatalogo(CatalogoContext db) {
    private static readonly Func<CatalogoContext, string, Task<FichaDto?>>
        ConsultaFicha = EF.CompileAsyncQuery(
            (CatalogoContext ctx, string sku) =>
                ctx.Articulos.AsNoTracking()
                    .Where(a => a.Sku == sku)
                    .Select(a => new FichaDto(a.Sku, a.Nombre,
                        a.Precio, a.Ficha.Marca,
                        a.Ficha.GarantiaMeses, a.Version))
                    .FirstOrDefault());

    public Task<FichaDto?> FichaPorSkuAsync(string sku)
        => ConsultaFicha(db, sku);

    public async Task<ResultadoCambio> CambiarPrecioAsync(int id, decimal nuevoPrecio, byte[] version) {
        if (nuevoPrecio <= 0)
            return ResultadoCambio.PrecioNoValido;

        var filas = await db.Articulos
            .Where(a => a.Id == id && a.Version == version)
            .ExecuteUpdateAsync(s => s.SetProperty(
                a => a.Precio, nuevoPrecio));

        if (filas == 1) return ResultadoCambio.Actualizado;

        return await db.Articulos.AnyAsync(a => a.Id == id)
            ? ResultadoCambio.Conflicto
            : ResultadoCambio.NoEncontrado;
    }

    public Task<int> AmpliarGarantiaAsync(string marca, int meses)
        => db.Articulos
            .Where(a => a.Ficha.Marca == marca)
            .ExecuteUpdateAsync(s => s.SetProperty(
                a => a.Ficha.GarantiaMeses,
                a => a.Ficha.GarantiaMeses + meses));
}
