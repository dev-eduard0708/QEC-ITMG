using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qec.Itmg.BuildingBlocks.Time;
using Qec.Itmg.Compliance.Domain;
using Qec.Itmg.Compliance.Persistence;
using Qec.Itmg.Governance.Domain;
using Qec.Itmg.Governance.Persistence;

namespace Qec.Itmg.Host.Compliance;

public interface IComplianceReadinessSeedRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);
}

public sealed class ComplianceReadinessSeedRunner(
    ComplianceDbContext compliance,
    GovernanceDbContext governance,
    IClock clock,
    ILogger<ComplianceReadinessSeedRunner> logger) : IComplianceReadinessSeedRunner
{
    /// <summary>Stable non-empty actor id for idempotent seed writes.</summary>
    private static readonly Guid SeedActorId = Guid.Parse("01990000-0000-7000-8000-0000000000c1");

    private sealed record QuestionSeed(string Code, string TitleEn, string TitleAr);
    private sealed record DomainSeed(string Code, string TitleEn, string TitleAr, QuestionSeed[] Questions);
    private sealed record LinkSeed(string RequirementCode, string Route, string TitleEn, string TitleAr, OperationalLinkType Type);
    private sealed record MappingSeed(string RequirementCode, string ControlNumber);

    private static readonly DomainSeed[] IsaDomains =
    [
        new("ISA-DOM-IT", "IT Environment & Governance", "بيئة تقنية المعلومات والحوكمة",
        [
            new("ISA-IT-01", "Has QEC identified the major IT systems supporting business and financial-reporting processes?", "هل حددت شركة جودة التعليم أنظمة تقنية المعلومات الرئيسية الداعمة للعمليات التجارية والتقارير المالية؟"),
            new("ISA-IT-02", "Are system owners and IT responsibilities identified?", "هل تم تحديد مالكي الأنظمة ومسؤوليات تقنية المعلومات؟"),
            new("ISA-IT-03", "Are relevant IT policies approved, published and periodically reviewed?", "هل تمت الموافقة على سياسات تقنية المعلومات ذات الصلة ونشرها ومراجعتها دورياً؟"),
            new("ISA-IT-04", "Are significant IT risks recorded and assigned to owners?", "هل تُسجَّل مخاطر تقنية المعلومات الجوهرية وتُسند إلى مالكين؟")
        ]),
        new("ISA-DOM-APP", "Financial-Reporting-Relevant Applications", "التطبيقات ذات الصلة بالتقارير المالية",
        [
            new("ISA-APP-01", "Are applications relevant to financial reporting identified?", "هل تم تحديد التطبيقات ذات الصلة بالتقارير المالية؟"),
            new("ISA-APP-02", "Are application owners and support responsibilities identified?", "هل تم تحديد مالكي التطبيقات ومسؤوليات الدعم؟"),
            new("ISA-APP-03", "Are significant application dependencies documented?", "هل وُثقت الاعتماديات الجوهرية بين التطبيقات؟"),
            new("ISA-APP-04", "Are key application interfaces and integrations known?", "هل تُعرف واجهات التطبيقات والتكاملات الرئيسية؟")
        ]),
        new("ISA-DOM-INF", "Infrastructure & Technology", "البنية التحتية والتقنية",
        [
            new("ISA-INF-01", "Are critical servers, databases, network components and technology dependencies identified?", "هل تم تحديد الخوادم وقواعد البيانات ومكونات الشبكة والاعتماديات التقنية الحرجة؟"),
            new("ISA-INF-02", "Are operational responsibilities for critical infrastructure defined?", "هل حُددت المسؤوليات التشغيلية للبنية التحتية الحرجة؟"),
            new("ISA-INF-03", "Are infrastructure configurations and changes controlled?", "هل تُدار تكوينات البنية التحتية وتغييراتها بشكل منضبط؟")
        ]),
        new("ISA-DOM-DATA", "Interfaces & Data Flows", "الواجهات وتدفقات البيانات",
        [
            new("ISA-DATA-01", "Are significant system-to-system interfaces identified?", "هل تم تحديد الواجهات الجوهرية بين الأنظمة؟"),
            new("ISA-DATA-02", "Are important automated/manual data transfers documented?", "هل وُثقت عمليات نقل البيانات الآلية/اليدوية المهمة؟"),
            new("ISA-DATA-03", "Are failed or incomplete interfaces monitored and investigated where relevant?", "هل تُراقب الواجهات الفاشلة أو غير المكتملة وتُحقق فيها عند الاقتضاء؟")
        ]),
        new("ISA-DOM-IAM", "Logical Access Management", "إدارة الوصول المنطقي",
        [
            new("ISA-IAM-01", "Is user access formally requested before being granted?", "هل يُطلب الوصول للمستخدمين رسمياً قبل منحه؟"),
            new("ISA-IAM-02", "Is access approved by an authorized person before fulfillment?", "هل يُعتمد الوصول من شخص مخوّل قبل التنفيذ؟"),
            new("ISA-IAM-03", "Is actual access fulfillment recorded with the responsible person and date?", "هل يُسجَّل تنفيذ الوصول الفعلي مع المسؤول والتاريخ؟"),
            new("ISA-IAM-04", "Can QEC demonstrate the complete request-to-approval-to-fulfillment history?", "هل تستطيع شركة جودة التعليم إظهار سجل الطلب والاعتماد والتنفيذ بالكامل؟")
        ]),
        new("ISA-DOM-JML", "Joiner / Mover / Leaver", "الانضمام / النقل / المغادرة",
        [
            new("ISA-JML-01", "Is access for new employees formally authorized?", "هل يُفوَّض وصول الموظفين الجدد رسمياً؟"),
            new("ISA-JML-02", "Are access rights reconsidered when an employee changes role or department?", "هل تُعاد دراسة صلاحيات الوصول عند تغيير دور الموظف أو إدارته؟"),
            new("ISA-JML-03", "Is access disabled or removed promptly for leavers?", "هل يُعطَّل أو يُزال الوصول بسرعة للمغادرين؟"),
            new("ISA-JML-04", "Are mandatory offboarding access tasks tracked to completion or documented exception?", "هل تُتابع مهام إنهاء الوصول الإلزامية حتى الإكمال أو توثيق الاستثناء؟")
        ]),
        new("ISA-DOM-PRIV", "Privileged Access", "الوصول المميز",
        [
            new("ISA-PRIV-01", "Are privileged/admin accounts identified?", "هل تم تحديد حسابات الصلاحيات المميزة/الإدارية؟"),
            new("ISA-PRIV-02", "Is privileged access separately authorized?", "هل يُفوَّض الوصول المميز بشكل منفصل؟"),
            new("ISA-PRIV-03", "Are privileged accounts assigned to accountable owners?", "هل تُسند الحسابات المميزة إلى مالكين مسؤولين؟"),
            new("ISA-PRIV-04", "Is privileged access periodically reviewed?", "هل يُراجع الوصول المميز دورياً؟")
        ]),
        new("ISA-DOM-REV", "Access Reviews & Segregation of Duties", "مراجعات الوصول وفصل المهام",
        [
            new("ISA-REV-01", "Are user access reviews performed periodically for relevant systems?", "هل تُجرى مراجعات وصول المستخدمين دورياً للأنظمة ذات الصلة؟"),
            new("ISA-REV-02", "Are review decisions retained as evidence?", "هل تُحفظ قرارات المراجعة كأدلة؟"),
            new("ISA-SOD-01", "Are incompatible access combinations identified for relevant systems?", "هل تم تحديد تركيبات الوصول غير المتوافقة للأنظمة ذات الصلة؟"),
            new("ISA-SOD-02", "Are SoD exceptions explicitly approved and documented?", "هل تُعتمد استثناءات فصل المهام صراحةً وتُوثق؟")
        ]),
        new("ISA-DOM-CHG", "Change Management", "إدارة التغيير",
        [
            new("ISA-CHG-01", "Are production changes formally requested and recorded?", "هل تُطلب تغييرات الإنتاج رسمياً وتُسجَّل؟"),
            new("ISA-CHG-02", "Are changes assessed and authorized before implementation?", "هل تُقيَّم التغييرات وتُفوَّض قبل التنفيذ؟"),
            new("ISA-CHG-03", "Are testing/validation results retained where appropriate?", "هل تُحفظ نتائج الاختبار/التحقق عند الاقتضاء؟"),
            new("ISA-CHG-04", "Are emergency changes identified and subject to retrospective review?", "هل تُميَّز التغييرات الطارئة وتخضع لمراجعة لاحقة؟"),
            new("ISA-CHG-05", "Can implemented changes be traced to requester, approver and implementer?", "هل يمكن تتبع التغييرات المنفذة إلى الطالب والمعتمد والمنفذ؟")
        ]),
        new("ISA-DOM-OPS", "IT Operations", "عمليات تقنية المعلومات",
        [
            new("ISA-OPS-01", "Are critical operational jobs/processes identified?", "هل تم تحديد الوظائف/العمليات التشغيلية الحرجة؟"),
            new("ISA-OPS-02", "Are operational failures/events monitored and acted upon?", "هل تُراقب الإخفاقات/الأحداث التشغيلية ويُتخذ إجراء بشأنها؟"),
            new("ISA-OPS-03", "Are recurring operational activities assigned and tracked?", "هل تُسند الأنشطة التشغيلية المتكررة وتُتابع؟")
        ]),
        new("ISA-DOM-BKP", "Backup & Recovery", "النسخ الاحتياطي والاستعادة",
        [
            new("ISA-BKP-01", "Are critical systems/data included in defined backup processes?", "هل تُدرج الأنظمة/البيانات الحرجة ضمن عمليات النسخ الاحتياطي المحددة؟"),
            new("ISA-BKP-02", "Are backup failures detected and investigated?", "هل تُكتشف إخفاقات النسخ الاحتياطي وتُحقق فيها؟"),
            new("ISA-BKP-03", "Are restore/recovery tests performed periodically?", "هل تُجرى اختبارات الاستعادة/التعافي دورياً؟"),
            new("ISA-BKP-04", "Is evidence of backup and recovery testing retained?", "هل تُحفظ أدلة اختبار النسخ الاحتياطي والاستعادة؟")
        ]),
        new("ISA-DOM-LOG", "Logging & Monitoring", "التسجيل والمراقبة",
        [
            new("ISA-LOG-01", "Are important system/security events logged?", "هل تُسجَّل أحداث النظام/الأمن المهمة؟"),
            new("ISA-LOG-02", "Are critical logs monitored or reviewed?", "هل تُراقب السجلات الحرجة أو تُراجع؟"),
            new("ISA-LOG-03", "Are significant events traceable to incident/event records?", "هل يمكن تتبع الأحداث الجوهرية إلى سجلات الحوادث/الأحداث؟")
        ]),
        new("ISA-DOM-INC", "Incident & Problem Management", "إدارة الحوادث والمشكلات",
        [
            new("ISA-INC-01", "Are IT incidents formally recorded and tracked?", "هل تُسجَّل حوادث تقنية المعلومات رسمياً وتُتابع؟"),
            new("ISA-INC-02", "Are significant incidents escalated appropriately?", "هل تُصعَّد الحوادث الجوهرية بشكل مناسب؟"),
            new("ISA-INC-03", "Are recurring/significant problems subject to root-cause analysis where appropriate?", "هل تخضع المشكلات المتكررة/الجوهرية لتحليل السبب الجذري عند الاقتضاء؟"),
            new("ISA-INC-04", "Are corrective actions tracked?", "هل تُتابع الإجراءات التصحيحية؟")
        ]),
        new("ISA-DOM-SDLC", "System Development / Acquisition", "تطوير الأنظمة / الاستحواذ",
        [
            new("ISA-SDLC-01", "Are significant new systems or major application changes formally authorized?", "هل تُفوَّض الأنظمة الجديدة الجوهرية أو تغييرات التطبيقات الكبرى رسمياً؟"),
            new("ISA-SDLC-02", "Are requirements/testing/acceptance documented for significant implementations?", "هل تُوثق المتطلبات/الاختبار/القبول للتنفيذات الجوهرية؟"),
            new("ISA-SDLC-03", "Is production deployment controlled?", "هل يُدار نشر الإنتاج بشكل منضبط؟"),
            new("ISA-SDLC-04", "Are development and production responsibilities appropriately controlled where applicable?", "هل تُدار مسؤوليات التطوير والإنتاج بشكل مناسب عند الاقتضاء؟")
        ]),
        new("ISA-DOM-CYB", "Information Security / Cyber Risks", "أمن المعلومات / المخاطر السيبرانية",
        [
            new("ISA-CYB-01", "Are cybersecurity incidents that could affect important systems identified and escalated?", "هل تُحدَّد وتصعَّد الحوادث السيبرانية التي قد تؤثر على الأنظمة المهمة؟"),
            new("ISA-CYB-02", "Are vulnerability/security risks for important systems tracked?", "هل تُتابع مخاطر الثغرات/الأمن للأنظمة المهمة؟"),
            new("ISA-CYB-03", "Are security responsibilities documented?", "هل وُثقت مسؤوليات الأمن؟")
        ]),
        new("ISA-DOM-TP", "Third-Party / Service Providers", "الأطراف الثالثة / مزودو الخدمات",
        [
            new("ISA-TP-01", "Are critical IT vendors/service providers identified?", "هل تم تحديد مزودي خدمات/موردي تقنية المعلومات الحرجين؟"),
            new("ISA-TP-02", "Are responsibilities and service dependencies documented?", "هل وُثقت المسؤوليات واعتماديات الخدمة؟"),
            new("ISA-TP-03", "Are significant vendor risks/issues reviewed?", "هل تُراجع مخاطر/قضايا الموردين الجوهرية؟")
        ]),
        new("ISA-DOM-BC", "Business Continuity & Disaster Recovery", "استمرارية الأعمال والتعافي من الكوارث",
        [
            new("ISA-BC-01", "Are critical IT services covered by continuity/recovery plans?", "هل تغطي خطط الاستمرارية/التعافي خدمات تقنية المعلومات الحرجة؟"),
            new("ISA-BC-02", "Are recovery objectives defined for critical services?", "هل حُددت أهداف التعافي للخدمات الحرجة؟"),
            new("ISA-BC-03", "Are disaster recovery exercises/tests documented?", "هل وُثقت تمارين/اختبارات التعافي من الكوارث؟")
        ]),
        new("ISA-DOM-EVD", "Evidence / Audit Trail", "الأدلة / مسار التدقيق",
        [
            new("ISA-EVD-01", "Can QEC retrieve evidence supporting operation of key IT controls?", "هل تستطيع شركة جودة التعليم استرجاع أدلة تدعم تشغيل ضوابط تقنية المعلومات الرئيسية؟"),
            new("ISA-EVD-02", "Is evidence linked to the responsible control/process?", "هل تُربط الأدلة بالضابط/العملية المسؤولة؟"),
            new("ISA-EVD-03", "Are audit findings and corrective actions traceable?", "هل يمكن تتبع نتائج التدقيق والإجراءات التصحيحية؟")
        ])
    ];

    private static readonly DomainSeed[] CyberDomains =
    [
        new("CYB-DOM-GOV", "Governance & Security Policies", "الحوكمة وسياسات الأمن",
        [
            new("CYB-GOV-01", "Are cybersecurity responsibilities assigned?", "هل أُسندت مسؤوليات الأمن السيبراني؟"),
            new("CYB-GOV-02", "Are information-security policies approved and published?", "هل تمت الموافقة على سياسات أمن المعلومات ونشرها؟"),
            new("CYB-GOV-03", "Are security policies reviewed periodically?", "هل تُراجع سياسات الأمن دورياً؟"),
            new("CYB-GOV-04", "Are cybersecurity risks recorded and tracked?", "هل تُسجَّل مخاطر الأمن السيبراني وتُتابع؟")
        ]),
        new("CYB-DOM-AST", "Asset / System Identification", "تحديد الأصول / الأنظمة",
        [
            new("CYB-AST-01", "Are important systems and operational CIs identified?", "هل تم تحديد الأنظمة المهمة وعناصر التكوين التشغيلية؟"),
            new("CYB-AST-02", "Are system owners identified?", "هل تم تحديد مالكي الأنظمة؟"),
            new("CYB-AST-03", "Can systems be linked to business services/dependencies?", "هل يمكن ربط الأنظمة بخدمات الأعمال/الاعتماديات؟")
        ]),
        new("CYB-DOM-IAM", "Identity & Access Management", "إدارة الهوية والوصول",
        [
            new("CYB-IAM-01", "Is access formally requested and approved?", "هل يُطلب الوصول ويُعتمد رسمياً؟"),
            new("CYB-IAM-02", "Are Joiner/Mover/Leaver processes controlled?", "هل تُدار عمليات الانضمام/النقل/المغادرة بشكل منضبط؟"),
            new("CYB-IAM-03", "Are inactive/leaver accounts removed or disabled?", "هل تُزال أو تُعطَّل حسابات غير النشطين/المغادرين؟"),
            new("CYB-IAM-04", "Are user access rights reviewed periodically?", "هل تُراجع صلاحيات وصول المستخدمين دورياً؟")
        ]),
        new("CYB-DOM-PRIV", "Privileged Access", "الوصول المميز",
        [
            new("CYB-PRIV-01", "Are privileged accounts identified?", "هل تم تحديد الحسابات المميزة؟"),
            new("CYB-PRIV-02", "Is privileged access separately controlled?", "هل يُدار الوصول المميز بشكل منفصل؟"),
            new("CYB-PRIV-03", "Is privileged access periodically reviewed?", "هل يُراجع الوصول المميز دورياً؟")
        ]),
        new("CYB-DOM-AUTH", "Authentication / MFA", "المصادقة / المصادقة متعددة العوامل",
        [
            new("CYB-AUTH-01", "Is strong authentication required for critical systems?", "هل تُطلب مصادقة قوية للأنظمة الحرجة؟"),
            new("CYB-AUTH-02", "Is MFA enabled where required?", "هل تُفعَّل المصادقة متعددة العوامل حيث يُطلب ذلك؟"),
            new("CYB-AUTH-03", "Are authentication exceptions documented?", "هل وُثقت استثناءات المصادقة؟")
        ]),
        new("CYB-DOM-END", "Endpoint Security", "أمن الأجهزة الطرفية",
        [
            new("CYB-END-01", "Are managed endpoints protected by approved security controls?", "هل تُحمى الأجهزة الطرفية المُدارة بضوابط أمنية معتمدة؟"),
            new("CYB-END-02", "Are endpoint protection failures monitored?", "هل تُراقب إخفاقات حماية الأجهزة الطرفية؟"),
            new("CYB-END-03", "Are unsupported/end-of-life endpoints identified?", "هل تم تحديد الأجهزة الطرفية غير المدعومة/منتهية الدعم؟")
        ]),
        new("CYB-DOM-NET", "Network Security", "أمن الشبكات",
        [
            new("CYB-NET-01", "Are important networks segmented where appropriate?", "هل تُقسَّم الشبكات المهمة عند الاقتضاء؟"),
            new("CYB-NET-02", "Are firewall/security-device configurations controlled?", "هل تُدار تكوينات الجدران النارية/أجهزة الأمن؟"),
            new("CYB-NET-03", "Is guest/untrusted network access separated from business systems where appropriate?", "هل يُفصل وصول الشبكة للضيوف/غير الموثوق عن أنظمة الأعمال عند الاقتضاء؟"),
            new("CYB-NET-04", "Are critical network changes controlled?", "هل تُدار تغييرات الشبكة الحرجة؟")
        ]),
        new("CYB-DOM-CFG", "Secure Configuration", "التكوين الآمن",
        [
            new("CYB-CFG-01", "Are secure baseline configurations defined where appropriate?", "هل حُددت تكوينات أساسية آمنة عند الاقتضاء؟"),
            new("CYB-CFG-02", "Are unauthorized configuration changes detectable/reviewable?", "هل يمكن اكتشاف/مراجعة تغييرات التكوين غير المصرح بها؟")
        ]),
        new("CYB-DOM-VUL", "Vulnerability & Patch Management", "إدارة الثغرات والتحديثات الأمنية",
        [
            new("CYB-VUL-01", "Are vulnerabilities identified?", "هل تُحدَّد الثغرات الأمنية؟"),
            new("CYB-VUL-02", "Are security patches tracked?", "هل تُتابع التحديثات الأمنية؟"),
            new("CYB-VUL-03", "Are high-risk vulnerabilities assigned for remediation?", "هل تُسند الثغرات عالية المخاطر للمعالجة؟"),
            new("CYB-VUL-04", "Are remediation exceptions documented?", "هل وُثقت استثناءات المعالجة؟")
        ]),
        new("CYB-DOM-DATA", "Data Protection", "حماية البيانات",
        [
            new("CYB-DATA-01", "Is sensitive information identified/classified where appropriate?", "هل تُحدَّد/تُصنَّف المعلومات الحساسة عند الاقتضاء؟"),
            new("CYB-DATA-02", "Are access restrictions applied to sensitive data?", "هل تُطبَّق قيود الوصول على البيانات الحساسة؟"),
            new("CYB-DATA-03", "Are data protection/retention requirements documented?", "هل وُثقت متطلبات حماية/الاحتفاظ بالبيانات؟")
        ]),
        new("CYB-DOM-EMAIL", "Email / Collaboration Security", "أمن البريد / التعاون",
        [
            new("CYB-EMAIL-01", "Are organizational email/collaboration platforms protected?", "هل تُحمى منصات البريد/التعاون المؤسسية؟"),
            new("CYB-EMAIL-02", "Are suspicious email/security events investigated?", "هل تُحقق أحداث البريد/الأمن المشبوهة؟")
        ]),
        new("CYB-DOM-LOG", "Logging & Monitoring", "التسجيل والمراقبة",
        [
            new("CYB-LOG-01", "Are security-relevant events logged?", "هل تُسجَّل الأحداث ذات الصلة بالأمن؟"),
            new("CYB-LOG-02", "Are critical events reviewed/monitored?", "هل تُراجع/تُراقب الأحداث الحرجة؟"),
            new("CYB-LOG-03", "Are logs retained for an appropriate period?", "هل تُحفظ السجلات لفترة مناسبة؟")
        ]),
        new("CYB-DOM-IR", "Incident Response", "الاستجابة للحوادث",
        [
            new("CYB-IR-01", "Is there a documented security incident process?", "هل توجد عملية موثقة لحوادث الأمن؟"),
            new("CYB-IR-02", "Can security incidents be reported and tracked?", "هل يمكن الإبلاغ عن حوادث الأمن ومتابعتها؟"),
            new("CYB-IR-03", "Are significant incidents escalated?", "هل تُصعَّد الحوادث الجوهرية؟"),
            new("CYB-IR-04", "Are lessons/corrective actions captured?", "هل تُسجَّل الدروس/الإجراءات التصحيحية؟")
        ]),
        new("CYB-DOM-BKP", "Backup & Recovery", "النسخ الاحتياطي والاستعادة",
        [
            new("CYB-BKP-01", "Are critical systems backed up?", "هل تُنسخ الأنظمة الحرجة احتياطياً؟"),
            new("CYB-BKP-02", "Are backup failures monitored?", "هل تُراقب إخفاقات النسخ الاحتياطي؟"),
            new("CYB-BKP-03", "Are restores tested?", "هل تُختبر عمليات الاستعادة؟")
        ]),
        new("CYB-DOM-BC", "Business Continuity / DR", "استمرارية الأعمال / التعافي من الكوارث",
        [
            new("CYB-BC-01", "Are critical services included in BC/DR planning?", "هل تُدرج الخدمات الحرجة في تخطيط الاستمرارية/التعافي؟"),
            new("CYB-BC-02", "Are recovery procedures tested?", "هل تُختبر إجراءات التعافي؟")
        ]),
        new("CYB-DOM-APP", "Application / Change Security", "أمن التطبيقات / التغيير",
        [
            new("CYB-APP-01", "Are production changes authorized?", "هل تُفوَّض تغييرات الإنتاج؟"),
            new("CYB-APP-02", "Are significant changes tested?", "هل تُختبر التغييرات الجوهرية؟"),
            new("CYB-APP-03", "Are security-impacting changes traceable?", "هل يمكن تتبع التغييرات ذات الأثر الأمني؟")
        ]),
        new("CYB-DOM-TP", "Vendor / Third-Party Security", "أمن الموردين / الأطراف الثالثة",
        [
            new("CYB-TP-01", "Are critical technology vendors identified?", "هل تم تحديد موردي التقنية الحرجين؟"),
            new("CYB-TP-02", "Are vendor security risks considered?", "هل تُؤخذ مخاطر أمن الموردين في الاعتبار؟"),
            new("CYB-TP-03", "Are vendor issues/findings tracked?", "هل تُتابع قضايا/نتائج الموردين؟")
        ]),
        new("CYB-DOM-PHY", "Physical Security", "الأمن المادي",
        [
            new("CYB-PHY-01", "Is access to critical IT rooms/equipment controlled?", "هل يُدار الوصول إلى غرف/معدات تقنية المعلومات الحرجة؟"),
            new("CYB-PHY-02", "Are physical access permissions reviewed where appropriate?", "هل تُراجع صلاحيات الوصول المادي عند الاقتضاء؟")
        ]),
        new("CYB-DOM-AWR", "Security Awareness", "التوعية الأمنية",
        [
            new("CYB-AWR-01", "Are users informed of acceptable-use/security responsibilities?", "هل يُبلَّغ المستخدمون بمسؤوليات الاستخدام المقبول/الأمن؟"),
            new("CYB-AWR-02", "Are required security policies acknowledged?", "هل يُقرّ المستخدمون بالسياسات الأمنية المطلوبة؟")
        ]),
        new("CYB-DOM-EVD", "Evidence & Review", "الأدلة والمراجعة",
        [
            new("CYB-EVD-01", "Is control evidence retained?", "هل تُحفظ أدلة الضوابط؟"),
            new("CYB-EVD-02", "Are periodic reviews/assessments documented?", "هل وُثقت المراجعات/التقييمات الدورية؟"),
            new("CYB-EVD-03", "Are findings and corrective actions tracked?", "هل تُتابع النتائج والإجراءات التصحيحية؟")
        ])
    ];

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = clock.UtcNow;
        Dictionary<string, Guid> controls = await EnsureIamControlsAsync(now, cancellationToken);

        await EnsureProfileAsync(
            "ISA315-IT-READINESS",
            "ISA 315 IT Audit Readiness",
            "جاهزية تدقيق تقنية المعلومات وفق منظور ISA 315",
            "QEC Internal Audit Readiness Profile",
            "This is a QEC internal IT audit-readiness profile oriented to ISA 315 concepts. It is not the official text of ISA 315 and does not replace an external auditor's professional judgment.",
            "هذا ملف جاهزية داخلي لتدقيق تقنية المعلومات لدى شركة جودة التعليم موجّه بمفاهيم ISA 315. وهو ليس النص الرسمي لمعيار ISA 315 ولا يحل محل الحكم المهني للمدقق الخارجي.",
            FrameworkProfileType.AuditReadiness,
            IsaDomains,
            now,
            cancellationToken);

        await EnsureProfileAsync(
            "QEC-CYBER-READINESS",
            "QEC Cybersecurity Readiness",
            "جاهزية الأمن السيبراني لشركة جودة التعليم",
            "Quality Education Company",
            "Internal cybersecurity readiness checklist for QEC. It is not ISO 27001 certification, NIST certification, or regulatory attestation. Controls may separately map to official frameworks where licensed/appropriate.",
            "قائمة تحقق داخلية لجاهزية الأمن السيبراني لدى شركة جودة التعليم. وهي ليست شهادة ISO 27001 أو NIST أو إشهاداً تنظيمياً. يمكن ربط الضوابط لاحقاً بأطر رسمية مرخّصة عند الاقتضاء.",
            FrameworkProfileType.CybersecurityReadiness,
            CyberDomains,
            now,
            cancellationToken);

        await EnsureOperationalLinksAsync(IsaLinks, cancellationToken);
        await EnsureOperationalLinksAsync(CyberLinks, cancellationToken);
        await EnsureControlMappingsAsync(IsaMappings.Concat(CyberMappings), controls, now, cancellationToken);

        logger.LogInformation("Compliance readiness seed completed for ISA315-IT-READINESS and QEC-CYBER-READINESS.");
    }

    private async Task EnsureProfileAsync(
        string code, string nameEn, string nameAr, string publisher,
        string descEn, string descAr, FrameworkProfileType profileType,
        DomainSeed[] domains, DateTimeOffset now, CancellationToken ct)
    {
        Framework? framework = await compliance.Frameworks.FirstOrDefaultAsync(x => x.Code == code, ct);
        if (framework is null)
        {
            framework = Framework.Create(code, nameEn, publisher, now, descEn, profileType);
            compliance.Frameworks.Add(framework);
            await compliance.SaveChangesAsync(ct);
        }
        else if (framework.ProfileType != profileType)
        {
            framework.SetProfileType(profileType, now);
            await compliance.SaveChangesAsync(ct);
        }

        await EnsureFrameworkTranslationAsync(framework.Id, "en", nameEn, descEn, ct);
        await EnsureFrameworkTranslationAsync(framework.Id, "ar", nameAr, descAr, ct);

        FrameworkVersion? version = await compliance.FrameworkVersions
            .FirstOrDefaultAsync(x => x.FrameworkId == framework.Id && x.VersionCode == "2026.1", ct);
        if (version is null)
        {
            List<FrameworkVersion> currents = await compliance.FrameworkVersions
                .Where(x => x.FrameworkId == framework.Id && x.IsCurrent).ToListAsync(ct);
            foreach (FrameworkVersion c in currents) c.SetCurrent(false);
            version = FrameworkVersion.Create(framework.Id, "2026.1", now, nameEn, isCurrent: true);
            compliance.FrameworkVersions.Add(version);
            await compliance.SaveChangesAsync(ct);
        }
        else if (!version.IsCurrent)
        {
            List<FrameworkVersion> currents = await compliance.FrameworkVersions
                .Where(x => x.FrameworkId == framework.Id && x.IsCurrent).ToListAsync(ct);
            foreach (FrameworkVersion c in currents) c.SetCurrent(false);
            version.SetCurrent(true);
            await compliance.SaveChangesAsync(ct);
        }

        int sort = 0;
        foreach (DomainSeed domain in domains)
        {
            sort++;
            FrameworkRequirement domainReq = await EnsureRequirementAsync(
                version.Id, domain.Code, domain.TitleEn, FrameworkRequirementType.Domain, null, null, sort, ct);
            await EnsureRequirementTranslationAsync(domainReq.Id, "en", domain.TitleEn, null, ct);
            await EnsureRequirementTranslationAsync(domainReq.Id, "ar", domain.TitleAr, null, ct);

            int qSort = 0;
            foreach (QuestionSeed q in domain.Questions)
            {
                qSort++;
                FrameworkRequirement leaf = await EnsureRequirementAsync(
                    version.Id, q.Code, q.TitleEn, FrameworkRequirementType.Question, domainReq.Id, q.TitleEn, qSort, ct);
                await EnsureRequirementTranslationAsync(leaf.Id, "en", q.TitleEn, q.TitleEn, ct);
                await EnsureRequirementTranslationAsync(leaf.Id, "ar", q.TitleAr, q.TitleAr, ct);
            }
        }
    }

    private async Task EnsureFrameworkTranslationAsync(
        Guid frameworkId, string lang, string name, string? description, CancellationToken ct)
    {
        FrameworkTranslation? existing = await compliance.FrameworkTranslations
            .FirstOrDefaultAsync(x => x.FrameworkId == frameworkId && x.LanguageCode == lang, ct);
        if (existing is null)
        {
            compliance.FrameworkTranslations.Add(FrameworkTranslation.Create(frameworkId, lang, name, description));
            await compliance.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureRequirementTranslationAsync(
        Guid requirementId, string lang, string title, string? text, CancellationToken ct)
    {
        FrameworkRequirementTranslation? existing = await compliance.FrameworkRequirementTranslations
            .FirstOrDefaultAsync(x => x.FrameworkRequirementId == requirementId && x.LanguageCode == lang, ct);
        if (existing is null)
        {
            compliance.FrameworkRequirementTranslations.Add(
                FrameworkRequirementTranslation.Create(requirementId, lang, title, text));
            await compliance.SaveChangesAsync(ct);
        }
    }

    private async Task<FrameworkRequirement> EnsureRequirementAsync(
        Guid versionId, string code, string title, FrameworkRequirementType type,
        Guid? parentId, string? text, int sortOrder, CancellationToken ct)
    {
        FrameworkRequirement? existing = await compliance.FrameworkRequirements
            .FirstOrDefaultAsync(x => x.FrameworkVersionId == versionId && x.Code == code, ct);
        if (existing is not null) return existing;

        FrameworkRequirement entity = FrameworkRequirement.Create(
            versionId, code, title, type, parentId, text, sortOrder);
        compliance.FrameworkRequirements.Add(entity);
        await compliance.SaveChangesAsync(ct);
        return entity;
    }

    private async Task<Dictionary<string, Guid>> EnsureIamControlsAsync(DateTimeOffset now, CancellationToken ct)
    {
        (string Number, string Title, string Objective)[] catalog =
        [
            ("CTRL-IAM-001", "User Access Authorization",
                "Ensure user access is formally requested, approved, and fulfilled before grant."),
            ("CTRL-IAM-002", "Joiner Mover Leaver Access Lifecycle",
                "Ensure joiner, mover, and leaver access changes are authorized and completed."),
            ("CTRL-IAM-003", "Privileged Access Management",
                "Ensure privileged accounts are identified, authorized, owned, and reviewed."),
            ("CTRL-IAM-004", "Periodic Access Review",
                "Ensure periodic user and privileged access reviews are performed and retained."),
            ("CTRL-IAM-005", "Segregation of Duties",
                "Ensure incompatible access combinations are identified and SoD exceptions documented."),
        ];

        Dictionary<string, Guid> result = [];
        foreach ((string number, string title, string objective) in catalog)
        {
            InternalControl? existing = await governance.InternalControls
                .FirstOrDefaultAsync(x => x.ControlNumber == number, ct);
            if (existing is null)
            {
                existing = InternalControl.Create(
                    number, title, objective, objective, ControlDomainCodes.AccessManagement,
                    ControlFrequency.Quarterly, ControlAutomationType.ItmgNative, now);
                existing.Activate(now);
                governance.InternalControls.Add(existing);
                await governance.SaveChangesAsync(ct);
            }
            else if (existing.Status == ControlStatus.Draft)
            {
                existing.Activate(now);
                await governance.SaveChangesAsync(ct);
            }

            result[number] = existing.Id;
        }

        return result;
    }

    private async Task EnsureOperationalLinksAsync(LinkSeed[] links, CancellationToken ct)
    {
        foreach (LinkSeed link in links)
        {
            FrameworkRequirement? req = await compliance.FrameworkRequirements.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == link.RequirementCode, ct);
            if (req is null) continue;

            bool exists = await compliance.FrameworkRequirementOperationalLinks.AnyAsync(
                x => x.FrameworkRequirementId == req.Id && x.InternalRoute == link.Route, ct);
            if (exists) continue;

            compliance.FrameworkRequirementOperationalLinks.Add(
                FrameworkRequirementOperationalLink.Create(
                    req.Id, link.Type, link.TitleEn, link.TitleAr, link.Route, SeedActorId, clock.UtcNow));
            await compliance.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureControlMappingsAsync(
        IEnumerable<MappingSeed> mappings, Dictionary<string, Guid> controls, DateTimeOffset now, CancellationToken ct)
    {
        foreach (MappingSeed map in mappings)
        {
            if (!controls.TryGetValue(map.ControlNumber, out Guid controlId)) continue;
            FrameworkRequirement? req = await compliance.FrameworkRequirements.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == map.RequirementCode, ct);
            if (req is null) continue;

            bool exists = await compliance.ControlMappings.AnyAsync(
                x => x.InternalControlId == controlId && x.FrameworkRequirementId == req.Id, ct);
            if (exists) continue;

            compliance.ControlMappings.Add(ControlMapping.Create(
                controlId, req.Id, MappingRelationship.Primary, SeedActorId, now));
            await compliance.SaveChangesAsync(ct);
        }
    }

    private static readonly LinkSeed[] IsaLinks =
    [
        new("ISA-IAM-01", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("ISA-IAM-02", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("ISA-IAM-03", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("ISA-IAM-04", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("ISA-JML-01", "/it/access", "Joiner / Mover / Leaver", "الانضمام / النقل / المغادرة", OperationalLinkType.Feature),
        new("ISA-JML-02", "/it/access", "Joiner / Mover / Leaver", "الانضمام / النقل / المغادرة", OperationalLinkType.Feature),
        new("ISA-JML-03", "/it/access", "Joiner / Mover / Leaver", "الانضمام / النقل / المغادرة", OperationalLinkType.Feature),
        new("ISA-JML-04", "/it/access", "Joiner / Mover / Leaver", "الانضمام / النقل / المغادرة", OperationalLinkType.Feature),
        new("ISA-PRIV-01", "/it/access/accounts", "Privileged Accounts", "الحسابات المميزة", OperationalLinkType.Feature),
        new("ISA-PRIV-02", "/it/access/accounts", "Privileged Accounts", "الحسابات المميزة", OperationalLinkType.Feature),
        new("ISA-PRIV-03", "/it/access/accounts", "Privileged Accounts", "الحسابات المميزة", OperationalLinkType.Feature),
        new("ISA-PRIV-04", "/it/access/reviews", "Access Reviews", "مراجعات الوصول", OperationalLinkType.Feature),
        new("ISA-REV-01", "/it/access/reviews", "Access Reviews", "مراجعات الوصول", OperationalLinkType.Feature),
        new("ISA-REV-02", "/it/access/reviews", "Access Reviews", "مراجعات الوصول", OperationalLinkType.Feature),
        new("ISA-SOD-01", "/it/access/sod", "Segregation of Duties", "فصل المهام", OperationalLinkType.Feature),
        new("ISA-SOD-02", "/it/access/sod", "Segregation of Duties", "فصل المهام", OperationalLinkType.Feature),
        new("ISA-CHG-01", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("ISA-CHG-02", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("ISA-CHG-03", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("ISA-CHG-04", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("ISA-CHG-05", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("ISA-INC-01", "/it/tickets", "Incidents & Tickets", "الحوادث والتذاكر", OperationalLinkType.Module),
        new("ISA-INC-02", "/it/tickets", "Incidents & Tickets", "الحوادث والتذاكر", OperationalLinkType.Module),
        new("ISA-INC-03", "/it/tickets", "Incidents & Tickets", "الحوادث والتذاكر", OperationalLinkType.Module),
        new("ISA-INC-04", "/it/tickets", "Incidents & Tickets", "الحوادث والتذاكر", OperationalLinkType.Module),
        new("ISA-OPS-01", "/it/operations", "IT Operations", "عمليات تقنية المعلومات", OperationalLinkType.Module),
        new("ISA-OPS-02", "/it/operations", "IT Operations", "عمليات تقنية المعلومات", OperationalLinkType.Module),
        new("ISA-OPS-03", "/it/operations", "IT Operations", "عمليات تقنية المعلومات", OperationalLinkType.Module),
        new("ISA-BKP-01", "/it/operations", "Backup & Recovery", "النسخ الاحتياطي والاستعادة", OperationalLinkType.Feature),
        new("ISA-BKP-02", "/it/operations", "Backup & Recovery", "النسخ الاحتياطي والاستعادة", OperationalLinkType.Feature),
        new("ISA-BKP-03", "/it/operations", "Backup & Recovery", "النسخ الاحتياطي والاستعادة", OperationalLinkType.Feature),
        new("ISA-BKP-04", "/it/operations", "Backup & Recovery", "النسخ الاحتياطي والاستعادة", OperationalLinkType.Feature),
        new("ISA-EVD-01", "/it/evidence", "Evidence Library", "مكتبة الأدلة", OperationalLinkType.Module),
        new("ISA-EVD-02", "/it/evidence", "Evidence Library", "مكتبة الأدلة", OperationalLinkType.Module),
        new("ISA-EVD-03", "/it/evidence", "Evidence Library", "مكتبة الأدلة", OperationalLinkType.Module),
        new("ISA-BC-01", "/it/continuity", "Business Continuity", "استمرارية الأعمال", OperationalLinkType.Module),
        new("ISA-BC-02", "/it/continuity", "Business Continuity", "استمرارية الأعمال", OperationalLinkType.Module),
        new("ISA-BC-03", "/it/continuity", "Business Continuity", "استمرارية الأعمال", OperationalLinkType.Module),
        new("ISA-TP-01", "/it/vendors", "Vendor Management", "إدارة الموردين", OperationalLinkType.Module),
        new("ISA-TP-02", "/it/vendors", "Vendor Management", "إدارة الموردين", OperationalLinkType.Module),
        new("ISA-TP-03", "/it/vendors", "Vendor Management", "إدارة الموردين", OperationalLinkType.Module),
        new("ISA-IT-03", "/it/policies", "Policies", "السياسات", OperationalLinkType.Module),
        new("ISA-IT-02", "/it/controls", "Internal Controls", "الضوابط الداخلية", OperationalLinkType.Module),
    ];

    private static readonly LinkSeed[] CyberLinks =
    [
        new("CYB-IAM-01", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("CYB-IAM-02", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("CYB-IAM-03", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("CYB-IAM-04", "/it/access/reviews", "Access Reviews", "مراجعات الوصول", OperationalLinkType.Feature),
        new("CYB-PRIV-01", "/it/access/accounts", "Privileged Accounts", "الحسابات المميزة", OperationalLinkType.Feature),
        new("CYB-PRIV-02", "/it/access/accounts", "Privileged Accounts", "الحسابات المميزة", OperationalLinkType.Feature),
        new("CYB-PRIV-03", "/it/access/reviews", "Access Reviews", "مراجعات الوصول", OperationalLinkType.Feature),
        new("CYB-PHY-01", "/it/access", "Access Management", "إدارة الوصول", OperationalLinkType.Module),
        new("CYB-APP-01", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("CYB-APP-02", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("CYB-APP-03", "/it/changes", "Change Management", "إدارة التغيير", OperationalLinkType.Module),
        new("CYB-IR-02", "/it/tickets", "Incidents & Tickets", "الحوادث والتذاكر", OperationalLinkType.Module),
        new("CYB-IR-03", "/it/tickets", "Incidents & Tickets", "الحوادث والتذاكر", OperationalLinkType.Module),
        new("CYB-BKP-01", "/it/operations", "IT Operations", "عمليات تقنية المعلومات", OperationalLinkType.Module),
        new("CYB-BKP-02", "/it/operations", "IT Operations", "عمليات تقنية المعلومات", OperationalLinkType.Module),
        new("CYB-BKP-03", "/it/operations", "IT Operations", "عمليات تقنية المعلومات", OperationalLinkType.Module),
        new("CYB-BC-01", "/it/continuity", "Business Continuity", "استمرارية الأعمال", OperationalLinkType.Module),
        new("CYB-BC-02", "/it/continuity", "Business Continuity", "استمرارية الأعمال", OperationalLinkType.Module),
        new("CYB-TP-01", "/it/vendors", "Vendor Management", "إدارة الموردين", OperationalLinkType.Module),
        new("CYB-TP-02", "/it/vendors", "Vendor Management", "إدارة الموردين", OperationalLinkType.Module),
        new("CYB-TP-03", "/it/vendors", "Vendor Management", "إدارة الموردين", OperationalLinkType.Module),
        new("CYB-GOV-02", "/it/policies", "Policies", "السياسات", OperationalLinkType.Module),
        new("CYB-GOV-03", "/it/policies", "Policies", "السياسات", OperationalLinkType.Module),
        new("CYB-AWR-02", "/it/policies", "Policies", "السياسات", OperationalLinkType.Module),
        new("CYB-EVD-01", "/it/evidence", "Evidence Library", "مكتبة الأدلة", OperationalLinkType.Module),
        new("CYB-EVD-02", "/it/compliance/assessments", "Compliance Assessments", "تقييمات الامتثال", OperationalLinkType.Feature),
        new("CYB-EVD-03", "/it/controls", "Internal Controls", "الضوابط الداخلية", OperationalLinkType.Module),
        new("CYB-AST-01", "/it/controls", "Internal Controls", "الضوابط الداخلية", OperationalLinkType.Module),
    ];

    private static readonly MappingSeed[] IsaMappings =
    [
        new("ISA-IAM-01", "CTRL-IAM-001"), new("ISA-IAM-02", "CTRL-IAM-001"),
        new("ISA-IAM-03", "CTRL-IAM-001"), new("ISA-IAM-04", "CTRL-IAM-001"),
        new("ISA-JML-01", "CTRL-IAM-002"), new("ISA-JML-02", "CTRL-IAM-002"),
        new("ISA-JML-03", "CTRL-IAM-002"), new("ISA-JML-04", "CTRL-IAM-002"),
        new("ISA-PRIV-01", "CTRL-IAM-003"), new("ISA-PRIV-02", "CTRL-IAM-003"),
        new("ISA-PRIV-03", "CTRL-IAM-003"), new("ISA-PRIV-04", "CTRL-IAM-003"),
        new("ISA-REV-01", "CTRL-IAM-004"), new("ISA-REV-02", "CTRL-IAM-004"),
        new("ISA-SOD-01", "CTRL-IAM-005"), new("ISA-SOD-02", "CTRL-IAM-005"),
    ];

    private static readonly MappingSeed[] CyberMappings =
    [
        new("CYB-IAM-01", "CTRL-IAM-001"),
        new("CYB-IAM-02", "CTRL-IAM-002"), new("CYB-IAM-03", "CTRL-IAM-002"),
        new("CYB-IAM-04", "CTRL-IAM-004"),
        new("CYB-PRIV-01", "CTRL-IAM-003"), new("CYB-PRIV-02", "CTRL-IAM-003"),
        new("CYB-PRIV-03", "CTRL-IAM-003"),
    ];
}
