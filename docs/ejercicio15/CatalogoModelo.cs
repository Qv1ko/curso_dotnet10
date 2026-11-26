
using Microsoft.EntityFrameworkCore;

namespace Ejercicio15;

public record Caracteristica(string Nombre, string Valor);

public class Ficha
{
    public string Marca { get; set; } = "";
    public int GarantiaMeses { get; set; }
    public List<Caracteristica> Caracteristicas { get; set; } = [];
}

public class Articulo
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    public Ficha Ficha { get; set; } = new();
    public byte[] Version { get; set; } = [];
}

public class CatalogoContext(DbContextOptions<CatalogoContext> options)
    : DbContext(options)
{
    public DbSet<Articulo> Articulos => Set<Articulo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Articulo>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.Sku).IsUnique();
            b.Property(a => a.Sku).HasMaxLength(20).IsRequired();
            b.Property(a => a.Nombre).IsRequired();
            b.Property(a => a.Precio).HasPrecision(18, 2);
            b.Property(a => a.Version).IsRowVersion();
            b.ComplexProperty(a => a.Ficha, f =>
            {
                f.ToJson();
                f.ComplexCollection(x => x.Caracteristicas);
            });
        });
    }
}

public static class CatalogoExtensions
{
    public static IServiceCollection AddCatalogo(
        this IServiceCollection services, string cadena)
    {
        services.AddDbContextPool<CatalogoContext>(options =>
            options.UseSqlServer(cadena,
                sql => sql.UseCompatibilityLevel(170)));
        return services;
    }
}
