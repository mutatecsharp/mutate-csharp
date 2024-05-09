using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation;

public static class MutantSchemataGenerator
{
  public const string Namespace = "MutateCSharp";

  private static readonly string
    MutantIdType = FileLevelMutantSchemaRegistry.MutantIdType.ToClrTypeName();
  
  // Hack to optimise template generation time 
  private static readonly object?[] PredefinedParameterNames =
    ["argument1", "argument2"];

  private static ReadOnlySpan<object?> RequiredParameters(int count)
  {
    return new ReadOnlySpan<object?>(PredefinedParameterNames, 0, count);
  }

  private static void MaterialiseExpressionFromTemplate(this StringBuilder sb, string template, int argumentCount)
  {
    sb.AppendFormat(null, CompositeFormat.Parse(template), 
      RequiredParameters(argumentCount));
  }

  // Method signature: public static <type> <method name> (int mutantId, <type1> <parameter1>, <type2> <parameter2>, ...)
  private static StringBuilder GenerateSchemaMethodSignature(
    MutationGroup mutationGroup, FileLevelMutantSchemaRegistry schemaRegistry)
  {
    var result = new StringBuilder();

    result.Append(
      $"internal static {mutationGroup.SchemaReturnType} {schemaRegistry.GetUniqueSchemaName(mutationGroup)}({MutantIdType} mutantId"
    );

    for (var i = 0; i < mutationGroup.SchemaParameterTypes.Count; i++)
      result.Append(
        $", {mutationGroup.SchemaParameterTypes[i]} {PredefinedParameterNames[i]}");

    result.Append(')');
    result.AppendLine();
    return result;
  }

  // C# treats T and T? equally. Thus, given the same method signature, we can
  // only have one unique parameter set containing absolute types
  // Example: the following two method signatures are treated equivalently
  // public void foo(T t) {}
  // public void foo(T? t) {}
  // For reference types, we can assign null to the reference type without having
  // to make the type parameter signature nullable.
  // However, this does not hold for value types. We need to make the type parameter
  // signature nullable for value types.
  // We type cast the expression to the return type since we take in nullable
  // for primitive types and return the absolute type.
  private static StringBuilder GenerateSchemaCases(MutationGroup mutationGroup)
  {
    var result = new StringBuilder();

    // Out of range case: if (!ActivatedInRange(mutantId, mutantId + n)) { return originalExpression; }
    result.Append(
      $"if (!ActivatedInRange(mutantId, mutantId + {mutationGroup.SchemaMutantExpressions.Count - 1})) {{ return ");
    // result.Append($"({mutationGroup.SchemaReturnType}) (");
    result.MaterialiseExpressionFromTemplate(
      mutationGroup.SchemaOriginalExpression.ExpressionTemplate,
      mutationGroup.SchemaParameterTypes.Count);
    result.Append("; }");
    result.AppendLine();

    // Case: if (_activatedMutantId == mutantId + i) { return mutatedExpression; }
    for (var i = 0; i < mutationGroup.SchemaMutantExpressions.Count; i++)
    {
      result.Append($"if (ActivatedMutantId.Value == mutantId + {i}) {{ return ");
      // result.Append($"({mutationGroup.SchemaReturnType}) (");
      result.MaterialiseExpressionFromTemplate(
        mutationGroup.SchemaMutantExpressions[i].ExpressionTemplate,
        mutationGroup.SchemaParameterTypes.Count);
      result.Append("; }");
      result.AppendLine();
    }

    // Debug mode: throw new System.InvalidOperationException("Mutant ID out of range");
    // Release mode: return originalExpression;
    #if DEBUG
    result.Append(
      "throw new System.InvalidOperationException(\"Mutant ID out of range\");");
    #else
    result.Append("return ");
    // result.Append($"({mutationGroup.SchemaReturnType}) (");
    result.MaterialiseExpressionFromTemplate(
      mutationGroup.SchemaOriginalExpression.ExpressionTemplate,
      mutationGroup.SchemaParameterTypes.Count);
    result.Append(";");
    #endif
    result.AppendLine();

    return result;
  }

  /*
   * https://learn.microsoft.com/en-us/dotnet/framework/performance/lazy-initialization
   * We use lazy initialisation that guarantees thread safety by default and has
   * read-only .Value property, which also improves performance.
   */
  private static string GenerateInitialiseMethod(string environmentVariable)
  {
    return
      $$"""
      private static readonly System.Lazy<{{MutantIdType}}> ActivatedMutantId =
        new System.Lazy<{{MutantIdType}}>(() => {
          var activatedMutant = System.Environment.GetEnvironmentVariable("{{environmentVariable}}");
          return !string.IsNullOrEmpty(activatedMutant) ? {{MutantIdType}}.Parse(activatedMutant) : 0;
        });

      private static bool ActivatedInRange({{MutantIdType}} lowerBound, {{MutantIdType}} upperBound)
      {
        return lowerBound <= ActivatedMutantId.Value && ActivatedMutantId.Value <= upperBound;
      }
      """;
  }

  public static StringBuilder GenerateIndividualSchema(
    MutationGroup mutationGroup, FileLevelMutantSchemaRegistry schemaRegistry)
  {
    var result = new StringBuilder();

    result.Append(GenerateSchemaMethodSignature(mutationGroup, schemaRegistry));
    result.Append('{');
    result.AppendLine();
    result.Append(GenerateSchemaCases(mutationGroup));
    result.Append('}');
    result.AppendLine();

    return result;
  }

  public static StringBuilder GenerateSchemata(
    FileLevelMutantSchemaRegistry schemaRegistry)
  {
    var result = new StringBuilder();

    result.Append($"namespace {Namespace}");
    result.AppendLine();
    result.Append('{');
    result.AppendLine();
    result.Append($"internal class {schemaRegistry.ClassName}");
    result.AppendLine();
    result.Append('{');
    result.AppendLine();
    result.Append(GenerateInitialiseMethod(schemaRegistry.EnvironmentVariable));
    result.AppendLine();

    foreach (var mutationGroup in schemaRegistry.GetAllMutationGroups())
    {
      result.Append(GenerateIndividualSchema(mutationGroup, schemaRegistry));
      result.AppendLine();
    }

    result.Append('}');
    result.AppendLine();
    result.Append('}');
    result.AppendLine();
    
    return result;
  }

  public static NamespaceDeclarationSyntax? GenerateSchemataSyntax(
    FileLevelMutantSchemaRegistry registry)
  {
    var mutationGroups = registry.GetAllMutationGroups();
    if (mutationGroups.Count == 0) return null;

    var schemata = GenerateSchemata(registry);
    var ast = CSharpSyntaxTree.ParseText(schemata.ToString());
    var syntax = ast.GetCompilationUnitRoot().Members
      .OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

    return syntax;
  }
}