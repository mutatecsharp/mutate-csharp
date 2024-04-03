using System.Collections.Frozen;
using Serilog;

namespace MutateCSharp.SUT;

public class Solution
{
  private readonly Microsoft.Build.Construction.SolutionFile _solution;
  private readonly IDictionary<string, Project> _projectByName;

  public string Name { get; }
  public string AbsolutePath { get; }

  public Solution(string name, string absolutePath)
  {
    Name = name;
    AbsolutePath = absolutePath;
    _solution = Microsoft.Build.Construction.SolutionFile.Parse(absolutePath);
    var projects = new Dictionary<string, Project>();
    
    foreach (var project in _solution.ProjectsInOrder)
    {
      Log.Debug("\tProject {ProjectName}: {ProjectPath}", project.ProjectName, project.AbsolutePath);
      projects[project.ProjectName] = ProjectFactory.InspectProject(project.ProjectName, project.AbsolutePath);
    }

    _projectByName = projects.ToFrozenDictionary();
  }

  public IEnumerable<Project> GetProjectsOfType<T>() where T: Project
  {
    return _projectByName.Values.OfType<T>();
  }

  public IEnumerable<File> GetFilesOfType<T>() where T : File
  {
    return _projectByName.Values.SelectMany(project => project.GetFiles()).OfType<T>();
  }

  public override string ToString()
  {
    return $"Solution {Name}: {AbsolutePath}";
  }
}