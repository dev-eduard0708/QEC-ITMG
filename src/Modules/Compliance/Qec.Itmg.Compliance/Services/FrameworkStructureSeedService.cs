using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;

namespace Qec.Itmg.Compliance.Services;

/// <summary>
/// Seeds framework identity + high-level structure placeholders only.
/// Does not insert licensed requirement text (e.g. full COBIT practices).
/// </summary>
public sealed class FrameworkStructureSeedService(
    ComplianceDbContext db,
    FrameworkImportService import,
    IClock clock)
{
    public async Task<int> EnsureStructureAsync(CancellationToken ct)
    {
        int created = 0;
        created += await EnsureFramework("COBIT", "COBIT", "ISACA",
            "IT governance and management reference. Structure placeholders only — licensed content via import.",
            "2019", "COBIT 2019 structure placeholder",
            [
                ("EDM", "Evaluate, Direct and Monitor", FrameworkRequirementType.Domain, null, 1),
                ("APO", "Align, Plan and Organize", FrameworkRequirementType.Domain, null, 2),
                ("BAI", "Build, Acquire and Implement", FrameworkRequirementType.Domain, null, 3),
                ("DSS", "Deliver, Service and Support", FrameworkRequirementType.Domain, null, 4),
                ("MEA", "Monitor, Evaluate and Assess", FrameworkRequirementType.Domain, null, 5),
            ], ct);

        created += await EnsureFramework("ISO27001", "ISO/IEC 27001", "ISO",
            "ISMS reference. Clause structure placeholders only — full text via licensed import.",
            "2022", "ISO/IEC 27001:2022 structure placeholder",
            [
                ("4", "Context of the organization", FrameworkRequirementType.Clause, null, 4),
                ("5", "Leadership", FrameworkRequirementType.Clause, null, 5),
                ("6", "Planning", FrameworkRequirementType.Clause, null, 6),
                ("7", "Support", FrameworkRequirementType.Clause, null, 7),
                ("8", "Operation", FrameworkRequirementType.Clause, null, 8),
                ("9", "Performance evaluation", FrameworkRequirementType.Clause, null, 9),
                ("10", "Improvement", FrameworkRequirementType.Clause, null, 10),
                ("A", "Annex A controls", FrameworkRequirementType.Domain, null, 100),
            ], ct);

        created += await EnsureFramework("NISTCSF", "NIST Cybersecurity Framework", "NIST",
            "CSF functions as structure placeholders.",
            "2.0", "NIST CSF 2.0 structure placeholder",
            [
                ("GV", "Govern", FrameworkRequirementType.Domain, null, 1),
                ("ID", "Identify", FrameworkRequirementType.Domain, null, 2),
                ("PR", "Protect", FrameworkRequirementType.Domain, null, 3),
                ("DE", "Detect", FrameworkRequirementType.Domain, null, 4),
                ("RS", "Respond", FrameworkRequirementType.Domain, null, 5),
                ("RC", "Recover", FrameworkRequirementType.Domain, null, 6),
            ], ct);

        created += await EnsureFramework("CIS", "CIS Controls", "Center for Internet Security",
            "CIS Controls identity placeholder — detailed safeguards via import.",
            "v8", "CIS Controls v8 structure placeholder",
            [
                ("IG1", "Implementation Group 1", FrameworkRequirementType.Domain, null, 1),
                ("IG2", "Implementation Group 2", FrameworkRequirementType.Domain, null, 2),
                ("IG3", "Implementation Group 3", FrameworkRequirementType.Domain, null, 3),
            ], ct);

        created += await EnsureFramework("COSO", "COSO Internal Control — Integrated Framework", "COSO",
            "Organization-wide internal control principles. Not an IT governance framework.",
            "2013", "COSO 2013 structure placeholder",
            [
                ("CE", "Control Environment", FrameworkRequirementType.Domain, null, 1),
                ("RA", "Risk Assessment", FrameworkRequirementType.Domain, null, 2),
                ("CA", "Control Activities", FrameworkRequirementType.Domain, null, 3),
                ("IC", "Information and Communication", FrameworkRequirementType.Domain, null, 4),
                ("MO", "Monitoring Activities", FrameworkRequirementType.Domain, null, 5),
            ], ct);

        created += await EnsureFramework("INTERNAL", "Internal Checklist", "QEC",
            "QEC internal cybersecurity / IT checklist structure.",
            "1.0", "Internal checklist v1",
            [
                ("IAM", "Access management checks", FrameworkRequirementType.Domain, null, 1),
                ("CHG", "Change management checks", FrameworkRequirementType.Domain, null, 2),
                ("OPS", "Operations checks", FrameworkRequirementType.Domain, null, 3),
                ("SEC", "Security checks", FrameworkRequirementType.Domain, null, 4),
            ], ct);

        return created;
    }

    private async Task<int> EnsureFramework(
        string code, string name, string publisher, string description,
        string versionCode, string versionTitle,
        (string Code, string Title, FrameworkRequirementType Type, string? Parent, int Sort)[] structure,
        CancellationToken ct)
    {
        if (await db.Frameworks.AnyAsync(x => x.Code == code, ct))
            return 0;

        FrameworkImportPayload payload = new(
            code, name, publisher, description, versionCode, versionTitle, true,
            structure.Select(s => new FrameworkImportRequirement(
                s.Code, s.Title, s.Type.ToString(), s.Parent,
                "Structure placeholder — licensed/public detailed text imported separately.", s.Sort)).ToList());

        await import.ImportAsync(payload, ct);
        _ = clock;
        return 1;
    }
}
