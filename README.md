[![Nuget](https://img.shields.io/nuget/v/ArchRoslyn.Analyzers)](https://www.nuget.org/packages/ArchRoslyn.Analyzers)
[![codecov](https://codecov.io/gh/gbtb/RoslynArchitectureAnalyzer/branch/master/graph/badge.svg?token=SP9HHTRPE7)](https://codecov.io/gh/gbtb/RoslynArchitectureAnalyzer)

# Roslyn Architecture Analyzer

The ultimate goal of this project is to augment Roslyn C# compiler with additional architecture-related rules. Unlike the more common unit-test based approach, I envision this analyzers to be a part of the main compilation process, with near-instant feedback for developer.
These rules should be:
* Fast
* Portable
* Configurable by the end-user

Right now project consists of only one simple analyzer, inspired by an [InternalsVisibleTo](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.internalsvisibletoattribute?view=net-6.0) attribute.
If you need more advanced capabilities, you should check out [ArchUnitNET](https://github.com/TNG/ArchUnitNET) or [NetArchTest](https://github.com/BenMorris/NetArchTest).

## CannotBeReferencedBy attribute

This is an assembly attribute that takes one string argument with a name of assembly and forbids direct and transitive references from the specified assembly to the assembly marked with this attribute.
Basically, this attribute is an antipode of InternalsVisibleTo attribute.

### Usage instructions
Install ArchRoslyn.Analyzer into *all* your projects to enable validation of rules imposed by an attribute.  

Analyzer package contains both analyzer and an attribute and it's required to install analyzer in all of your projects because of Roslyn analyzer API limitations.
Diagnostic analyzers only work on a single compilation at a time, and they can't access other compilations directly.
That is why we have to workaround and share state between analyzer runs for different compilation.
Also this design implies that it is possible for shared state to become incoherent in case of aggressive caching of compilation in IDE.
Nevertheless, for a fresh run of a dotnet build this should work fine.

## List of ideas
* Explore unit-testing lib APIs for inspiration and possible adoption as an interface for Roslyn-based rules.
* How to expand CannotBeReferencedBy attributes to allow more generic configuration? - e.g. layers, folders, wildcards, etc.
