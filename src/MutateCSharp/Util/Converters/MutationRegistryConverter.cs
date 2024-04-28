using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.Util.Converters;

public sealed class MutationRegistryConverter: JsonConverter<MutationRegistry>
{
  public override MutationRegistry Read(ref Utf8JsonReader reader, Type typeToConvert,
    JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
      throw new JsonException("Expected StartObject token");

    var builder = new Dictionary<string, FileLevelMutationRegistry>();

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
        return new MutationRegistry
        {
          RelativePathToRegistry = builder.ToFrozenDictionary()
        };

      if (reader.TokenType != JsonTokenType.PropertyName)
        throw new JsonException("Expected PropertyName token");

      var relativePath = reader.GetString()!;

      if (!reader.Read())
        throw new JsonException("Unexpected end of JSON object");

      var fileLevelRegistry =
        JsonSerializer.Deserialize<FileLevelMutationRegistry>(ref reader,
          options)!;

      builder[relativePath] = fileLevelRegistry;
    }
    
    throw new JsonException("Unexpected end of JSON object");
  }

  public override void Write(Utf8JsonWriter writer, MutationRegistry value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    foreach (var (relativePath, registry) in value.RelativePathToRegistry)
    {
      writer.WritePropertyName(relativePath);
      JsonSerializer.Serialize(writer, registry,
        typeof(FileLevelMutationRegistry), options);
    }
    writer.WriteEndObject();
  }
}