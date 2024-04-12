using System.Collections.Frozen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Mutation.OperatorImplementation;

public partial class BinaryOperatorReplacer(SemanticModel semanticModel)
  : AbstractMutationOperator<BinaryExpressionSyntax>(semanticModel)
{
  protected override bool CanBeApplied(BinaryExpressionSyntax originalNode)
  {
    ReadOnlySpan<ExpressionSyntax> children = [originalNode.Left, originalNode.Right];
    return true;
  }

  protected override string OriginalExpressionTemplate(
    BinaryExpressionSyntax originalNode)
  {
    if (ArithmeticOperators.Contains(originalNode.Kind()))
      return ArithmeticOriginalExpressionTemplate(originalNode);

    if (BitwiseOperators.Contains(originalNode.Kind()))
      return BitwiseOriginalExpressionTemplate(originalNode);

    if (LogicalOperators.Contains(originalNode.Kind()))
      return LogicalOriginalExpressionTemplate(originalNode);

    if (RelationalOperators.Contains(originalNode.Kind()))
      return RelationalOriginalExpressionTemplate(originalNode);

    if (TypeCastOperators.Contains(originalNode.Kind()))
      return TypeCastOriginalExpressionTemplate(originalNode);

    if (originalNode.IsKind(SyntaxKind.CoalesceExpression))
      return NullCoalesceOriginalExpressionTemplate(originalNode);

    throw new NotSupportedException("Binary operator is unsupported.");
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    BinaryExpressionSyntax originalNode)
  {
    throw new NotImplementedException();
  }

  protected override IList<string> ParameterTypes(
    BinaryExpressionSyntax originalNode)
  {
    throw new NotImplementedException();
  }

  protected override string ReturnType(BinaryExpressionSyntax originalNode)
  {
    throw new NotImplementedException();
  }

  protected override string SchemaBaseName(BinaryExpressionSyntax originalNode)
  {
    throw new NotImplementedException();
  }
}

// Arithmetic operators
public partial class BinaryOperatorReplacer
{
  private static readonly ISet<SyntaxKind> ArithmeticOperators = new HashSet<SyntaxKind>
  {
    SyntaxKind.AddExpression,
    SyntaxKind.SubtractExpression,
    SyntaxKind.ModuloExpression,
    SyntaxKind.MultiplyExpression,
    SyntaxKind.DivideExpression
  }.ToFrozenSet();
  
  private string ArithmeticOriginalExpressionTemplate(BinaryExpressionSyntax node)
  {
    return node.Kind() switch
    {
      SyntaxKind.AddExpression => "{0} + {1}",
      SyntaxKind.SubtractExpression => "{0} - {1}",
      SyntaxKind.MultiplyExpression => "{0} * {1}",
      SyntaxKind.DivideExpression => "{0} / {1}",
      SyntaxKind.ModuloExpression => "{0} % {1}",
    };
  }
}

// (boolean and integral) bitwise operators
public partial class BinaryOperatorReplacer
{
  private static readonly ISet<SyntaxKind> BitwiseOperators =
    new HashSet<SyntaxKind>
    {
      SyntaxKind.BitwiseAndExpression,
      SyntaxKind.BitwiseOrExpression,
      SyntaxKind.ExclusiveOrExpression,
      SyntaxKind.LeftShiftExpression,
      SyntaxKind.RightShiftExpression,
      SyntaxKind.UnsignedRightShiftExpression
    }.ToFrozenSet();

  private string BitwiseOriginalExpressionTemplate(BinaryExpressionSyntax node)
  {
    return node.Kind() switch
    {
      SyntaxKind.BitwiseAndExpression => "{0} & {1}",
      SyntaxKind.BitwiseOrExpression => "{0} | {1}",
      SyntaxKind.ExclusiveOrExpression => "{0} ^ {1}",
      SyntaxKind.LeftShiftExpression => "{0} << {1}",
      SyntaxKind.RightShiftExpression => "{0} >> {1}",
      SyntaxKind.UnsignedRightShiftExpression => "{0} >>> {1}"
    };
  }
}

// boolean-only logical operators
public partial class BinaryOperatorReplacer
{
  private static readonly ISet<SyntaxKind> LogicalOperators =
    new HashSet<SyntaxKind>
    {
      SyntaxKind.LogicalAndExpression,
      SyntaxKind.LogicalOrExpression
    }.ToFrozenSet();

  private string LogicalOriginalExpressionTemplate(BinaryExpressionSyntax node)
  {
    return node.Kind() switch
    {
      SyntaxKind.LogicalAndExpression => "{0} && {1}",
      SyntaxKind.LogicalOrExpression => "{0} || {1}"
    };
  }
}

// integral-only relational operators
public partial class BinaryOperatorReplacer
{
  private static readonly ISet<SyntaxKind> RelationalOperators =
    new HashSet<SyntaxKind>
    {
      SyntaxKind.EqualsExpression,
      SyntaxKind.NotEqualsExpression,
      SyntaxKind.LessThanExpression,
      SyntaxKind.LessThanOrEqualExpression,
      SyntaxKind.GreaterThanExpression,
      SyntaxKind.GreaterThanOrEqualExpression
    };

  private string RelationalOriginalExpressionTemplate(
    BinaryExpressionSyntax node)
  {
    return node.Kind() switch
    {
      SyntaxKind.EqualsExpression => "{0} == {1}",
      SyntaxKind.NotEqualsExpression => "{0} != {1}",
      SyntaxKind.LessThanExpression => "{0} < {1}",
      SyntaxKind.LessThanOrEqualExpression => "{0} <= {1}",
      SyntaxKind.GreaterThanExpression => "{0} > {1}",
      SyntaxKind.GreaterThanOrEqualExpression => "{0} >= {1}"
    };
  }
}

// type-cast operators
public partial class BinaryOperatorReplacer
{
  private static readonly ISet<SyntaxKind> TypeCastOperators =
    new HashSet<SyntaxKind>
    {
      SyntaxKind.IsExpression,
      SyntaxKind.AsExpression
    }.ToFrozenSet();

  private string TypeCastOriginalExpressionTemplate(BinaryExpressionSyntax node)
  {
    return node.Kind() switch
    {
      SyntaxKind.IsExpression => "{0} is {1}",
      SyntaxKind.AsExpression => "{0} as {1}"
    };
  }
}

// Null-coalesce operators
public partial class BinaryOperatorReplacer
{
  private string NullCoalesceOriginalExpressionTemplate(
    BinaryExpressionSyntax node) => "{0} ?? {1}";
}