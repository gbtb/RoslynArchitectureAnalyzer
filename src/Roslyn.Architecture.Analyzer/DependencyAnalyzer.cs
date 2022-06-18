using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
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

    private ConcurrentDictionary<string, SearchContext> _forbiddenReferenceChains;

    public DependencyAnalyzer()
    {
        _forbiddenReferenceChains = new ConcurrentDictionary<string, SearchContext>();
        SupportedDiagnostics = ImmutableArray.Create(CannotReferenceDiagnostic);
    }

    public override void Initialize(AnalysisContext context)
    {
        Debugger.Launch();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
#pragma warning disable RS1013
        context.RegisterCompilationStartAction(startContext =>
#pragma warning restore RS1013
        {
            startContext.RegisterCompilationEndAction(AnalyzeReferences);
        });
    }
   

    private void AnalyzeReferences(CompilationAnalysisContext ctx)
    {
        var compilation = ctx.Compilation;
        if (_forbiddenReferenceChains.Count > 0)
        {
            var forbiddenDeps =
                compilation.ReferencedAssemblyNames.Where(r => _forbiddenReferenceChains.ContainsKey(r.Name));

            foreach (var assemblyIdentity in forbiddenDeps)
            {
                var searchContext = _forbiddenReferenceChains[assemblyIdentity.Name];
                if (searchContext.ForbiddenReferrerName == compilation.AssemblyName)
                    ctx.ReportDiagnostic(Diagnostic.Create(CannotReferenceDiagnostic, Location.None, 
                        compilation.AssemblyName, searchContext.ForbiddenReferenceName,
                        string.Join("->", searchContext.ReferenceChain.Push(compilation.AssemblyName)))
                    );
                
                var newContext = searchContext with
                {
                    ReferenceChain = searchContext.ReferenceChain.Push(assemblyIdentity.Name)
                };
                _forbiddenReferenceChains[assemblyIdentity.Name] = newContext;
            }
        }
        
        var cannotBeReferencedAttrs = GetAssemblyAttributesFromCompilation(compilation.Assembly);
        foreach (var attributeData in cannotBeReferencedAttrs)
        {
            if (attributeData.ConstructorArguments.IsEmpty && attributeData.ConstructorArguments[0].Value is string refName && compilation.AssemblyName is {})
            {
                var c = new SearchContext
                {
                    ReferenceChain = ImmutableStack<string>.Empty.Push(compilation.AssemblyName),
                    ForbiddenReferrerName = refName,
                    ForbiddenReferenceName = compilation.AssemblyName
                };

                _forbiddenReferenceChains[compilation.AssemblyName] = c;
            }
        }
    }

    internal static IEnumerable<AttributeData> GetAssemblyAttributesFromCompilation(IAssemblySymbol assemblySymbol)
    {
        var attributes = assemblySymbol.GetAttributes();
        if (attributes.IsEmpty)
            return Enumerable.Empty<AttributeData>();

        return attributes.Where(a => a.AttributeClass?.Name is nameof(CannotBeReferencedByAttribute) or "CannotBeReferencedBy");
    }

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
}

