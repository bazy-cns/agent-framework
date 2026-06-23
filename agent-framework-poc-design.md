# Harmless PoC design notes only

No PoC files were generated. These are minimal manual validation designs for reviewers.

## AF-001 unrestricted Python checkpoint decode

- Create a temporary script outside the repository or use an interactive scratch shell.
- Build a checkpoint-shaped dict with `__pickled__` and `__type__` markers whose pickle payload returns a harmless marker string/integer only.
- Call `decode_checkpoint_value(payload)` and observe that unrestricted mode attempts to unpickle.
- Repeat with `decode_checkpoint_value(payload, allowed_types=frozenset())` and observe non-allowlisted globals are blocked.
- Do not use payloads that run commands, read files, import network clients, or access secrets.

## AF-002 declarative HTTP SSRF reachability

- Use a fake `IHttpRequestHandler` or fake `HttpMessageHandler`; do not perform real network requests.
- Construct the smallest declarative `HttpRequestAction` whose URL expression evaluates to `http://127.0.0.1/marker` or `http://169.254.169.254/marker`.
- Execute the action in a test harness and assert the handler receives the exact URL.
- Safe expected hardened behavior would reject the URL before dispatch.

## AF-003 declarative MCP server/tool routing

- Use a fake `IMcpToolHandler`; do not start or contact an MCP server.
- Construct a minimal `InvokeMcpToolAction` with `ServerUrl = "https://example.invalid/mcp"`, `ToolName = "marker_tool"`, harmless scalar arguments, and `RequireApproval = false`.
- Execute the action and assert the fake handler is called with the exact server URL and tool name.
- Safe expected hardened behavior would reject non-registered server URLs or force approval/policy checks before invocation.
