using System.Collections.Frozen;
using MethodTimer;
using Serilog;

namespace MutateCSharp.SUT;

public class Application
{
  private readonly string _rootDirectory;
  private readonly IDictionary<string, Solution> _solutionByName;
  private static readonly string[] ExclusionPatterns = ["Jennisys"];

  [Time("Discover project structure")]
  public Application(string rootDirectory)
  {
    _rootDirectory = rootDirectory;
    var solutionFilepaths = Directory.GetFiles(_rootDirectory, "*.sln", SearchOption.AllDirectories)
      .Where(path => !ExclusionPatterns.Any(path.Contains));
    var solutions = new Dictionary<string, Solution>();

    foreach (var filepath in solutionFilepaths)
    {
      var name = Path.GetFileName(filepath);
      Log.Debug("Solution {SolutionName}: {SolutionPath}", name, filepath);
      solutions[name] = new Solution(name, filepath);
    }

    _solutionByName = solutions.ToFrozenDictionary();
  }
}

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

  public override string ToString()
  {
    return $"Solution {Name}: {AbsolutePath}";
  }
}