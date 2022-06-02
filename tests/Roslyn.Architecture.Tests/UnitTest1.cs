using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Roslyn.Architecture.Analyzer;
using RoslynTestKit;

namespace Roslyn.Architecture.Tests;

public class Tests: AnalyzerTestFixture
{
    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        MSBuildLocator.RegisterDefaults();
    }
    
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public async Task TestAttributeCanBeFound()
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

    protected override string LanguageName => LanguageNames.CSharp;
    protected override DiagnosticAnalyzer CreateAnalyzer()
    {
        return new DependencyAnalyzer();
    }
}