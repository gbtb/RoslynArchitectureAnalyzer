using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Architecture.Abstractions;

[assembly:InternalsVisibleTo("Roslyn.Architecture.Tests")]
namespace Roslyn.Architecture.Analyzer;

public class DependencyAnalyzer: DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CannotReferenceDiagnostic = new("RARCH1", "Cannot reference assembly",
        "Assembly {0} has a forbidden reference to assembly {1}", "Architecture", DiagnosticSeverity.Error, true);
    
    public DependencyAnalyzer()
    {
        SupportedDiagnostics = ImmutableArray.Create(CannotReferenceDiagnostic);
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeReferences);
    }

    private void AnalyzeReferences(CompilationAnalysisContext ctx)
    {
        SearchLoop(ctx, ctx.Compilation);
    }

    private bool SearchLoop(CompilationAnalysisContext ctx, Compilation compilation)
    {
        foreach (var reference in compilation.References.OfType<CompilationReference>())
        {
            var cannotBeReferencedAttrs = GetAssemblyAttributesFromCompilation(reference.Compilation);

            bool Predicate(AttributeData attr)
            {
                return !attr.ConstructorArguments.IsEmpty && (string?) attr.ConstructorArguments[0].Value == ctx.Compilation.AssemblyName;
            }

            if (cannotBeReferencedAttrs.Any(Predicate))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(CannotReferenceDiagnostic, Location.None, ctx.Compilation.AssemblyName, reference.Compilation.AssemblyName));
                return true;
            }
        }

        foreach (var reference in compilation.References.OfType<CompilationReference>())
        {
            if (SearchLoop(ctx, reference.Compilation))
                return true;
        }

        return false;
    }

    internal IEnumerable<AttributeData> GetAssemblyAttributesFromCompilation(Compilation referenceCompilation)
    {
        var attributes = referenceCompilation.Assembly.GetAttributes();
        if (attributes.IsEmpty)
            return Enumerable.Empty<AttributeData>();

        return attributes.Where(a => a.AttributeClass?.Name is nameof(CannotBeReferencedByAttribute) or "CannotBeReferencedBy");
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
}