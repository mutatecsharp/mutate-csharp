using System;
using System.Collections.Generic;
using System.Data;

namespace Search;

public class ExponentialSearch<T> : ISearch<T> where T: IComparable<T>
{
  public int FindIndex(IList<T> collection, T target)
  {
    if (collection.Count == 0) throw new DataException();

    var bound = 1;
    while (bound < collection.Count && collection[bound].CompareTo(target) < 0)
    {
      bound *= 2;
    }

    return BinarySearchRecursive<T>.FindIndex(collection, target, bound / 2,
      Math.Min(bound, collection.Count - 1));
  }
}