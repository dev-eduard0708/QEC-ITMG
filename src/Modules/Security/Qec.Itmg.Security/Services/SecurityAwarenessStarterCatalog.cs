using Qec.Itmg.Security.Domain;

namespace Qec.Itmg.Security.Services;

public sealed record StarterOptionSeed(string TextEn, string TextAr, bool IsCorrect);

public sealed record StarterQuestionSeed(
    AwarenessQuestionType Type,
    string QuestionEn,
    string QuestionAr,
    string ExplanationEn,
    string ExplanationAr,
    int Points,
    IReadOnlyList<StarterOptionSeed> Options);

public sealed record StarterBlockSeed(
    string TitleEn,
    string TitleAr,
    string BodyEn,
    string BodyAr,
    int EstimatedMinutes);

public sealed record StarterCampaignSeed(
    string StarterKey,
    string TitleEn,
    string TitleAr,
    string DescriptionEn,
    string DescriptionAr,
    int EstimatedMinutes,
    IReadOnlyList<StarterBlockSeed> Blocks,
    IReadOnlyList<StarterQuestionSeed> Questions);

/// <summary>
/// Static bilingual catalog of Head Office Security Awareness starter campaigns.
/// Seeded as editable Drafts; never overwrites after first insert.
/// </summary>
public static class SecurityAwarenessStarterCatalog
{
    public static IReadOnlyList<StarterCampaignSeed> All { get; } =
    [
        Phishing(),
        Password(),
        Data(),
        Collab(),
        Remote(),
    ];

    private static StarterCampaignSeed Phishing() => new(
        "SEC-AWARE-PHISHING",
        "Phishing & Suspicious Email Awareness",
        "التوعية بالتصيد الاحتيالي ورسائل البريد الإلكتروني المشبوهة",
        "Learn how to identify phishing, suspicious messages, fake login requests, malicious links and attachments, and business email compromise attempts, and how to report them safely.",
        "التعرف على أساليب التصيد الاحتيالي والرسائل المشبوهة وطلبات تسجيل الدخول المزيفة والروابط والمرفقات الضارة ومحاولات انتحال البريد الإلكتروني للأعمال، وكيفية الإبلاغ عنها بطريقة آمنة.",
        8,
        [
            new(
                "What phishing looks like",
                "كيف يبدو التصيد الاحتيالي",
                "Phishing is a social engineering attack that tries to make you reveal information, open something harmful, approve a request, or take an unsafe action.\n\nAttackers often impersonate managers, HR, Finance, IT, suppliers, banks, couriers, or trusted online services. Messages may use urgency, threats, prizes, secrecy, or unexpected requests to push you to act quickly.\n\nProfessional branding, logos, or formal wording do not prove a message is legitimate. Pause and verify before acting on an unexpected or unusual request.\n\nFollow applicable QEC policies and procedures.",
                "التصيد الاحتيالي هجوم هندسة اجتماعية يهدف إلى دفعك للكشف عن معلومات، أو فتح محتوى ضار، أو الموافقة على طلب، أو تنفيذ إجراء غير آمن.\n\nغالباً ما ينتحل المهاجمون صفة المديرين أو الموارد البشرية أو المالية أو تقنية المعلومات أو الموردين أو البنوك أو شركات التوصيل أو الخدمات الموثوقة. وقد تستخدم الرسائل الاستعجال أو التهديد أو الجوائز أو السرية أو الطلبات غير المتوقعة لدفعك للتصرف بسرعة.\n\nالعلامة التجارية الاحترافية أو الشعارات أو الصياغة الرسمية لا تثبت أن الرسالة شرعية. توقّف وتحقق قبل الاستجابة لأي طلب غير متوقع أو غير معتاد.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Check the sender carefully",
                "تحقق من المرسل بعناية",
                "Always inspect the actual sender address, not only the display name. Domains that look similar to a trusted company or service can still be deceptive.\n\nA display name alone is not proof of identity. An unexpected reply-to address is also a warning sign. Even a message that appears to come from someone you know may indicate a compromised account if the request is unusual.\n\nExample: an email appears to come from a manager but asks for an unusual confidential action. Verify sensitive requests using a trusted second channel such as a known phone number or an approved company collaboration tool—not by replying to the suspicious message.\n\nFollow applicable QEC policies and procedures.",
                "افحص دائماً عنوان المرسل الفعلي، وليس اسم العرض فقط. النطاقات التي تشبه شركة أو خدمة موثوقة قد تكون مضللة.\n\nاسم العرض وحده ليس دليلاً على الهوية. وعنوان الرد غير المتوقع علامة تحذير أيضاً. حتى الرسالة التي تبدو من شخص تعرفه قد تشير إلى اختراق الحساب إذا كان الطلب غير معتاد.\n\nمثال: رسالة تبدو من مدير لكنها تطلب إجراءً سرياً غير معتاد. تحقق من الطلبات الحساسة عبر قناة ثانية موثوقة مثل رقم هاتف معروف أو أداة تعاون معتمدة في الشركة—وليس بالرد على الرسالة المشبوهة.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Links, attachments and QR codes",
                "الروابط والمرفقات ورموز الاستجابة السريعة",
                "Do not open unexpected attachments. Where possible, inspect where a link will take you before opening it. Shortened or obfuscated links deserve extra caution.\n\nQR codes can also redirect to malicious sites. Never install unexpected software received by email or chat. If you are unsure, report the message rather than experimenting.\n\nQEC uses Google Workspace for everyday work. Prefer opening company links and files through approved Google Workspace tools when you need to review shared content.\n\nFollow applicable QEC policies and procedures.",
                "لا تفتح المرفقات غير المتوقعة. حيثما أمكن، افحص وجهة الرابط قبل فتحه. الروابط المختصرة أو المبهمة تستحق حذراً إضافياً.\n\nرموز الاستجابة السريعة (QR) قد تعيد التوجيه أيضاً إلى مواقع ضارة. لا تثبّت برامجاً غير متوقعة تصل عبر البريد أو الدردشة. إذا لم تكن متأكداً، أبلغ عن الرسالة بدلاً من التجربة.\n\nتستخدم QEC Google Workspace للعمل اليومي. فضّل فتح روابط وملفات الشركة عبر أدوات Google Workspace المعتمدة عند مراجعة المحتوى المشترك.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Fake login pages and MFA prompts",
                "صفحات تسجيل الدخول المزيفة وطلبات المصادقة متعددة العوامل",
                "Phishing pages may imitate Google sign-in or another trusted login screen. Always verify the domain before entering credentials.\n\nNever provide passwords or one-time verification codes through email or chat. Never approve an MFA prompt you did not initiate. An unexpected MFA request may mean someone knows or is testing your password.\n\nIf you see an unexpected authentication prompt for Google Workspace or another company service, deny it and report it through the approved QEC channel.\n\nFollow applicable QEC policies and procedures.",
                "قد تقلّد صفحات التصيد شاشة تسجيل الدخول إلى Google أو خدمة موثوقة أخرى. تحقق دائماً من النطاق قبل إدخال بيانات الاعتماد.\n\nلا تقدّم كلمات المرور أو رموز التحقق لمرة واحدة عبر البريد أو الدردشة. لا توافق على طلب مصادقة متعددة العوامل لم تبادر إليه. الطلب غير المتوقع قد يعني أن شخصاً يعرف كلمة المرور أو يختبرها.\n\nإذا ظهر طلب مصادقة غير متوقع لـ Google Workspace أو خدمة أخرى للشركة، ارفضه وأبلغ عبر القناة المعتمدة في QEC.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Payment requests and reporting suspicious messages",
                "طلبات الدفع والإبلاغ عن الرسائل المشبوهة",
                "Business email compromise often involves unusual financial or sensitive requests, such as:\n\n- changing supplier bank details\n- urgent payment requests\n- buying gift cards\n- bypassing normal approval\n- requesting confidential payroll or personnel information\n\nVerify financial or sensitive changes through established QEC procedures and a known trusted contact method.\n\nIf a message is suspicious: do not reply, do not click, and do not forward it unnecessarily. Report it to IT / Information Security through the approved QEC channel. If you already clicked a link or entered credentials, report immediately so impact can be reduced.\n\nFollow applicable QEC policies and procedures.",
                "غالباً ما يتضمن انتحال البريد الإلكتروني للأعمال طلبات مالية أو حساسة غير معتادة، مثل:\n\n- تغيير بيانات الحساب البنكي للمورد\n- طلبات دفع عاجلة\n- شراء بطاقات هدايا\n- تجاوز الموافقات المعتادة\n- طلب معلومات سرية عن الرواتب أو الموظفين\n\nتحقق من التغييرات المالية أو الحساسة عبر إجراءات QEC المعتمدة وطريقة تواصل موثوقة ومعروفة.\n\nإذا كانت الرسالة مشبوهة: لا ترد، لا تنقر، ولا تعيد توجيهها دون حاجة. أبلغ تقنية المعلومات / أمن المعلومات عبر القناة المعتمدة في QEC. وإذا كنت قد نقرت بالفعل أو أدخلت بيانات اعتماد، أبلغ فوراً لتقليل الأثر.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
        ],
        [
            new(
                AwarenessQuestionType.SingleChoice,
                "You receive an unexpected email saying your company password expires today and providing a login link. What should you do?",
                "تتلقى رسالة بريد غير متوقعة تفيد بأن كلمة مرور شركتك تنتهي اليوم مع رابط لتسجيل الدخول. ماذا تفعل؟",
                "Do not use links from unexpected password messages. Verify independently through a known company method or report the message to IT / Information Security.",
                "لا تستخدم الروابط الواردة في رسائل انتهاء كلمة المرور غير المتوقعة. تحقق بشكل مستقل عبر طريقة شركة معروفة أو أبلغ عن الرسالة لتقنية المعلومات / أمن المعلومات.",
                1,
                [
                    new("Click the link immediately so your account is not locked.", "انقر الرابط فوراً حتى لا يُقفل حسابك.", false),
                    new("Do not use the email link; verify independently or report the suspicious message.", "لا تستخدم رابط الرسالة؛ تحقق بشكل مستقل أو أبلغ عن الرسالة المشبوهة.", true),
                    new("Forward the email to colleagues so they can renew their passwords too.", "أعد توجيه الرسالة للزملاء حتى يجددوا كلمات مرورهم أيضاً.", false),
                    new("Reply with your current password and ask them to extend it.", "أجب بكلمة مرورك الحالية واطلب تمديدها.", false),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "Which sender characteristic should raise concern?",
                "أي خاصية لدى المرسل يجب أن تثير القلق؟",
                "A domain that is slightly different from the expected company or service domain is a common phishing indicator. Display names alone are not reliable.",
                "النطاق الذي يختلف قليلاً عن نطاق الشركة أو الخدمة المتوقع مؤشر شائع على التصيد. أسماء العرض وحدها غير موثوقة.",
                1,
                [
                    new("A polite greeting and company logo in the signature.", "تحية مهذبة وشعار الشركة في التوقيع.", false),
                    new("A message sent during normal business hours.", "رسالة أُرسلت خلال ساعات العمل المعتادة.", false),
                    new("A domain that is slightly different from the expected company or service domain.", "نطاق يختلف قليلاً عن نطاق الشركة أو الخدمة المتوقع.", true),
                    new("A request that matches a process you already planned.", "طلب يتوافق مع إجراء كنت تخطط له مسبقاً.", false),
                ]),
            new(
                AwarenessQuestionType.TrueFalse,
                "You should approve an unexpected MFA notification if the message looks like it came from Google.",
                "يجب الموافقة على إشعار مصادقة متعددة العوامل غير متوقع إذا بدا أن الرسالة جاءت من Google.",
                "Only approve authentication requests you initiated. Unexpected MFA prompts should be denied and reported.",
                "وافق فقط على طلبات المصادقة التي بادرت بها أنت. ارفض إشعارات المصادقة غير المتوقعة وأبلغ عنها.",
                1,
                [
                    new("True", "صحيح", false),
                    new("False", "خطأ", true),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "A supplier emails unexpected new bank details and asks Finance to use them immediately. What is the best response?",
                "يرسل مورد بريداً يتضمن بيانات بنكية جديدة غير متوقعة ويطلب من المالية استخدامها فوراً. ما أفضل استجابة؟",
                "Unusual bank-detail changes are a common business email compromise tactic. Verify through the approved process using a known independent contact method.",
                "تغيير بيانات الحساب البنكي غير المعتاد أسلوب شائع لانتحال البريد الإلكتروني للأعمال. تحقق عبر الإجراء المعتمد وطريقة تواصل مستقلة ومعروفة.",
                1,
                [
                    new("Update the payment details immediately to avoid delaying the supplier.", "حدّث بيانات الدفع فوراً لتجنب تأخير المورد.", false),
                    new("Ask the same email thread for a scanned copy of the bank letter only.", "اطلب عبر نفس سلسلة الرسائل نسخة ممسوحة من خطاب البنك فقط.", false),
                    new("Ignore the message because suppliers never change bank accounts.", "تجاهل الرسالة لأن الموردين لا يغيرون حساباتهم البنكية أبداً.", false),
                    new("Verify the request through the approved process using a known independent contact method.", "تحقق من الطلب عبر الإجراء المعتمد باستخدام طريقة تواصل مستقلة ومعروفة.", true),
                ]),
            new(
                AwarenessQuestionType.MultipleChoice,
                "What should you do after accidentally entering your password into a suspicious page? (Select all that apply.)",
                "ماذا تفعل بعد إدخال كلمة المرور عن طريق الخطأ في صفحة مشبوهة؟ (اختر كل ما ينطبق.)",
                "Report immediately, reset the credential through the approved legitimate method, and follow IT/security instructions. Do not ignore the incident or delay reporting.",
                "أبلغ فوراً، وأعد تعيين بيانات الاعتماد عبر الطريقة الشرعية المعتمدة، واتبع تعليمات تقنية المعلومات/الأمن. لا تتجاهل الحادث ولا تؤخر الإبلاغ.",
                1,
                [
                    new("Report the incident immediately.", "أبلغ عن الحادث فوراً.", true),
                    new("Ignore it because the login page appeared to succeed.", "تجاهله لأن صفحة تسجيل الدخول بدت ناجحة.", false),
                    new("Change or reset the credential through the approved legitimate method.", "غيّر أو أعد تعيين بيانات الاعتماد عبر الطريقة الشرعية المعتمدة.", true),
                    new("Wait until the next day to see if anything unusual happens.", "انتظر حتى اليوم التالي لترى إن حدث شيء غير معتاد.", false),
                    new("Follow IT / Information Security instructions.", "اتبع تعليمات تقنية المعلومات / أمن المعلومات.", true),
                ]),
        ]);

    private static StarterCampaignSeed Password() => new(
        "SEC-AWARE-PASSWORD",
        "Password & Authentication Security",
        "أمن كلمات المرور والمصادقة",
        "Learn how to protect your QEC account using strong authentication practices, secure passwords, multi-factor authentication, and proper handling of verification codes.",
        "التعرف على كيفية حماية حساب QEC باستخدام ممارسات مصادقة آمنة وكلمات مرور قوية والمصادقة متعددة العوامل والتعامل الصحيح مع رموز التحقق.",
        7,
        [
            new(
                "Protect your account",
                "حماية حسابك",
                "Your QEC account credentials protect access to company information systems such as Google Workspace and other approved tools. Credentials are personal and must stay confidential.\n\nYou are responsible for safeguarding your password and any verification methods linked to your account. Do not leave sessions unlocked on shared or public devices.\n\nStrong authentication habits reduce the chance that someone else can access company email, files, or internal services in your name.\n\nFollow applicable QEC policies and procedures.",
                "تحمي بيانات اعتماد حساب QEC الوصول إلى أنظمة معلومات الشركة مثل Google Workspace والأدوات المعتمدة الأخرى. بيانات الاعتماد شخصية ويجب أن تبقى سرية.\n\nأنت مسؤول عن حماية كلمة المرور وأي وسائل تحقق مرتبطة بحسابك. لا تترك الجلسات مفتوحة على أجهزة مشتركة أو عامة.\n\nممارسات المصادقة القوية تقلل احتمال وصول شخص آخر إلى بريد الشركة أو ملفاتها أو خدماتها الداخلية باسمك.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Use strong and unique passwords",
                "استخدم كلمات مرور قوية وفريدة",
                "Use long, unique passwords or passphrases for your QEC account. Never reuse your company password on personal websites or other services.\n\nAvoid predictable choices such as names, birth dates, simple sequences, or words related to your role. If QEC provides an approved password management method, use only that method—do not store passwords in unapproved personal notes or shared files.\n\nA unique company password limits damage if a personal website is breached.\n\nFollow applicable QEC policies and procedures.",
                "استخدم كلمات مرور أو عبارات مرور طويلة وفريدة لحساب QEC. لا تعيد استخدام كلمة مرور الشركة على مواقع شخصية أو خدمات أخرى.\n\nتجنب الخيارات المتوقعة مثل الأسماء أو تواريخ الميلاد أو التسلسلات البسيطة أو الكلمات المرتبطة بدورك. إذا وفرت QEC طريقة معتمدة لإدارة كلمات المرور، فاستخدمها فقط—ولا تخزّن كلمات المرور في ملاحظات شخصية غير معتمدة أو ملفات مشتركة.\n\nكلمة مرور فريدة للشركة تحدّ من الضرر إذا تم اختراق موقع شخصي.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Never share passwords or verification codes",
                "لا تشارك كلمات المرور أو رموز التحقق",
                "IT should not need your password to support you. One-time passwords (OTP) and MFA codes are confidential and must never be shared.\n\nDo not send credentials by email, chat, or phone messages. Do not allow colleagues to use your account, even temporarily. Shared access makes it harder to protect information and investigate misuse.\n\nIf someone claiming to be from IT asks for your password or verification code, treat it as suspicious and report it.\n\nFollow applicable QEC policies and procedures.",
                "يجب ألا تحتاج تقنية المعلومات إلى كلمة مرورك لدعمك. كلمات المرور لمرة واحدة ورموز المصادقة متعددة العوامل سرية ويجب ألا تُشارك أبداً.\n\nلا ترسل بيانات الاعتماد عبر البريد أو الدردشة أو الرسائل الهاتفية. لا تسمح للزملاء باستخدام حسابك حتى مؤقتاً. الوصول المشترك يصعّب حماية المعلومات والتحقيق في إساءة الاستخدام.\n\nإذا طلب شخص يدّعي أنه من تقنية المعلومات كلمة مرورك أو رمز التحقق، اعتبر ذلك مشبوهاً وأبلغ عنه.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Multi-factor authentication",
                "المصادقة متعددة العوامل",
                "Multi-factor authentication (MFA) adds a second check after your password. Approve only prompts you initiated, such as when signing in to Google Workspace yourself.\n\nDeny unexpected prompts. Repeated MFA prompts may indicate someone is trying to access your account. Report suspicious MFA activity to IT / Information Security through the approved channel.\n\nMFA protects your account even if a password is guessed or stolen—but only if you treat unexpected prompts carefully.\n\nFollow applicable QEC policies and procedures.",
                "تضيف المصادقة متعددة العوامل (MFA) تحققاً ثانياً بعد كلمة المرور. وافق فقط على الطلبات التي بادرت بها أنت، مثل تسجيل الدخول إلى Google Workspace بنفسك.\n\nارفض الطلبات غير المتوقعة. تكرار طلبات المصادقة قد يشير إلى محاولة الوصول إلى حسابك. أبلغ عن نشاط المصادقة المشبوه لتقنية المعلومات / أمن المعلومات عبر القناة المعتمدة.\n\nتحمي المصادقة متعددة العوامل حسابك حتى لو خُمّنت كلمة المرور أو سُرقت—لكن فقط إذا تعاملت بحذر مع الطلبات غير المتوقعة.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "If you suspect account compromise",
                "عند الاشتباه في اختراق الحساب",
                "If you suspect your account is compromised, report it immediately. Use the legitimate account recovery or password-change process provided by QEC—not links from unexpected messages.\n\nWhere supported, sign out unknown sessions. Follow IT instructions promptly. Speed matters: early reporting helps protect company email, files, and related services.\n\nDo not wait to see if the problem continues. Follow applicable QEC policies and procedures.",
                "إذا اشتبهت في اختراق حسابك، أبلغ فوراً. استخدم عملية استعادة الحساب أو تغيير كلمة المرور الشرعية التي توفرها QEC—وليس الروابط من الرسائل غير المتوقعة.\n\nحيثما كان ذلك مدعوماً، سجّل الخروج من الجلسات غير المعروفة. اتبع تعليمات تقنية المعلومات بسرعة. السرعة مهمة: الإبلاغ المبكر يساعد في حماية بريد الشركة وملفاتها والخدمات المرتبطة.\n\nلا تنتظر لترى إن استمرت المشكلة. التزم بسياسات وإجراءات QEC المعمول بها.",
                1),
        ],
        [
            new(
                AwarenessQuestionType.SingleChoice,
                "Which practice best protects your QEC account password?",
                "أي ممارسة تحمي كلمة مرور حساب QEC بشكل أفضل؟",
                "Using a unique password for company services reduces risk if a personal website is breached. Reusing passwords across services is unsafe.",
                "استخدام كلمة مرور فريدة لخدمات الشركة يقلل المخاطر إذا تم اختراق موقع شخصي. إعادة استخدام كلمات المرور عبر الخدمات غير آمن.",
                1,
                [
                    new("Reuse the same strong password for personal and company accounts.", "أعد استخدام نفس كلمة المرور القوية للحسابات الشخصية وحسابات الشركة.", false),
                    new("Use a unique password for company services and do not reuse it elsewhere.", "استخدم كلمة مرور فريدة لخدمات الشركة ولا تعد استخدامها في أماكن أخرى.", true),
                    new("Share your password with one trusted teammate for coverage.", "شارك كلمة مرورك مع زميل موثوق واحد للتغطية.", false),
                    new("Write your password on a sticky note under the keyboard.", "اكتب كلمة المرور على ملاحظة لاصقة تحت لوحة المفاتيح.", false),
                ]),
            new(
                AwarenessQuestionType.TrueFalse,
                "It is acceptable to share your QEC password with a colleague so they can finish urgent work while you are away.",
                "من المقبول مشاركة كلمة مرور QEC مع زميل حتى ينهي عملاً عاجلاً أثناء غيابك.",
                "Account credentials are personal. Do not allow colleagues to use your account. Use approved delegation or handoff processes instead.",
                "بيانات اعتماد الحساب شخصية. لا تسمح للزملاء باستخدام حسابك. استخدم عمليات التفويض أو التسليم المعتمدة بدلاً من ذلك.",
                1,
                [
                    new("True", "صحيح", false),
                    new("False", "خطأ", true),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "You receive an unexpected MFA prompt while you are not signing in. What should you do?",
                "يصلك طلب مصادقة متعددة العوامل غير متوقع وأنت لست في طور تسجيل الدخول. ماذا تفعل؟",
                "Deny unexpected MFA prompts and report suspicious activity. Approving a prompt you did not initiate can give an attacker access.",
                "ارفض طلبات المصادقة غير المتوقعة وأبلغ عن النشاط المشبوه. الموافقة على طلب لم تبادر إليه قد تمنح المهاجم وصولاً.",
                1,
                [
                    new("Approve it in case it is a delayed Google Workspace sign-in.", "وافق عليه فقد يكون تسجيل دخول متأخراً إلى Google Workspace.", false),
                    new("Approve it, then change your password later if something looks wrong.", "وافق عليه، ثم غيّر كلمة المرور لاحقاً إذا بدا شيء خاطئاً.", false),
                    new("Deny the prompt and report the suspicious MFA activity.", "ارفض الطلب وأبلغ عن نشاط المصادقة المشبوه.", true),
                    new("Ignore the prompt and take no further action.", "تجاهل الطلب ولا تتخذ أي إجراء إضافي.", false),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "Someone claiming to be from IT asks you to send your one-time verification code by chat. What should you do?",
                "شخص يدّعي أنه من تقنية المعلومات يطلب منك إرسال رمز التحقق لمرة واحدة عبر الدردشة. ماذا تفعل؟",
                "OTP and MFA codes are confidential. Legitimate IT support should not ask you to send them by chat or email.",
                "رموز التحقق لمرة واحدة والمصادقة متعددة العوامل سرية. الدعم الشرعي لتقنية المعلومات يجب ألا يطلب إرسالها عبر الدردشة أو البريد.",
                1,
                [
                    new("Send the code quickly so IT can finish unlocking your account.", "أرسل الرمز بسرعة حتى تنهي تقنية المعلومات فتح حسابك.", false),
                    new("Do not share the code; treat the request as suspicious and report it.", "لا تشارك الرمز؛ اعتبر الطلب مشبوهاً وأبلغ عنه.", true),
                    new("Send the code only if the person also knows your employee number.", "أرسل الرمز فقط إذا كان الشخص يعرف أيضاً رقم موظفك.", false),
                    new("Reply with your password instead so they can reset MFA.", "أجب بكلمة مرورك بدلاً من ذلك حتى يعيدوا ضبط المصادقة.", false),
                ]),
            new(
                AwarenessQuestionType.MultipleChoice,
                "If you suspect your QEC account is compromised, which actions are appropriate? (Select all that apply.)",
                "إذا اشتبهت في اختراق حساب QEC، أي الإجراءات مناسبة؟ (اختر كل ما ينطبق.)",
                "Report immediately and secure the credential through legitimate recovery or password-change steps. Delay increases risk to company information.",
                "أبلغ فوراً وأمّن بيانات الاعتماد عبر خطوات الاستعادة أو تغيير كلمة المرور الشرعية. التأخير يزيد خطر تعرض معلومات الشركة.",
                1,
                [
                    new("Report the suspicion immediately to IT / Information Security.", "أبلغ عن الاشتباه فوراً لتقنية المعلومات / أمن المعلومات.", true),
                    new("Wait a few days to confirm whether the issue continues.", "انتظر بضعة أيام للتأكد مما إذا استمرت المشكلة.", false),
                    new("Use the legitimate password-change or account recovery process.", "استخدم عملية تغيير كلمة المرور أو استعادة الحساب الشرعية.", true),
                    new("Post the details in a public chat so everyone is aware.", "انشر التفاصيل في دردشة عامة ليكون الجميع على علم.", false),
                ]),
        ]);

    private static StarterCampaignSeed Data() => new(
        "SEC-AWARE-DATA",
        "Data Protection & Confidentiality",
        "حماية البيانات والسرية",
        "Understand how to handle company, employee, financial, customer and other sensitive information safely throughout its lifecycle.",
        "فهم كيفية التعامل الآمن مع معلومات الشركة والموظفين والبيانات المالية وبيانات العملاء وغيرها من المعلومات الحساسة طوال دورة حياتها.",
        8,
        [
            new(
                "Recognize sensitive information",
                "التعرف على المعلومات الحساسة",
                "Not all company information has the same sensitivity. Examples of sensitive information include:\n\n- employee information\n- financial information\n- contracts\n- credentials\n- customer, student, or project information\n- internal confidential documents\n\nTreat information according to its sensitivity and follow QEC classification or data-handling policy where applicable. When unsure, ask before sharing widely.\n\nFollow applicable QEC policies and procedures.",
                "ليست كل معلومات الشركة بنفس مستوى الحساسية. من أمثلة المعلومات الحساسة:\n\n- معلومات الموظفين\n- المعلومات المالية\n- العقود\n- بيانات الاعتماد\n- معلومات العملاء أو الطلاب أو المشاريع\n- المستندات الداخلية السرية\n\nتعامل مع المعلومات وفق حساسيتها واتبع تصنيف QEC أو سياسة التعامل مع البيانات حيثما انطبق ذلك. عند الشك، اسأل قبل المشاركة على نطاق واسع.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Share only with authorized people",
                "شارك المعلومات مع الأشخاص المخولين فقط",
                "Apply the need-to-know principle: share sensitive information only with people who require it for their work. Verify the recipient before sending.\n\nAvoid broad distribution lists when a smaller audience is enough. Do not send sensitive QEC information to personal email accounts. Keep work information inside approved QEC systems such as Google Workspace.\n\nIf someone asks for sensitive data without a clear business need, pause and confirm through the proper channel.\n\nFollow applicable QEC policies and procedures.",
                "طبّق مبدأ الحاجة إلى المعرفة: شارك المعلومات الحساسة فقط مع من يحتاجها لعمله. تحقق من المستلم قبل الإرسال.\n\nتجنب قوائم التوزيع الواسعة عندما يكفي جمهور أصغر. لا ترسل معلومات QEC الحساسة إلى حسابات بريد شخصية. أبقِ معلومات العمل داخل أنظمة QEC المعتمدة مثل Google Workspace.\n\nإذا طلب شخص بيانات حساسة دون حاجة عمل واضحة، توقّف وأكد عبر القناة المناسبة.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Email and file-sharing precautions",
                "احتياطات البريد الإلكتروني ومشاركة الملفات",
                "Before sending email, check To and CC carefully and review attachments. When sharing files in Google Workspace or another approved system, check permissions and prefer access limited to specific recipients.\n\nAvoid public “anyone with the link” sharing for sensitive information. Remove access when it is no longer needed. Double-check that the file you attach or share is the correct version.\n\nFollow applicable QEC policies and procedures.",
                "قبل إرسال البريد، تحقق بعناية من حقلي إلى ونسخة وإلى، وراجع المرفقات. عند مشاركة الملفات في Google Workspace أو نظام معتمد آخر، افحص الصلاحيات وفضّل الوصول المحدود لمستلمين محددين.\n\nتجنب المشاركة العامة «لأي شخص لديه الرابط» للمعلومات الحساسة. أزل الوصول عندما تنتفي الحاجة. تحقق مرتين من أن الملف الذي ترفقه أو تشاركه هو النسخة الصحيحة.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Protect data on devices and in physical form",
                "حماية البيانات على الأجهزة والنسخ الورقية",
                "Lock your screen when you step away. Do not leave sensitive documents exposed on desks or printers. Collect printouts promptly and store or dispose of them securely according to QEC practice.\n\nUse portable media only according to QEC policy. Avoid copying sensitive company information to unauthorized personal devices or personal cloud storage.\n\nPhysical and digital care work together to protect confidentiality.\n\nFollow applicable QEC policies and procedures.",
                "اقفل الشاشة عند الابتعاد. لا تترك المستندات الحساسة مكشوفة على المكاتب أو الطابعات. اجمع المطبوعات فوراً واحفظها أو تخلص منها بشكل آمن وفق ممارسة QEC.\n\nاستخدم وسائط التخزين المحمولة فقط وفق سياسة QEC. تجنب نسخ معلومات الشركة الحساسة إلى أجهزة شخصية غير مخولة أو تخزين سحابي شخصي.\n\nالعناية المادية والرقمية معاً تحميان السرية.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Report accidental disclosure quickly",
                "الإبلاغ السريع عن الإفصاح غير المقصود",
                "Accidents happen. Examples include sending to the wrong recipient, losing a device, creating a public sharing link by mistake, or misplacing a printed document.\n\nReport accidental disclosure quickly so QEC can reduce impact—for example by recalling access, resetting credentials, or advising next steps. Do not hide mistakes. Early reporting is responsible and expected.\n\nFollow applicable QEC policies and procedures.",
                "قد تحدث أخطاء. من الأمثلة: الإرسال إلى مستلم خاطئ، أو فقدان جهاز، أو إنشاء رابط مشاركة عام بالخطأ، أو ضياع مستند مطبوع.\n\nأبلغ عن الإفصاح غير المقصود بسرعة حتى تتمكن QEC من تقليل الأثر—مثلاً بسحب الوصول أو إعادة تعيين بيانات الاعتماد أو توجيه الخطوات التالية. لا تُخفِ الأخطاء. الإبلاغ المبكر مسؤول ومتوقع.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
        ],
        [
            new(
                AwarenessQuestionType.SingleChoice,
                "What does the need-to-know principle mean for sharing company information?",
                "ماذا يعني مبدأ الحاجة إلى المعرفة عند مشاركة معلومات الشركة؟",
                "Share sensitive information only with people who require it for their work role, not with everyone who might be interested.",
                "شارك المعلومات الحساسة فقط مع من يحتاجها لدوره الوظيفي، وليس مع كل من قد يهتم بها.",
                1,
                [
                    new("Share widely so teams stay informed about every detail.", "شارك على نطاق واسع ليبقى الجميع مطلعاً على كل التفاصيل.", false),
                    new("Share sensitive information only with people who require it for their work.", "شارك المعلومات الحساسة فقط مع من يحتاجها لعمله.", true),
                    new("Share with personal contacts if they promise confidentiality.", "شارك مع جهات اتصال شخصية إذا وعدوا بالسرية.", false),
                    new("Post sensitive files in a company-wide chat for convenience.", "انشر الملفات الحساسة في دردشة على مستوى الشركة للسهولة.", false),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "You accidentally email a confidential attachment to the wrong recipient. What should you do first?",
                "أرسلت عن طريق الخطأ مرفقاً سرياً إلى مستلم خاطئ. ماذا تفعل أولاً؟",
                "Report accidental disclosure quickly so IT / Information Security can help reduce impact. Waiting or ignoring the mistake increases risk.",
                "أبلغ عن الإفصاح غير المقصود بسرعة حتى تساعد تقنية المعلومات / أمن المعلومات في تقليل الأثر. الانتظار أو تجاهل الخطأ يزيد المخاطر.",
                1,
                [
                    new("Wait to see whether the recipient notices the attachment.", "انتظر لترى إن لاحظ المستلم المرفق.", false),
                    new("Ask the wrong recipient to ignore it and take no further action.", "اطلب من المستلم الخاطئ تجاهله دون أي إجراء إضافي.", false),
                    new("Report the accidental disclosure promptly through the approved channel.", "أبلغ عن الإفصاح غير المقصود فوراً عبر القناة المعتمدة.", true),
                    new("Delete the sent item locally and assume the problem is solved.", "احذف العنصر المرسل محلياً وافترض أن المشكلة حُلّت.", false),
                ]),
            new(
                AwarenessQuestionType.TrueFalse,
                "Sending sensitive QEC documents to your personal email so you can work from home later is an acceptable practice.",
                "إرسال مستندات QEC الحساسة إلى بريدك الشخصي للعمل من المنزل لاحقاً ممارسة مقبولة.",
                "Sensitive company information should stay in approved QEC systems. Personal email and personal storage are not appropriate for sensitive QEC data.",
                "يجب أن تبقى معلومات الشركة الحساسة في أنظمة QEC المعتمدة. البريد الشخصي والتخزين الشخصي غير مناسبين لبيانات QEC الحساسة.",
                1,
                [
                    new("True", "صحيح", false),
                    new("False", "خطأ", true),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "When sharing a sensitive file in Google Workspace, which approach is safest?",
                "عند مشاركة ملف حساس في Google Workspace، أي نهج هو الأكثر أماناً؟",
                "Prefer recipient-based access and avoid public links for sensitive content. Remove access when it is no longer needed.",
                "فضّل الوصول المبني على المستلمين وتجنب الروابط العامة للمحتوى الحساس. أزل الوصول عندما تنتفي الحاجة.",
                1,
                [
                    new("Use “anyone with the link” so partners can open it easily.", "استخدم «أي شخص لديه الرابط» ليسهل على الشركاء فتحه.", false),
                    new("Grant access only to specific authorized recipients and review permissions.", "امنح الوصول فقط لمستلمين مخولين محددين وراجع الصلاحيات.", true),
                    new("Upload the file to a personal cloud account and share from there.", "ارفع الملف إلى حساب سحابي شخصي وشاركه من هناك.", false),
                    new("Leave broad sharing enabled indefinitely after the project ends.", "اترك المشاركة الواسعة مفعّلة إلى أجل غير مسمى بعد انتهاء المشروع.", false),
                ]),
            new(
                AwarenessQuestionType.MultipleChoice,
                "Which situations should you report quickly as accidental disclosure or data risk? (Select all that apply.)",
                "أي الحالات يجب الإبلاغ عنها بسرعة كإفصاح غير مقصود أو خطر على البيانات؟ (اختر كل ما ينطبق.)",
                "Wrong recipients, lost devices, unintended public links, and misplaced printed documents all need prompt reporting so impact can be reduced.",
                "المستلمون الخاطئون والأجهزة المفقودة والروابط العامة غير المقصودة والمستندات المطبوعة الضائعة كلها تحتاج إبلاغاً فورياً لتقليل الأثر.",
                1,
                [
                    new("You lost a company device that may contain work information.", "فقدت جهازاً تابعاً للشركة قد يحتوي معلومات عمل.", true),
                    new("You created a public sharing link for sensitive content by mistake.", "أنشأت رابط مشاركة عاماً لمحتوى حساس بالخطأ.", true),
                    new("You finished reading a public news article unrelated to QEC.", "انتهيت من قراءة مقال أخبار عام غير مرتبط بـ QEC.", false),
                    new("You misplaced a printed document containing confidential details.", "ضيّعت مستنداً مطبوعاً يحتوي تفاصيل سرية.", true),
                ]),
        ]);

    private static StarterCampaignSeed Collab() => new(
        "SEC-AWARE-COLLAB",
        "Safe Internet, Email & Collaboration",
        "الاستخدام الآمن للإنترنت والبريد الإلكتروني وأدوات التعاون",
        "Learn safe practices for web browsing, email, cloud collaboration, file sharing and everyday online communication at QEC.",
        "التعرف على الممارسات الآمنة لتصفح الإنترنت والبريد الإلكتروني والتعاون السحابي ومشاركة الملفات والتواصل الإلكتروني اليومي في QEC.",
        7,
        [
            new(
                "Use company communication tools safely",
                "الاستخدام الآمن لأدوات التواصل الخاصة بالشركة",
                "Use your approved company account for work communication. Keep work information in approved systems such as Google Workspace rather than moving company files to personal accounts for convenience.\n\nPersonal tools can bypass company protections and make it harder to control access later. When collaborating, prefer official QEC channels so messages and files stay where they belong.\n\nFollow applicable QEC policies and procedures.",
                "استخدم حساب الشركة المعتمد للتواصل الوظيفي. أبقِ معلومات العمل في الأنظمة المعتمدة مثل Google Workspace بدلاً من نقل ملفات الشركة إلى حسابات شخصية للسهولة.\n\nالأدوات الشخصية قد تتجاوز حمايات الشركة وتصعّب التحكم في الوصول لاحقاً. عند التعاون، فضّل قنوات QEC الرسمية حتى تبقى الرسائل والملفات في مكانها الصحيح.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Be careful with links and downloads",
                "توخ الحذر مع الروابط والتنزيلات",
                "Download only business-required files from trusted sources. Be cautious with unexpected browser downloads, fake update prompts, malicious advertisements, and QR codes that lead to unknown sites.\n\nIf a site or pop-up asks you to install software you did not request, stop and verify with IT. Do not click through security warnings just to finish a task quickly.\n\nFollow applicable QEC policies and procedures.",
                "نزّل فقط الملفات المطلوبة للعمل من مصادر موثوقة. كن حذراً مع التنزيلات غير المتوقعة في المتصفح وطلبات التحديث المزيفة والإعلانات الضارة ورموز QR التي تؤدي إلى مواقع غير معروفة.\n\nإذا طلب موقع أو نافذة منبثقة تثبيت برنامج لم تطلبه، توقّف وتحقق مع تقنية المعلومات. لا تتجاوز تحذيرات الأمان لمجرد إنهاء مهمة بسرعة.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Share cloud files carefully",
                "مشاركة الملفات السحابية بعناية",
                "When sharing in Google Workspace, prefer recipient-based access. Review external users carefully and avoid “anyone with the link” for sensitive content.\n\nRemove outdated sharing permissions and check the file before sending. Confirm you selected the correct document and the right people.\n\nGood sharing habits protect confidentiality while still allowing collaboration.\n\nFollow applicable QEC policies and procedures.",
                "عند المشاركة في Google Workspace، فضّل الوصول المبني على المستلمين. راجع المستخدمين الخارجيين بعناية وتجنب «أي شخص لديه الرابط» للمحتوى الحساس.\n\nأزل صلاحيات المشاركة القديمة وتحقق من الملف قبل الإرسال. تأكد أنك اخترت المستند الصحيح والأشخاص الصحيحين.\n\nعادات المشاركة الجيدة تحمي السرية مع السماح بالتعاون.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Browsing and public networks",
                "التصفح والشبكات العامة",
                "Take browser certificate and security warnings seriously. Avoid entering sensitive information on suspicious sites.\n\nOn public Wi-Fi, use extra caution: avoid sensitive actions when the connection or environment feels unsafe, and use only QEC-approved secure remote-access methods where required for company systems.\n\nDo not dismiss warnings because a page “looks familiar.” Follow applicable QEC policies and procedures.",
                "خذ تحذيرات شهادة المتصفح والأمان على محمل الجد. تجنب إدخال معلومات حساسة على مواقع مشبوهة.\n\nعلى شبكات Wi-Fi العامة، استخدم حذراً إضافياً: تجنب الإجراءات الحساسة عندما يبدو الاتصال أو المكان غير آمن، واستخدم فقط طرق الوصول عن بُعد الآمنة المعتمدة في QEC حيث يُطلب ذلك لأنظمة الشركة.\n\nلا تتجاهل التحذيرات لأن الصفحة «تبدو مألوفة». التزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Stop and report unusual activity",
                "التوقف والإبلاغ عن الأنشطة غير المعتادة",
                "Stop and report unusual activity such as unexpected browser pop-ups, account sign-in alerts, unusual file-share requests, malware warnings, or requests to install unknown extensions or software.\n\nAvoid further interaction with the suspicious item. Contact IT / Information Security through the approved QEC channel and follow their guidance.\n\nEarly reporting helps protect you and the organization. Follow applicable QEC policies and procedures.",
                "توقّف وأبلغ عن الأنشطة غير المعتادة مثل النوافذ المنبثقة غير المتوقعة، وتنبيهات تسجيل الدخول، وطلبات مشاركة ملفات غير معتادة، وتحذيرات البرمجيات الضارة، أو طلبات تثبيت إضافات أو برامج غير معروفة.\n\nتجنب المزيد من التفاعل مع العنصر المشبوه. تواصل مع تقنية المعلومات / أمن المعلومات عبر القناة المعتمدة في QEC واتبع توجيهاتهم.\n\nالإبلاغ المبكر يساعد في حمايتك وحماية المنظمة. التزم بسياسات وإجراءات QEC المعمول بها.",
                1),
        ],
        [
            new(
                AwarenessQuestionType.SingleChoice,
                "Where should you keep QEC work files for day-to-day collaboration?",
                "أين يجب أن تحفظ ملفات عمل QEC للتعاون اليومي؟",
                "Keep work information in approved company systems such as Google Workspace. Moving files to personal accounts for convenience weakens control and protection.",
                "أبقِ معلومات العمل في أنظمة الشركة المعتمدة مثل Google Workspace. نقل الملفات إلى حسابات شخصية للسهولة يضعف التحكم والحماية.",
                1,
                [
                    new("In your personal cloud account so you can access it anywhere.", "في حسابك السحابي الشخصي لتصل إليه من أي مكان.", false),
                    new("In approved company systems such as Google Workspace.", "في أنظمة الشركة المعتمدة مثل Google Workspace.", true),
                    new("On a USB drive left in a shared drawer.", "على محرك USB متروك في درج مشترك.", false),
                    new("In a public messaging app chat history.", "في سجل دردشة تطبيق مراسلة عام.", false),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "Which sharing setting is generally inappropriate for sensitive QEC content?",
                "أي إعداد مشاركة غير مناسب عموماً لمحتوى QEC الحساس؟",
                "Public “anyone with the link” sharing can expose sensitive content beyond intended recipients. Prefer specific recipient access.",
                "المشاركة العامة «لأي شخص لديه الرابط» قد تكشف محتوى حساساً خارج المستلمين المقصودين. فضّل وصول مستلمين محددين.",
                1,
                [
                    new("Access limited to named authorized recipients.", "وصول محدود لمستلمين مخولين مسمّيين.", false),
                    new("“Anyone with the link” for sensitive content.", "«أي شخص لديه الرابط» للمحتوى الحساس.", true),
                    new("External sharing only after verifying the business need.", "مشاركة خارجية فقط بعد التحقق من حاجة العمل.", false),
                    new("Removing access when collaboration ends.", "إزالة الوصول عند انتهاء التعاون.", false),
                ]),
            new(
                AwarenessQuestionType.TrueFalse,
                "If a website shows a browser security warning, you should usually continue so you can finish downloading an unexpected update.",
                "إذا أظهر موقع تحذير أمان من المتصفح، يجب عادة المتابعة لإنهاء تنزيل تحديث غير متوقع.",
                "Browser security warnings are important signals. Do not continue into suspicious downloads or fake updates; stop and verify with IT if needed.",
                "تحذيرات أمان المتصفح إشارات مهمة. لا تتابع إلى تنزيلات مشبوهة أو تحديثات مزيفة؛ توقّف وتحقق مع تقنية المعلومات عند الحاجة.",
                1,
                [
                    new("True", "صحيح", false),
                    new("False", "خطأ", true),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "A pop-up asks you to install a browser extension you did not request in order to open a work document. What should you do?",
                "تطلب نافذة منبثقة تثبيت إضافة متصفح لم تطلبها لفتح مستند عمل. ماذا تفعل؟",
                "Unexpected software or extension requests are a common risk. Stop, avoid installing, and report unusual activity to IT / Information Security.",
                "طلبات البرامج أو الإضافات غير المتوقعة خطر شائع. توقّف، تجنب التثبيت، وأبلغ عن النشاط غير المعتاد لتقنية المعلومات / أمن المعلومات.",
                1,
                [
                    new("Install the extension so you can open the document quickly.", "ثبّت الإضافة لتفتح المستند بسرعة.", false),
                    new("Stop, do not install it, and report the unusual request if needed.", "توقّف، لا تثبّتها، وأبلغ عن الطلب غير المعتاد عند الحاجة.", true),
                    new("Install it on a personal browser profile instead.", "ثبّتها على ملف متصفح شخصي بدلاً من ذلك.", false),
                    new("Share the pop-up link with colleagues so they can install it too.", "شارك رابط النافذة المنبثقة مع الزملاء ليثبّتوها أيضاً.", false),
                ]),
            new(
                AwarenessQuestionType.MultipleChoice,
                "Which examples of unusual activity should you stop and report? (Select all that apply.)",
                "أي أمثلة على نشاط غير معتاد يجب التوقف عنها والإبلاغ؟ (اختر كل ما ينطبق.)",
                "Unexpected pop-ups, sign-in alerts, unusual share requests, and malware warnings should be stopped and reported through the approved channel.",
                "النوافذ المنبثقة غير المتوقعة وتنبيهات تسجيل الدخول وطلبات المشاركة غير المعتادة وتحذيرات البرمجيات الضارة يجب إيقافها والإبلاغ عنها عبر القناة المعتمدة.",
                1,
                [
                    new("Unexpected account sign-in alerts you did not initiate.", "تنبيهات تسجيل دخول غير متوقعة لم تبادر إليها.", true),
                    new("A malware or security warning on your device.", "تحذير برمجيات ضارة أو أمان على جهازك.", true),
                    new("A routine calendar reminder for a scheduled meeting.", "تذكير تقويم روتيني لاجتماع مجدول.", false),
                    new("An unusual request to share a large set of company files externally.", "طلب غير معتاد لمشاركة مجموعة كبيرة من ملفات الشركة خارجياً.", true),
                ]),
        ]);

    private static StarterCampaignSeed Remote() => new(
        "SEC-AWARE-REMOTE",
        "Remote Access & Device Security",
        "أمن الوصول عن بُعد والأجهزة",
        "Learn how to protect QEC devices and information when working inside or outside the office, including physical security, remote access, updates and lost-device reporting.",
        "التعرف على كيفية حماية أجهزة QEC ومعلومات الشركة أثناء العمل داخل المكتب أو خارجه، بما في ذلك الحماية المادية والوصول عن بُعد والتحديثات والإبلاغ عن الأجهزة المفقودة.",
        8,
        [
            new(
                "Keep devices physically secure",
                "الحفاظ على الأمان المادي للأجهزة",
                "Do not leave laptops or other company devices unattended in public places. Lock the screen whenever you step away, even briefly.\n\nDo not allow unauthorized persons to use your device. In cafés, airports, and shared spaces, keep the device in sight and angled away from casual viewers when possible.\n\nPhysical control of the device is the first layer of protection for the information it can access.\n\nFollow applicable QEC policies and procedures.",
                "لا تترك أجهزة الحاسوب المحمولة أو أجهزة الشركة الأخرى دون مراقبة في الأماكن العامة. اقفل الشاشة كلما ابتعدت، حتى لبضع لحظات.\n\nلا تسمح لأشخاص غير مخولين باستخدام جهازك. في المقاهي والمطارات والأماكن المشتركة، أبقِ الجهاز في مرمى نظرك وبعيداً عن أعين المارة قدر الإمكان.\n\nالتحكم المادي بالجهاز هو الطبقة الأولى لحماية المعلومات التي يمكنه الوصول إليها.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Keep systems updated and approved",
                "الحفاظ على تحديث الأنظمة واستخدام البرامج المعتمدة",
                "Do not disable security tools installed for company protection. Install only authorized applications and apply updates according to the QEC / IT process.\n\nReport security warnings rather than ignoring them. Unauthorized software can introduce risk and may interfere with protections already in place.\n\nKeeping systems updated and approved helps devices stay reliable and safer for company work, including Google Workspace access.\n\nFollow applicable QEC policies and procedures.",
                "لا تعطّل أدوات الأمان المثبتة لحماية الشركة. ثبّت التطبيقات المعتمدة فقط وطبّق التحديثات وفق عملية QEC / تقنية المعلومات.\n\nأبلغ عن تحذيرات الأمان بدلاً من تجاهلها. البرامج غير المعتمدة قد تُدخل مخاطر وقد تتداخل مع الحمايات القائمة.\n\nالحفاظ على الأنظمة محدّثة ومعتمدة يساعد الأجهزة على البقاء موثوقة وأكثر أماناً لعمل الشركة، بما في ذلك الوصول إلى Google Workspace.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Safe remote access",
                "الوصول الآمن عن بُعد",
                "Use only QEC-approved remote access methods when connecting to company systems from outside the office. Verify login pages before entering credentials.\n\nProtect your device from shoulder surfing and public exposure. Do not accept unknown remote-support requests; technician identity and your consent should be clear before any remote session begins.\n\nIf a remote-access request feels unexpected or unclear, stop and confirm with IT through a known channel.\n\nFollow applicable QEC policies and procedures.",
                "استخدم فقط طرق الوصول عن بُعد المعتمدة في QEC عند الاتصال بأنظمة الشركة من خارج المكتب. تحقق من صفحات تسجيل الدخول قبل إدخال بيانات الاعتماد.\n\nاحمِ جهازك من التطلع من فوق الكتف والتعرض العام. لا تقبل طلبات دعم عن بُعد غير معروفة؛ يجب أن تكون هوية الفني وموافقتك واضحين قبل بدء أي جلسة عن بُعد.\n\nإذا بدا طلب الوصول عن بُعد غير متوقع أو غير واضح، توقّف وأكد مع تقنية المعلومات عبر قناة معروفة.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
            new(
                "Portable media and external devices",
                "وسائط التخزين المحمولة والأجهزة الخارجية",
                "Unknown USB drives and other storage devices can carry malware. Follow company policy for portable media. Do not connect found or untrusted devices to company computers.\n\nProtect company data from unauthorized copying to personal storage. If you must use approved portable media, handle it carefully and report loss promptly.\n\nFollow applicable QEC policies and procedures.",
                "محركات USB غير المعروفة ووسائط التخزين الأخرى قد تحمل برمجيات ضارة. التزم بسياسة الشركة لوسائط التخزين المحمولة. لا تصل أجهزة عُثر عليها أو غير موثوقة بأجهزة الشركة.\n\nاحمِ بيانات الشركة من النسخ غير المخول إلى تخزين شخصي. إذا كان يجب استخدام وسائط محمولة معتمدة، تعامل معها بحذر وأبلغ عن فقدانها فوراً.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                1),
            new(
                "Lost, stolen or suspicious devices",
                "الأجهزة المفقودة أو المسروقة أو المشبوهة",
                "Report loss or theft of a company device immediately. Also report suspicious remote activity and unexpected security alerts.\n\nDo not delay because the device has a password. IT may need to protect accounts and data quickly—including Google Workspace access linked to that device.\n\nPrompt reporting reduces impact and is part of responsible device use.\n\nFollow applicable QEC policies and procedures.",
                "أبلغ فوراً عن فقدان أو سرقة جهاز تابع للشركة. وأبلغ أيضاً عن النشاط عن بُعد المشبوه وتحذيرات الأمان غير المتوقعة.\n\nلا تؤخر الإبلاغ لأن الجهاز محمي بكلمة مرور. قد تحتاج تقنية المعلومات إلى حماية الحسابات والبيانات بسرعة—بما في ذلك الوصول إلى Google Workspace المرتبط بذلك الجهاز.\n\nالإبلاغ الفوري يقلل الأثر وهو جزء من الاستخدام المسؤول للأجهزة.\n\nالتزم بسياسات وإجراءات QEC المعمول بها.",
                2),
        ],
        [
            new(
                AwarenessQuestionType.SingleChoice,
                "You step away from your desk for a short meeting. What should you do with your laptop?",
                "تغادر مكتبك لاجتماع قصير. ماذا تفعل بحاسوبك المحمول؟",
                "Lock the screen whenever you step away so unauthorized people cannot use the device or view open information.",
                "اقفل الشاشة كلما ابتعدت حتى لا يستخدم أشخاص غير مخولين الجهاز أو يروا المعلومات المفتوحة.",
                1,
                [
                    new("Leave it unlocked so you can return quickly.", "اتركه غير مقفل لتعود بسرعة.", false),
                    new("Lock the screen before you leave.", "اقفل الشاشة قبل المغادرة.", true),
                    new("Ask a nearby visitor to watch it for you.", "اطلب من زائر قريب مراقبته نيابة عنك.", false),
                    new("Hide it under papers but keep the session open.", "أخفه تحت الأوراق مع إبقاء الجلسة مفتوحة.", false),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "A colleague suggests installing unauthorized software to “make work easier.” What should you do?",
                "يقترح زميل تثبيت برنامج غير معتمد «لتسهيل العمل». ماذا تفعل؟",
                "Install only authorized applications through the QEC / IT process. Unauthorized software can introduce security risk.",
                "ثبّت التطبيقات المعتمدة فقط عبر عملية QEC / تقنية المعلومات. البرامج غير المعتمدة قد تُدخل مخاطر أمنية.",
                1,
                [
                    new("Install it if it seems helpful and uninstall it later.", "ثبّته إذا بدا مفيداً وأزله لاحقاً.", false),
                    new("Install only authorized applications according to the QEC / IT process.", "ثبّت التطبيقات المعتمدة فقط وفق عملية QEC / تقنية المعلومات.", true),
                    new("Disable security tools temporarily so the install succeeds.", "عطّل أدوات الأمان مؤقتاً حتى ينجح التثبيت.", false),
                    new("Download it from any website that offers a free copy.", "حمّله من أي موقع يقدم نسخة مجانية.", false),
                ]),
            new(
                AwarenessQuestionType.TrueFalse,
                "It is safe to connect a USB drive you found in a public area to your company laptop to check what is on it.",
                "من الآمن توصيل محرك USB عثرت عليه في مكان عام بحاسوب الشركة للتحقق مما فيه.",
                "Unknown or found USB devices can carry malware. Do not connect untrusted portable media to company computers.",
                "أجهزة USB غير المعروفة أو التي يُعثر عليها قد تحمل برمجيات ضارة. لا تصل وسائط محمولة غير موثوقة بأجهزة الشركة.",
                1,
                [
                    new("True", "صحيح", false),
                    new("False", "خطأ", true),
                ]),
            new(
                AwarenessQuestionType.SingleChoice,
                "You receive an unexpected call asking you to allow remote support access immediately. What should you do?",
                "تتلقى مكالمة غير متوقعة تطلب منك السماح فوراً بالوصول للدعم عن بُعد. ماذا تفعل؟",
                "Do not allow unknown remote-support requests. Verify technician identity and use only approved support processes.",
                "لا تسمح بطلبات الدعم عن بُعد غير المعروفة. تحقق من هوية الفني واستخدم فقط عمليات الدعم المعتمدة.",
                1,
                [
                    new("Allow access immediately so the issue can be fixed.", "اسمح بالوصول فوراً حتى تُحل المشكلة.", false),
                    new("Do not allow unknown remote support; verify through a known IT channel first.", "لا تسمح بدعم عن بُعد غير معروف؛ تحقق أولاً عبر قناة تقنية معلومات معروفة.", true),
                    new("Share your password so they can connect without remote tools.", "شارك كلمة مرورك حتى يتصلوا دون أدوات عن بُعد.", false),
                    new("Allow access only if they promise to disconnect after five minutes.", "اسمح بالوصول فقط إذا وعدوا بالقطع بعد خمس دقائق.", false),
                ]),
            new(
                AwarenessQuestionType.MultipleChoice,
                "If a company device is lost or stolen, which actions are appropriate? (Select all that apply.)",
                "إذا فُقد جهاز تابع للشركة أو سُرق، أي الإجراءات مناسبة؟ (اختر كل ما ينطبق.)",
                "Report immediately even if the device has a password. IT may need to protect accounts and data quickly. Do not delay reporting.",
                "أبلغ فوراً حتى لو كان الجهاز محمياً بكلمة مرور. قد تحتاج تقنية المعلومات إلى حماية الحسابات والبيانات بسرعة. لا تؤخر الإبلاغ.",
                1,
                [
                    new("Report the loss or theft immediately.", "أبلغ عن الفقدان أو السرقة فوراً.", true),
                    new("Wait a week in case the device turns up.", "انتظر أسبوعاً فقد يظهر الجهاز.", false),
                    new("Report even if the device is password-protected.", "أبلغ حتى لو كان الجهاز محمياً بكلمة مرور.", true),
                    new("Assume nothing can happen because the screen lock is strong.", "افترض أنه لن يحدث شيء لأن قفل الشاشة قوي.", false),
                ]),
        ]);
}
