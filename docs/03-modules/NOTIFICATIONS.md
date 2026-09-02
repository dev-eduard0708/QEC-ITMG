# Notifications

Related: [../01-architecture/REALTIME-ARCHITECTURE.md](../01-architecture/REALTIME-ARCHITECTURE.md)

## Channels (v1)

In-app + email. Future: Teams, SMS, push.

## Model

`Notification`, `NotificationTemplate`, `NotificationChannel`, recipient, `DeliveryAttempt` (status, retry), user `NotificationPreference` (digest vs immediate; cannot disable legally required security notices such as remote session requests).

## Events (initial catalog)

Ticket assigned/updated; SLA warning/breach; change approval required; remote request; policy review due; evidence expiry; audit action due; finding overdue; vulnerability overdue; contract expiry; certificate expiry; backup failure; DR review due.

## Sending

Hangfire: render template, send, retry with backoff. In-app insert in same transaction as triggering use case when possible; email always async.

## Permissions

Users read own notifications. `notification.admin` for templates.
