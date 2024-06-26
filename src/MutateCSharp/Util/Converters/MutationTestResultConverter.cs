using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using MutateCSharp.ExecutionTracing;
using MutateCSharp.MutationTesting;

namespace MutateCSharp.Util.Converters;

public sealed class MutantStatusConverter : JsonConverter<
  FrozenDictionary<MutantActivationInfo, MutantStatus>>
{
  private static readonly EnumDisplayConverter<MutantStatus>
    MutantStatusEnumConverter = new();
  
  public override FrozenDictionary<MutantActivationInfo, MutantStatus> Read(
    ref Utf8JsonReader reader, Type typeToConvert,
    JsonSerializerOptions options)
  {
    var dictionary = new Dictionary<MutantActivationInfo, MutantStatus>();

    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException();
    }

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
      {
        return dictionary.ToFrozenDictionary();
      }

      if (reader.TokenType == JsonTokenType.PropertyName)
      {
        var key = reader.GetString()!;
        var split = key.Split(':');
        reader.Read();
        var value =
          MutantStatusEnumConverter.Read(ref reader, typeof(MutantStatus), options);
        dictionary.Add(new MutantActivationInfo(split[0], int.Parse(split[1])),
          value);
      }
    }

    throw new JsonException();
  }

  public override void Write(Utf8JsonWriter writer,
    FrozenDictionary<MutantActivationInfo, MutantStatus> value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();

    foreach (var kvp in value)
    {
      writer.WritePropertyName($"{kvp.Key.EnvVar}:{kvp.Key.MutantId}");
      MutantStatusEnumConverter.Write(writer, kvp.Value, options);
    }

    writer.WriteEndObject();
  }
}

public sealed class IndividualTestCaseMutantTestResultConverter : JsonConverter<
  FrozenDictionary<MutantActivationInfo, TestRunResult>>
{
  private static readonly EnumDisplayConverter<TestRunResult>
    TestRunResultEnumConverter = new();
  
  public override FrozenDictionary<MutantActivationInfo, TestRunResult> Read(
    ref Utf8JsonReader reader, Type typeToConvert,
    JsonSerializerOptions options)
  {
    var dictionary = new Dictionary<MutantActivationInfo, TestRunResult>();

    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException();
    }

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
      {
        return dictionary.ToFrozenDictionary();
      }

      if (reader.TokenType == JsonTokenType.PropertyName)
      {
        var key = reader.GetString()!;
        var split = key.Split(':');
        reader.Read();
        var value =
          TestRunResultEnumConverter.Read(ref reader, typeof(MutantStatus), options);
        dictionary.Add(
          new MutantActivationInfo(split[0], int.Parse(split[1])),
          value);
      }
    }

    throw new JsonException();
  }

  public override void Write(Utf8JsonWriter writer,
    FrozenDictionary<MutantActivationInfo, TestRunResult> value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();

    foreach (var kvp in value)
    {
      writer.WritePropertyName($"{kvp.Key.EnvVar}:{kvp.Key.MutantId}");
      TestRunResultEnumConverter.Write(writer, kvp.Value, options);
    }

    writer.WriteEndObject();
  }
}

public sealed class MutantTestResultsConverter : JsonConverter<FrozenDictionary<
  string, FrozenDictionary<MutantActivationInfo, TestRunResult>>>
{
  private static readonly IndividualTestCaseMutantTestResultConverter
    IndividualConverter = new();

  public override FrozenDictionary<string,
    FrozenDictionary<MutantActivationInfo, TestRunResult>> Read(
    ref Utf8JsonReader reader, Type typeToConvert,
    JsonSerializerOptions options)
  {
    var dictionary =
      new Dictionary<string,
        FrozenDictionary<MutantActivationInfo, TestRunResult>>();

    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException();
    }

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
      {
        return dictionary.ToFrozenDictionary();
      }

      if (reader.TokenType == JsonTokenType.PropertyName)
      {
        var testName = reader.GetString()!;
        reader.Read();

        var mutantTestRunResult =
          IndividualConverter.Read(ref reader, typeToConvert, options);
        dictionary[testName] = mutantTestRunResult;
      }
    }

    throw new JsonException();
  }

  public override void Write(Utf8JsonWriter writer,
    FrozenDictionary<string,
      FrozenDictionary<MutantActivationInfo, TestRunResult>> value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();

    foreach (var kvp in value)
    {
      writer.WritePropertyName(kvp.Key);
      IndividualConverter.Write(writer, kvp.Value, options);
    }

    writer.WriteEndObject();
  }
}