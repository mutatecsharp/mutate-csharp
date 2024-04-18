using System.Text;

namespace MutateCSharp.Mutation;

public static class MutantSchemataGenerator
{
  public const string Namespace = "MutateCSharp";
  public const string Class = "Schemata";

  // Hack to optimise template generation time 
  private static readonly object?[] PredefinedParameterNames =
    ["argument1", "argument2", "argument3", "argument4"];

  private static ReadOnlySpan<object?> RequiredParameters(int count)
  {
    return new ReadOnlySpan<object?>(PredefinedParameterNames, 0, count);
  }
  
  // Method signature: public static <type> <method name> (int mutantId, <type1> <parameter1>, <type2> <parameter2>, ...)
  private static StringBuilder GenerateSchemaMethodSignature(MutationGroup mutationGroup)
  {
    var result = new StringBuilder();

    result.Append(
      $"public static {mutationGroup.SchemaReturnType} {mutationGroup.SchemaName}(int mutantId"
    );

    for (var i = 0; i < mutationGroup.SchemaParameterTypes.Count; i++)
    {
      result.Append(
        $", {mutationGroup.SchemaParameterTypes[i]} {PredefinedParameterNames[i]}");
    }

    result.Append(')');
    result.AppendLine();
    return result;
  }
  
  private static StringBuilder GenerateSchemaCases(MutationGroup mutationGroup)
  {
    var result = new StringBuilder();
    
    // Out of range case: if (!ActivatedInRange(mutantId, mutantId + n)) return originalExpression;
    result.Append(
      $"if (!ActivatedInRange(mutantId, mutantId + {mutationGroup.SchemaMutantExpressionsTemplate.Count - 1})) return ");
    result.AppendFormat(null,
      CompositeFormat.Parse(mutationGroup.SchemaOriginalExpressionTemplate),
      RequiredParameters(mutationGroup.SchemaParameterTypes.Count));
    result.Append(';');
    result.AppendLine();
    
    // Case: if (_activatedMutantId == mutantId + i) return mutatedExpression;
    for (var i = 0; i < mutationGroup.SchemaMutantExpressionsTemplate.Count; i++)
    {
      result.Append($"if (_activatedMutantId == mutantId + {i}) return ");
      result.AppendFormat(null,
        CompositeFormat.Parse(mutationGroup.SchemaMutantExpressionsTemplate[i]),
        RequiredParameters(mutationGroup.SchemaParameterTypes.Count));
      result.Append(';');
      result.AppendLine();
    }
    
    // Default case: throw new System.Diagnostics.UnreachableException("Mutant ID out of range");
    result.Append(
      "throw new System.Diagnostics.UnreachableException(\"Mutant ID out of range\");");
    result.AppendLine();

    return result;
  }

  private static string GenerateInitialiseMethod()
  {
    return
      """
      private static void Initialise()
      {
        if (_initialised) return;
        var activatedMutant = Environment.GetEnvironmentVariable("MUTATE_CSHARP_ACTIVATED_MUTANT");
        if (!string.IsNullOrEmpty(activatedMutant)) _activatedMutantId = int.Parse(activatedMutant);
        _initialised = true;
      }

      private static bool ActivatedInRange(int lowerBound, int upperBound)
      {
        Initialise();
        return lowerBound <= _activatedMutantId && _activatedMutantId <= upperBound;
      }
      """;
  }
  
  public static StringBuilder GenerateIndividualSchema(MutationGroup mutationGroup)
  {
    var result = new StringBuilder();

    result.Append(GenerateSchemaMethodSignature(mutationGroup));
    result.Append('{');
    result.AppendLine();
    result.Append(GenerateSchemaCases(mutationGroup));
    result.Append('}');
    result.AppendLine();

    return result;
  }
  
  public static StringBuilder GenerateSchemata(IEnumerable<MutationGroup> mutationGroups)
  {
    var result = new StringBuilder();

    result.Append($"namespace {Namespace}");
    result.AppendLine();
    result.Append('{');
    result.AppendLine();
    result.Append($"public static class {Class}");
    result.AppendLine();
    result.Append('{');
    result.AppendLine();
    result.Append(GenerateInitialiseMethod());
    result.AppendLine();

    foreach (var mutationGroup in mutationGroups)
    {
      result.Append(GenerateIndividualSchema(mutationGroup));
      result.AppendLine();
    }

    result.Append('}');
    result.AppendLine();
    result.Append('}');
    result.AppendLine();

    return result;
  }

  // // Use a document editor here
  // public SyntaxTree InjectSchemata(SyntaxTree tree)
  // {
  // }
}