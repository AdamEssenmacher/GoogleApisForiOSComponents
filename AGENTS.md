# Agent Instructions (public OSS)

## Non-negotiables
- Documentation and samples must be generic (not app-specific).
- Do not reference private business context.
- Do not commit secrets; samples must rely on local config templates.

## Working agreements
- Keep changes reviewable and releaseable (small PR-sized diffs).
- Prefer minimal disruption to existing package IDs and build scripts.

## Quick commands
- Build and pack a single component (macOS):
  - `dotnet tool install -g cake.tool --version 0.38.5`
  - `dotnet cake --target=nuget --names=Google.SignIn`

## CI
- GitHub Actions should run build/pack validation without requiring any app secrets.
