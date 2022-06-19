using System.Collections.Concurrent;

namespace ArchRoslyn.Analyzers;

public class ProjectNode
{
    public ProjectNode(string name, IReadOnlyCollection<ProjectNode> referencedProjects, IReadOnlyCollection<string> forbiddenReferrers)
    {
        Name = name;
        ReferencedProjects = referencedProjects;
        ForbiddenReferrers = forbiddenReferrers;
    }

    public string Name { get; }
    
    public IReadOnlyCollection<ProjectNode> ReferencedProjects { get; set; }
    
    public IReadOnlyCollection<string> ForbiddenReferrers { get; set; }

    public IEnumerable<string> AllForbiddenReferrers =>
        ForbiddenReferrers.Concat(ReferencedProjects.SelectMany(r => r.AllForbiddenReferrers));

}