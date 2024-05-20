using System.Collections.Frozen;
using System.Text.Json;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using MutateCSharp.Mutation.Registry;
using MutateCSharp.Util;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Converters;

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
          OriginalExpressionTemplate = "{0} & {1}",
          MutantOperation = SyntaxKind.BitwiseOrExpression,
          MutantOperandKind = CodeAnalysisUtil.OperandKind.LeftOperand,
          MutantExpressionTemplate = "{0} | {1}",
          SourceSpan = new TextSpan(42, 2),
          LineSpan = new FileLinePositionSpan(string.Empty,
            new LinePosition(42, 1), new LinePosition(42, 11))
        },
        [2] = new()
        {
          MutantId = 2,
          OriginalOperation = SyntaxKind.BitwiseOrExpression,
          OriginalExpressionTemplate = "{0} | {1}",
          MutantOperation = SyntaxKind.BitwiseAndExpression,
          MutantOperandKind = CodeAnalysisUtil.OperandKind.RightOperand,
          MutantExpressionTemplate = "{0} & {1}",
          SourceSpan = new TextSpan(42, 2),
          LineSpan = new FileLinePositionSpan(string.Empty,
            new LinePosition(42, 1), new LinePosition(42, 11))
        }
      }.ToFrozenDictionary()
    };

  private static ProjectLevelMutationRegistry MutationRegistryCreator()
  {
    var builder = new ProjectLevelMutationRegistryBuilder();
    builder.AddRegistry(ExampleFileLevelMutationRegistry);
    return builder.ToFinalisedRegistry();
  }

  private static readonly ProjectLevelMutationRegistry ExampleProjectLevelMutationRegistry =
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
  public void ProjectLevelMutationRegistryShouldBeSerialisable()
  {
    var json = JsonSerializer.Serialize(ExampleProjectLevelMutationRegistry);
    var restoredRegistry = JsonSerializer.Deserialize<ProjectLevelMutationRegistry>(json);
    restoredRegistry.Should().BeEquivalentTo(ExampleProjectLevelMutationRegistry);
  }

  [Fact]
  public async Task ProjectLevelMutationRegistryShouldBeSerialisableAsync()
  {
    using var stream = new MemoryStream();
    await JsonSerializer.SerializeAsync(stream, ExampleProjectLevelMutationRegistry);
    stream.Position = 0;
    var restoredRegistry =
      await JsonSerializer.DeserializeAsync<ProjectLevelMutationRegistry>(stream);
    restoredRegistry.Should().BeEquivalentTo(ExampleProjectLevelMutationRegistry);
  }
}