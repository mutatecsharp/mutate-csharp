using System.Text.Json;
using System.Text.Json.Serialization;

namespace MutateCSharp.Util.Converters;

public class EnumDisplayConverter<T> : JsonConverter<T> where T : struct, Enum
{
  public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType != JsonTokenType.String)
    {
      throw new JsonException();
    }

    var enumString = reader.GetString()!;
    if (Enum.TryParse(enumString, out T value))
    {
      return value;
    }

    throw new JsonException($"Unable to convert \"{enumString}\" to enum \"{typeof(T)}\"");
  }

  public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
  {
    writer.WriteStringValue(value.ToString());
  }
}
