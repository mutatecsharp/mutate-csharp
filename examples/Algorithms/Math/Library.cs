namespace Math;

public static class Library
{
  public static long BinomialCoefficient(long n, long k)
  {
    if (n < 0 || k < 0 || k > n) throw new ArgumentException();

    k = System.Math.Min(n - k, k);
    var numerator = 1L;
    var denominator = 1L;

    for (var i = n - k + 1; i <= n; i++) numerator *= i;
    for (var i = k; i > 1; i--) denominator *= i;

    return numerator / denominator;
  }

  public static long Factorial(long n)
  {
    if (n < 0) throw new ArgumentException();

    var result = 1L;
    for (var i = n; i > 0; i--) result *= i;

    return result;
  }

  public static bool IsPerfectNumber(long n)
  {
    if (n < 0) throw new ArgumentException();
    if (n == 0) return false;

    var sum = 0L;

    for (var i = 1; i < n; i++) if (n % i == 0) sum += i;

    return sum == n;
  }

  public static bool IsPerfectSquare(long n)
  {
    if (n < 0) throw new ArgumentException();

    var sqrt = Convert.ToInt64(System.Math.Sqrt(n));
    return sqrt * sqrt == n;
  }
}