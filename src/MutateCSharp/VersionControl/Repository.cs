using LibGit2Sharp;
using Serilog;

namespace MutateCSharp.VersionControl;

public sealed class Repository
{
  private readonly string _localDirectory;

  public Repository(string remoteUri, string localDirectory, string branchName)
  {
    try
    {
      var cloneOptions = new CloneOptions
      {
        RecurseSubmodules = true,
        BranchName = string.IsNullOrEmpty(branchName) ? null : branchName
      };
      _localDirectory = LibGit2Sharp.Repository.Clone(remoteUri, localDirectory, cloneOptions);
      Log.Verbose("Cloned repository into: {Directory}", _localDirectory);
    }
    catch (NameConflictException)
    {
      _localDirectory = Path.GetFullPath(localDirectory);
      Log.Information("Proceed with existing repository: {Directory}", _localDirectory);
    }
    
    using var repo = new LibGit2Sharp.Repository(_localDirectory);
    var currentBranchName = repo.Head.FriendlyName; 
    
    if (string.IsNullOrEmpty(branchName))
    {
      Log.Information("Checkout branch: {Branch}", repo.Head.FriendlyName);
    }
    else if (!string.IsNullOrEmpty(branchName) && !branchName.Equals(currentBranchName))
    {
      Log.Warning("Specified branch {SpecifiedBranch} is invalid, checked out {DefaultBranch} instead", branchName, currentBranchName);
    }
    else
    {
      Log.Verbose("Checkout branch: {Branch}", repo.Head.FriendlyName);
    }
  }
}