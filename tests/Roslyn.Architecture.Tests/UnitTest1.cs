using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Roslyn.Architecture.Abstractions;
using Roslyn.Architecture.Analyzer;
using RoslynTestKit;
using RoslynTestKit.Utils;

[assembly:CannotBeReferencedBy("111")]
namespace Roslyn.Architecture.Tests;

public class Tests: AnalyzerTestFixture
{
    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        MSBuildLocator.RegisterDefaults();
    }
    
    [Test]
    public async Task Test_AttributeCanBeFound()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib", "Lib", LanguageNames.CSharp));
        
        var sourceText = @"
                using System;
                using Roslyn.Architecture.Abstractions;

                [assembly:CannotBeReferencedBy(""SomeProject.Name"")]
                [assembly:CannotBeReferencedByAttribute(""SomeProject.Name"")]
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }
                }
            ";
        
        var doc = libProject.AddDocument("Lib.cs", SourceText.From(sourceText));

        var compilation = await doc.Project.GetCompilationAsync();
        var attributes = compilation.Assembly.GetAttributes();
        Assert.That(attributes, Is.Not.Empty);

        var analyzer = new DependencyAnalyzer();
        
        Assert.That(analyzer.GetAssemblyAttributesFromCompilation(compilation).Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Test_AnalyzerFindsDirectReference()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = PrepareLibProject(workspace);
        var mainProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Main", "Main", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                using Roslyn.Architecture.Abstractions;

                [assembly:CannotBeReferencedBy(""Main"")]
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }
                }
            ";
        
        var doc = workspace.AddDocument(libProject.Id, "Lib.cs", SourceText.From(sourceText));
        libProject = doc.Project;

        var reference = new ProjectReference(libProject.Id);
        var emptyDoc = workspace.CurrentSolution.GetProject(mainProject.Id)?.AddProjectReference(reference).AddDocument("Empty.cs", "");

        workspace.WorkspaceFailed += (_, err) => Assert.Fail(err.ToString());
        Assert.That(emptyDoc.Project.Solution.Projects.First().Documents.Count(), Is.EqualTo(1), "Expected solution structure hasn't been formed");

        var compilation = await emptyDoc.Project.GetCompilationAsync();
        var compilationWithAnalyzers = compilation?.WithAnalyzers(ImmutableArray<DiagnosticAnalyzer>.Empty.Add(this.CreateAnalyzer()));
        var diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        Assert.That(diags.IsEmpty, Is.False);
        Assert.That(diags[0].Id, Is.EqualTo("RARCH1"));
        Assert.That(diags[0].GetMessage(), Is.EqualTo("Assembly Main has a forbidden reference to assembly Lib. Reference chain: Main->Lib."));
    }

    private Project PrepareLibProject(AdhocWorkspace workspace, string name = "Lib")
    {
        var runtimeAssembly = Assembly.Load("System.Runtime");
        var netstandardAssembly = Assembly.Load("netstandard");
        var libProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, name,
            name, LanguageNames.CSharp,
            metadataReferences: CreateFrameworkMetadataReferences().Concat(
                new[]
                {
                    ReferenceSource.FromType<CannotBeReferencedByAttribute>(),
                    ReferenceSource.FromAssembly(runtimeAssembly),
                    ReferenceSource.FromAssembly(netstandardAssembly)
                }))
        );
        return libProject;
    }

    [Test]
    public async Task Test_AnalyzerFindsTransitiveReference()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = PrepareLibProject(workspace);
        var lib2Project = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Lib2", "Lib2", LanguageNames.CSharp));
        var mainProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Main", "Main", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                using Roslyn.Architecture.Abstractions;

                [assembly:CannotBeReferencedBy(""Main"")]
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }
                }
            ";
        
        
        var doc = workspace.AddDocument(libProject.Id, "Lib.cs", SourceText.From(sourceText));
        libProject = doc.Project;

        var reference = new ProjectReference(libProject.Id);
        solution = workspace.CurrentSolution.AddProjectReference(lib2Project.Id, reference);
        reference = new ProjectReference(lib2Project.Id);
        solution = solution.AddProjectReference(mainProject.Id, reference);
        var emptyDoc = solution.GetProject(mainProject.Id)?.AddDocument("Empty.cs", "");

        workspace.WorkspaceFailed += (_, err) => Assert.Fail(err.ToString());
        Assert.That(emptyDoc.Project.Solution.Projects.First().Documents.Count(), Is.EqualTo(1), "Expected solution structure hasn't been formed");

        var compilation = await emptyDoc.Project.GetCompilationAsync();
        var compilationWithAnalyzers = compilation?.WithAnalyzers(ImmutableArray<DiagnosticAnalyzer>.Empty.Add(this.CreateAnalyzer()));
        var diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        Assert.That(diags.IsEmpty, Is.False);
        Assert.That(diags[0].Id, Is.EqualTo("RARCH1"));
        Assert.That(diags[0].GetMessage(), Is.EqualTo("Assembly Main has a forbidden reference to assembly Lib. Reference chain: Main->Lib2->Lib."));
    }
    
    [Test]
    public async Task Test_AnalyzerFindsMultipleViolations()
    {
        var workspace = new AdhocWorkspace();
        var solution = workspace.AddSolution(SolutionInfo.Create(SolutionId.CreateNewId(), VersionStamp.Default));
        var libProject = PrepareLibProject(workspace);
        var lib2Project = PrepareLibProject(workspace, "Lib2");
        var mainProject = workspace.AddProject(ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Default, "Main", "Main", LanguageNames.CSharp));

        var sourceText = @"
                using System;
                using Roslyn.Architecture.Abstractions;

                [assembly:CannotBeReferencedBy(""Main"")]
                namespace Lib 
                {
                    public class Foo
                    {
                        public int Prop { get; set; }
                    }
                }
            ";
        
        
        var doc = workspace.AddDocument(libProject.Id, "Lib.cs", SourceText.From(sourceText));
        libProject = doc.Project;
        doc = workspace.AddDocument(lib2Project.Id, "Lib2.cs", SourceText.From(sourceText));
        lib2Project = doc.Project;

        var reference = new ProjectReference(libProject.Id);
        solution = workspace.CurrentSolution.AddProjectReference(mainProject.Id, reference);
        reference = new ProjectReference(lib2Project.Id);
        solution = solution.AddProjectReference(mainProject.Id, reference);
        var emptyDoc = solution.GetProject(mainProject.Id)?.AddDocument("Empty.cs", "");

        workspace.WorkspaceFailed += (_, err) => Assert.Fail(err.ToString());
        Assert.That(emptyDoc.Project.Solution.Projects.First().Documents.Count(), Is.EqualTo(1), "Expected solution structure hasn't been formed");

        var compilation = await emptyDoc.Project.GetCompilationAsync();
        var compilationWithAnalyzers = compilation?.WithAnalyzers(ImmutableArray<DiagnosticAnalyzer>.Empty.Add(this.CreateAnalyzer()));
        var diags = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        Assert.That(diags.Count, Is.EqualTo(2));
        Assert.That(diags[0].Id, Is.EqualTo("RARCH1"));
        Assert.That(diags[0].GetMessage(), Is.EqualTo("Assembly Main has a forbidden reference to assembly Lib. Reference chain: Main->Lib."));
        Assert.That(diags[1].Id, Is.EqualTo("RARCH1"));
        Assert.That(diags[1].GetMessage(), Is.EqualTo("Assembly Main has a forbidden reference to assembly Lib2. Reference chain: Main->Lib2."));
    }

    protected override string LanguageName => LanguageNames.CSharp;
    protected override DiagnosticAnalyzer CreateAnalyzer()
    {
        return new DependencyAnalyzer();
    }
}