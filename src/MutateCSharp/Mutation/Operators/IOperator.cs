using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.Operators;

public abstract class Operator<T> where T : SyntaxNode
{
  // Check that mutation operator can be applied on currently visited node.
  // Should be run before other methods in this class are called.
  public abstract bool CanBeApplied(T originalNode);
  // Generate list of valid mutations for the currently visited node.
  public abstract IList<Mutation> ValidMutations(T originalNode);
  // Create the parameter list for schema generation.
  public abstract IList<ParameterSyntax> SchemaParameters(T originalNode);

  // The method name used to identify the replacement operation.
  // Note: this is not unique as there can be multiple expressions of the
  // same form and type replaced; these can call the same method
  // (The deduplication process will be handled in MutantSchemataGenerator)
  public abstract string SchemaName(T originalNode);
}

public class BooleanConstantReplacer
  : Operator<LiteralExpressionSyntax>
{
  public override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    return originalNode.IsKind(SyntaxKind.TrueLiteralExpression) ||
           originalNode.IsKind(SyntaxKind.FalseLiteralExpression);
  }

  public override IList<Mutation> ValidMutations(
    LiteralExpressionSyntax originalNode) => originalNode.Kind() switch
  {
    SyntaxKind.TrueLiteralExpression => ImmutableArray.Create(new Mutation(
      originalNode,
      SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))),
    SyntaxKind.FalseLiteralExpression => ImmutableArray.Create(new Mutation(
      originalNode,
      SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))),
    _ => throw new UnreachableException(
      "Operator can only replace boolean constants.")
  };

  public override IList<ParameterSyntax> SchemaParameters(
    LiteralExpressionSyntax _)
  {
    var parameter = SyntaxFactory
      .Parameter(SyntaxFactory.Identifier("argument"))
      .WithType(
        SyntaxFactory.PredefinedType(
          SyntaxFactory.Token(SyntaxKind.BoolKeyword)));
    return ImmutableArray.Create(parameter);
  }

  public override string SchemaName(LiteralExpressionSyntax originalNode)
  {
    var suffix = originalNode.Kind() switch
    {
      SyntaxKind.TrueLiteralExpression => "True",
      SyntaxKind.FalseLiteralExpression => "False",
      _ => throw new UnreachableException(
        "Operator can only replace boolean constants.")
    };
    return $"ReplaceBooleanConstant_{suffix}";
  }
}

public class NumericConstantReplacer(SemanticModel semanticModel)
  : Operator<LiteralExpressionSyntax>
{
  private readonly IDictionary<Type, SyntaxKind> _primitiveNumericTypes =
    new Dictionary<Type, SyntaxKind>
    {
      { typeof(sbyte), SyntaxKind.SByteKeyword },
      { typeof(short), SyntaxKind.ShortKeyword },
      { typeof(int), SyntaxKind.IntKeyword },
      { typeof(long), SyntaxKind.LongKeyword },
      { typeof(byte), SyntaxKind.ByteKeyword },
      { typeof(ushort), SyntaxKind.UShortKeyword },
      { typeof(uint), SyntaxKind.UIntKeyword },
      { typeof(ulong), SyntaxKind.ULongKeyword },
      { typeof(float), SyntaxKind.FloatKeyword },
      { typeof(double), SyntaxKind.DoubleKeyword }
    }.ToFrozenDictionary();

  public override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    var type = semanticModel.GetTypeInfo(originalNode).Type?.GetType();
    return type != null
           && (_primitiveNumericTypes.ContainsKey(type) ||
               (type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(Nullable<>)
                && Nullable.GetUnderlyingType(type) != null
                && _primitiveNumericTypes.ContainsKey(
                  Nullable.GetUnderlyingType(type)!)));
  }

  public override IList<Mutation> ValidMutations(
    LiteralExpressionSyntax originalNode)
  {
    throw new NotImplementedException();
  }

  // Todo: Handle nullable parameters
  public override IList<ParameterSyntax> SchemaParameters(
    LiteralExpressionSyntax originalNode)
  {
    var type = semanticModel.GetTypeInfo(originalNode).Type!.GetType();
    var kind = _primitiveNumericTypes[type];
    return ImmutableArray.Create(
      SyntaxFactoryUtil.CreatePredefinedUnaryParameters(kind));
  }

  public override string SchemaName(LiteralExpressionSyntax originalNode) =>
    $"Replace{semanticModel.GetTypeInfo(originalNode).Type!.Name}Constant";
}

// Not to be confused with stringbuilder instances
public class StringConstantReplacer : Operator<LiteralExpressionSyntax>
{
  public override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    return originalNode.IsKind(SyntaxKind.StringLiteralExpression)
      && originalNode.Token.ValueText.Length > 0;
  }

  public override IList<Mutation> ValidMutations(
    LiteralExpressionSyntax originalNode)
  {
    // Replace non-empty string constant with empty string constant
    if (originalNode.Token.ValueText.Length > 0)
    {
      return ImmutableArray.Create(new Mutation(
        originalNode,
        SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
          SyntaxFactory.Literal(""))));
    }

    return ImmutableArray<Mutation>.Empty;
  }

  public override IList<ParameterSyntax> SchemaParameters(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(
      SyntaxFactoryUtil.CreatePredefinedUnaryParameters(
        SyntaxKind.StringKeyword));
  }

  public override string SchemaName(LiteralExpressionSyntax originalNode)
  {
    return "ReplaceNonEmptyConstantString";
  }
}