# PoC run results

Date: 2026-06-23
Working directory: `/workspace/agent-framework/poc-dotnet-declarative-http-ssrf`

## .NET SDK

Command:

```sh
$HOME/.dotnet/dotnet --info
```

Observed SDK/runtime summary:

- .NET SDK Version: `8.0.422`
- SDK Commit: `34560b12f2`
- MSBuild Version: `17.11.48+02bf66295`
- OS: `ubuntu 24.04`, `linux-x64`
- Host Version: `8.0.28`
- Installed SDK: `8.0.422 [/root/.dotnet/sdk]`

## File presence check

All requested PoC files were present before build/run:

- `poc-dotnet-declarative-http-ssrf.csproj`
- `HttpSsrSinkHarness.cs`
- `workflow-localhost.yaml`
- `workflow-localhost.json`
- `workflow-metadata-string-only.yaml`

## Build/restore/run commands

Initial no-restore build command:

```sh
$HOME/.dotnet/dotnet build ./poc-dotnet-declarative-http-ssrf.csproj --no-restore
```

Initial result: failed with `NETSDK1004` because `obj/project.assets.json` was missing.

The PoC project has no remote `PackageReference` entries, so restore was run only to generate assets for this project:

```sh
$HOME/.dotnet/dotnet restore ./poc-dotnet-declarative-http-ssrf.csproj
```

Restore result: succeeded.

Build after restore:

```sh
$HOME/.dotnet/dotnet build ./poc-dotnet-declarative-http-ssrf.csproj --no-restore
```

Build result: succeeded with `0 Warning(s)` and `0 Error(s)`.

Harness run:

```sh
$HOME/.dotnet/dotnet run --project ./poc-dotnet-declarative-http-ssrf.csproj --no-restore --no-build
```

Run result: succeeded with exit code `0`.

## A-F results

| Item | Result |
| --- | --- |
| A. 未配置 `IHttpRequestHandler` 时是否 fail closed | PASS: `missing IHttpRequestHandler fails closed before dispatch`. |
| B. `RecordingHttpRequestHandler` 是否捕获 workflow-controlled localhost URL | PASS: localhost URL was captured without network I/O. |
| C. metadata URL 是否只被字符串捕获，`NetworkSendCount` 是否为 0 | PASS: metadata URL was captured only as a string by the fake handler, and `NetworkSendCount == 0`. |
| D. declarative headers 是否进入 request，且只包含 fake token | PASS: headers reached `HttpRequestInfo`; bearer value was `Bearer fake-token-do-not-send`. |
| E. fake `HttpMessageHandler` 是否捕获 `DefaultHttpRequestHandler` 构造出的 request | PASS: fake handler captured the constructed request. |
| F. redirect 是否只是 fake 302 链路/文档建模，是否有框架级 revalidation | PASS: redirect was modeled with a fake 302 only. No framework-level redirect revalidation was observed in this fake chain; real redirect following, if enabled, is `HttpClientHandler` behavior. |

Harness output:

```text
No-network declarative HTTP sink PoC results:
PASS A: missing IHttpRequestHandler fails closed before dispatch.
PASS B: RecordingHttpRequestHandler captured workflow-controlled localhost URL without network I/O.
PASS C: metadata URL was captured only as a string by the fake handler; no real HttpClient/network path was used.
PASS D: declarative headers reached HttpRequestInfo in the recording handler; token value is fake and was not sent.
PASS E: fake HttpMessageHandler captured the request constructed by DefaultHttpRequestHandler.
PASS F: redirect was modeled with a fake handler only; no framework-level redirect revalidation was observed in this fake chain. Real redirect following, if enabled, is HttpClientHandler behavior.
```

## Safety confirmation

- No public internet target was accessed by the harness.
- The real `169.254.169.254` endpoint was not accessed.
- No real credentials were read.
- No real token was sent.
- The harness used only `RecordingHttpRequestHandler` and fake `HttpMessageHandler` paths.
- Framework source files were not modified.
- The result is not a default SSRF claim; it only demonstrates declarative URL/header reachability to configured/fake request sinks.
