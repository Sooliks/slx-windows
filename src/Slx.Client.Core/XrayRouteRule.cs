using System.Text.Json.Serialization;
namespace Slx.Client.Core;
internal sealed record XrayRouteRule([property: JsonPropertyName("outboundTag")] string OutboundTag)
{
    [JsonPropertyName("type")] public string Type => "field";
    [JsonPropertyName("ip"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? Ips { get; init; }
    [JsonPropertyName("domain"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? Domains { get; init; }
    [JsonPropertyName("inboundTag"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? Inbounds { get; init; }
    [JsonPropertyName("process"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string[]? Processes { get; init; }
    [JsonPropertyName("network"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Network { get; init; }
    [JsonPropertyName("port"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Port { get; init; }
    [JsonPropertyName("ruleTag"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RuleTag { get; init; }
}
