using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.MutationTesting;
using Xunit.Abstractions;

namespace MutateCSharp.Test.Converters;

public class MutationTestResultTest(ITestOutputHelper testOutputHelper)
{
  private static readonly MutationTestResult ExampleMutationTestResult =
    new()
    {
      MutantStatus = new Dictionary<MutantActivationInfo, MutantStatus>
      {
        [new MutantActivationInfo("mutant-3", 12)] = MutantStatus.Survived,
        [new MutantActivationInfo("mutant-4", 23)] = MutantStatus.Killed
      }.ToFrozenDictionary()
    };

  [Fact]
  public void MutationTestResultShouldBeSerialisable()
  {
    var json = JsonSerializer.Serialize(ExampleMutationTestResult);
    
    testOutputHelper.WriteLine(json);
    
    var restoredResult = JsonSerializer.Deserialize<MutationTestResult>(json);
    restoredResult.Should().BeEquivalentTo(ExampleMutationTestResult);
  }

  [Fact]
  public async Task MutationTestResultShouldSerialisableAsync()
  {
    using var stream = new MemoryStream();
    await JsonSerializer.SerializeAsync(stream, ExampleMutationTestResult);
    stream.Position = 0;
    var restoredRegistry =
      await JsonSerializer.DeserializeAsync<MutationTestResult>(stream);
    restoredRegistry.Should().BeEquivalentTo(ExampleMutationTestResult);
  }
}