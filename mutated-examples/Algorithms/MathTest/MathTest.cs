namespace MathTest;

public class MathTest
{
  [Theory]
  [InlineData(0, 0, 1)]
  [InlineData(10, 5, 252)]
  [InlineData(100, 5, 75287520)]
  [InlineData(1000L, 5L, 8250291250200L)]
  public void BinomialCoefficientShouldBeCorrect(long n, long k, long expected)
  {
    Assert.Equal(Math.Library.BinomialCoefficient(n, k), expected);
  }

  [Theory]
  [InlineData(-1, -1)]
  [InlineData(-1, 0)]
  [InlineData(0, -1)]
  [InlineData(int.MinValue, 0)]
  [InlineData(int.MaxValue, int.MinValue)]
  public void BinomialCoefficientShouldThrowIfArgumentsAreLessThanZero(long n,
    long k)
  {
    Assert.Throws<ArgumentException>(() =>
      Math.Library.BinomialCoefficient(n, k));
  }

  [Theory]
  [InlineData(0, 1)]
  [InlineData(1, 1)]
  [InlineData(10, 3628800L)]
  [InlineData(15, 1307674368000L)]
  public void FactorialShouldBeCorrect(long n, long expected)
  {
    Assert.Equal(Math.Library.Factorial(n), expected);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(-2)]
  [InlineData(int.MinValue)]
  [InlineData(long.MinValue)]
  public void FactorialShouldThrowIfArgumentsAreLessThanZero(long n)
  {
    Assert.Throws<ArgumentException>(() => Math.Library.Factorial(n));
  }

  [Theory]
  [InlineData(0, false)]
  [InlineData(1, false)]
  [InlineData(2, false)]
  [InlineData(6, true)]
  [InlineData(28, true)]
  [InlineData(496, true)]
  [InlineData(400, false)]
  public void IsPerfectNumberShouldBeCorrect(long n, bool expected)
  {
    Assert.Equal(Math.Library.IsPerfectNumber(n), expected);
  }
  
  [Theory]
  [InlineData(-1)]
  [InlineData(-2)]
  [InlineData(int.MinValue)]
  [InlineData(long.MinValue)]
  public void IsPerfectNumberShouldThrowIfArgumentsAreLessThanZero(long n)
  {
    Assert.Throws<ArgumentException>(() => Math.Library.IsPerfectNumber(n));
  }

  [Theory]
  [InlineData(0, true)]
  [InlineData(1, true)]
  [InlineData(2, false)]
  [InlineData(4, true)]
  [InlineData(16, true)]
  [InlineData(521284, true)]
  [InlineData(521283, false)]
  [InlineData(521285, false)]
  public void IsPerfectSquareShouldBeCorrect(long n, bool expected)
  {
    Assert.Equal(Math.Library.IsPerfectSquare(n), expected);
  }

  [Theory]
  [InlineData(-1)]
  [InlineData(-2)]
  [InlineData(int.MinValue)]
  [InlineData(long.MinValue)]
  public void IsPerfectSquareShouldThrowIfArgumentsAreLessThanZero(long n)
  {
    Assert.Throws<ArgumentException>(() => Math.Library.IsPerfectSquare(n));
  }
}