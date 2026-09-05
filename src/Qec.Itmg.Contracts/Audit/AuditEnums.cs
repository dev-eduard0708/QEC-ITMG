namespace Qec.Itmg.Contracts.Audit;

public enum AuditAggregateType
{
    User = 1,
    Role = 2,
    Ticket = 3,
    Problem = 4,
    Change = 5,
    Event = 6,
    Access = 7,
    Document = 8,
    Control = 9,
    Assessment = 10,
    Evidence = 11,
    AuditEngagement = 12,
    Finding = 13,
    Vulnerability = 14,
    Risk = 15,
    PolicyException = 16,
    PenetrationTest = 17,
    BiaRecord = 18,
    ContinuityPlan = 19,
    RecoveryProcedure = 20,
    DrTest = 21,
    Vendor = 22,
    Contract = 23,
    VendorAssessment = 24,
    ReportExport = 25,
    ReportSnapshot = 26,
    IntegrationRun = 27,
}

public enum AuditActorType
{
    User = 1,
    System = 2,
    Integration = 3,
}

public enum AuditSource
{
    Ui = 1,
    Api = 2,
    Job = 3,
    Integration = 4,
}

public enum BusinessAuditAction
{
    Created = 1,
    Updated = 2,
    StatusChanged = 3,
    Assigned = 4,
    Unassigned = 5,
    Linked = 6,
    Unlinked = 7,
}

public enum SecurityEventType
{
    LoginSuccess = 1,
    LoginFailure = 2,
    Logout = 3,
    PermissionDenied = 4,
    RoleAssigned = 5,
    RoleUnassigned = 6,
    PermissionGranted = 7,
    PermissionRevoked = 8,
    UserDisabled = 9,
    UserEnabled = 10,
    BreakGlassLoginSuccess = 11,
    BreakGlassLoginFailed = 12,
}

public enum SecurityEventOutcome
{
    Success = 1,
    Failure = 2,
    Denied = 3,
}
