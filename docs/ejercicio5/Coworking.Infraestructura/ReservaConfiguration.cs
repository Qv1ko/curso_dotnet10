using Coworking.Dominio;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infraestructura;

public sealed class ReservaConfiguration : IEntityTypeConfiguration<Reserva> {
    public void Configure(EntityTypeBuilder<Reserva> builder) {
        builder.ToTable("Reservas");
        builder.Ignore(reserva => reserva.Eventos);
        builder.HasKey(reserva => reserva.Id);
        builder.Property(reserva => reserva.Id).ValueGeneratedNever();
        builder.Property(reserva => reserva.Estado).HasConversion<string>().HasMaxLength(20);

        builder.OwnsOne(reserva => reserva.Franja, f => {
            f.Property(x => x.Inicio).HasColumnName("Inicio");
            f.Property(x => x.Fin).HasColumnName("Fin");
        });

        builder.OwnsOne(reserva => reserva.PrecioPorHora, p => {
            p.Property(x => x.Cantidad).HasColumnName("PrecioPorHora").HasPrecision(10, 2);
            p.Property(x => x.Moneda).HasColumnName("Moneda").HasMaxLength(3).IsFixedLength();
        });

        builder.Navigation(reserva => reserva.Franja).IsRequired();
        builder.Navigation(reserva => reserva.PrecioPorHora).IsRequired();
    }
}