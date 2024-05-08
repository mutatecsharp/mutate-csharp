using System;
using System.Collections.Generic;
using System.Data;

namespace Search;

public class LinearSearch<T> : ISearch<T> where T: IComparable<T>
{
  public int FindIndex(IList<T> collection, T target)
  {
    for (var i = 0; i < collection.Count; i++)
    {
      if (collection[i].CompareTo(target) == 0) return i;
    }

    throw new DataException();
  }
}