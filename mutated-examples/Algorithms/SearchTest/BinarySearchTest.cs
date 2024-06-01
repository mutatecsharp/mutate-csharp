using System.Data;
using Search;

namespace SearchTest;

public class BinarySearchTest
{
  [Fact]
  public void BinarySearchIterativeShouldReturnIndexOfItemInList_Case1()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Equal(0, searcher.FindIndex(new[] {1}, 1));
  }
  
  [Fact]
  public void BinarySearchIterativeShouldReturnIndexOfItemInList_Case2()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Equal(4, searcher.FindIndex(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 7));
  }
  
  [Fact]
  public void BinarySearchIterativeShouldReturnIndexOfItemInList_Case3()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Equal(0, searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 3));
  }
  
  [Fact]
  public void BinarySearchIterativeShouldReturnIndexOfItemInList_Case4()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Equal(6, searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 9));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldReturnIndexOfItemInList_Case1()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Equal(0, searcher.FindIndex(new[] {1}, 1));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldReturnIndexOfItemInList_Case2()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Equal(4, searcher.FindIndex(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 7));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldReturnIndexOfItemInList_Case3()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Equal(0, searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 3));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldReturnIndexOfItemInList_Case4()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Equal(6, searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 9));
  }

  [Fact]
  public void BinarySearchIterativeShouldThrowIfItemNotInList_Case1()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new int[] {}, 1));
  }
  
  [Fact]
  public void BinarySearchIterativeShouldThrowIfItemNotInList_Case2()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 9));
  }
  
  [Fact]
  public void BinarySearchIterativeShouldThrowIfItemNotInList_Case3()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 0));
  }
  
  [Fact]
  public void BinarySearchIterativeShouldThrowIfItemNotInList_Case4()
  {
    var searcher = new BinarySearchIterative<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 10));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldThrowIfItemNotInList_Case1()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new int[] {}, 1));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldThrowIfItemNotInList_Case2()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new[] {1, 3, 4, 6, 7, 8, 10, 13}, 9));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldThrowIfItemNotInList_Case3()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 0));
  }
  
  [Fact]
  public void BinarySearchRecursiveShouldThrowIfItemNotInList_Case4()
  {
    var searcher = new BinarySearchRecursive<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(new [] {3, 4, 5, 6, 7, 8, 9}, 10));
  }
}