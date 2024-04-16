using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MutateCSharp.Util;

public static class CodeAnalysisUtil
{
  [Flags]
  public enum SupportedType
  {
    Integral = 1 << 0,
    FloatingPoint = 1 << 1,
    Boolean = 1 << 2,
    String = 1 << 3,
    NotSupported = 1 << 4,
    Numeric = Integral | FloatingPoint,
    All = Integral | FloatingPoint | Boolean | String
  }

  public record TypeSignature(
    SupportedType OperandType,
    SupportedType ReturnType);

  public static readonly TypeSignature[] ArithmeticTypeSignature
    = [ new TypeSignature(SupportedType.Numeric, SupportedType.Numeric)];

  public static readonly TypeSignature[] BitwiseLogicalTypeSignature
    = [new TypeSignature(SupportedType.Integral, SupportedType.Integral),
      new TypeSignature(SupportedType.Boolean, SupportedType.Boolean)];

  public static readonly TypeSignature[] BitwiseShiftTypeSignature
    = [new TypeSignature(SupportedType.Integral, SupportedType.Integral)];
  
  public static readonly TypeSignature[] BooleanLogicalTypeSignature
    = [new TypeSignature(SupportedType.Boolean, SupportedType.Boolean)];

  public static readonly TypeSignature[] EqualityTypeSignature
    = [new TypeSignature(SupportedType.All, SupportedType.Boolean)];

  public static readonly TypeSignature[] InequalityTypeSignature
    = [new(SupportedType.Numeric, SupportedType.Boolean)];
  
  public record BinOp(
    SyntaxKind ExprKind,
    SyntaxKind TokenKind,
    ICollection<TypeSignature> TypeSignatures)
  {
    public override string ToString()
    {
      return SyntaxFacts.GetText(TokenKind);
    }
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
        or SpecialType.System_Byte
        or SpecialType.System_UInt16
        or SpecialType.System_UInt32
        or SpecialType.System_UInt64
        => SupportedType.Integral,
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
        {WellKnownMemberNames.AdditionOperatorName, SyntaxKind.AddAssignmentExpression},
        {WellKnownMemberNames.SubtractionOperatorName, SyntaxKind.SubtractAssignmentExpression},
        {WellKnownMemberNames.MultiplyOperatorName, SyntaxKind.MultiplyAssignmentExpression},
        {WellKnownMemberNames.DivisionOperatorName, SyntaxKind.DivideAssignmentExpression},
        {WellKnownMemberNames.ModulusOperatorName, SyntaxKind.ModuloAssignmentExpression},
        {WellKnownMemberNames.BitwiseAndOperatorName, SyntaxKind.AndAssignmentExpression},
        {WellKnownMemberNames.BitwiseOrOperatorName, SyntaxKind.OrAssignmentExpression},
        {WellKnownMemberNames.ExclusiveOrOperatorName, SyntaxKind.ExclusiveOrAssignmentExpression},
        {WellKnownMemberNames.LeftShiftOperatorName, SyntaxKind.LeftShiftAssignmentExpression},
        {WellKnownMemberNames.RightShiftOperatorName, SyntaxKind.RightShiftAssignmentExpression},
        {WellKnownMemberNames.UnsignedRightShiftOperatorName, SyntaxKind.UnsignedRightShiftAssignmentExpression}
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
      .ToDictionary(method => SupportedOverloadedOperators[method.Name], method => method);
  }
}
