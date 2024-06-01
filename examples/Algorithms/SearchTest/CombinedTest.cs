using System.Data;
using Search;

namespace SearchTest;

public class CombinedTest
{
  private static readonly IList<ISearch<int>> AllSearchers = new List<ISearch<int>>
  {
    new ExponentialSearch<int>(), 
    new LinearSearch<int>(),
    new BinarySearchIterative<int>(), 
    new BinarySearchRecursive<int>()
  };
  
  [Fact]
  public void AllSearchShouldFindTheSameIndex_Case1()
  {
    var allEqual = AllSearchers
      .Select(searcher => searcher.FindIndex(new [] {-2655, 8483}, -2655))
      .Distinct().Count() == 1;
    Assert.True(allEqual);
  }
  
  [Fact]
  public void AllSearchShouldFindTheSameIndex_Case2()
  {
    var allEqual = AllSearchers
      .Select(searcher => searcher.FindIndex(new[] { 1236, 4150, 4873, 6397, 6662, 6677, 8344, 9447, 9659, 9682 }, 6677))
      .Distinct().Count() == 1;
    Assert.True(allEqual);
  }
  
  [Fact]
  public void AllSearchShouldFindTheSameIndex_Case3()
  {
    var allEqual = AllSearchers
      .Select(searcher => searcher.FindIndex(new[] { 2198, 2818, 3387, 4202, 7216, 8716 }, 8716))
      .Distinct().Count() == 1;
    Assert.True(allEqual);
  }
  
  [Fact]
  public void AllSearchShouldFindTheSameIndex_Case4()
  {
    var allEqual = AllSearchers
      .Select(searcher => searcher.FindIndex(new [] {3656, 9126}, 3656))
      .Distinct().Count() == 1;
    Assert.True(allEqual);
  }
  
  [Fact]
  public void AllSearchShouldThrowIfItemNotInList_Case1()
  {
    foreach (var searcher in AllSearchers)
    {
      Assert.Throws<DataException>(() =>
        searcher.FindIndex(new int[] {}, -11));
    }
  }
  
  [Fact]
  public void AllSearchShouldThrowIfItemNotInList_Case2()
  {
    foreach (var searcher in AllSearchers)
    {
      Assert.Throws<DataException>(() =>
        searcher.FindIndex(new[] { 1236, 4150, 4873, 6397, 6662, 6677, 8344, 9447, 9659, 9682 }, 5000));
    }
  }
  
  [Fact]
  public void AllSearchShouldThrowIfItemNotInList_Case3()
  {
    foreach (var searcher in AllSearchers)
    {
      Assert.Throws<DataException>(() =>
        searcher.FindIndex(new[] { 2198, 2818, 3387, 4202, 7216, 8716 }, 8717));
    }
  }
  
  [Fact]
  public void AllSearchShouldThrowIfItemNotInList_Case4()
  {
    foreach (var searcher in AllSearchers)
    {
      Assert.Throws<DataException>(() =>
        searcher.FindIndex(new [] {3656, 9126}, 3655));
    }
  }
}