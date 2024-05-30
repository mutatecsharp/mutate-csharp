namespace MutateCSharp.Mutation.SchemataGenerator;

public static class ExecutionTracerWriterLockGenerator
{
  public const string Class = "MutatorGlobalInstance";
  public const string LockObjectName = "WriterLock";
  public const string FileName = $"{Class}.cs";
  
  public static string GenerateGlobalWriterLock()
  {
    return 
      $$"""
      using System;
      
      namespace {{ExecutionTracerSchemataGenerator.Namespace}};
      
      public static class {{Class}}
      {
        public static readonly object {{LockObjectName}} = new();
      }
      """;
  }
  
}