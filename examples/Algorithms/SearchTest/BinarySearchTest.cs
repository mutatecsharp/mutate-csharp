using System.Data;
using Search;

namespace SearchTest;

public class BinarySearchTest
{
  [Theory]
  [InlineData(new[] {1}, 1, 0)]
  [InlineData(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 7, 4)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 3, 0)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 9, 6)]
  public void BinarySearchIterativeShouldReturnIndexOfItemInList(
    IList<int> collection, int target, int expected)
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Equal(searcher.FindIndex(collection, target), expected);
  }
  
  [Theory]
  [InlineData(new[] {1}, 1, 0)]
  [InlineData(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 7, 4)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 3, 0)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 9, 6)]
  public void BinarySearchRecursiveShouldReturnIndexOfItemInList(
    IList<int> collection, int target, int expected)
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Equal(searcher.FindIndex(collection, target), expected);
  }
  
  [Theory]
  [InlineData(new int[] {}, 1)]
  [InlineData(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 9)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 0)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 10)]
  public void BinarySearchIterativeShouldThrowIfItemNotInList(
    IList<int> collection, int target)
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(collection, target));
  }
  
  [Theory]
  [InlineData(new int[] {}, 1)]
  [InlineData(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 9)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 0)]
  [InlineData(new [] {3, 4, 5, 6, 7, 8, 9}, 10)]
  public void BinarySearchRecursiveShouldThrowIfItemNotInList(
    IList<int> collection, int target)
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(collection, target));
  }
}