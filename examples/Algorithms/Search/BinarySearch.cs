using System;
using System.Collections.Generic;
using System.Data;

namespace Search;

public class BinarySearchIterative<T> : ISearch<T> where T: IComparable<T>
{
  public int FindIndex(IList<T> collection, T target)
  {
    var l = 0;
    var r = collection.Count - 1;

    while (l <= r)
    {
      var mid = l + (r - l) / 2;
      var current = collection[mid];

      switch (target.CompareTo(current))
      {
        case > 0:
          l = mid + 1;
          break;
        case < 0:
          r = mid - 1;
          break;
        default:
          return mid;
      }
    }

    throw new DataException();
  }
}

public class BinarySearchRecursive<T> : ISearch<T> where T : IComparable<T>
{
  public int FindIndex(IList<T> collection, T target)
  {
    return FindIndex(collection, target, 0, collection.Count - 1);
  }

  public static int FindIndex(IList<T> collection, T target, int left, int right)
  {
    if (left > right) throw new DataException();
    var mid = left + (right - left) / 2;
    var result = target.CompareTo(collection[mid]);

    return result switch
    {
      > 0 => FindIndex(collection, target, mid + 1, right),
      < 0 => FindIndex(collection, target, left, mid - 1),
      _ => mid
    };
  }
}