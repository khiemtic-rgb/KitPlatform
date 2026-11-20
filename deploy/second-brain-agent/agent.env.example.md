# Second Brain Agent environment sample

This file is a template. It contains no secrets. Do not copy a real password, JWT, or model API key into source control.

The service starts with the business-intent model disabled. An empty research publication root stays `MISSING` and does not block health, login, or the paper ledger.

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5095
SecondBrain__WebRoot=/opt/second-brain-agent/ui
SecondBrain__SystemIdentifier=<pg_control_system of the 5433 cluster>
PersonalIntelligence__ExchangeBackfill__WorkerEnabled=false
PersonalIntelligence__ResearchPublication__Root=
PersonalIntelligence__BusinessIntent__Enabled=false
```

Set these only in the service environment file, never in git:

- `ConnectionStrings__Default` — host `127.0.0.1`, port `5433`, database `second_brain_agent_v1`
- `SecondBrain__JwtSecret` — at least 32 characters, distinct from any pharmacy JWT
- `SecondBrain__BootstrapAdminPassword` — used only when `public.users` is empty

Enable a model only after a separate approval. All four values are required, and startup refuses an enabled but incomplete model:

- `PersonalIntelligence__BusinessIntent__Enabled=true`
- `PersonalIntelligence__BusinessIntent__BaseUrl` — OpenAI-compatible origin, for example `http://127.0.0.1:11434/v1`
- `PersonalIntelligence__BusinessIntent__ApiKey` — required non-empty bearer token; use a local dummy for a no-cost endpoint
- `PersonalIntelligence__BusinessIntent__Model`

Optional limits, rejected outside these ranges when the model is enabled:

- `PersonalIntelligence__BusinessIntent__MaxOutputTokens` default 512, maximum 1024
- `PersonalIntelligence__BusinessIntent__MaxCallsPerProcess` default 32, maximum 64
- `PersonalIntelligence__BusinessIntent__RequestTimeoutSeconds` default 20, range 5–45

There is one attempt per call. A timeout or a full call budget does not run a handler and does not write a decision or task.
