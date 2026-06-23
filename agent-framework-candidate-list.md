# Agent Framework static security candidate list

Scope: static-only audit of Python and .NET Agent Framework workflow, tool-calling, checkpoint, state, memory, serialization, connector, HTTP, file, and credential paths. No dependencies were installed and no build/test/audit commands were run.

## Candidate AF-001: Python unrestricted checkpoint decode helper remains a dangerous public sink

- **value:** Medium
- **source:** untrusted checkpoint JSON or any attacker-controlled object graph containing `{"__pickled__": "...", "__type__": "..."}` markers.
- **entry:** direct or indirect application call to `agent_framework._workflows._checkpoint_encoding.decode_checkpoint_value(value)` without `allowed_types`, or any custom checkpoint backend that calls the helper with `allowed_types=None`.
- **sink:** Python pickle deserialization (`pickle.loads`) before the post-decode type check.
- **static reachability:** `decode_checkpoint_value` delegates to `_decode`; `_decode` recognizes dicts containing `_PICKLE_MARKER` and `_TYPE_MARKER`, calls `_base64_to_unpickle`, then calls `_verify_type`; `_base64_to_unpickle` uses unrestricted `pickle.loads` when `allowed_types is None`. The module-level security model explicitly says checkpoint storage is trusted and pickle can execute arbitrary code.
- **blocker analysis:** The default `FileCheckpointStorage` constructor normalizes `allowed_checkpoint_types` to an empty `frozenset`, so its `load()` path uses the restricted unpickler. This blocks the normal file-backed default path. However, the public helper defaults to unrestricted mode for backward compatibility, and in-memory/custom checkpoint storage can still pass checkpoint-shaped attacker data into the helper if developers treat it as a safe decoder.
- **call chain:** `decode_checkpoint_value(value, allowed_types=None)` -> `_decode()` -> `_base64_to_unpickle(encoded, allowed_types=None)` -> `pickle.loads(pickled)` -> `_verify_type()`.
- **why valuable:** This is not a prompt-injection issue; it is a code-level deserialization sink reachable from checkpoint restore utilities. It is downgraded from High because the built-in file storage default now supplies a restricted allowlist and the docs warn that checkpoints are trusted.
- **likely safe result:** Any checkpoint decode entry point exposed to untrusted input should either reject pickle markers outright, require non-`None` `allowed_types`, or use the durable-task marker-stripping pattern before decoding.
- **PoC idea:** In a throwaway local script only, call `decode_checkpoint_value` with a harmless pickle marker whose reducer returns a benign marker string or integer; verify unrestricted mode accepts it, then verify `allowed_types=frozenset()` blocks non-allowlisted globals. Do not create repo PoC files.

## Candidate AF-002: .NET declarative HTTP action can dispatch workflow-controlled URLs without built-in SSRF policy

- **value:** Medium
- **source:** declarative workflow model fields or state/formula outputs controlling `HttpRequestAction.Url`, method, headers, body, and query parameters.
- **entry:** execution of a declarative workflow containing `HttpRequestAction`.
- **sink:** `HttpClient.SendAsync` in the default HTTP request handler.
- **static reachability:** `HttpRequestExecutor.ExecuteAsync` evaluates the model URL with `GetUrl()`, builds `HttpRequestInfo`, and passes it to `IHttpRequestHandler.SendAsync`. `DefaultHttpRequestHandler.SendAsync` validates only that URL/method strings are non-empty, builds `HttpRequestMessage` from the string URL, appends query parameters, and calls `HttpClient.SendAsync`.
- **blocker analysis:** No scheme/host/IP allowlist, metadata-IP denylist, loopback/private-network denylist, DNS pinning, or credential-scope binding is visible in the default handler. There is an `httpClientProvider` extension point and a `ConnectionName` field for hosted scenarios, but the default fallback uses a plain internally-owned `HttpClient`; therefore SSRF protection appears caller-supplied rather than default. This candidate depends on a host loading workflow definitions or formula inputs from an untrusted boundary.
- **call chain:** `HttpRequestExecutor.ExecuteAsync()` -> `GetUrl()` / `GetHeaders()` / `GetQueryParameters()` -> `new HttpRequestInfo { Url = url, ... }` -> `DefaultHttpRequestHandler.SendAsync()` -> `BuildHttpRequestMessage()` -> `ResolveRequestUri()` -> `HttpClient.SendAsync()`.
- **why valuable:** This is a concrete network sink controlled by declarative workflow data, not ordinary LLM output. A multi-tenant host that accepts user-authored declarative workflows could expose internal services or metadata endpoints unless it supplies a hardened handler.
- **likely safe result:** Default local execution should reject loopback, link-local, private ranges, non-HTTP(S) schemes, and metadata endpoints unless an explicit trusted handler/allowlist opts in.
- **PoC idea:** Use a no-network unit harness with a fake `IHttpRequestHandler` or custom `HttpMessageHandler` to assert that a workflow URL like `http://127.0.0.1/marker` reaches the handler. Do not perform real network access.

## Candidate AF-003: .NET declarative MCP action can route workflow-controlled server URL/tool name to remote tool invocation

- **value:** Medium
- **source:** declarative workflow model fields or formula/state outputs controlling `InvokeMcpToolAction.ServerUrl`, `ToolName`, arguments, and approval flag.
- **entry:** execution of a declarative workflow containing `InvokeMcpToolAction`.
- **sink:** MCP remote tool invocation through `mcpToolHandler.InvokeToolAsync`.
- **static reachability:** `InvokeMcpToolExecutor.ExecuteAsync` evaluates `serverUrl`, `serverLabel`, `toolName`, `requireApproval`, `arguments`, `headers`, and `connectionName`. If `requireApproval` is false, the executor directly invokes `mcpToolHandler.InvokeToolAsync(serverUrl, serverLabel, toolName, arguments, headers, connectionName, ...)`. If approval is required, the approval request exposes tool name and arguments but intentionally excludes transport headers.
- **blocker analysis:** There is an approval flag in the declarative model, but no visible default allowlist on server URL or tool name in this executor. The security boundary likely depends on how workflow definitions are admitted and how `DefaultMcpToolHandler` or hosting layers constrain MCP servers/connections. Candidate remains Medium because the workflow author may be trusted in many deployments, and a separate handler may add policy.
- **call chain:** `InvokeMcpToolExecutor.ExecuteAsync()` -> `GetServerUrl()` / `GetToolName()` / `GetRequireApproval()` / `GetArguments()` -> direct `mcpToolHandler.InvokeToolAsync(...)` when approval is false, or external approval request then resume with captured snapshot.
- **why valuable:** This is a code-level tool routing sink: workflow-controlled data chooses a remote MCP server and tool. In a system where users can upload declarative workflows, this can cross tool-permission or network boundaries.
- **likely safe result:** Hosts should bind declarative MCP actions to a pre-approved server registry/connection name, require approval by default for untrusted definitions, and reject arbitrary server URLs/tool names not in policy.
- **PoC idea:** Use a fake `IMcpToolHandler`/test double and a declarative action with harmless `serverUrl="https://example.invalid/mcp"`, `toolName="marker_tool"`, and `requireApproval=false`; verify the executor attempts to invoke the exact server/tool without contacting a real server.
