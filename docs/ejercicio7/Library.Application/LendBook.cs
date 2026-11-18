using Library.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Application;

public sealed record LendBookRequest(Guid BookId, Guid MemberId, int Days);

public sealed record LendBookResult(Guid LoanId, DateOnly DueDate);

public interface ILendBookUseCase {
    Task<LendBookResult> ExecuteAsync(LendBookRequest request, CancellationToken ct);
}

public interface ILoanRepository {
    Task<int> CountActiveByMemberAsync(Guid memberId, CancellationToken ct);
    Task AddAsync(Loan loan, CancellationToken ct);
}

internal sealed class LendBookUseCase(ILoanRepository loans, TimeProvider clock) : ILendBookUseCase {
    private const int MaxActiveLoans = 3;

    public async Task<LendBookResult> ExecuteAsync(LendBookRequest request, CancellationToken ct) {
        if (await loans.CountActiveByMemberAsync(request.MemberId, ct) >= MaxActiveLoans)
            throw new LoanLimitExceededException(request.MemberId);

        var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        var loan = Loan.Create(request.BookId, request.MemberId, request.Days, today);
        await loans.AddAsync(loan, ct);
        return new LendBookResult(loan.Id, loan.DueDate);
    }
}

public static class ApplicationServiceCollectionExtensions {
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services.AddScoped<ILendBookUseCase, LendBookUseCase>();
}
