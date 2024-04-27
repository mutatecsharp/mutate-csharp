using System.Collections.Frozen;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using MutateCSharp.Mutation.Registry;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation;

public class FileLevelMutationRegistryTest(ITestOutputHelper testOutputHelper)
{
  private static JsonSerializerOptions JsonOptions =>
    new() { WriteIndented = true };

  [Fact]
  public void FileLevelMutationRegistryShouldBeSerialisableAndDeserialisable()
  {
    var mutationRegistry = new FileLevelMutationRegistry
    {
      FileRelativePath = "temp/file.cs",
      EnvironmentVariable = "MUTANT_CSHARP_ACTIVATED_MUTANT",
      Mutations = new Dictionary<long, MutateCSharp.Mutation.Mutation>
      {
        [1] = new()
        {
          MutantId = 1,
          OriginalOperation = SyntaxKind.BitwiseAndExpression,
          MutantOperation = SyntaxKind.BitwiseOrExpression,
          SourceSpan = new TextSpan(42, 2),
          LineSpan = new FileLinePositionSpan("path/to/some/file.cs",
            new LinePosition(42, 1), new LinePosition(42, 11))
        }
      }.ToFrozenDictionary()
    };

    var json = JsonSerializer.Serialize(mutationRegistry, JsonOptions);
    var restoreMutationRegistry =
      JsonSerializer.Deserialize<FileLevelMutationRegistry>(json);
    mutationRegistry.Should().BeEquivalentTo(restoreMutationRegistry);
  }
}