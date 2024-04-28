using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace MutateCSharp.Util.Converters;

public class FileLinePositionSpanConverter : JsonConverter<FileLinePositionSpan>
{
  public override FileLinePositionSpan Read(ref Utf8JsonReader reader,
    Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException("Expected StartObject token");
    }

    // Do not record source file path on a position span level; it is redundant
    LinePosition startLinePosition = default;
    LinePosition endLinePosition = default;

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
        return new FileLinePositionSpan(string.Empty, startLinePosition, endLinePosition);

      if (reader.TokenType != JsonTokenType.PropertyName)
        throw new JsonException("Expected PropertyName token");

      var propertyName = reader.GetString();

      if (!reader.Read())
        throw new JsonException("Unexpected end of JSON object");

      switch (propertyName)
      {
        case "StartLinePosition":
          startLinePosition = ReadLinePosition(ref reader);
          break;
        case "EndLinePosition":
          endLinePosition = ReadLinePosition(ref reader);
          break;
        default:
          throw new JsonException($"Unexpected property: {propertyName}");
      }
    }

    throw new JsonException("Unexpected end of JSON object");
  }

  private static LinePosition ReadLinePosition(ref Utf8JsonReader reader)
  {
    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException("Expected StartObject token for LinePosition");
    }

    int line = 0;
    int character = 0;

    while (reader.Read())
    {
      if (reader.TokenType == JsonTokenType.EndObject)
        return new LinePosition(line, character);

      if (reader.TokenType != JsonTokenType.PropertyName)
        throw new JsonException("Expected PropertyName token for LinePosition");

      var propertyName = reader.GetString();

      if (!reader.Read())
        throw new JsonException("Unexpected end of LinePosition object");

      switch (propertyName)
      {
        case "Line":
          line = reader.GetInt32();
          break;
        case "Character":
          character = reader.GetInt32();
          break;
        default:
          throw new JsonException(
            $"Unexpected property in LinePosition: {propertyName}");
      }
    }

    throw new JsonException("Unexpected end of LinePosition object");
  }

  public override void Write(Utf8JsonWriter writer, FileLinePositionSpan value,
    JsonSerializerOptions options)
  {
    writer.WriteStartObject();
    writer.WriteStartObject("StartLinePosition");
    writer.WriteNumber("Line", value.StartLinePosition.Line);
    writer.WriteNumber("Character", value.StartLinePosition.Character);
    writer.WriteEndObject();
    writer.WriteStartObject("EndLinePosition");
    writer.WriteNumber("Line", value.EndLinePosition.Line);
    writer.WriteNumber("Character", value.EndLinePosition.Character);
    writer.WriteEndObject();
    writer.WriteEndObject();
  }
}