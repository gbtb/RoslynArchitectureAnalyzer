namespace Roslyn.Architecture.Abstractions;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class CannotBeReferencedByAttribute: Attribute
{
    public CannotBeReferencedByAttribute(string assemblyName)
    {
    }
}