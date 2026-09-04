using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;
using Qec.Itmg.Contracts.Evidence;

namespace Qec.Itmg.Compliance.Services;

public sealed record CoverageResultDistribution(
    int Compliant, int PartiallyCompliant, int NonCompliant, int NotApplicable, int NotTested);

public sealed record FrameworkCoverageDto(
    Guid FrameworkVersionId,
    string FrameworkCode,
    string VersionCode,
    DateTimeOffset AsOfUtc,
    int TotalRequirements,
    int MappedRequirements,
    int UnmappedRequirements,
    int MappedControls,
    int AssessedControls,
    int UnassessedControls,
    CoverageResultDistribution ResultDistribution,
    int EvidenceAvailable,
    int EvidenceMissing,
    int EvidenceExpired,
    string Notes);

public sealed class CoverageService(
    ComplianceDbContext db,
    IClock clock,
    IEvidenceCoverageQuery evidenceCoverage)
{
    public async Task<FrameworkCoverageDto> GetCoverageAsync(
        Guid frameworkVersionId, DateOnly? periodStart, DateOnly? periodEnd, CancellationToken ct)
    {
        FrameworkVersion version = await db.FrameworkVersions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == frameworkVersionId, ct)
            ?? throw new InvalidOperationException("Framework version was not found.");
        Framework framework = await db.Frameworks.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == version.FrameworkId, ct)
            ?? throw new InvalidOperationException("Framework was not found.");

        List<FrameworkRequirement> requirements = await db.FrameworkRequirements.AsNoTracking()
            .Where(x => x.FrameworkVersionId == frameworkVersionId && x.IsActive)
            .ToListAsync(ct);
        Guid[] reqIds = requirements.Select(x => x.Id).ToArray();

        List<ControlMapping> mappings = await db.ControlMappings.AsNoTracking()
            .Where(x => reqIds.Contains(x.FrameworkRequirementId))
            .ToListAsync(ct);

        HashSet<Guid> mappedReqIds = mappings.Select(x => x.FrameworkRequirementId).ToHashSet();
        HashSet<Guid> mappedControlIds = mappings.Select(x => x.InternalControlId).ToHashSet();

        IQueryable<ControlAssessment> assessmentsQ = db.ControlAssessments.AsNoTracking()
            .Where(x => x.Status == AssessmentStatus.Complete && mappedControlIds.Contains(x.InternalControlId));
        if (periodStart is DateOnly ps)
            assessmentsQ = assessmentsQ.Where(x => x.PeriodEnd == null || x.PeriodEnd >= ps);
        if (periodEnd is DateOnly pe)
            assessmentsQ = assessmentsQ.Where(x => x.PeriodStart == null || x.PeriodStart <= pe);

        List<ControlAssessment> completed = await assessmentsQ
            .OrderByDescending(x => x.AssessmentDateUtc ?? x.UpdatedAtUtc)
            .ToListAsync(ct);

        Dictionary<Guid, ControlAssessment> latestByControl = completed
            .GroupBy(x => x.InternalControlId)
            .ToDictionary(g => g.Key, g => g.First());

        int assessed = latestByControl.Count;
        int unassessed = mappedControlIds.Count - assessed;

        int compliant = 0, partial = 0, non = 0, na = 0, notTested = 0;
        foreach (ControlAssessment a in latestByControl.Values)
        {
            switch (a.Result)
            {
                case AssessmentResult.Compliant: compliant++; break;
                case AssessmentResult.PartiallyCompliant: partial++; break;
                case AssessmentResult.NonCompliant: non++; break;
                case AssessmentResult.NotApplicable: na++; break;
                default: notTested++; break;
            }
        }

        int evidenceAvailable = 0, evidenceMissing = mappedControlIds.Count, evidenceExpired = 0;
        if (mappedControlIds.Count > 0)
        {
            EvidenceCoverageSnapshot snap = await evidenceCoverage.GetForControlsAsync(
                mappedControlIds.ToList(), clock.UtcNow, ct);
            evidenceAvailable = snap.ControlsWithAvailableEvidence;
            evidenceMissing = snap.ControlsMissingEvidence;
            evidenceExpired = snap.ControlsWithExpiredOnlyEvidence;
        }

        return new(
            frameworkVersionId,
            framework.Code,
            version.VersionCode,
            clock.UtcNow,
            requirements.Count,
            mappedReqIds.Count,
            requirements.Count - mappedReqIds.Count,
            mappedControlIds.Count,
            assessed,
            Math.Max(0, unassessed),
            new CoverageResultDistribution(compliant, partial, non, na, notTested),
            evidenceAvailable,
            evidenceMissing,
            evidenceExpired,
            "Counts only. Mapping or evidence does not imply compliance. No organization-wide percentage is computed.");
    }
}
