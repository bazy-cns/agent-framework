# Evidence index

This index lists the files to attach or reference with the MSRC report. Paths are relative to `poc-dotnet-declarative-http-ssrf/` unless otherwise noted.

| Evidence file | Status in current branch | Purpose |
| --- | --- | --- |
| `poc-run-results.md` | Present | Records .NET SDK version, build/restore/run commands, A-F PASS results, and safety confirmation. |
| `HttpSsrSinkHarness.cs` | Present | No-network model harness using `RecordingHttpRequestHandler` and fake `HttpMessageHandler` paths. |
| `RealSourceIntegrationHarness.cs` | Present | No-network real-source integration harness that links repository source files for real declarative HTTP handler types and uses fake `HttpMessageHandler` transport. |
| `workflow-localhost.yaml` | Present | YAML fixture with workflow-controlled localhost URL and fake headers. |
| `workflow-localhost.json` | Present | JSON fixture with workflow-controlled localhost URL, query parameter, and fake headers. |
| `workflow-metadata-string-only.yaml` | Present | Metadata-looking URL fixture that must be captured only as a string. |
| `SSRF-default-exposure-analysis.md` | Present | Static/default-exposure analysis for the host-enabled handler condition. |
| `SSRF-registration-callgraph.md` | Present | Registration/callgraph analysis for the declarative HTTP source-to-sink path. |
| `SSRF-policy-and-redirect-analysis.md` | Present | Policy and redirect behavior analysis for the reviewed default-handler path. |
| `SSRF-value-decision.md` | Present | Value/severity decision record for Medium / defense-in-depth framing. |

## Evidence summary

- The verified PoC evidence is in `poc-run-results.md`, `HttpSsrSinkHarness.cs`, and `RealSourceIntegrationHarness.cs`.
- The workflow fixtures are safe strings only and use fake token values.
- No evidence file in this directory requires or performs public internet access, real metadata endpoint access, credential reads, or real token transmission.
- The supplemental `SSRF-*.md` files listed above are now present in this directory.
