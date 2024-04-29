using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using MutateCSharp.Mutation.Registry;

namespace MutateCSharp.Util.Converters;

public sealed class ProjectLevelMutationRegistryConverter: JsonConverter<ProjectLevelMutationRegistry>
{
  public override ProjectLevelMutationRegistry Read(ref Utf8JsonReader reader, Type typeToConvert,
    JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
      throw new JsonException("Expected StartObject token");

    var builder = new Dictionary<string, FileLevelMutationRegistry>();

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
        return new ProjectLevelMutationRegistry
        {
          ProjectRelativePathToRegistry = builder.ToFrozenDictionary()
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

  public override void Write(Utf8JsonWriter writer, ProjectLevelMutationRegistry value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    foreach (var (relativePath, registry) in value.ProjectRelativePathToRegistry)
    {
      writer.WritePropertyName(relativePath);
      JsonSerializer.Serialize(writer, registry,
        typeof(FileLevelMutationRegistry), options);
    }
    writer.WriteEndObject();
  }
}