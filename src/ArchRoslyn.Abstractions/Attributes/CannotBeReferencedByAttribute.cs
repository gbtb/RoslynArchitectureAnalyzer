namespace ArchRoslyn.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class CannotBeReferencedByAttribute: Attribute
{
    public CannotBeReferencedByAttribute(string assemblyName)
    {
    }
}