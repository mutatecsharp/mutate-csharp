using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MutateCSharp.Util;

public static class CodeAnalysisUtil
{
  [Flags]
  public enum SupportedType
  {
    UnsignedIntegral = 1 << 0,
    SignedIntegral = 1 << 1,
    FloatingPoint = 1 << 2,
    Boolean = 1 << 3,
    Character = 1 << 4,
    String = 1 << 5,
    NotSupported = 1 << 6,
    Integral = UnsignedIntegral | SignedIntegral,
    Numeric = Integral | FloatingPoint,
    All = Integral | FloatingPoint | Boolean | Character | String
  }

  public record TypeSignature(
    SupportedType OperandType,
    SupportedType ReturnType);

  public static readonly TypeSignature[] IncrementOrDecrementTypeSignature
    =
    [
      new TypeSignature(SupportedType.UnsignedIntegral, SupportedType.UnsignedIntegral),
      new TypeSignature(SupportedType.SignedIntegral, SupportedType.SignedIntegral),
      new TypeSignature(SupportedType.FloatingPoint, SupportedType.FloatingPoint),
      new TypeSignature(SupportedType.Character, SupportedType.Character)
    ];

  // char arithmetic operations return int type
  public static readonly TypeSignature[] ArithmeticTypeSignature
    =
    [
      new TypeSignature(SupportedType.Numeric | SupportedType.Character,
        SupportedType.Numeric)
    ];

  // char bitwise logical operations return int type
  public static readonly TypeSignature[] BitwiseLogicalTypeSignature
    =
    [
      new TypeSignature(SupportedType.Integral | SupportedType.Character,
        SupportedType.Integral),
      new TypeSignature(SupportedType.Boolean, SupportedType.Boolean)
    ];

  public static readonly TypeSignature[] BitwiseShiftTypeSignature
    = [new TypeSignature(SupportedType.Integral, SupportedType.Integral)];

  public static readonly TypeSignature[] BooleanLogicalTypeSignature
    = [new TypeSignature(SupportedType.Boolean, SupportedType.Boolean)];

  public static readonly TypeSignature[] EqualityTypeSignature
    = [new TypeSignature(SupportedType.All, SupportedType.Boolean)];

  public static readonly TypeSignature[] InequalityTypeSignature
    =
    [
      new TypeSignature(SupportedType.Numeric | SupportedType.Character,
        SupportedType.Boolean)
    ];

  public record UnaryOp(SyntaxKind ExprKind, SyntaxKind TokenKind, ICollection<TypeSignature> TypeSignatures)
  {
    public override string ToString() => SyntaxFacts.GetText(TokenKind);
  }

  public record BinOp(
    SyntaxKind ExprKind,
    SyntaxKind TokenKind,
    ICollection<TypeSignature> TypeSignatures)
  {
    public override string ToString() => SyntaxFacts.GetText(TokenKind);
  }

  public static SupportedType GetSpecialTypeClassification(SpecialType type)
  {
    return type switch
    {
      SpecialType.System_Boolean
        => SupportedType.Boolean,
      SpecialType.System_Char // char type is integral in C#
        or SpecialType.System_SByte
        or SpecialType.System_Int16
        or SpecialType.System_Int32
        or SpecialType.System_Int64
        => SupportedType.SignedIntegral,
      SpecialType.System_Byte
        or SpecialType.System_UInt16
        or SpecialType.System_UInt32
        or SpecialType.System_UInt64
        => SupportedType.UnsignedIntegral,
      SpecialType.System_Single
        or SpecialType.System_Double
        or SpecialType.System_Decimal
        => SupportedType.FloatingPoint,
      SpecialType.System_String
        => SupportedType.String,
      _ => SupportedType.NotSupported
    };
  }

  public static readonly FrozenDictionary<string, SyntaxKind>
    SupportedOverloadedOperators =
      new Dictionary<string, SyntaxKind>
      {
        {
          WellKnownMemberNames.AdditionOperatorName,
          SyntaxKind.AddAssignmentExpression
        },
        {
          WellKnownMemberNames.SubtractionOperatorName,
          SyntaxKind.SubtractAssignmentExpression
        },
        {
          WellKnownMemberNames.MultiplyOperatorName,
          SyntaxKind.MultiplyAssignmentExpression
        },
        {
          WellKnownMemberNames.DivisionOperatorName,
          SyntaxKind.DivideAssignmentExpression
        },
        {
          WellKnownMemberNames.ModulusOperatorName,
          SyntaxKind.ModuloAssignmentExpression
        },
        {
          WellKnownMemberNames.BitwiseAndOperatorName,
          SyntaxKind.AndAssignmentExpression
        },
        {
          WellKnownMemberNames.BitwiseOrOperatorName,
          SyntaxKind.OrAssignmentExpression
        },
        {
          WellKnownMemberNames.ExclusiveOrOperatorName,
          SyntaxKind.ExclusiveOrAssignmentExpression
        },
        {
          WellKnownMemberNames.LeftShiftOperatorName,
          SyntaxKind.LeftShiftAssignmentExpression
        },
        {
          WellKnownMemberNames.RightShiftOperatorName,
          SyntaxKind.RightShiftAssignmentExpression
        },
        {
          WellKnownMemberNames.UnsignedRightShiftOperatorName,
          SyntaxKind.UnsignedRightShiftAssignmentExpression
        }
      }.ToFrozenDictionary();

  public static bool IsAString(this SyntaxNode node)
  {
    return node.IsKind(SyntaxKind.StringLiteralExpression) ||
           node.IsKind(SyntaxKind.Utf8StringLiteralExpression) ||
           node.IsKind(SyntaxKind.InterpolatedStringExpression);
  }

  public static IDictionary<SyntaxKind, IMethodSymbol>
    GetOverloadedOperatorsInUserDefinedType(INamedTypeSymbol customType)
  {
    return customType
      .GetMembers().OfType<IMethodSymbol>()
      .Where(method => method.MethodKind == MethodKind.UserDefinedOperator)
      .Where(method => SupportedOverloadedOperators.ContainsKey(method.Name))
      .ToDictionary(method => SupportedOverloadedOperators[method.Name],
        method => method);
  }

  public static dynamic GetNumericMinValue(SpecialType type)
  {
    return type switch
    {
      SpecialType.System_Char => char.MinValue,
      SpecialType.System_SByte => sbyte.MinValue,
      SpecialType.System_Int16 => short.MinValue,
      SpecialType.System_Int32 => int.MinValue,
      SpecialType.System_Int64 => long.MinValue,
      SpecialType.System_Byte => byte.MinValue,
      SpecialType.System_UInt16 => short.MinValue,
      SpecialType.System_UInt32 => uint.MinValue,
      SpecialType.System_UInt64 => ulong.MinValue,
      SpecialType.System_Single => float.MinValue,
      SpecialType.System_Double => double.MinValue,
      SpecialType.System_Decimal => decimal.MinValue,
      _ => throw new NotSupportedException(
        $"{type} cannot be downcast to its numeric type.")
    };
  }

  public static dynamic GetNumericMaxValue(SpecialType type)
  {
    return type switch
    {
      SpecialType.System_Char => char.MaxValue,
      SpecialType.System_SByte => sbyte.MaxValue,
      SpecialType.System_Int16 => short.MaxValue,
      SpecialType.System_Int32 => int.MaxValue,
      SpecialType.System_Int64 => long.MaxValue,
      SpecialType.System_Byte => byte.MaxValue,
      SpecialType.System_UInt16 => short.MaxValue,
      SpecialType.System_UInt32 => uint.MaxValue,
      SpecialType.System_UInt64 => ulong.MaxValue,
      SpecialType.System_Single => float.MinValue,
      SpecialType.System_Double => double.MinValue,
      SpecialType.System_Decimal => decimal.MinValue,
      _ => throw new NotSupportedException(
        $"{type} cannot be downcast to its numeric type.")
    };
  }

  public static dynamic ConvertNumber(object value, SpecialType type)
  {
    return type switch
    {
      SpecialType.System_Char => Convert.ToChar(value),
      // Signed numeric types
      SpecialType.System_Int16 => Convert.ToInt16(value),
      SpecialType.System_Int32 => Convert.ToInt32(value),
      SpecialType.System_Int64 => Convert.ToInt64(value),
      // Unsigned numeric types
      SpecialType.System_UInt16 => Convert.ToUInt16(value),
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
}