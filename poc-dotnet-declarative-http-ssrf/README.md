# .NET declarative HTTP request sink no-network PoC

This directory contains a minimal, no-network proof of concept for the .NET declarative HTTP candidate described in the repository audit notes:

- `../agent-framework-poc-design.md` describes the AF-002 PoC shape: use a fake `IHttpRequestHandler` or fake `HttpMessageHandler`, construct a declarative `HttpRequestAction` with localhost/metadata-looking URL strings, and assert the handler receives the exact URL.
- `../agent-framework-candidate-list.md` scopes AF-002 to workflow-controlled declarative HTTP fields reaching the HTTP request sink when a host admits untrusted workflow definitions and supplies HTTP execution.

## Important scope statement

This PoC does **not** claim that Agent Framework is a default SSRF primitive. Declarative HTTP execution is host mediated. A host must explicitly enable/configure an `IHttpRequestHandler` for HTTP actions to dispatch. Without that host-provided handler, the expected behavior is fail closed.

The PoC only demonstrates reachability of declarative `HttpRequestAction.url` and `headers` into:

1. a host-supplied `IHttpRequestHandler` (`RecordingHttpRequestHandler`), and
2. the `HttpRequestMessage` constructed by `DefaultHttpRequestHandler` when its `HttpClient` is backed by a fake `HttpMessageHandler`.

## Safety rules

- Do not access public internet targets.
- Do not access the real `169.254.169.254` metadata endpoint.
- Do not read real credentials.
- Do not send real tokens.
- Use only `RecordingHttpRequestHandler` or a fake `HttpMessageHandler`.
- Do not replace the fake handler with a real `HttpClientHandler` for the localhost or metadata fixtures.

## Files

- `HttpSsrSinkHarness.cs` — console harness with A-F checks.
- `workflow-localhost.yaml` / `workflow-localhost.json` — harmless localhost URL and fake header fixtures.
- `workflow-metadata-string-only.yaml` — metadata-looking URL fixture that must be captured as a string only.
- `expected-safe-result.md` — expected hardened behavior and safe interpretation.
- `msrc-report-draft.md` — draft report text with limited, non-overstated impact language.

## Build/run status

This PoC has now been restored, built, and run with the current .NET SDK. The verified commands were:

```sh
cd poc-dotnet-declarative-http-ssrf
$HOME/.dotnet/dotnet build ./poc-dotnet-declarative-http-ssrf.csproj --no-restore
$HOME/.dotnet/dotnet run --project ./poc-dotnet-declarative-http-ssrf.csproj --no-restore
```

The first no-restore build reported missing `obj/project.assets.json`; because the PoC project has no `PackageReference` entries, restore was run to generate assets, then build and run succeeded. See `poc-run-results.md`.

## A-F coverage

A. Missing `IHttpRequestHandler` fails closed in the host dispatch shim.
B. `RecordingHttpRequestHandler` captures a workflow-controlled localhost URL without network I/O.
C. Metadata-looking URL is captured only as a string by a fake handler and never enters a real network path.
D. Declarative headers, including an intentionally fake bearer token, reach the recorded request object but are not sent.
E. Fake `HttpMessageHandler` captures the `HttpRequestMessage` constructed by `DefaultHttpRequestHandler`.
F. Redirect behavior is modeled only with a fake handler. The PoC documents that real redirect following, if enabled, is `HttpClientHandler` behavior rather than a demonstrated framework-level revalidation path.
