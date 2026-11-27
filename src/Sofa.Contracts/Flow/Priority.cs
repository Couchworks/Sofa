using System.Text.Json.Serialization;

namespace Sofa.Contracts.Flow;

[JsonConverter(typeof(JsonStringEnumConverter<Priority>))]
public enum Priority
{
    Urgent,
    High,
    Medium,
    Low,
    None,
}
