namespace MutateCSharp.Mutation.SchemataGenerator;

public static class ExecutionTracerWriterLockGenerator
{
  public const string Class = "MutatorGlobalInstance";
  public const string LockObjectName = "FileMutexLock";
  public const string FileName = $"{Class}.cs";
  
  public static string GenerateGlobalWriterLock()
  {
    return 
      $$"""
      using System;
      
      namespace {{ExecutionTracerSchemataGenerator.Namespace}};
      
      public static class {{Class}}
      {
        public static readonly System.Lazy<string> {{LockObjectName}} =
          new System.Lazy<string>(() => {
            var mutexName = System.Environment.GetEnvironmentVariable("{{ExecutionTracerSchemataGenerator.MutantTracerGlobalMutexEnvVar}}");
            return !string.IsNullOrEmpty(mutexName) ? $"Global\\{mutexName}" : string.Empty;
          });
        
        public static readonly System.Lazy<string> MutantTracerFilePath =
          new System.Lazy<string>(() => {
            var tracerFilePath = System.Environment.GetEnvironmentVariable("{{ExecutionTracerSchemataGenerator.MutantTracerFilePathEnvVar}}");
            return !string.IsNullOrEmpty(tracerFilePath) ? tracerFilePath : string.Empty;
          });
      }
      """;
  }
  
}