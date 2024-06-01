using System.Data;
using Search;

namespace SearchTest;

public class ExponentialSearchTest
{
  [Fact]
  public void ExponentialSearchShouldReturnIndexOfItemInList_Case1()
  {
    IList<int> collection = new[] { 1 };
    int target = 1;
    int expected = 0;

    var searcher = new ExponentialSearch<int>();
    Assert.Equal(searcher.FindIndex(collection, target), expected);
  }

  [Fact]
  public void ExponentialSearchShouldReturnIndexOfItemInList_Case2()
  {
    IList<int> collection = new[] { 1, 3, 4, 6, 7, 8, 10, 13 };
    int target = 7;
    int expected = 4;

    var searcher = new ExponentialSearch<int>();
    Assert.Equal(searcher.FindIndex(collection, target), expected);
  }

  [Fact]
  public void ExponentialSearchShouldReturnIndexOfItemInList_Case3()
  {
    IList<int> collection = new[] { 3, 4, 5, 6, 7, 8, 9 };
    int target = 3;
    int expected = 0;

    var searcher = new ExponentialSearch<int>();
    Assert.Equal(searcher.FindIndex(collection, target), expected);
  }

  [Fact]
  public void ExponentialSearchShouldReturnIndexOfItemInList_Case4()
  {
    IList<int> collection = new[] { 3, 4, 5, 6, 7, 8, 9 };
    int target = 9;
    int expected = 6;

    var searcher = new ExponentialSearch<int>();
    Assert.Equal(searcher.FindIndex(collection, target), expected);
  }
  
  [Fact]
  public void ExponentialSearchShouldThrowIfItemNotInList_Case1()
  {
    IList<int> collection = new int[] { };
    int target = 1;

    var searcher = new ExponentialSearch<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(collection, target));
  }

  [Fact]
  public void ExponentialSearchShouldThrowIfItemNotInList_Case2()
  {
    IList<int> collection = new[] { 1, 3, 4, 6, 7, 8, 10, 13 };
    int target = 9;

    var searcher = new ExponentialSearch<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(collection, target));
  }

  [Fact]
  public void ExponentialSearchShouldThrowIfItemNotInList_Case3()
  {
    IList<int> collection = new[] { 3, 4, 5, 6, 7, 8, 9 };
    int target = 0;

    var searcher = new ExponentialSearch<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(collection, target));
  }

  [Fact]
  public void ExponentialSearchShouldThrowIfItemNotInList_Case4()
  {
    IList<int> collection = new[] { 3, 4, 5, 6, 7, 8, 9 };
    int target = 10;

    var searcher = new ExponentialSearch<int>();
    Assert.Throws<DataException>(() => searcher.FindIndex(collection, target));
  }
}