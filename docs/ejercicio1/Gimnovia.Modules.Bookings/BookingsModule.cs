using Gimnovia.Modules.Bookings.Contracts;
using Gimnovia.Modules.Members.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gimnovia.Modules.Bookings;

internal class Booking
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string ClassCode { get; set; } = "";
    public DateOnly Date { get; set; }
}

internal class BookingsDbContext(DbContextOptions<BookingsDbContext> options) : DbContext(options)
{
    public DbSet<Booking> Bookings => Set<Booking>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("bookings");

        builder.Entity<Booking>()
            .Property(b => b.Total)
            .HasPrecision(18, 2);
    }
}

internal class BookingService(BookingsDbContext db, IMembershipService members) : IBookingService
{
    public async Task<BookingResult> BookAsync(BookClassRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ClassCode)) return Fail("Código de clase obligatorio.");
        var member = await members.GetStatusAsync(req.MemberId);
        if (member is null) return Fail("El socio no existe.");
        if (!member.IsActive) return Fail("El socio no está activo.");
        if (await db.Bookings.AnyAsync(booking => booking.MemberId == req.MemberId && booking.ClassCode == req.ClassCode && booking.Date == req.Date))
            return Fail("Reserva duplicada.");
        var booking = new Booking { MemberId = req.MemberId, ClassCode = req.ClassCode, Date = req.Date };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync();
        return new BookingResult(true, booking.Id, null);
    }
    private static BookingResult Fail(string error) => new(false, null, error);
}

public static class BookingsModuleExtensions
{
    public static IServiceCollection AddBookingsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BookingsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IBookingService, BookingService>();
        return services;
    }
}
