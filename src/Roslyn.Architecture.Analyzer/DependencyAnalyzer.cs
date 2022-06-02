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
        "", "Architecture", DiagnosticSeverity.Error, true);
    
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
        foreach (var reference in ctx.Compilation.References.OfType<CompilationReference>())
        {
            var cannotBeReferencedAttrs = GetAssemblyAttributesFromCompilation(reference.Compilation);
            
            if (cannotBeReferencedAttrs.Any(attr => (string?) attr.ConstructorArguments[0].Value == ctx.Compilation.AssemblyName))
                ctx.ReportDiagnostic(Diagnostic.Create(CannotReferenceDiagnostic, Location.None));
        }
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