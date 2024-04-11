namespace MutateCSharp.Mutation;

public class MutationGroup
{
  // For schema generation use (duck typing)
  public required string SchemaReturnType { get; init; }
  public required IList<string> SchemaParameterTypes { get; init; }
  public required string SchemaOriginalExpressionTemplate { get; init; }
  public required IList<string> SchemaMutantExpressionsTemplate { get; init; }
  public required string SchemaName { get; init; }

  public override int GetHashCode()
  {
    return HashCode.Combine(SchemaParameterTypes, SchemaReturnType,
      SchemaMutantExpressionsTemplate);
  }
  public override bool Equals(object? other)
  {
    if (ReferenceEquals(this, other)) return true;
    if (ReferenceEquals(this, null) || ReferenceEquals(other, null) ||
        GetType() != other.GetType())
      return false;
    var otherGroup = (other as MutationGroup)!;
    return SchemaName.Equals(otherGroup.SchemaName);
  }
  
}