# OSLC log-relay client

Use `log-relay agent-context --json` before an automated workflow. Configure a
named profile non-interactively, then list sources and retrieve bounded lines:

```sh
log-relay profiles set --name strictdoc --url https://<relay>/ --api-key "$LOG_RELAY_API_KEY"
log-relay sources list --profile strictdoc --json
log-relay logs get --profile strictdoc --source application --limit 100 --json
```

Use `logs stream` only while actively diagnosing an integration. Use `contains`
with the Jazz trace identifier to correlate a request. All commands accept
`--json`; diagnostics go to stderr and exit codes are stable. `--output <file>`
routes command output to an explicitly selected local file.

Profile resolution is deterministic: `--url`/`--api-key`, then
`LOG_RELAY_URL`/`LOG_RELAY_API_KEY`, then `--profile`/`LOG_RELAY_PROFILE`, then
the `default` profile. Use `--force` only to replace or delete a saved profile.
There are no remote mutations or asynchronous jobs in this protocol.
