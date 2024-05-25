using System.Collections.Frozen;
using System.Text.Json;
using MutateCSharp.Mutation.Registry;
using System.Text.Json.Serialization;

namespace MutateCSharp.Util.Converters;

public sealed class
  FileLevelMutationRegistryConverter : JsonConverter<FileLevelMutationRegistry>
{
  public override FileLevelMutationRegistry Read(ref Utf8JsonReader reader,
    Type typeToConvert,
    JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
      throw new JsonException("Expected StartObject token");

    var relativePath = string.Empty;
    var envVar = string.Empty;
    var mutations = FrozenDictionary<int, Mutation.Mutation>.Empty;

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
      {
        return new FileLevelMutationRegistry
        {
          EnvironmentVariable = envVar,
          FileRelativePath = relativePath,
          Mutations = mutations
        };
      }

      if (reader.TokenType != JsonTokenType.PropertyName)
        throw new JsonException("Expected PropertyName token");

      var propertyName = reader.GetString();

      if (!reader.Read())
        throw new JsonException("Unexpected end of JSON object");

      switch (propertyName)
      {
        case "FileRelativePath":
          relativePath = reader.GetString() ?? string.Empty;
          break;
        case "EnvironmentVariable":
          envVar = reader.GetString() ?? string.Empty;
          break;
        case "Mutations":
          if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token for Mutations");

          var mutationsDictionary = new Dictionary<int, Mutation.Mutation>();
          while (reader.Read())
          {
            if (reader.TokenType == JsonTokenType.EndObject)
              break;

            if (reader.TokenType != JsonTokenType.PropertyName)
              throw new JsonException("Expected PropertyName token for Mutations");

            var mutationKey = Convert.ToInt32(reader.GetString());
            var mutationValue = JsonSerializer.Deserialize<Mutation.Mutation>(ref reader, options);
            
            mutationsDictionary[mutationKey] = mutationValue!;
          }

          mutations = mutationsDictionary.ToFrozenDictionary();
          break;
        default:
          throw new JsonException($"Unexpected property: {propertyName}");
      }
    }

    throw new JsonException("Unexpected end of JSON object");
  }

  public override void Write(Utf8JsonWriter writer,
    FileLevelMutationRegistry value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    writer.WriteString("FileRelativePath", value.FileRelativePath);
    writer.WriteString("EnvironmentVariable", value.EnvironmentVariable); 
    writer.WritePropertyName("Mutations");
    writer.WriteStartObject();
    foreach (var (id, mutation) in value.Mutations)
    {
      writer.WritePropertyName(id.ToString());
      JsonSerializer.Serialize(writer, mutation, typeof(Mutation.Mutation), options);
    }
    writer.WriteEndObject();
    writer.WriteEndObject();
  }
}