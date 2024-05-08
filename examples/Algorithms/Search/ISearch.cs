using System;
using System.Collections.Generic;

namespace Search;

public interface ISearch<T> where T: IComparable<T>
{
  public int FindIndex(IList<T> collection, T target);
}