namespace Library.Domain;

public class DomainException(string message) : Exception(message);

public sealed class LoanLimitExceededException(Guid memberId)
    : DomainException($"El socio {memberId} ya tiene el máximo de préstamos activos.");

public sealed class Loan {
    public Guid Id { get; private set; }
    public Guid BookId { get; private set; }
    public Guid MemberId { get; private set; }
    public DateOnly DueDate { get; private set; }
    public bool IsReturned { get; private set; }

    private Loan() { }

    public static Loan Create(Guid bookId, Guid memberId, int days, DateOnly today) =>
        days is < 1 or > 30
            ? throw new DomainException("Duración no válida.")
            : new Loan { Id = Guid.NewGuid(), BookId = bookId, MemberId = memberId, DueDate = today.AddDays(days) };
}
