using System.Reflection;
using Library.Application;
using Library.Domain;
using Xunit;

namespace Library.ArchitectureTests;

public class DependencyRuleTests {
    private static readonly Assembly DomainAssembly = typeof(Loan).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ILendBookUseCase).Assembly;

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

    [Fact]
    public void Domain_DoesNotUseAnyLibraryAssembly() =>
        Assert.DoesNotContain(References(DomainAssembly),
            n => n.StartsWith("Library.", StringComparison.Ordinal));

    [Fact]
    public void Application_DoesNotUseOuterLayersNorAspNetCore() {
        string[] forbidden = ["Library.Infrastructure", "Library.Api", "Microsoft.AspNetCore"];
        Assert.DoesNotContain(References(ApplicationAssembly),
            n => forbidden.Any(f => n.StartsWith(f, StringComparison.Ordinal)));
    }

    [Fact]
    public void Application_PublicClassesDoNotImplementUseCaseInterfaces() {
        var offenders = ApplicationAssembly.GetExportedTypes()
            .Where(t => t.IsClass && t.GetInterfaces()
                .Any(i => i.Name.EndsWith("UseCase", StringComparison.Ordinal)))
            .Select(t => t.FullName);
        Assert.Empty(offenders);
    }
}
