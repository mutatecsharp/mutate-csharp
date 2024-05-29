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
  
  [Theory]
  [InlineData(new [] {-2655, 8483}, -2655)]
  [InlineData(
    new[] { 1236, 4150, 4873, 6397, 6662, 6677, 8344, 9447, 9659, 9682 }, 6677)]
  [InlineData(new[] { 2198, 2818, 3387, 4202, 7216, 8716 }, 8716)]
  [InlineData(new [] {3656, 9126}, 3656)]
  public void AllSearchShouldFindTheSameIndex(IList<int> collection, int target)
  {
    var allEqual = AllSearchers
      .Select(searcher => searcher.FindIndex(collection, target))
      .Distinct().Count() == 1;
    Assert.True(allEqual);
  }
  
  [Theory]
  [InlineData(new int[] {}, -11)]
  [InlineData(
    new[] { 1236, 4150, 4873, 6397, 6662, 6677, 8344, 9447, 9659, 9682 }, 5000)]
  [InlineData(new[] { 2198, 2818, 3387, 4202, 7216, 8716 }, 8717)]
  [InlineData(new [] {3656, 9126}, 3655)]
  public void AllSearchShouldThrowIfItemNotInList(IList<int> collection, int target)
  {
    foreach (var searcher in AllSearchers)
    {
      Assert.Throws<DataException>(() =>
        searcher.FindIndex(collection, target));
    }
  }
}