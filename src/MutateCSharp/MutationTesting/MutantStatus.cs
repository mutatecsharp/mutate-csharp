using MutateCSharp.Util.Converters;
using Newtonsoft.Json;

namespace MutateCSharp.MutationTesting;

[JsonConverter(typeof(EnumDisplayConverter<MutantStatus>))]
public enum MutantStatus
{
  None,
  Killed,
  Survived,
  Timeout,
  Uncovered
}