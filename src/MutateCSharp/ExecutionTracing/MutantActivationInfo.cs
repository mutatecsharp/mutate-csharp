using System.Text.Json.Serialization;
using MutateCSharp.Util.Converters;

namespace MutateCSharp.ExecutionTracing;

[JsonConverter(typeof(MutantActivationInfoConverter))]
public record MutantActivationInfo(string EnvVar, int MutantId);
