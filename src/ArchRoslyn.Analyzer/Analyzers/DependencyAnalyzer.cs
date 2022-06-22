using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using ArchRoslyn.Abstractions.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

[assembly:InternalsVisibleTo("ArchRoslyn.Tests")]
namespace ArchRoslyn.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DependencyAnalyzer: DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CannotReferenceDiagnostic = new("RARCH1", "Cannot reference assembly",
        "Assembly {0} has a forbidden reference to assembly {1}. Reference chain: {2}.", "Architecture", DiagnosticSeverity.Error, true);

    private static readonly SourceText _emptySource = SourceText.From("");
    private static readonly SourceTextValueProvider<ConcurrentDictionary<string, ProjectNode>> _valueProvider;

    static DependencyAnalyzer()
    {
        _valueProvider =
            new SourceTextValueProvider<ConcurrentDictionary<string, ProjectNode>>(_ =>
                new ConcurrentDictionary<string, ProjectNode>());
    }
    
    public DependencyAnalyzer()
    {
        SupportedDiagnostics = ImmutableArray.Create(CannotReferenceDiagnostic);
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.TryGetValue(_emptySource, _valueProvider, out var _); //initializing shared state
#pragma warning disable RS1013
        context.RegisterCompilationStartAction(startContext =>
#pragma warning restore RS1013
        {
            startContext.RegisterCompilationEndAction(AnalyzeReferences);
        });
    }
   

    private void AnalyzeReferences(CompilationAnalysisContext ctx)
    {
        if (!ctx.TryGetValue(_emptySource, _valueProvider, out var forbiddenReferenceChains))
            throw new ApplicationException("Shared state hasn't been initialized properly");
        
        var compilation = ctx.Compilation;
        if (forbiddenReferenceChains.Count > 0)
        {
            ProduceDiagnostics(ctx, compilation, forbiddenReferenceChains);
        }

        UpdateDependencyGraph(compilation, forbiddenReferenceChains);
    }

    private static void UpdateDependencyGraph(Compilation compilation, ConcurrentDictionary<string, ProjectNode> forbiddenReferenceChains)
    {
        var referencedProjects = CollectReferencedProjects(compilation, forbiddenReferenceChains);

        var forbiddenReferrers = CollectOwnAttributes(compilation);

        UpdateNodeInDictionary(compilation, forbiddenReferenceChains, referencedProjects, forbiddenReferrers);
    }

    private static void UpdateNodeInDictionary(Compilation compilation, ConcurrentDictionary<string, ProjectNode> forbiddenReferenceChains,
        IReadOnlyCollection<ProjectNode> referencedProjects, IReadOnlyCollection<string> forbiddenReferrers)
    {
        if (!forbiddenReferenceChains.TryGetValue(compilation.AssemblyName!, out var oldNode))
        {
            if (referencedProjects.Count == 0 && forbiddenReferrers.Count == 0)
                return;

            var newNode = new ProjectNode(compilation.AssemblyName!, referencedProjects, forbiddenReferrers);
            forbiddenReferenceChains[newNode.Name] = newNode;
        }
        else
        {
            oldNode.ReferencedProjects = referencedProjects;
            oldNode.ForbiddenReferrers = forbiddenReferrers;
        }
    }

    private static List<ProjectNode> CollectReferencedProjects(Compilation compilation, ConcurrentDictionary<string, ProjectNode> forbiddenReferenceChains)
    {
        var refs = compilation.ReferencedAssemblyNames.Where(r => forbiddenReferenceChains.ContainsKey(r.Name));
        var bag = new List<ProjectNode>();
        foreach (var assemblyIdentity in refs)
        {
            if (forbiddenReferenceChains.TryGetValue(assemblyIdentity.Name, out var node))
            {
                bag.Add(node);
            }
        }

        return bag;
    }

    private static List<string> CollectOwnAttributes(Compilation compilation)
    {
        var cannotBeReferencedAttrs = GetAssemblyAttributesFromCompilation(compilation.Assembly);
        var forbiddenReferrers = new List<string>();
        foreach (var attributeData in cannotBeReferencedAttrs)
        {
            if (!attributeData.ConstructorArguments.IsEmpty &&
                attributeData.ConstructorArguments[0].Value is string refName && compilation.AssemblyName is { })
            {
                forbiddenReferrers.Add(refName);
            }
        }

        return forbiddenReferrers;
    }

    private void ProduceDiagnostics(CompilationAnalysisContext ctx, Compilation compilation,
        ConcurrentDictionary<string, ProjectNode> forbiddenReferenceChains)
    {
        var forbiddenDeps =
            compilation.ReferencedAssemblyNames.Where(r => forbiddenReferenceChains.ContainsKey(r.Name));

        foreach (var assemblyIdentity in forbiddenDeps)
        {
            var projectNode = forbiddenReferenceChains[assemblyIdentity.Name];
            if (projectNode.AllForbiddenReferrers.Contains(compilation.AssemblyName))
            {
                var path = new List<string>();
                var forbiddenReferenceName = GetForbiddenReferenceName(compilation.AssemblyName!, projectNode, path);
                path.Insert(0, compilation.AssemblyName!);
                ctx.ReportDiagnostic(Diagnostic.Create(CannotReferenceDiagnostic, Location.None,
                    compilation.AssemblyName, forbiddenReferenceName,
                    string.Join("->", path))
                );
            }
        }
    }

    private string GetForbiddenReferenceName(string assemblyName, ProjectNode projectNode, List<string> list)
    {
        list.Add(projectNode.Name);
        if (projectNode.ForbiddenReferrers.Contains(assemblyName))
            return projectNode.Name;
        
        var node = projectNode.ReferencedProjects.FirstOrDefault(p => p.AllForbiddenReferrers.Contains(assemblyName));
        if (node != null)
            return GetForbiddenReferenceName(assemblyName, node, list);

        return "";
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

