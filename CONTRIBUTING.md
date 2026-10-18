# Contributing

Read AGENTS.md, PROJECT_SPEC.md and docs/ARCHITECTURE.md.
Use .NET SDK pinned by global.json, Windows x64 and PowerShell 7. Run scripts/verify.ps1.
Do not commit real profiles, tokens, environment files, personal executable paths or screenshots containing secrets.
Keep all production UI text in the RU/EN resources. Do not introduce mock connection states.
Run core parser checks without starting TUN; privileged acceptance is explicit and isolated.
Bug reports should include app/Windows version, safe error code and reproduction steps, never subscription credentials.
