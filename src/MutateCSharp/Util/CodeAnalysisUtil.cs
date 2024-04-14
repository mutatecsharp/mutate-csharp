using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MutateCSharp.Util;

public static class CodeAnalysisUtil
{
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
