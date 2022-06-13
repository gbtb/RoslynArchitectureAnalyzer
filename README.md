[![Nuget](https://img.shields.io/nuget/v/ArchRoslyn.Analyzer)](https://www.nuget.org/packages/Roslyn.Architecture.Analyzer/)
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

This is an assembly attribute that takes one string argument with a name of an assembly and forbids direct and transitive references from the specified assembly to the assembly marked with this attribute.
Basically, this attribute is an antipode of InternalsVisibleTo attribute.

### Usage instructions
Install ArchRoslyn.Abstractions package in order to mark assemblies with this attribute.
Install ArchRoslyn.Analyzer into your projects to enable validation of rules imposed by an attribute.

## List of ideas
* Explore unit-testing lib APIs for inspiration and possible adoption as an interface for Roslyn-based rules.
* How to expand CannotBeReferencedBy attributes to allow more generic configuration? - e.g. layers, folders, wildcards, etc.
