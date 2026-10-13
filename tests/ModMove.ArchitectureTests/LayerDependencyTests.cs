using System.Reflection;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.ArchitectureTests;

/// <summary>
/// Enforces the onion rule: dependencies only point inward
/// (WebApi -> Infrastructure -> Application -> Domain).
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = ModMove.Domain.AssemblyReference.Assembly;
    private static readonly Assembly ApplicationAssembly = ModMove.Application.AssemblyReference.Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(ModMove.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Domain_depends_on_no_other_layer_or_framework()
    {
        AssertNoReference(DomainAssembly,
            "ModMove.Application", "ModMove.Infrastructure", "ModMove.WebApi",
            "MediatR", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");
    }

    [Fact]
    public void Application_does_not_depend_on_outer_layers_or_ef_core()
    {
        AssertNoReference(ApplicationAssembly,
            "ModMove.Infrastructure", "ModMove.WebApi",
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_WebApi()
    {
        AssertNoReference(InfrastructureAssembly, "ModMove.WebApi", "Microsoft.AspNetCore");
    }

    [Fact]
    public void Query_handlers_do_not_use_write_side_persistence()
    {
        var offenders = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(IsQueryHandler))
            .Where(t => t.GetConstructors().SelectMany(c => c.GetParameters()).Any(p => IsWriteSide(p.ParameterType)))
            .Select(t => t.FullName)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Query handlers must read via ISqlConnectionFactory, not IRepository/IUnitOfWork: " + string.Join(", ", offenders));
    }

    private static bool IsQueryHandler(Type i) =>
        i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>);

    private static bool IsWriteSide(Type t) =>
        t == typeof(IUnitOfWork) ||
        t.GetInterfaces().Append(t).Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRepository<>));

    private static void AssertNoReference(Assembly assembly, params string[] forbiddenPrefixes)
    {
        var violations = assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => forbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.True(violations.Count == 0,
            $"{assembly.GetName().Name} must not reference: {string.Join(", ", violations)}");
    }
}
