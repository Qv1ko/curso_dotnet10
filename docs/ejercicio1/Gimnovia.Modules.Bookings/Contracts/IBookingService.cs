namespace Gimnovia.Modules.Bookings.Contracts;

public record BookClassRequest(Guid MemberId, string ClassCode, DateOnly Date);
public record BookingResult(bool Succeeded, Guid? BookingId, string? Error);

public interface IBookingService
{
    Task<BookingResult> BookAsync(BookClassRequest request);
}