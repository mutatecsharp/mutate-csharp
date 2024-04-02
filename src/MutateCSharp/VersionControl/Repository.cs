using LibGit2Sharp;
using MethodTimer;
using Serilog;

namespace MutateCSharp.VersionControl;

public sealed class Repository: IDisposable
{
  private readonly LibGit2Sharp.Repository _repository;
  public string RootDirectory { get; }

  public Repository(string remoteUri, string localRootDirectory, string branchName)
  {
    RootDirectory = localRootDirectory;
    _repository = Clone(remoteUri);
    Checkout(branchName);
  }

  [Time("Clone repository")]
  private LibGit2Sharp.Repository Clone(string remoteUri)
  {
    try
    {
      var cloneOptions = new CloneOptions { RecurseSubmodules = true };
      LibGit2Sharp.Repository.Clone(remoteUri, RootDirectory, cloneOptions);
      Log.Information("Clone repository to: {Directory}", RootDirectory);
    }
    catch (NameConflictException)
    {
      Log.Information("Proceed with existing repository: {Directory}", RootDirectory);
    }
    
    return new LibGit2Sharp.Repository(RootDirectory);
  }
  
  private void Checkout(string branchName)
  {
    if (!string.IsNullOrEmpty(branchName))
    {
      var branch = _repository.Branches[branchName];
      if (branch is null)
      {
        throw new ArgumentException($"Specified branch {branchName} does not exist");
      }
      
      // Update HEAD to point to checked out branch
      Commands.Checkout(_repository, branch);
      _repository.Branches.Update(branch, b => b.TrackedBranch = branch.CanonicalName);
    }
    
    Log.Information("Checkout branch: {Branch}", _repository.Head.FriendlyName);
  }

  public void Dispose()
  {
    _repository.Dispose();
  }
}