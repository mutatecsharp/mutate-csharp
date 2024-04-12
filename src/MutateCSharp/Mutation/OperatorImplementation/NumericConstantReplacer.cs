using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SemanticModel = Microsoft.CodeAnalysis.SemanticModel;

namespace MutateCSharp.Mutation.OperatorImplementation;


public class NumericConstantReplacer(SemanticModel semanticModel)
  : AbstractMutationOperator<LiteralExpressionSyntax>(semanticModel)
{
  // C# does not support specifying short, ushort, byte, and sbyte literals
  // These types have to be obtained through casting / explicit conversion / assignment
  private readonly ISet<SpecialType>
    _supportedNumericTypes =
      new HashSet<SpecialType>
      {
        // Signed numeric types
        SpecialType.System_Int32, SpecialType.System_Int64,
        // Unsigned numeric types
        SpecialType.System_UInt32, SpecialType.System_UInt64,
        // Floating point types
        SpecialType.System_Single, SpecialType.System_Double,
        SpecialType.System_Decimal
      }.ToFrozenSet();

  private static dynamic ConvertToNumericType(object value,
    SpecialType type)
  {
    return type switch
    {
      // Signed numeric types
      SpecialType.System_Int32 => Convert.ToInt32(value),
      SpecialType.System_Int64 => Convert.ToInt64(value),
      // Unsigned numeric types
      SpecialType.System_UInt32 => Convert.ToUInt32(value),
      SpecialType.System_UInt64 => Convert.ToUInt64(value),
      // Floating point types
      SpecialType.System_Single => Convert.ToSingle(value),
      SpecialType.System_Double => Convert.ToDouble(value),
      SpecialType.System_Decimal => Convert.ToDecimal(value),
      // Unhandled cases
      _ => throw new NotSupportedException(
        $"{type} cannot be downcast to its numeric type.")
    };
  }

  protected override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type?.SpecialType;
    return type.HasValue && _supportedNumericTypes.Contains(type.Value);
  }

  protected override string OriginalExpressionTemplate(
    LiteralExpressionSyntax originalNode)
  {
    return "{0}";
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    LiteralExpressionSyntax originalNode)
  {
    var type = SemanticModel.GetTypeInfo(originalNode).Type?.SpecialType!;
    var value = ConvertToNumericType(originalNode.Token.Value!, type.Value);
    var result = new List<(int, string)>();

    // Mutation: value => 0
    if (value != 0) result.Add((1, "0"));
    // Mutation: value => -value
    // The ulong type doesn't support the unary - operator.
    if (type != SpecialType.System_UInt64 && value != 0)
      result.Add((2, "-{0}"));
    // Mutation: value => -1
    // Negative signed value cannot be cast to an unsigned value.
    if (type != SpecialType.System_UInt64 &&
        type != SpecialType.System_UInt32 && value != -1) result.Add((3, "-1"));
    // Mutation: value => value - 1
    result.Add((4, "{0} - 1"));
    // Mutation: value => value + 1
    result.Add((5, "{0} + 1"));

    return result.ToImmutableArray();
  }

  protected override IList<string> ParameterTypes(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(ReturnType(originalNode));
  }

  protected override string ReturnType(LiteralExpressionSyntax originalNode)
  {
    return SemanticModel.GetTypeInfo(originalNode).Type?.Name ?? string.Empty;
  }

  protected override string
    SchemaBaseName(LiteralExpressionSyntax originalNode)
  {
    var typeName = SemanticModel.GetTypeInfo(originalNode).Type?.Name;
    return typeName is not null ? $"Replace{typeName}Constant" : string.Empty;
  }
}