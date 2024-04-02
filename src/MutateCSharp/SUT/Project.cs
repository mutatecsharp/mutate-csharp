using Serilog;

namespace MutateCSharp.SUT;

public static class ProjectFactory
{
  private static readonly string[] TestProjectNamePatterns = ["Test", "XUnit"];
  
  public static Project InspectProject(string name, string absolutePath)
  {
    return TestProjectNamePatterns.Any(absolutePath.Contains) 
      ? new ProjectTests(name, absolutePath) 
      : new ProjectUnderTest(name, absolutePath);
  }
}

public abstract class Project
{
  protected readonly Microsoft.Build.Evaluation.Project _project;
  public string Name { get; }
  public string AbsolutePath { get; }

  protected Project(string name, string absolutePath)
  {
    Name = name;
    AbsolutePath = absolutePath;
    _project = new Microsoft.Build.Evaluation.Project(absolutePath);
  }
}

public class ProjectTests : Project
{
  private readonly IList<File> _files;
  
  public ProjectTests(string name, string absolutePath) : base(name, absolutePath)
  {
    var projectItems = _project.Items.Where(item => item.ItemType is "Compile" && item.EvaluatedInclude.EndsWith(".cs")).ToList();
    projectItems.ForEach(item => Log.Debug("\t\tTest File: {Filepath}", item.EvaluatedInclude));
    _files = projectItems.Select(item => new TestFile(item.UnevaluatedInclude, item.EvaluatedInclude)).ToList<File>();
  }

  public override string ToString()
  {
    return $"Test Project {Name}: {AbsolutePath}";
  }
}

public class ProjectUnderTest : Project
{
  private readonly IList<File> _files;
  
  public ProjectUnderTest(string name, string absolutePath) : base(name, absolutePath)
  {
    var projectItems = _project.Items.Where(item => item.ItemType is "Compile").ToList();
    projectItems.ForEach(item => Log.Debug("\t\tSource File: {Filepath}", item.EvaluatedInclude));
    _files = projectItems.Select(item => new FileUnderTest(item.UnevaluatedInclude, item.EvaluatedInclude)).ToList<File>();
  }

  public override string ToString()
  {
    return $"Source Project {Name}: {AbsolutePath}";
  }
}