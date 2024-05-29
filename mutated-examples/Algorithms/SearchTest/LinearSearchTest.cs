using System.Data;
using Search;

namespace SearchTest;

public class LinearSearchTest
{
  [Theory]
  [InlineData(new[] {1}, 1, 0)]
  [InlineData(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 7, 4)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 3, 0)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 9, 6)]
  public void LinearSearchShouldReturnIndexOfItemInList(
    IList<int> collection, int target, int expected)
  {
    var searcher = new LinearSearch<int>();
    Assert.Equal(searcher.FindIndex(collection, target), expected);
  }
  
  [Theory]
  [InlineData(new int[] {}, 1)]
  [InlineData(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 9)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 0)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 10)]
  public void LinearSearchShouldThrowIfItemNotInList(
    IList<int> collection, int target)
  {
    var searcher = new LinearSearch<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(collection, target));
  }
}