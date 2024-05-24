namespace CalculateMultiples;

class Example
{
  public static long SumOfMultiplesOf3And5()
  {
    long sum = 0;
  
    for (var i = 0; i < 1000; i++)
    {
      if (i % 3 == 0 || i % 5 == 0) sum += i;
    }

    return sum;
  }
  
  public static void Main()
  {
    Console.WriteLine(SumOfMultiplesOf3And5());
  }
}
