using MutateCSharp.Util.Converters;
using Newtonsoft.Json;

namespace MutateCSharp.ExecutionTracing;

[JsonConverter(typeof(MutantActivationInfoConverter))]
public record MutantActivationInfo(string EnvVar, int MutantId);
