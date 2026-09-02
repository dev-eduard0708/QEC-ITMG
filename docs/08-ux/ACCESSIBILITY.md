# Accessibility

Target **WCAG 2.2 AA** for QEC ITMG UI (engine UIs are third-party).

- Keyboard: queues, dialogs, consent
- Focus visible
- Labels on every input
- Status not color-only
- `aria-live` for toast/notifications
- Contrast
- Charts have tables
- i18n and `lang`

Test: axe in E2E smoke + manual keyboard pass per major screen.
