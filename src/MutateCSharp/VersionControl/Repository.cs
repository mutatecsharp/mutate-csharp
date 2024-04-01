using LibGit2Sharp;
using MethodTimer;
using Serilog;

namespace MutateCSharp.VersionControl;

public sealed class Repository: IDisposable
{
  private readonly LibGit2Sharp.Repository _repository;

  public Repository(string remoteUri, string localDirectory, string branchName)
  {
    _repository = Clone(remoteUri, localDirectory);
    Checkout(branchName);
  }

  [Time("Clone repository")]
  private static LibGit2Sharp.Repository Clone(string remoteUri, string localDirectory)
  {
    try
    {
      var cloneOptions = new CloneOptions { RecurseSubmodules = true };
      LibGit2Sharp.Repository.Clone(remoteUri, localDirectory, cloneOptions);
      Log.Information("Clone repository to: {Directory}", localDirectory);
    }
    catch (NameConflictException)
    {
      Log.Information("Proceed with existing repository: {Directory}", localDirectory);
    }
    
    return new LibGit2Sharp.Repository(localDirectory);
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