using System.Collections.Immutable;
using Serilog;

namespace MutateCSharp.SUT;

public static class ProjectFactory
{
  private static readonly string[] TestProjectNamePatterns = ["Test", "XUnit"];
  
  public static Project InspectProject(string name, string absolutePath)
  {
    var project = new Microsoft.Build.Evaluation.Project(absolutePath);
    
    if (TestProjectNamePatterns.Any(name.Contains))
    {
      var projectItems = project.Items.Where(item => item.ItemType is "Compile").ToList();
      projectItems.ForEach(item => Log.Debug("\t\tTest File: {Filepath}", item.EvaluatedInclude));
      return new TestProject(
        name, absolutePath, project,
        projectItems.Select(item => new TestFile(item.UnevaluatedInclude, item.EvaluatedInclude)).ToImmutableArray<File>()
        );
    }
    else
    {
      var projectItems = project.Items
        .Where(item => item.ItemType is "Compile" && item.EvaluatedInclude.EndsWith(".cs")).ToList();
      projectItems.ForEach(item => Log.Debug("\t\tSource File: {Filepath}", item.EvaluatedInclude));
      return new ProjectUnderTest(
        name, absolutePath, project,
        projectItems.Select(item => new FileUnderTest(item.UnevaluatedInclude, item.EvaluatedInclude)).ToImmutableArray<File>()
        );
    }
  }
}

public abstract class Project: IDisposable
{
  protected readonly Microsoft.Build.Evaluation.Project _project;
  protected readonly IList<File> _files;
  public string Name { get; }
  public string AbsolutePath { get; }

  protected Project(string name, string absolutePath, Microsoft.Build.Evaluation.Project project, IList<File> files)
  {
    Name = name;
    AbsolutePath = absolutePath;
    _project = project;
    _files = files;
  }

  public int FileCount()
  {
    return _files.Count;
  }

  public IEnumerable<File> GetFiles()
  {
    return _files;
  }

  public void Dispose()
  {
    _project.ProjectCollection.Dispose();
    GC.SuppressFinalize(this);
  }
}

public class TestProject : Project
{
  public TestProject(string name, string absolutePath, Microsoft.Build.Evaluation.Project project, IList<File> files) : base(name, absolutePath, project, files)
  {
  }

  public override string ToString()
  {
    return $"Test Project {Name}: {AbsolutePath}";
  }
}

public class ProjectUnderTest : Project
{
  public ProjectUnderTest(string name, string absolutePath, Microsoft.Build.Evaluation.Project project, IList<File> files) : base(name, absolutePath, project, files)
  {
  }

  public override string ToString()
  {
    return $"Source Project {Name}: {AbsolutePath}";
  }
}