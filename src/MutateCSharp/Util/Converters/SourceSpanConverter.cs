using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis.Text;

namespace MutateCSharp.Util.Converters;

public sealed class SourceSpanConverter: JsonConverter<TextSpan>
{
  public override TextSpan Read(ref Utf8JsonReader reader, Type typeToConvert,
    JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
      throw new JsonException("Expected StartObject token");
    
    var start = 0;
    var length = 0;

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
        return new TextSpan(start, length);
      
      if (reader.TokenType != JsonTokenType.PropertyName)
        throw new JsonException("Expected PropertyName token");

      var propertyName = reader.GetString();

      if (!reader.Read())
      {
        throw new JsonException("Unexpected end of JSON object");
      }

      switch (propertyName)
      {
        case "Start":
          start = reader.GetInt32();
          break;
        case "Length":
          length = reader.GetInt32();
          break;
        default:
          throw new JsonException($"Unexpected property: {propertyName}");
      }
    }

    throw new JsonException("Unexpected end of JSON object");
  }

  public override void Write(Utf8JsonWriter writer, TextSpan value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    writer.WriteNumber("Start", value.Start);
    writer.WriteNumber("Length", value.Length);
    writer.WriteEndObject();
  }
}