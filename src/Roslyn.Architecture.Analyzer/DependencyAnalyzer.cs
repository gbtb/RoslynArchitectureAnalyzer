using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Architecture.Abstractions;

[assembly:InternalsVisibleTo("Roslyn.Architecture.Tests")]
namespace Roslyn.Architecture.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DependencyAnalyzer: DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CannotReferenceDiagnostic = new("RARCH1", "Cannot reference assembly",
        "Assembly {0} has a forbidden reference to assembly {1}. Reference chain: {2}.", "Architecture", DiagnosticSeverity.Error, true);
    
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
        SearchLoop(new SearchContext(ctx, ImmutableList<string>.Empty.Add(ctx.Compilation.AssemblyName ?? string.Empty), 0), ctx.Compilation);
    }

    private void SearchLoop(SearchContext ctx, Compilation compilation)
    {
        if (ctx.Depth > 32) //protection from SO, just in case
            return;
        
        foreach (var reference in compilation.References.OfType<CompilationReference>())
        {
            var cannotBeReferencedAttrs = GetAssemblyAttributesFromCompilation(reference.Compilation);

            if (cannotBeReferencedAttrs.Any(attr => !attr.ConstructorArguments.IsEmpty && (string?) attr.ConstructorArguments[0].Value == ctx.Ctx.Compilation.AssemblyName))
            {
                var assemblyName = reference.Compilation.AssemblyName ?? string.Empty;
                ctx.Ctx.ReportDiagnostic(Diagnostic.Create(CannotReferenceDiagnostic, Location.None, 
                    ctx.Ctx.Compilation.AssemblyName, reference.Compilation.AssemblyName,
                    string.Join("->", ctx.ReferencesPath.Concat(new []{assemblyName}))
                    ));
            }
        }

        foreach (var reference in compilation.References.OfType<CompilationReference>())
        {
            SearchLoop(
                ctx with {ReferencesPath = ctx.ReferencesPath.Add(reference.Compilation.AssemblyName ?? string.Empty), Depth = ctx.Depth + 1},
                reference.Compilation);
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

    private record SearchContext(CompilationAnalysisContext Ctx, ImmutableList<string> ReferencesPath, int Depth);
}

