using System.Collections.Immutable;

namespace ArchRoslyn.Analyzers;

public record SearchContext
{
    public string ForbiddenReferrerName { get; init; } = null!;

    public ImmutableStack<string> ReferenceChain { get; init; } = null!;

    public string ForbiddenReferenceName { get; init; } = null!;
}