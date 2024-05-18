using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MutateCSharp.Mutation;

public class MutationGroup
{
  // For schema generation use (duck typing)
  public required string SchemaReturnType { get; init; }
  public required ImmutableArray<string> SchemaParameterTypes { get; init; }
  public required ExpressionRecord SchemaOriginalExpression { get; init; }
  public required ImmutableArray<ExpressionRecord> SchemaMutantExpressions { get; init; }
  
  // For schema generation use (compilation type symbols)
  public required ITypeSymbol ReturnTypeSymbol { get; init; }
  
  public required ImmutableArray<ITypeSymbol> ParameterTypeSymbols { get; init; }
  
  public required string SchemaName { get; init; }

  // For internal use (registry)
  public required Location OriginalLocation { get; init; }

  // Compute hash code that takes into consideration the schema method name,
  // return type and parameter types
  public override int GetHashCode()
  {
    var hashCode = new HashCode();
    
    hashCode.Add(SchemaName);
    hashCode.Add(SchemaReturnType);

    foreach (var parameterDisplay in SchemaParameterTypes)
    {
      hashCode.Add(parameterDisplay);
    }
    
    hashCode.Add(SchemaOriginalExpression.ExpressionTemplate);

    foreach (var mutantExpression in SchemaMutantExpressions)
    {
      hashCode.Add(mutantExpression.ExpressionTemplate);
    }

    return hashCode.ToHashCode();
  }

  public override bool Equals(object? other)
  {
    if (ReferenceEquals(this, other)) return true;
    if (ReferenceEquals(this, null) || ReferenceEquals(other, null) ||
        GetType() != other.GetType())
      return false;
    var otherGroup = (other as MutationGroup)!;

    var mutantExpr = SchemaMutantExpressions.Select(mutant => mutant.ExpressionTemplate);
    var otherMutantExpr =
      otherGroup.SchemaMutantExpressions.Select(mutant => mutant.ExpressionTemplate);

    return SchemaName.Equals(otherGroup.SchemaName) &&
          SchemaReturnType.Equals(otherGroup.SchemaReturnType) &&
          SchemaParameterTypes.SequenceEqual(otherGroup.SchemaParameterTypes) &&
          SchemaOriginalExpression.ExpressionTemplate.Equals(
            otherGroup.SchemaOriginalExpression.ExpressionTemplate) &&
           mutantExpr.SequenceEqual(otherMutantExpr);
  }
}