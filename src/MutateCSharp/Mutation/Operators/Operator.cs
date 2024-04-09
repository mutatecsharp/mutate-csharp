using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.Operators;

public abstract class Operator<T>(SemanticModel semanticModel)
  where T : ExpressionSyntax
{
  // Check that mutation operator can be applied on currently visited node.
  // Should be run before other methods in this class are called.
  public abstract bool CanBeApplied(T originalNode);

  public MutationGroup? CreateMutationGroup(T originalNode)
  {
    var mutations = ValidMutations(originalNode);
    if (mutations.Count == 0) return null;

    return new MutationGroup
    {
      SchemaBaseName = SchemaBaseName(originalNode),
      SchemaParameters = SchemaParameters(originalNode),
      Mutations = ValidMutations(originalNode)
    };
  }

  // Generate list of valid mutations for the currently visited node.
  protected abstract IList<Mutation> ValidMutations(T originalNode);

  // Create the parameter list for schema generation.
  protected abstract IList<ParameterSyntax> SchemaParameters(T originalNode);

  // The method name used to identify the replacement operation.
  // Note: this is not unique as there can be multiple expressions of the
  // same form and type replaced; these can call the same method
  // (The deduplication process will be handled in MutantSchemataGenerator)
  protected abstract string SchemaBaseName(T originalNode);

  protected Mutation CreateMutation(T originalNode, T replacementNode)
  {
    return new Mutation(originalNode, replacementNode,
      semanticModel.GetTypeInfo(originalNode));
  }
}

public class BooleanConstantReplacer(SemanticModel semanticModel)
  : Operator<LiteralExpressionSyntax>(semanticModel)
{
  public override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    return originalNode.IsKind(SyntaxKind.TrueLiteralExpression) ||
           originalNode.IsKind(SyntaxKind.FalseLiteralExpression);
  }

  protected override IList<Mutation> ValidMutations(
    LiteralExpressionSyntax originalNode)
  {
    return originalNode.Kind() switch
    {
      SyntaxKind.TrueLiteralExpression => ImmutableArray.Create(CreateMutation(
        originalNode,
        SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression))),
      SyntaxKind.FalseLiteralExpression => ImmutableArray.Create(CreateMutation(
        originalNode,
        SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))),
      _ => throw new UnreachableException(
        "Operator can only replace boolean constants.")
    };
  }

  protected override IList<ParameterSyntax> SchemaParameters(
    LiteralExpressionSyntax _)
  {
    var parameter = SyntaxFactory
      .Parameter(SyntaxFactory.Identifier("argument"))
      .WithType(
        SyntaxFactory.PredefinedType(
          SyntaxFactory.Token(SyntaxKind.BoolKeyword)));
    return ImmutableArray.Create(parameter);
  }

  protected override string SchemaBaseName(LiteralExpressionSyntax originalNode)
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
  : Operator<LiteralExpressionSyntax>(semanticModel)
{
  private readonly SemanticModel _semanticModel = semanticModel;

  private readonly IDictionary<SpecialType, SyntaxKind>
    _numericTypeToSyntaxKind =
      new Dictionary<SpecialType, SyntaxKind>
      {
        // Signed numeric types
        { SpecialType.System_SByte, SyntaxKind.SByteKeyword },
        { SpecialType.System_Int16, SyntaxKind.ShortKeyword },
        { SpecialType.System_Int32, SyntaxKind.IntKeyword },
        { SpecialType.System_Int64, SyntaxKind.LongKeyword },
        // Unsigned numeric types
        { SpecialType.System_Byte, SyntaxKind.ByteKeyword },
        { SpecialType.System_UInt16, SyntaxKind.UShortKeyword },
        { SpecialType.System_UInt32, SyntaxKind.UIntKeyword },
        { SpecialType.System_UInt64, SyntaxKind.ULongKeyword },
        // Floating point types
        { SpecialType.System_Single, SyntaxKind.FloatKeyword },
        { SpecialType.System_Double, SyntaxKind.DoubleKeyword }
      }.ToFrozenDictionary();

  private static dynamic ConvertToNumericType(object value,
    SpecialType type)
  {
    return type switch
    {
      // Signed numeric types
      SpecialType.System_SByte => Convert.ToSByte(value),
      SpecialType.System_Int16 => Convert.ToInt16(value),
      SpecialType.System_Int32 => Convert.ToInt32(value),
      SpecialType.System_Int64 => Convert.ToInt64(value),
      // Unsigned numeric types
      SpecialType.System_Byte => Convert.ToByte(value),
      SpecialType.System_UInt16 => Convert.ToUInt16(value),
      SpecialType.System_UInt32 => Convert.ToUInt32(value),
      SpecialType.System_UInt64 => Convert.ToUInt64(value),
      // Floating point types
      SpecialType.System_Single => Convert.ToSingle(value),
      SpecialType.System_Double => Convert.ToDouble(value),
      // Unhandled cases
      _ => throw new NotSupportedException(
        $"{type} cannot be downcast to its numeric type.")
    };
  }

  public override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    var type = _semanticModel.GetTypeInfo(originalNode).Type?.SpecialType;
    return type.HasValue && _numericTypeToSyntaxKind.ContainsKey(type.Value);
  }

  protected override IList<Mutation> ValidMutations(
    LiteralExpressionSyntax originalNode)
  {
    var type = _semanticModel.GetTypeInfo(originalNode).Type?.SpecialType!;
    var value = ConvertToNumericType(originalNode.Token.Value!, type.Value);
    var result = new List<Mutation>();

    if (value != 0)
    {
      // Mutation: value => 0
      var mutateToZero = CreateMutation(originalNode,
        SyntaxFactoryUtil.CreateNumericLiteral(
          ConvertToNumericType(0, type.Value)));
      result.Add(mutateToZero);

      // Mutation: value => -value
      var mutateToNegativeConstant = CreateMutation(originalNode,
        SyntaxFactoryUtil.CreateNumericLiteral(-value));
      result.Add(mutateToNegativeConstant);
    }

    if (value != -1)
    {
      // Mutation: value => -1
      var mutateToNegativeOne = CreateMutation(originalNode,
        SyntaxFactoryUtil.CreateNumericLiteral(
          ConvertToNumericType(-1, type.Value)));
      result.Add(mutateToNegativeOne);
    }

    if (value != 1)
    {
      // Mutation: value => 1
      var mutateToOne = CreateMutation(originalNode,
        SyntaxFactoryUtil.CreateNumericLiteral(
          ConvertToNumericType(1, type.Value)));
      result.Add(mutateToOne);
    }

    // TODO: Handle overflow and underflow
    // Mutation: value => value - 1
    var mutateMinusOne = CreateMutation(originalNode,
      SyntaxFactoryUtil.CreateNumericLiteral(value - 1));
    result.Add(mutateMinusOne);

    // Mutation: value => value + 1
    var mutatePlusOne = CreateMutation(originalNode,
      SyntaxFactoryUtil.CreateNumericLiteral(value + 1));
    result.Add(mutatePlusOne);

    return result;
  }

  protected override IList<ParameterSyntax> SchemaParameters(
    LiteralExpressionSyntax originalNode)
  {
    var type = _semanticModel.GetTypeInfo(originalNode).Type!.Name;
    // var type = _semanticModel.GetTypeInfo(originalNode).Type?.SpecialType!;
    // var kind = _numericTypeToSyntaxKind[type.Value];
    return ImmutableArray.Create(
      SyntaxFactoryUtil.CreatePredefinedUnaryParameters(type));
  }

  protected override string
    SchemaBaseName(LiteralExpressionSyntax originalNode) =>
    $"Replace{_semanticModel.GetTypeInfo(originalNode).Type!.Name}Constant";
}

// Not to be confused with interpolated string instances
public class StringConstantReplacer(SemanticModel semanticModel)
  : Operator<LiteralExpressionSyntax>(semanticModel)
{
  public override bool CanBeApplied(LiteralExpressionSyntax originalNode)
  {
    return originalNode.IsKind(SyntaxKind.StringLiteralExpression)
           && originalNode.Token.ValueText.Length > 0;
  }

  protected override IList<Mutation> ValidMutations(
    LiteralExpressionSyntax originalNode)
  {
    // Replace non-empty string constant with empty string constant
    if (originalNode.Token.ValueText.Length > 0)
    {
      return ImmutableArray.Create(CreateMutation(
        originalNode,
        SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
          SyntaxFactory.Literal(string.Empty))));
    }

    return ImmutableArray<Mutation>.Empty;
  }

  protected override IList<ParameterSyntax> SchemaParameters(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(
      SyntaxFactoryUtil.CreatePredefinedUnaryParameters(
        "string"));
  }

  protected override string SchemaBaseName(LiteralExpressionSyntax originalNode)
  {
    return "ReplaceStringConstant";
  }
}