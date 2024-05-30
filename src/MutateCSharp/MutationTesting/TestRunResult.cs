using MutateCSharp.Util.Converters;
using Newtonsoft.Json;

namespace MutateCSharp.MutationTesting;

[JsonConverter(typeof(EnumDisplayConverter<TestRunResult>))]
public enum TestRunResult
{
  None,
  Success,
  Failed,
  Timeout,
  Skipped
}