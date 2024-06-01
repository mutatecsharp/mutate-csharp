using System.Text.Json;

namespace MutateCSharp.Util;

public static class ConverterUtil
{
  public static T? DeserializeToAnonymousType<T>(
    string json, T _,
    JsonSerializerOptions? options = default)
  {
    return JsonSerializer.Deserialize<T>(json, options);
  }

  public static ValueTask<T?> DeserializeToAnonymousTypeAsync<T>(
    Stream stream, T _,
    JsonSerializerOptions? options = default,
    CancellationToken cancellationToken = default)
  {
    return JsonSerializer.DeserializeAsync<T>(stream, options, cancellationToken);
  }
}