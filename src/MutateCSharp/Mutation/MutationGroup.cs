using Microsoft.CodeAnalysis;

namespace MutateCSharp.Mutation;

public class MutationGroup
{
  // For schema generation use (duck typing)
  public required string SchemaReturnType { get; init; }
  public required IList<string> SchemaParameterTypes { get; init; }
  public required ExpressionRecord SchemaOriginalExpression { get; init; }
  public required IList<ExpressionRecord> SchemaMutantExpressions { get; init; }

  public required string SchemaName { get; init; }

  // For internal use (registry)
  public required Location OriginalLocation { get; init; }

  // Compute hash code that takes into consideration the schema method name
  // and the (order-aware) schema parameter types
  public override int GetHashCode()
  {
    return SchemaParameterTypes.Aggregate(
      SchemaName.GetHashCode(), HashCode.Combine);
  }

  public override bool Equals(object? other)
  {
    if (ReferenceEquals(this, other)) return true;
    if (ReferenceEquals(this, null) || ReferenceEquals(other, null) ||
        GetType() != other.GetType())
      return false;
    var otherGroup = (other as MutationGroup)!;
    return SchemaName.Equals(otherGroup.SchemaName)
           && SchemaParameterTypes.SequenceEqual(
             otherGroup.SchemaParameterTypes);
  }
}