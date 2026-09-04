using Qec.Itmg.Contracts.Audit;

namespace Qec.Itmg.ServiceDesk.Services;

internal static class ServiceDeskAuditComposer
{
    public static BusinessAuditEntry TicketField(
        Guid ticketId,
        string? ticketNumber,
        string fieldName,
        string? oldValue,
        string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated) =>
        new()
        {
            AggregateType = AuditAggregateType.Ticket,
            AggregateId = ticketId,
            BusinessNumber = ticketNumber,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry ProblemCreated(Guid problemId, string problemNumber) =>
        new()
        {
            AggregateType = AuditAggregateType.Problem,
            AggregateId = problemId,
            BusinessNumber = problemNumber,
            Action = BusinessAuditAction.Created,
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry ProblemField(
        Guid problemId,
        string? problemNumber,
        string fieldName,
        string? oldValue,
        string? newValue,
        BusinessAuditAction action = BusinessAuditAction.Updated) =>
        new()
        {
            AggregateType = AuditAggregateType.Problem,
            AggregateId = problemId,
            BusinessNumber = problemNumber,
            Action = action,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry ProblemIncidentLinked(
        Guid problemId,
        string? problemNumber,
        Guid incidentTicketId,
        string? incidentNumber) =>
        new()
        {
            AggregateType = AuditAggregateType.Problem,
            AggregateId = problemId,
            BusinessNumber = problemNumber,
            Action = BusinessAuditAction.Linked,
            FieldName = "Incident",
            NewValue = $"{incidentNumber}|{incidentTicketId:D}",
            Source = AuditSource.Api,
        };

    public static BusinessAuditEntry ProblemIncidentUnlinked(
        Guid problemId,
        string? problemNumber,
        Guid incidentTicketId,
        string? incidentNumber) =>
        new()
        {
            AggregateType = AuditAggregateType.Problem,
            AggregateId = problemId,
            BusinessNumber = problemNumber,
            Action = BusinessAuditAction.Unlinked,
            FieldName = "Incident",
            OldValue = $"{incidentNumber}|{incidentTicketId:D}",
            Source = AuditSource.Api,
        };
}
