# Cybersecurity checklist strategy

## Purpose

QEC may use internal or regulator-style **cybersecurity checklists** and **external auditor questionnaires**. These are additional `Framework` records with `Requirement.Type = Question`.

## Rules

- Do not fork Internal Controls for each questionnaire
- Map questions to existing controls where valid
- Allow **unmapped** questions (gap) rather than fake mapping
- Completing a checklist produces answers + evidence links
- Roll-up does not auto-complete COBIT/ISO requirements except through shared evidence and mapped controls’ assessments

## External auditor questionnaires

Store as a framework or as `AuditEngagement` questions linked to FrameworkRequirement. Prefer mapping to controls to reuse evidence.
