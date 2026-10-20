using Gimnovia.Modules.Members.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Gimnovia.Modules.Members;

public static class MembersModuleExtensions
{
    public static IServiceCollection AddMembersModule(this IServiceCollection s, string cs)
        => s.AddSingleton<IMembershipService, FakeMembership>();
}

internal class FakeMembership : IMembershipService
{
    public static readonly Guid M1 = new("00000000-0000-0000-0000-000000000001");
    public static readonly Guid M2 = new("00000000-0000-0000-0000-000000000002");
    public Task<MemberStatus?> GetStatusAsync(Guid id) => Task.FromResult<MemberStatus?>(
        id == M1 ? new(id, true) : id == M2 ? new(id, false) : null);
}