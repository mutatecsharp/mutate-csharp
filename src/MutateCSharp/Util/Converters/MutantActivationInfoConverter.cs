using MutateCSharp.ExecutionTracing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MutateCSharp.Util.Converters;

public sealed class MutantActivationInfoConverter : JsonConverter<MutantActivationInfo>
{
  public override MutantActivationInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException("Expected StartObject token");
    }

    string envVar = string.Empty;
    int mutantId = 0;

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
      {
        return new MutantActivationInfo(envVar, mutantId);
      }

      if (reader.TokenType == JsonTokenType.PropertyName)
      {
        var propertyName = reader.GetString()!;
        reader.Read();

        switch (propertyName)
        {
          case nameof(MutantActivationInfo.EnvVar):
            envVar = reader.GetString()!;
            break;
          case nameof(MutantActivationInfo.MutantId):
            mutantId = reader.GetInt32();
            break;
          default:
            throw new JsonException($"Unexpected property: {propertyName}");
        }
      }
    }

    throw new JsonException("Unexpected end of JSON object");
  }

  public override void Write(Utf8JsonWriter writer, MutantActivationInfo value, JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    writer.WriteString(nameof(MutantActivationInfo.EnvVar), value.EnvVar);
    writer.WriteNumber(nameof(MutantActivationInfo.MutantId), value.MutantId);
    writer.WriteEndObject();
  }
}
