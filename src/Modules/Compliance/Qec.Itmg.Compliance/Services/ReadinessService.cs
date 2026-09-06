using Microsoft.EntityFrameworkCore;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;
using Qec.Itmg.Contracts.Audit;
using Qec.Itmg.Contracts.Evidence;
using Qec.Itmg.Contracts.Governance;

namespace Qec.Itmg.Compliance.Services;

public sealed record ReadinessMetricsDto(
    int ApplicableRequirements,
    int NotApplicableRequirements,
    int MappedRequirements,
    int UnmappedRequirements,
    double MappedCoveragePercent,
    int AssessedRequirements,
    int UnassessedRequirements,
    double AssessmentCoveragePercent,
    int EvidenceAvailable,
    int EvidenceMissing,
    int EvidenceExpired,
    double EvidenceCoveragePercent,
    int ReadyForReview,
    int NotReadyForReview,
    double ReadinessCoveragePercent,
    CoverageResultDistribution ResultDistribution);

public sealed record ReadinessLandingCardDto(
    Guid FrameworkId,
    string FrameworkCode,
    string Name,
    string? Description,
    string ProfileType,
    string? VersionCode,
    Guid? FrameworkVersionId,
    ReadinessMetricsDto Metrics,
    int OpenGaps);

public sealed record ReadinessLandingDto(
    string Disclaimer,
    IReadOnlyList<ReadinessLandingCardDto> Cards);

public sealed record ReadinessDomainDto(
    Guid DomainRequirementId,
    string Code,
    string Title,
    int ApplicableCount,
    int ReadyForReviewCount,
    double ReadinessPercent,
    int MappedCount,
    int AssessedCount,
    int EvidenceMissingCount,
    int UnmappedCount);

public sealed record ReadinessFrameworkSummaryDto(
    Guid FrameworkId,
    string FrameworkCode,
    string Name,
    string? Description,
    string ProfileType,
    string VersionCode,
    Guid FrameworkVersionId,
    string Disclaimer,
    ReadinessMetricsDto Metrics,
    IReadOnlyList<ReadinessDomainDto> Domains);

public sealed record ReadinessRequirementListItemDto(
    Guid Id,
    string Code,
    string Title,
    string? Text,
    string RequirementType,
    Guid? ParentRequirementId,
    string? DomainCode,
    string? DomainTitle,
    string ReadinessState,
    string ApplicabilityStatus,
    string? ApplicabilityReason,
    int MappedControlCount,
    bool HasCompletedAssessment,
    bool HasAvailableEvidence,
    bool HasExpiredEvidence,
    string? LatestAssessmentResult,
    IReadOnlyList<OperationalLinkDto> OperationalLinks);

public sealed record OperationalLinkDto(
    Guid Id,
    Guid FrameworkRequirementId,
    string LinkType,
    string TitleEn,
    string TitleAr,
    string InternalRoute,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    Guid CreatedByUserId);

public sealed record ReadinessMappedControlDto(
    Guid InternalControlId,
    string? ControlNumber,
    string? Title,
    string? Status,
    Guid? PrimaryOwnerUserId,
    string? LatestAssessmentStatus,
    string? LatestAssessmentResult,
    DateTimeOffset? LatestAssessmentDateUtc,
    bool HasAvailableEvidence,
    bool HasExpiredOnlyEvidence);

public sealed record ReadinessRequirementDetailDto(
    Guid Id,
    string Code,
    string Title,
    string? Text,
    string RequirementType,
    Guid FrameworkVersionId,
    string FrameworkCode,
    Guid? ParentRequirementId,
    string? DomainCode,
    string? DomainTitle,
    string ReadinessState,
    string ApplicabilityStatus,
    string? ApplicabilityReason,
    Guid? ApplicabilitySetByUserId,
    DateTimeOffset? ApplicabilitySetAtUtc,
    IReadOnlyList<ReadinessMappedControlDto> MappedControls,
    IReadOnlyList<OperationalLinkDto> OperationalLinks);

public sealed class ReadinessService(
    ComplianceDbContext db,
    IClock clock,
    IEvidenceCoverageQuery evidenceCoverage,
    IInternalControlLookup controlLookup,
    IBusinessAuditWriter businessAudit)
{
    public const string DisclaimerEn =
        "Readiness indicates documented control coverage, assessment activity and evidence availability. It is not certification or an external audit opinion.";

    public async Task<ReadinessLandingDto> GetLandingAsync(string? locale, CancellationToken ct)
    {
        List<Framework> frameworks = await db.Frameworks.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProfileType == FrameworkProfileType.AuditReadiness ? 0
                : x.ProfileType == FrameworkProfileType.CybersecurityReadiness ? 1
                : x.ProfileType == FrameworkProfileType.Governance ? 2 : 3)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);

        List<ReadinessLandingCardDto> cards = [];
        foreach (Framework fw in frameworks)
        {
            FrameworkVersion? version = await GetCurrentVersionAsync(fw.Id, ct);
            if (version is null) continue;

            (string name, string? description) = await LocalizeFrameworkAsync(fw, locale, ct);
            ReadinessMetricsDto metrics = await ComputeMetricsAsync(version.Id, null, null, clock.UtcNow, ct);
            int openGaps = metrics.ApplicableRequirements - metrics.ReadyForReview;
            cards.Add(new(
                fw.Id, fw.Code, name, description, fw.ProfileType.ToString(),
                version.VersionCode, version.Id, metrics, Math.Max(0, openGaps)));
        }

        return new(DisclaimerEn, cards);
    }

    public async Task<ReadinessFrameworkSummaryDto?> GetFrameworkSummaryAsync(
        string frameworkCode, string? locale, DateOnly? periodStart, DateOnly? periodEnd, CancellationToken ct)
    {
        Framework? fw = await FindFrameworkAsync(frameworkCode, ct);
        if (fw is null) return null;
        FrameworkVersion? version = await GetCurrentVersionAsync(fw.Id, ct);
        if (version is null) return null;

        (string name, string? description) = await LocalizeFrameworkAsync(fw, locale, ct);
        DateTimeOffset asOf = clock.UtcNow;
        ReadinessComputation comp = await ComputeAsync(version.Id, periodStart, periodEnd, asOf, ct);
        ReadinessMetricsDto metrics = ToMetrics(comp);

        List<FrameworkRequirement> domains = await db.FrameworkRequirements.AsNoTracking()
            .Where(x => x.FrameworkVersionId == version.Id && x.IsActive
                && x.RequirementType == FrameworkRequirementType.Domain)
            .OrderBy(x => x.SortOrder ?? int.MaxValue).ThenBy(x => x.Code)
            .ToListAsync(ct);

        Dictionary<Guid, (string Title, string? Text)> domainTitles = await LocalizeRequirementsAsync(
            domains.Select(x => x.Id).ToList(), locale, ct);

        List<ReadinessDomainDto> domainDtos = [];
        foreach (FrameworkRequirement domain in domains)
        {
            List<RequirementState> leaves = comp.States
                .Where(s => s.DomainRequirementId == domain.Id)
                .ToList();
            int applicable = leaves.Count(s => s.State != RequirementReadinessState.NotApplicable);
            int ready = leaves.Count(s => s.State == RequirementReadinessState.ReadyForReview);
            int mapped = leaves.Count(s =>
                s.State is RequirementReadinessState.MappedNeedsAssessment
                    or RequirementReadinessState.AssessedNeedsEvidence
                    or RequirementReadinessState.ReadyForReview);
            int assessed = leaves.Count(s =>
                s.State is RequirementReadinessState.AssessedNeedsEvidence
                    or RequirementReadinessState.ReadyForReview);
            int evidenceMissing = leaves.Count(s => s.State == RequirementReadinessState.AssessedNeedsEvidence);
            int unmapped = leaves.Count(s => s.State == RequirementReadinessState.Unmapped);
            string title = domainTitles.TryGetValue(domain.Id, out (string Title, string? Text) t)
                ? t.Title : domain.Title;

            domainDtos.Add(new(
                domain.Id, domain.Code, title, applicable, ready,
                Percent(ready, applicable), mapped, assessed, evidenceMissing, unmapped));
        }

        return new(
            fw.Id, fw.Code, name, description, fw.ProfileType.ToString(),
            version.VersionCode, version.Id, DisclaimerEn, metrics, domainDtos);
    }

    public async Task<IReadOnlyList<ReadinessRequirementListItemDto>> ListRequirementsAsync(
        string frameworkCode, string? locale, Guid? domainRequirementId, string? readinessState,
        string? search, DateOnly? periodStart, DateOnly? periodEnd, CancellationToken ct)
    {
        Framework? fw = await FindFrameworkAsync(frameworkCode, ct)
            ?? throw new InvalidOperationException("Framework was not found.");
        FrameworkVersion version = await GetCurrentVersionAsync(fw.Id, ct)
            ?? throw new InvalidOperationException("Framework version was not found.");

        ReadinessComputation comp = await ComputeAsync(version.Id, periodStart, periodEnd, clock.UtcNow, ct);
        RequirementReadinessState? filterState = null;
        if (!string.IsNullOrWhiteSpace(readinessState)
            && Enum.TryParse(readinessState, true, out RequirementReadinessState parsed))
            filterState = parsed;

        IEnumerable<RequirementState> query = comp.States;
        if (domainRequirementId is Guid domainId)
            query = query.Where(s => s.DomainRequirementId == domainId);
        if (filterState is RequirementReadinessState st)
            query = query.Where(s => s.State == st);
        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim();
            query = query.Where(s =>
                s.Requirement.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || s.Requirement.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (s.Requirement.Text is not null
                    && s.Requirement.Text.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        List<RequirementState> items = query
            .OrderBy(s => s.Requirement.SortOrder ?? int.MaxValue)
            .ThenBy(s => s.Requirement.Code)
            .ToList();

        Dictionary<Guid, (string Title, string? Text)> titles =
            await LocalizeRequirementsAsync(items.Select(s => s.Requirement.Id).ToList(), locale, ct);

        HashSet<Guid> domainIds = items.Where(s => s.DomainRequirementId.HasValue)
            .Select(s => s.DomainRequirementId!.Value).ToHashSet();
        Dictionary<Guid, FrameworkRequirement> domains = domainIds.Count == 0
            ? []
            : await db.FrameworkRequirements.AsNoTracking()
                .Where(x => domainIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        Dictionary<Guid, (string Title, string? Text)> domainTitles =
            await LocalizeRequirementsAsync(domainIds.ToList(), locale, ct);

        Guid[] reqIds = items.Select(s => s.Requirement.Id).ToArray();
        Dictionary<Guid, List<FrameworkRequirementOperationalLink>> links = await db
            .FrameworkRequirementOperationalLinks.AsNoTracking()
            .Where(x => reqIds.Contains(x.FrameworkRequirementId))
            .GroupBy(x => x.FrameworkRequirementId)
            .ToDictionaryAsync(g => g.Key, g => g.OrderBy(x => x.TitleEn).ToList(), ct);

        List<ReadinessRequirementListItemDto> result = [];
        foreach (RequirementState s in items)
        {
            (string title, string? text) = titles.TryGetValue(s.Requirement.Id, out (string Title, string? Text) loc)
                ? loc : (s.Requirement.Title, s.Requirement.Text);
            string? domainCode = null, domainTitle = null;
            if (s.DomainRequirementId is Guid did && domains.TryGetValue(did, out FrameworkRequirement? dom))
            {
                domainCode = dom.Code;
                domainTitle = domainTitles.TryGetValue(did, out (string Title, string? Text) dt) ? dt.Title : dom.Title;
            }

            links.TryGetValue(s.Requirement.Id, out List<FrameworkRequirementOperationalLink>? reqLinks);
            result.Add(new(
                s.Requirement.Id, s.Requirement.Code, title, text, s.Requirement.RequirementType.ToString(),
                s.Requirement.ParentRequirementId, domainCode, domainTitle, s.State.ToString(),
                s.ApplicabilityStatus.ToString(), s.ApplicabilityReason,
                s.MappedControlIds.Count, s.HasCompletedAssessment, s.HasAvailableEvidence, s.HasExpiredEvidence,
                s.LatestAssessmentResult?.ToString(),
                (reqLinks ?? []).Select(MapLink).ToList()));
        }

        return result;
    }

    public async Task<ReadinessRequirementDetailDto?> GetRequirementDetailAsync(
        string frameworkCode, Guid requirementId, string? locale,
        DateOnly? periodStart, DateOnly? periodEnd, CancellationToken ct)
    {
        Framework? fw = await FindFrameworkAsync(frameworkCode, ct);
        if (fw is null) return null;
        FrameworkVersion? version = await GetCurrentVersionAsync(fw.Id, ct);
        if (version is null) return null;

        FrameworkRequirement? req = await db.FrameworkRequirements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requirementId && x.FrameworkVersionId == version.Id, ct);
        if (req is null) return null;

        ReadinessComputation comp = await ComputeAsync(version.Id, periodStart, periodEnd, clock.UtcNow, ct);
        RequirementState? state = comp.States.FirstOrDefault(s => s.Requirement.Id == requirementId);
        if (state is null)
        {
            // Non-leaf (e.g. domain) — still return basic detail without readiness leaf state.
            Dictionary<Guid, (string Title, string? Text)> titles =
                await LocalizeRequirementsAsync([req.Id], locale, ct);
            (string title, string? text) = titles.TryGetValue(req.Id, out (string Title, string? Text) loc)
                ? loc : (req.Title, req.Text);
            IReadOnlyList<OperationalLinkDto> links = await ListOperationalLinksAsync(req.Id, ct);
            return new(
                req.Id, req.Code, title, text, req.RequirementType.ToString(), version.Id, fw.Code,
                req.ParentRequirementId, null, null, RequirementReadinessState.Unmapped.ToString(),
                RequirementApplicabilityStatus.Applicable.ToString(), null, null, null, [], links);
        }

        Dictionary<Guid, (string Title, string? Text)> reqTitles =
            await LocalizeRequirementsAsync([req.Id], locale, ct);
        (string reqTitle, string? reqText) = reqTitles.TryGetValue(req.Id, out (string Title, string? Text) rtl)
            ? rtl : (req.Title, req.Text);

        string? domainCode = null, domainTitle = null;
        if (state.DomainRequirementId is Guid did)
        {
            FrameworkRequirement? dom = await db.FrameworkRequirements.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == did, ct);
            if (dom is not null)
            {
                domainCode = dom.Code;
                Dictionary<Guid, (string Title, string? Text)> dTitles =
                    await LocalizeRequirementsAsync([did], locale, ct);
                domainTitle = dTitles.TryGetValue(did, out (string Title, string? Text) dt) ? dt.Title : dom.Title;
            }
        }

        IReadOnlyDictionary<Guid, InternalControlRefDto> controls =
            await controlLookup.GetByIdsAsync(state.MappedControlIds, ct);
        IReadOnlyList<EvidenceControlCoverageItem> evidenceItems =
            await evidenceCoverage.GetPerControlAsync(state.MappedControlIds, clock.UtcNow, ct);
        Dictionary<Guid, EvidenceControlCoverageItem> evidenceByControl =
            evidenceItems.ToDictionary(x => x.InternalControlId);

        Dictionary<Guid, ControlAssessment> latestAssessments = await LoadLatestAssessmentsAsync(
            state.MappedControlIds, periodStart, periodEnd, ct);

        List<ReadinessMappedControlDto> mapped = [];
        foreach (Guid controlId in state.MappedControlIds)
        {
            controls.TryGetValue(controlId, out InternalControlRefDto? ctrl);
            evidenceByControl.TryGetValue(controlId, out EvidenceControlCoverageItem? ev);
            latestAssessments.TryGetValue(controlId, out ControlAssessment? assessment);
            mapped.Add(new(
                controlId,
                ctrl?.ControlNumber,
                ctrl?.Title,
                ctrl?.Status,
                ctrl?.PrimaryOwnerUserId,
                assessment?.Status.ToString(),
                assessment?.Result.ToString(),
                assessment?.AssessmentDateUtc,
                ev?.HasAvailableEvidence ?? false,
                ev?.HasExpiredOnlyEvidence ?? false));
        }

        FrameworkRequirementApplicability? app = await db.FrameworkRequirementApplicabilities.AsNoTracking()
            .FirstOrDefaultAsync(x => x.FrameworkRequirementId == requirementId, ct);

        return new(
            req.Id, req.Code, reqTitle, reqText, req.RequirementType.ToString(), version.Id, fw.Code,
            req.ParentRequirementId, domainCode, domainTitle, state.State.ToString(),
            state.ApplicabilityStatus.ToString(), state.ApplicabilityReason,
            app?.SetByUserId, app?.SetAtUtc, mapped,
            await ListOperationalLinksAsync(requirementId, ct));
    }

    public async Task<FrameworkRequirementApplicability> SetApplicabilityAsync(
        Guid requirementId, RequirementApplicabilityStatus status, string? reason, Guid userId, CancellationToken ct)
    {
        _ = await db.FrameworkRequirements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requirementId, ct)
            ?? throw new InvalidOperationException("Framework requirement was not found.");

        FrameworkRequirementApplicability? existing = await db.FrameworkRequirementApplicabilities
            .FirstOrDefaultAsync(x => x.FrameworkRequirementId == requirementId, ct);

        string? oldStatus = existing?.Status.ToString();
        if (existing is null)
        {
            existing = FrameworkRequirementApplicability.Create(requirementId, status, userId, clock.UtcNow, reason);
            db.FrameworkRequirementApplicabilities.Add(existing);
        }
        else
        {
            existing.SetStatus(status, userId, clock.UtcNow, reason);
        }

        await db.SaveChangesAsync(ct);
        await businessAudit.AppendAsync(new BusinessAuditEntry
        {
            AggregateType = AuditAggregateType.Assessment,
            AggregateId = requirementId,
            Action = BusinessAuditAction.Updated,
            FieldName = "Applicability",
            OldValue = oldStatus,
            NewValue = status.ToString(),
            Source = AuditSource.Api,
        }, ct);
        return existing;
    }

    public async Task<IReadOnlyList<OperationalLinkDto>> ListOperationalLinksAsync(Guid requirementId, CancellationToken ct)
    {
        List<FrameworkRequirementOperationalLink> items = await db.FrameworkRequirementOperationalLinks.AsNoTracking()
            .Where(x => x.FrameworkRequirementId == requirementId)
            .OrderBy(x => x.TitleEn)
            .ToListAsync(ct);
        return items.Select(MapLink).ToList();
    }

    public async Task<OperationalLinkDto> CreateOperationalLinkAsync(
        Guid requirementId, OperationalLinkType linkType, string titleEn, string titleAr,
        string internalRoute, Guid userId, string? notes, CancellationToken ct)
    {
        _ = await db.FrameworkRequirements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requirementId, ct)
            ?? throw new InvalidOperationException("Framework requirement was not found.");

        FrameworkRequirementOperationalLink entity = FrameworkRequirementOperationalLink.Create(
            requirementId, linkType, titleEn, titleAr, internalRoute, userId, clock.UtcNow, notes);
        db.FrameworkRequirementOperationalLinks.Add(entity);
        await db.SaveChangesAsync(ct);
        return MapLink(entity);
    }

    public async Task<OperationalLinkDto> UpdateOperationalLinkAsync(
        Guid linkId, OperationalLinkType linkType, string titleEn, string titleAr,
        string internalRoute, string? notes, CancellationToken ct)
    {
        FrameworkRequirementOperationalLink entity = await db.FrameworkRequirementOperationalLinks
            .FirstOrDefaultAsync(x => x.Id == linkId, ct)
            ?? throw new InvalidOperationException("Operational link was not found.");
        entity.Update(linkType, titleEn, titleAr, internalRoute, notes);
        await db.SaveChangesAsync(ct);
        return MapLink(entity);
    }

    public async Task DeleteOperationalLinkAsync(Guid linkId, CancellationToken ct)
    {
        FrameworkRequirementOperationalLink? entity = await db.FrameworkRequirementOperationalLinks
            .FirstOrDefaultAsync(x => x.Id == linkId, ct);
        if (entity is null) return;
        db.FrameworkRequirementOperationalLinks.Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ReadinessMetricsDto> ComputeMetricsAsync(
        Guid versionId, DateOnly? periodStart, DateOnly? periodEnd, DateTimeOffset asOf, CancellationToken ct)
    {
        ReadinessComputation comp = await ComputeAsync(versionId, periodStart, periodEnd, asOf, ct);
        return ToMetrics(comp);
    }

    private static ReadinessMetricsDto ToMetrics(ReadinessComputation comp)
    {
        int applicable = comp.States.Count(s => s.State != RequirementReadinessState.NotApplicable);
        int notApplicable = comp.States.Count(s => s.State == RequirementReadinessState.NotApplicable);
        int mapped = comp.States.Count(s =>
            s.State is RequirementReadinessState.MappedNeedsAssessment
                or RequirementReadinessState.AssessedNeedsEvidence
                or RequirementReadinessState.ReadyForReview);
        int unmapped = comp.States.Count(s => s.State == RequirementReadinessState.Unmapped);
        int assessed = comp.States.Count(s =>
            s.State is RequirementReadinessState.AssessedNeedsEvidence
                or RequirementReadinessState.ReadyForReview);
        int unassessed = mapped - assessed;
        int ready = comp.States.Count(s => s.State == RequirementReadinessState.ReadyForReview);
        int notReady = applicable - ready;

        int evidenceAvailable = 0, evidenceMissing = 0, evidenceExpired = 0;
        foreach (RequirementState s in comp.States.Where(x => x.State != RequirementReadinessState.NotApplicable))
        {
            if (s.MappedControlIds.Count == 0) continue;
            if (s.HasAvailableEvidence) evidenceAvailable++;
            else if (s.HasExpiredEvidence) evidenceExpired++;
            else evidenceMissing++;
        }

        int compliant = 0, partial = 0, non = 0, na = 0, notTested = 0;
        foreach (AssessmentResult? result in comp.States
            .Where(s => s.State != RequirementReadinessState.NotApplicable && s.LatestAssessmentResult.HasValue)
            .Select(s => s.LatestAssessmentResult))
        {
            switch (result)
            {
                case AssessmentResult.Compliant: compliant++; break;
                case AssessmentResult.PartiallyCompliant: partial++; break;
                case AssessmentResult.NonCompliant: non++; break;
                case AssessmentResult.NotApplicable: na++; break;
                default: notTested++; break;
            }
        }

        return new(
            applicable, notApplicable, mapped, unmapped, Percent(mapped, applicable),
            assessed, Math.Max(0, unassessed), Percent(assessed, mapped),
            evidenceAvailable, evidenceMissing, evidenceExpired, Percent(evidenceAvailable, mapped),
            ready, Math.Max(0, notReady), Percent(ready, applicable),
            new CoverageResultDistribution(compliant, partial, non, na, notTested));
    }

    private async Task<ReadinessComputation> ComputeAsync(
        Guid versionId, DateOnly? periodStart, DateOnly? periodEnd, DateTimeOffset asOf, CancellationToken ct)
    {
        List<FrameworkRequirement> requirements = await db.FrameworkRequirements.AsNoTracking()
            .Where(x => x.FrameworkVersionId == versionId && x.IsActive)
            .ToListAsync(ct);

        List<FrameworkRequirement> leaves = requirements
            .Where(x => x.RequirementType != FrameworkRequirementType.Domain)
            .ToList();

        Dictionary<Guid, FrameworkRequirement> byId = requirements.ToDictionary(x => x.Id);
        Guid[] leafIds = leaves.Select(x => x.Id).ToArray();

        Dictionary<Guid, FrameworkRequirementApplicability> apps = leafIds.Length == 0
            ? []
            : await db.FrameworkRequirementApplicabilities.AsNoTracking()
                .Where(x => leafIds.Contains(x.FrameworkRequirementId))
                .ToDictionaryAsync(x => x.FrameworkRequirementId, ct);

        List<ControlMapping> mappings = leafIds.Length == 0
            ? []
            : await db.ControlMappings.AsNoTracking()
                .Where(x => leafIds.Contains(x.FrameworkRequirementId))
                .ToListAsync(ct);

        Dictionary<Guid, List<Guid>> mappedControls = mappings
            .GroupBy(x => x.FrameworkRequirementId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.InternalControlId).Distinct().ToList());

        HashSet<Guid> allControlIds = mappings.Select(x => x.InternalControlId).ToHashSet();

        // Prefer Active controls when lookup is available; unknown ids still count as mapped.
        IReadOnlyDictionary<Guid, InternalControlRefDto> controlRefs =
            await controlLookup.GetByIdsAsync(allControlIds, ct);
        foreach (KeyValuePair<Guid, List<Guid>> kv in mappedControls.ToList())
        {
            List<Guid> active = kv.Value
                .Where(id => !controlRefs.TryGetValue(id, out InternalControlRefDto? c)
                    || !string.Equals(c.Status, "Retired", StringComparison.OrdinalIgnoreCase))
                .ToList();
            mappedControls[kv.Key] = active;
        }

        HashSet<Guid> activeControlIds = mappedControls.Values.SelectMany(x => x).ToHashSet();
        Dictionary<Guid, ControlAssessment> latestByControl =
            await LoadLatestAssessmentsAsync(activeControlIds, periodStart, periodEnd, ct);

        IReadOnlyList<EvidenceControlCoverageItem> evidenceItems =
            await evidenceCoverage.GetPerControlAsync(activeControlIds, asOf, ct);
        Dictionary<Guid, EvidenceControlCoverageItem> evidenceByControl =
            evidenceItems.ToDictionary(x => x.InternalControlId);

        List<RequirementState> states = [];
        foreach (FrameworkRequirement leaf in leaves)
        {
            Guid? domainId = FindDomainId(leaf, byId);
            apps.TryGetValue(leaf.Id, out FrameworkRequirementApplicability? app);
            RequirementApplicabilityStatus appStatus = app?.Status ?? RequirementApplicabilityStatus.Applicable;
            string? appReason = app?.Reason;

            if (appStatus == RequirementApplicabilityStatus.NotApplicable)
            {
                states.Add(new RequirementState(
                    leaf, domainId, RequirementReadinessState.NotApplicable, appStatus, appReason,
                    [], false, false, false, null));
                continue;
            }

            mappedControls.TryGetValue(leaf.Id, out List<Guid>? controlIds);
            controlIds ??= [];
            if (controlIds.Count == 0)
            {
                states.Add(new RequirementState(
                    leaf, domainId, RequirementReadinessState.Unmapped, appStatus, appReason,
                    [], false, false, false, null));
                continue;
            }

            List<ControlAssessment> completed = controlIds
                .Where(latestByControl.ContainsKey)
                .Select(id => latestByControl[id])
                .Where(a => a.Status == AssessmentStatus.Complete)
                .ToList();
            bool hasAssessment = completed.Count > 0;
            AssessmentResult? latestResult = completed
                .OrderByDescending(a => a.AssessmentDateUtc ?? a.UpdatedAtUtc)
                .Select(a => (AssessmentResult?)a.Result)
                .FirstOrDefault();

            bool hasAvailable = controlIds.Any(id =>
                evidenceByControl.TryGetValue(id, out EvidenceControlCoverageItem? e) && e.HasAvailableEvidence);
            bool hasExpired = !hasAvailable && controlIds.Any(id =>
                evidenceByControl.TryGetValue(id, out EvidenceControlCoverageItem? e) && e.HasExpiredOnlyEvidence);

            RequirementReadinessState state;
            if (!hasAssessment)
                state = RequirementReadinessState.MappedNeedsAssessment;
            else if (!hasAvailable)
                state = RequirementReadinessState.AssessedNeedsEvidence;
            else
                state = RequirementReadinessState.ReadyForReview;

            states.Add(new RequirementState(
                leaf, domainId, state, appStatus, appReason, controlIds,
                hasAssessment, hasAvailable, hasExpired, latestResult));
        }

        return new ReadinessComputation(states);
    }

    private async Task<Dictionary<Guid, ControlAssessment>> LoadLatestAssessmentsAsync(
        IReadOnlyCollection<Guid> controlIds, DateOnly? periodStart, DateOnly? periodEnd, CancellationToken ct)
    {
        if (controlIds.Count == 0) return [];

        IQueryable<ControlAssessment> q = db.ControlAssessments.AsNoTracking()
            .Where(x => x.Status == AssessmentStatus.Complete && controlIds.Contains(x.InternalControlId));
        if (periodStart is DateOnly ps)
            q = q.Where(x => x.PeriodEnd == null || x.PeriodEnd >= ps);
        if (periodEnd is DateOnly pe)
            q = q.Where(x => x.PeriodStart == null || x.PeriodStart <= pe);

        List<ControlAssessment> completed = await q
            .OrderByDescending(x => x.AssessmentDateUtc ?? x.UpdatedAtUtc)
            .ToListAsync(ct);

        return completed
            .GroupBy(x => x.InternalControlId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private static Guid? FindDomainId(FrameworkRequirement leaf, Dictionary<Guid, FrameworkRequirement> byId)
    {
        Guid? current = leaf.ParentRequirementId;
        int guard = 0;
        while (current is Guid id && guard++ < 32)
        {
            if (!byId.TryGetValue(id, out FrameworkRequirement? parent)) return current;
            if (parent.RequirementType == FrameworkRequirementType.Domain) return parent.Id;
            current = parent.ParentRequirementId;
        }

        return null;
    }

    private async Task<Framework?> FindFrameworkAsync(string frameworkCode, CancellationToken ct)
    {
        string code = frameworkCode.Trim().ToUpperInvariant();
        return await db.Frameworks.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct);
    }

    private async Task<FrameworkVersion?> GetCurrentVersionAsync(Guid frameworkId, CancellationToken ct)
    {
        return await db.FrameworkVersions.AsNoTracking()
            .Where(x => x.FrameworkId == frameworkId)
            .OrderByDescending(x => x.IsCurrent)
            .ThenByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<(string Name, string? Description)> LocalizeFrameworkAsync(
        Framework fw, string? locale, CancellationToken ct)
    {
        string lang = NormalizeLocale(locale);
        List<FrameworkTranslation> translations = await db.FrameworkTranslations.AsNoTracking()
            .Where(x => x.FrameworkId == fw.Id)
            .ToListAsync(ct);
        FrameworkTranslation? match = translations.FirstOrDefault(x => x.LanguageCode == lang)
            ?? translations.FirstOrDefault(x => x.LanguageCode == "en");
        return match is null ? (fw.Name, fw.Description) : (match.Name, match.Description ?? fw.Description);
    }

    private async Task<Dictionary<Guid, (string Title, string? Text)>> LocalizeRequirementsAsync(
        IReadOnlyCollection<Guid> requirementIds, string? locale, CancellationToken ct)
    {
        if (requirementIds.Count == 0) return [];
        Guid[] ids = requirementIds.Distinct().ToArray();
        string lang = NormalizeLocale(locale);

        List<FrameworkRequirement> baseReqs = await db.FrameworkRequirements.AsNoTracking()
            .Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        List<FrameworkRequirementTranslation> translations = await db.FrameworkRequirementTranslations.AsNoTracking()
            .Where(x => ids.Contains(x.FrameworkRequirementId)).ToListAsync(ct);

        Dictionary<Guid, (string Title, string? Text)> result = [];
        foreach (FrameworkRequirement req in baseReqs)
        {
            FrameworkRequirementTranslation? match = translations
                .FirstOrDefault(t => t.FrameworkRequirementId == req.Id && t.LanguageCode == lang)
                ?? translations.FirstOrDefault(t => t.FrameworkRequirementId == req.Id && t.LanguageCode == "en");
            result[req.Id] = match is null
                ? (req.Title, req.Text)
                : (match.Title, match.Text ?? req.Text);
        }

        return result;
    }

    private static string NormalizeLocale(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return "en";
        string v = locale.Trim().ToLowerInvariant();
        if (v.StartsWith("ar", StringComparison.Ordinal)) return "ar";
        return "en";
    }

    private static double Percent(int numerator, int denominator) =>
        denominator <= 0 ? 0d : Math.Round(100d * numerator / denominator, 1);

    private static OperationalLinkDto MapLink(FrameworkRequirementOperationalLink x) => new(
        x.Id, x.FrameworkRequirementId, x.LinkType.ToString(), x.TitleEn, x.TitleAr,
        x.InternalRoute, x.Notes, x.CreatedAtUtc, x.CreatedByUserId);

    private sealed record RequirementState(
        FrameworkRequirement Requirement,
        Guid? DomainRequirementId,
        RequirementReadinessState State,
        RequirementApplicabilityStatus ApplicabilityStatus,
        string? ApplicabilityReason,
        IReadOnlyList<Guid> MappedControlIds,
        bool HasCompletedAssessment,
        bool HasAvailableEvidence,
        bool HasExpiredEvidence,
        AssessmentResult? LatestAssessmentResult);

    private sealed record ReadinessComputation(IReadOnlyList<RequirementState> States);
}
