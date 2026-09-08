using System.Text.Json.Serialization;

namespace Qec.Itmg.Organization.Domain;

/// <summary>Organizational unit classification for persisted Department rows.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DepartmentUnitType
{
    /// <summary>Default for functional units; keep non-zero first values if adding defaults later.</summary>
    Department = 0,
    Company = 1,
    Section = 2,
    Office = 3,
    Team = 4,
    Other = 5,
}
