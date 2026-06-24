# Title

.NET Agent Framework declarative HTTP actions can route workflow-controlled URLs and headers to a host-enabled HTTP request sink without observed framework-level SSRF policy


# Reviewed version

- Reviewed commit: `3ddd72984507d2ed7a5f913bba2e052ed5a75134`
- Reviewed branch: `work`
- Package/version if applicable: source checkout only; `dotnet/nuget/nuget-package.props` declares `VersionPrefix` `1.11.0`, but this PoC links repository source files directly rather than consuming a published NuGet package.

# Summary

This report describes a conditional SSRF defense-in-depth issue in the .NET Agent Framework declarative workflow HTTP action path.

This is **not** a default SSRF report. Declarative HTTP dispatch requires a host to explicitly configure/enable an `IHttpRequestHandler`. The framework option documentation states that if no HTTP request handler is set, HTTP request actions fail. The no-network PoC confirmed the same fail-closed behavior in the harness.

If a host enables the built-in/default HTTP request handler for workflows supplied by an untrusted boundary, workflow-controlled declarative `HttpRequestAction` URL, query, and header values can reach an HTTP request sink. In the currently reviewed default handler path, I did not observe framework-level SSRF policy such as host allowlisting, scheme allowlisting, loopback/private/link-local/metadata denylisting, or redirect target revalidation. Declarative headers are copied into the outgoing request object.

Recommended severity: **Medium / defense-in-depth**, unless a Microsoft first-party host enables this handler for untrusted workflows without compensating egress or URL/header policy.

I did not test Microsoft internal, metadata, loopback, or private-network targets. I only verified framework-level and fake/no-network behavior. If any Microsoft first-party hosted workflow runtime accepts untrusted declarative workflows and enables this handler without compensating policy, impact may increase. Please validate first-party hosted deployments internally.

# Affected component

- Component: .NET Agent Framework declarative workflows HTTP action path.
- Primary types/files reviewed:
  - `IHttpRequestHandler`, `HttpRequestInfo`, and `HttpRequestResult` in `dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/IHttpRequestHandler.cs`.
  - `DefaultHttpRequestHandler` in `dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/DefaultHttpRequestHandler.cs`.
  - `HttpRequestExecutor` in `dotnet/src/Microsoft.Agents.AI.Workflows.Declarative/ObjectModel/HttpRequestExecutor.cs` for the local source-to-handler call chain in the reviewed checkout.
- PoC directory: `poc-dotnet-declarative-http-ssrf/`.

# Security boundary

The relevant boundary is between an untrusted or lower-trust workflow author and the host process/network environment that executes declarative workflows.

The issue requires a host to cross that boundary by accepting untrusted declarative workflow definitions or formula-controlled HTTP fields and enabling HTTP dispatch for those workflows. Without a configured `IHttpRequestHandler`, this should fail closed and is not a network SSRF primitive.

# Preconditions

All of the following are required for security impact:

1. A host accepts declarative workflow definitions, workflow inputs, or formula outputs from an untrusted or cross-tenant source.
2. The host explicitly configures/enables an `IHttpRequestHandler` for declarative HTTP actions.
3. The handler used by the host is the built-in/default HTTP request handler or another handler without equivalent SSRF policy.
4. The host lacks compensating controls such as egress policy, URL admission control, host/scheme allowlists, metadata/loopback/private-address blocks, credential binding, and redirect revalidation.

# Source-to-sink call chain

Static source review indicates this path:

1. The reviewed public contract for host HTTP execution is `IHttpRequestHandler`; it defines the handler boundary for HTTP requests emitted by declarative workflow `HttpRequestAction` actions. In the reviewed checkout, local host wiring passes a configured handler into the HTTP executor; if no handler is configured, the expected behavior is fail closed.
2. `HttpRequestExecutor.ExecuteAsync()` evaluates declarative `HttpRequestAction` fields: method, URL, headers, query parameters, body, timeout, conversation ID, and connection name.
3. `HttpRequestExecutor` creates `HttpRequestInfo` with the evaluated URL, headers, query parameters, body, timeout, and connection name.
4. `HttpRequestExecutor` calls `httpRequestHandler.SendAsync(requestInfo, cancellationToken)`.
5. In the default handler path, `DefaultHttpRequestHandler.SendAsync()` selects a provided or owned `HttpClient`, builds an `HttpRequestMessage`, and calls `HttpClient.SendAsync()`.
6. `DefaultHttpRequestHandler.BuildHttpRequestMessage()` copies declarative headers into the `HttpRequestMessage`; `ResolveRequestUri()` appends declarative query parameters to the URL string.

Current review did not identify framework-level checks in this path for:

- host allowlist;
- scheme allowlist;
- loopback/private/link-local/metadata denylist;
- redirect target revalidation;
- blocking declarative headers from entering the request.

# PoC steps

The PoC is intentionally no-network and uses only fake/recording handlers.

Commands executed in `poc-dotnet-declarative-http-ssrf/`:

```sh
$HOME/.dotnet/dotnet --info
$HOME/.dotnet/dotnet build ./poc-dotnet-declarative-http-ssrf.csproj --no-restore
$HOME/.dotnet/dotnet restore ./poc-dotnet-declarative-http-ssrf.csproj
$HOME/.dotnet/dotnet build ./poc-dotnet-declarative-http-ssrf.csproj --no-restore
$HOME/.dotnet/dotnet run --project ./poc-dotnet-declarative-http-ssrf.csproj --no-restore --no-build
```

The first `--no-restore` build failed only because `obj/project.assets.json` was missing. The PoC project has no `PackageReference` entries, so restore was used to generate local build assets, then build and run succeeded.

The harness checks:

A. Missing `IHttpRequestHandler` fails closed before dispatch.
B. `RecordingHttpRequestHandler` captures a workflow-controlled localhost URL and query parameters without network I/O.
C. A metadata-looking URL is captured only as a string by the fake handler, with `NetworkSendCount == 0`.
D. Declarative headers reach the request object and contain only the fake token `Bearer fake-token-do-not-send`.
E. A fake `HttpMessageHandler` captures the request constructed by the model harness.
F. Redirect behavior is modeled only with a fake `302`; no real redirect/network request is made.
G-I. A real-source integration harness compiles the repository source files for the real `IHttpRequestHandler`, `HttpRequestInfo`, `HttpRequestResult`, and `DefaultHttpRequestHandler` types, then exercises request construction, metadata-string-only handling, and fake redirect behavior through a fake `HttpMessageHandler`.

# Actual result

The no-network harness completed successfully with A-F all passing:

```text
PASS A: missing IHttpRequestHandler fails closed before dispatch.
PASS B: RecordingHttpRequestHandler captured workflow-controlled localhost URL without network I/O.
PASS C: metadata URL was captured only as a string by the fake handler; no real HttpClient/network path was used.
PASS D: declarative headers reached HttpRequestInfo in the recording handler; token value is fake and was not sent.
PASS E: fake HttpMessageHandler captured the request constructed by DefaultHttpRequestHandler.
PASS F: redirect was modeled with a fake handler only; no framework-level redirect revalidation was observed in this fake chain. Real redirect following, if enabled, is HttpClientHandler behavior.
PASS G: real source DefaultHttpRequestHandler + IHttpRequestHandler types constructed the expected request through a fake HttpMessageHandler.
PASS H: real source DefaultHttpRequestHandler saw metadata-looking URL only through the fake HttpMessageHandler; no real network handler was used.
PASS I: real source DefaultHttpRequestHandler fake redirect remained a fake 302 response; no real redirect/network follow was performed.
```

The PoC did not access public internet targets, did not access the real `169.254.169.254` endpoint, did not read real credentials, and did not send a real token.

# Expected result

For hosts that enable declarative HTTP for untrusted workflows, expected safe behavior would be one of:

- fail closed unless a policy-enforcing handler is configured;
- enforce allowlists for scheme, host, port, and/or connection name before dispatch;
- reject loopback, private, link-local, and cloud metadata targets by default;
- prevent arbitrary workflow-authored credentials/authorization headers from being sent outside approved connection policy;
- disable redirects or revalidate each redirect target with the same policy;
- document that the built-in/default HTTP handler is suitable only for trusted workflows or policy-controlled hosts.

# Impact

In a host that accepts untrusted declarative workflows and enables the built-in/default HTTP handler without additional policy, an attacker-controlled workflow may be able to route HTTP requests toward network locations visible from the host process, including loopback, private network, link-local, or metadata-style URLs. Declarative headers may also reach the request object, so hosts should avoid passing workflow-authored secrets/tokens to arbitrary destinations.

Recommended severity is **Medium / defense-in-depth** because the issue is conditional on host enablement and an untrusted workflow boundary. Severity may increase if a Microsoft first-party service enables this handler for untrusted workflows without compensating controls.

I did not test Microsoft internal, metadata, loopback, or private-network targets. I only verified framework-level and fake/no-network behavior. If any Microsoft first-party hosted workflow runtime accepts untrusted declarative workflows and enables this handler without compensating policy, impact may increase. Please validate first-party hosted deployments internally.

# Limitations / non-claims

- This is not a default SSRF claim.
- This does not show that all Agent Framework applications are vulnerable.
- This does not show that an HTTP request is sent when no `IHttpRequestHandler` is configured.
- I did not test Microsoft internal, metadata, loopback, or private-network targets.
- I only verified framework-level and fake/no-network behavior.
- The PoC does not contact public internet targets.
- The PoC does not contact the real `169.254.169.254` endpoint.
- The PoC does not read real credentials.
- The PoC does not send real tokens.
- The PoC uses only `RecordingHttpRequestHandler` and fake `HttpMessageHandler` paths.
- The PoC's redirect check is fake 302 modeling only; it does not perform a real redirect follow test.

# Suggested mitigations

For framework and host owners:

1. Provide or recommend a policy-enforcing `IHttpRequestHandler` for untrusted declarative workflows.
2. Add URL admission controls before constructing/sending requests: allowed schemes, allowed hosts, allowed ports, and optional connection-name binding.
3. Deny loopback, private, link-local, and cloud metadata address ranges by default unless explicitly allowed by trusted host configuration.
4. Disable automatic redirect following or revalidate every redirect target against the same URL policy.
5. Bind credentials and authorization headers to approved connections; do not allow arbitrary workflow-authored authorization headers to arbitrary hosts.
6. Document that the default/built-in handler should only be used with trusted workflows or with external egress controls.
7. Add tests covering localhost, link-local/metadata strings, private network ranges, scheme restrictions, header propagation, and redirect target policy.

# Evidence files

See `evidence-index.md` for the evidence inventory. Primary PoC artifacts:

- `poc-run-results.md`
- `HttpSsrSinkHarness.cs`
- `RealSourceIntegrationHarness.cs`
- `workflow-localhost.yaml`
- `workflow-localhost.json`
- `workflow-metadata-string-only.yaml`
- `expected-safe-result.md`

# Tested environment

- Date: 2026-06-23
- OS: Ubuntu 24.04, `linux-x64`
- .NET SDK: `8.0.422`
- MSBuild: `17.11.48+02bf66295`
- .NET Host: `8.0.28`
- Working directory: `/workspace/agent-framework/poc-dotnet-declarative-http-ssrf`

# Timeline / disclosure note

- 2026-06-23: No-network PoC created and verified locally with fake/recording handlers only.
- 2026-06-23: Final MSRC report draft prepared.

This report is intended for coordinated disclosure/triage. It intentionally avoids live SSRF testing and does not attempt to access real metadata services, public internet targets, credentials, or tokens.
