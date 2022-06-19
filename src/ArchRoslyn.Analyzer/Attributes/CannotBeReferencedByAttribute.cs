namespace ArchRoslyn.Attributes;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class CannotBeReferencedByAttribute: Attribute
{
    public CannotBeReferencedByAttribute(string assemblyName)
    {
    }
}