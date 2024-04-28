using System.Collections.Frozen;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using MutateCSharp.Mutation.Registry;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Mutation;

public class MutationRegistryTest(ITestOutputHelper testOutputHelper)
{
  private static readonly FileLevelMutationRegistry ExampleFileLevelMutationRegistry =
    new()
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
          LineSpan = new FileLinePositionSpan(string.Empty,
            new LinePosition(42, 1), new LinePosition(42, 11))
        },
        [2] = new()
        {
          MutantId = 2,
          OriginalOperation = SyntaxKind.BitwiseOrExpression,
          MutantOperation = SyntaxKind.BitwiseAndExpression,
          SourceSpan = new TextSpan(42, 2),
          LineSpan = new FileLinePositionSpan(string.Empty,
            new LinePosition(42, 1), new LinePosition(42, 11))
        }
      }.ToFrozenDictionary()
    };

  private static MutationRegistry MutationRegistryCreator()
  {
    var builder = new MutationRegistryBuilder();
    builder.AddRegistry(ExampleFileLevelMutationRegistry);
    return builder.ToFinalisedRegistry();
  }

  private static readonly MutationRegistry ExampleMutationRegistry =
    MutationRegistryCreator();

  [Fact]
  public void FileLevelMutationRegistryShouldBeSerialisable()
  {
    var json = JsonSerializer.Serialize(ExampleFileLevelMutationRegistry);
    var restoredRegistry =
      JsonSerializer.Deserialize<FileLevelMutationRegistry>(json);
    restoredRegistry.Should().BeEquivalentTo(ExampleFileLevelMutationRegistry);
  }

  [Fact]
  public async Task FileLevelMutationRegistryShouldBeSerialisableAsync()
  {
    using var stream = new MemoryStream();
    await JsonSerializer.SerializeAsync(stream, ExampleFileLevelMutationRegistry);
    stream.Position = 0;
    var restoredRegistry =
      await JsonSerializer.DeserializeAsync<FileLevelMutationRegistry>(stream);
    restoredRegistry.Should().BeEquivalentTo(ExampleFileLevelMutationRegistry);
  }

  [Fact]
  public void MutationRegistryShouldBeSerialisable()
  {
    var json = JsonSerializer.Serialize(ExampleMutationRegistry);
    var restoredRegistry = JsonSerializer.Deserialize<MutationRegistry>(json);
    restoredRegistry.Should().BeEquivalentTo(ExampleMutationRegistry);
  }

  [Fact]
  public async Task MutationRegistryShouldBeSerialisableAsync()
  {
    using var stream = new MemoryStream();
    await JsonSerializer.SerializeAsync(stream, ExampleMutationRegistry);
    stream.Position = 0;
    var restoredRegistry =
      await JsonSerializer.DeserializeAsync<MutationRegistry>(stream);
    restoredRegistry.Should().BeEquivalentTo(ExampleMutationRegistry);
  }
}