using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MutateCSharp.Mutation.Operators;

public abstract class Operator<T>(SemanticModel semanticModel)
  where T : ExpressionSyntax
{
  protected readonly SemanticModel SemanticModel = semanticModel;

  // Check that mutation operator can be applied on currently visited node.
  // Should be run before other methods in this class are called.
  public abstract bool CanBeApplied(T? originalNode);

  public MutationGroup? CreateMutationGroup(T? originalNode)
  {
    if (originalNode is null || !CanBeApplied(originalNode)) return null;
    var mutationsWithId = ValidMutantExpressionsTemplate(originalNode);
    if (mutationsWithId.Count == 0) return null;

    var mutations = mutationsWithId.Select(entry => entry.Item2);
    var uniqueMutantsId =
      string.Join("", mutationsWithId.Select(entry => entry.Item1));

    return new MutationGroup
    {
      SchemaName = $"{SchemaBaseName(originalNode)}{uniqueMutantsId}",
      SchemaParameterTypes = ParameterTypes(originalNode),
      SchemaReturnType = ReturnType(originalNode),
      SchemaOriginalExpressionTemplate =
        OriginalExpressionTemplate(originalNode),
      SchemaMutantExpressionsTemplate = mutations.ToImmutableArray()
    };
  }

  protected abstract string OriginalExpressionTemplate(T originalNode);

  // Generate list of valid mutations for the currently visited node, in the
  // form of string template to be formatted later to insert arguments.
  // Each valid name is given an ID that is unique within the context of the
  // mutation operator class.
  protected abstract IList<(int, string)> ValidMutantExpressionsTemplate(
    T originalNode);

  // Parameter type of the programming construct.
  protected abstract IList<string> ParameterTypes(T originalNode);

  // Return type of the programming construct (expression, statement)
  // represented by type of node under mutation.
  protected abstract string ReturnType(T originalNode);

  // The method name used to identify the replacement operation.
  // Note: this is not unique as there can be multiple expressions of the
  // same form and type replaced; these can call the same method
  // (The deduplication process will be handled in MutantSchemataGenerator)
  protected abstract string SchemaBaseName(T originalNode);
}

public class BooleanConstantReplacer(SemanticModel semanticModel)
  : Operator<LiteralExpressionSyntax>(semanticModel)
{
  public override bool CanBeApplied(LiteralExpressionSyntax? originalNode)
  {
    return originalNode.IsKind(SyntaxKind.TrueLiteralExpression) ||
           originalNode.IsKind(SyntaxKind.FalseLiteralExpression);
  }

  protected override string OriginalExpressionTemplate(
    LiteralExpressionSyntax originalNode)
  {
    return "{0}";
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    LiteralExpressionSyntax _)
  {
    return ImmutableArray.Create((1, "!{0}"));
  }

  protected override IList<string> ParameterTypes(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(ReturnType(originalNode));
  }

  protected override string ReturnType(LiteralExpressionSyntax _)
  {
    return "bool";
  }

  protected override string SchemaBaseName(LiteralExpressionSyntax _)
  {
    return "ReplaceBooleanConstant";
  }
}

public class NumericConstantReplacer(SemanticModel semanticModel)
  : Operator<LiteralExpressionSyntax>(semanticModel)
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

  public override bool CanBeApplied(LiteralExpressionSyntax? originalNode)
  {
    if (originalNode is null) return false;
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

// Not to be confused with interpolated string instances
public class StringConstantReplacer(SemanticModel semanticModel)
  : Operator<LiteralExpressionSyntax>(semanticModel)
{
  public override bool CanBeApplied(LiteralExpressionSyntax? originalNode)
  {
    return originalNode.IsKind(SyntaxKind.StringLiteralExpression) &&
           originalNode.Token.ValueText.Length > 0;
  }

  protected override string OriginalExpressionTemplate(
    LiteralExpressionSyntax originalNode)
  {
    return "{0}";
  }

  protected override IList<(int, string)> ValidMutantExpressionsTemplate(
    LiteralExpressionSyntax originalNode)
  {
    var result = new List<(int, string)>();

    // Mutation: non-empty string constant => empty string constant
    if (originalNode.Token.ValueText.Length > 0)
      result.Add((1, "string.Empty"));

    return result.ToImmutableArray();
  }

  protected override IList<string> ParameterTypes(
    LiteralExpressionSyntax originalNode)
  {
    return ImmutableArray.Create(ReturnType(originalNode));
  }

  protected override string ReturnType(LiteralExpressionSyntax _)
  {
    return "string";
  }

  protected override string SchemaBaseName(LiteralExpressionSyntax _)
  {
    return "ReplaceStringConstant";
  }
}