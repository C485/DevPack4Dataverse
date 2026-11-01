# Dataverse Documentation

Use the Microsoft Power Apps documentation repository as the source of truth when working with Dataverse code in this repository.

## Primary sources

- **Documentation repository:** https://github.com/MicrosoftDocs/powerapps-docs/tree/main
- **Dataverse developer docs:** https://github.com/MicrosoftDocs/powerapps-docs/tree/main/powerapps-docs/developer/data-platform
- **Published documentation:** https://learn.microsoft.com/power-apps/developer/data-platform/

## Local synced copy (required)

Always sync the documentation before working on Dataverse code. The local checkout lives at `docs/powerapps-docs` and is ignored by git.

```bash
# First time only
git clone --depth 1 --filter=blob:none --sparse https://github.com/MicrosoftDocs/powerapps-docs.git docs/powerapps-docs
git -C docs/powerapps-docs sparse-checkout set powerapps-docs/developer/data-platform

# Every subsequent Dataverse task
git -C docs/powerapps-docs pull --ff-only
```

Or run:

```powershell
pwsh ./docs/sync-dataverse-docs.ps1
```

Read the relevant pages from `docs/powerapps-docs/powerapps-docs/developer/data-platform/` before implementing or changing behavior.

## What to consult

| Area | Documentation path |
|---|---|
| SDK for .NET / Organization service | `powerapps-docs/developer/data-platform/org-service/` |
| Web API | `powerapps-docs/developer/data-platform/webapi/` |
| Plug-ins and custom workflow activities | `powerapps-docs/developer/data-platform/plug-ins/` |
| Metadata, tables, columns, and relationships | `powerapps-docs/developer/data-platform/` (metadata sections) |
| Solutions and ALM | `powerapps-docs/developer/data-platform/` (solution sections) |

## Related source code

- **Dataverse ServiceClient (open source):** https://github.com/microsoft/PowerPlatform-DataverseServiceClient
- The core SDK assemblies (`Microsoft.Xrm.Sdk`, `Microsoft.Crm.Sdk.Proxy`) are closed source; their API XML documentation ships in the `Microsoft.PowerPlatform.Dataverse.Client` NuGet package.

## Working agreement

1. Pull the latest documentation (`git -C docs/powerapps-docs pull --ff-only`) and check the relevant page before implementing or changing Dataverse behavior.
2. Prefer the documentation repository and the published Microsoft Learn pages over blog posts or outdated samples.
3. When behavior is non-obvious, reference the relevant documentation path in the PR description or code comment.
