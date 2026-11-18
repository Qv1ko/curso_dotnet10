using System.Collections.Concurrent;
using Library.Application;
using Library.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

internal sealed class InMemoryLoanRepository : ILoanRepository {
    private readonly ConcurrentBag<Loan> _loans = new();

    public Task<int> CountActiveByMemberAsync(Guid memberId, CancellationToken ct) =>
        Task.FromResult(_loans.Count(l => l.MemberId == memberId && !l.IsReturned));

    public Task AddAsync(Loan loan, CancellationToken ct) {
        _loans.Add(loan);
        return Task.CompletedTask;
    }
}

public static class InfrastructureServiceCollectionExtensions {
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString) =>
        services.AddSingleton<ILoanRepository, InMemoryLoanRepository>();
}
