# Logging and monitoring

- Serilog structured JSON
- Correlation id middleware
- Levels: Information for business commands, Warning authz fail, Error unexpected
- Metrics: request duration, Hangfire failures, SLA breaches count, engine errors
- Health: live (process), ready (SQL + disk)
- Alerting: via email/ops initially; SIEM later

Do not log passwords, tokens, full ticket bodies at Information.
