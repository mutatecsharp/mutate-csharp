using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Mutation.SyntaxRewriter;
using MutateCSharp.Util;

namespace MutateCSharp.Mutation.SchemataGenerator;

public static class ExecutionTracerSchemataGenerator
{
  public const string Namespace = "MutateCSharp";
  public const string MutantTracerFilePathEnvVar = "MUTATE_CSHARP_TRACER_FILEPATH";
  public const string MutantTracerGlobalMutexEnvVar = "MUTATE_CSHARP_TRACER_MUTEX";
  public const int LockTimeoutMilliseconds = 60000;
  
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
    var methodName = schemaRegistry.GetUniqueSchemaName(
      mutationGroup, SyntaxRewriterMode.TraceExecution);

    result.Append(
      $"internal static {mutationGroup.SchemaReturnType} {methodName}({MutantIdType} baseMutantId"
    );

    for (var i = 0; i < mutationGroup.SchemaParameterTypes.Length; i++)
      result.Append(
        $", {mutationGroup.SchemaParameterTypes[i]} {PredefinedParameterNames[i]}");

    result.Append(')');
    result.AppendLine();
    return result;
  }

  private static StringBuilder GenerateSchemaCases(MutationGroup mutationGroup)
  {
    var result = new StringBuilder();

    // Check if mutant execution is traced:
    // if (MutantIsAlreadyTraced(baseMutantId)) { return originalExpression; }
    result.Append(
      "if (MutantIsAlreadyTraced(baseMutantId)) { return ");
    result.MaterialiseExpressionFromTemplate(
      mutationGroup.SchemaOriginalExpression.ExpressionTemplate,
      mutationGroup.SchemaParameterTypes.Length);
    result.Append("; }");
    result.AppendLine();
    
    // Persist mutant execution trace to disk:
    // RecordMutantExecution(mutantId, mutationCount);
    // We know exactly the mutation count from the mutationGroup given
    result.Append($"RecordMutantExecution(baseMutantId, {mutationGroup.SchemaMutantExpressions.Length});");
    result.AppendLine();

    // Debug mode: throw new System.InvalidOperationException("Mutant ID out of range");
    // Release mode: return originalExpression;
#if DEBUG
    result.Append(
      "throw new System.InvalidOperationException(\"Mutant ID out of range\");");
#else
    result.Append("return ");
    result.MaterialiseExpressionFromTemplate(
      mutationGroup.SchemaOriginalExpression.ExpressionTemplate,
      mutationGroup.SchemaParameterTypes.Length);
    result.Append(';');
#endif
    result.AppendLine();

    return result;
  }
  
  /*
   * https://learn.microsoft.com/en-us/dotnet/framework/performance/lazy-initialization
   * We use lazy initialisation that guarantees thread safety by default and has
   * read-only .Value property to check the value of the test that is currently
   * running, which improves performance.
   *
   * To check if a mutant has been executed, we use a concurrent-safe dictionary
   * that acts as a hash set to record the execution trace, and write it to a
   * trace file defined by the environment variable.
   *
   * Note: Persisting mutant execution trace is not atomic. If the thread crashes
   * before persisting occurs, the execution of the mutant may not be recorded.
   * This is a sign of a more serious problem (ie. the application crashing)
   * so we do not handle this case for now.
   *
   * The mutation execution is recorded in the format "classname:mutantid",
   * where there is a unique 1:1 mapping between classname and file under mutation.
   */
  private static string GenerateInitialiseMethod(FileLevelMutantSchemaRegistry schemaRegistry)
  {
    // Computes the total mutation count for the file under mutation
    var totalMutationCount = schemaRegistry.GetAllIdToMutationGroups()
      .Select(entry => entry.Value.SchemaMutantExpressions.Length)
      .Sum();
      
    return
      $$"""
        private static readonly System.Collections.Concurrent
          .ConcurrentDictionary<{{MutantIdType}}, byte> MutantsExecuted
            = new (System.Environment.ProcessorCount, capacity: {{totalMutationCount}});

        private static bool MutantIsAlreadyTraced({{MutantIdType}} lowerBound)
        {
          return string.IsNullOrEmpty({{ExecutionTracerWriterLockGenerator.Class}}.MutantTracerFilePath.Value) || 
            MutantsExecuted.ContainsKey(lowerBound);
        }
        
        private static void RecordMutantExecution({{MutantIdType}} lowerBound, {{MutantIdType}} mutationCount)
        {
          var executedMutants = new System.Collections.Generic.List<string>();
        
          for (var i = lowerBound; i < lowerBound + mutationCount; i++)
          {
            MutantsExecuted.TryAdd(i, byte.MinValue);
            executedMutants.Add($"{{schemaRegistry.ActivatedMutantEnvVar}}:{i}\n");
          }
          
          var mutantsToRecord = string.Join(string.Empty, executedMutants);
          
          var mutexId = {{ExecutionTracerWriterLockGenerator.Class}}.{{ExecutionTracerWriterLockGenerator.LockObjectName}}.Value;
          if (string.IsNullOrEmpty(mutexId))
          {
            return;
          }
          
          bool newMutexCreated;
          using (var mutex = new Mutex(false, mutexId, out newMutexCreated))
          {
            var hasHandle = false;
            try
            {
              try
              {
                hasHandle = mutex.WaitOne({{LockTimeoutMilliseconds}}, false);
                if (hasHandle == false)
                  throw new Exception("Timeout waiting for exclusive access");
              }
              catch (System.Threading.AbandonedMutexException)
              {
                // Mutex was abandoned in another process; it will still get acquired
                hasHandle = true;
              }
          
              // Persist mutant execution trace to disk
              System.IO.File.AppendAllText({{ExecutionTracerWriterLockGenerator.Class}}.MutantTracerFilePath.Value, mutantsToRecord);
            }
            finally
            {
              if(hasHandle) mutex.ReleaseMutex();
            }
          }
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
    result.Append(GenerateInitialiseMethod(schemaRegistry));
    result.AppendLine();

    // Deduplicated mutation groups
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