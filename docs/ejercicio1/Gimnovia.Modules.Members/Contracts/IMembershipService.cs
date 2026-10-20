namespace Gimnovia.Modules.Members.Contracts;
public record MemberStatus(Guid MemberId, bool IsActive);
public interface IMembershipService
{
    Task<MemberStatus?> GetStatusAsync(Guid memberId); // null si el socio no existe
}
