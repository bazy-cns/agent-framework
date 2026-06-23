# Trust checkpoints and boundary notes

## Python

- Checkpoint files are a trusted storage boundary. The encoding module explicitly documents that pickle can execute arbitrary code and that checkpoint storage must not be writable by untrusted parties.
- `FileCheckpointStorage` defaults to restricted unpickling by passing an empty `frozenset` of additional allowed types to `decode_checkpoint_value`, which means built-ins, framework-internal types, and OpenAI SDK types are allowed but arbitrary application classes are blocked unless configured.
- The public `decode_checkpoint_value` helper keeps backward-compatible unrestricted behavior when `allowed_types=None`; this is the main remaining deserialization edge for custom storage or direct helper use.
- Workflow checkpoints carry `workflow_name` and `graph_signature_hash`; restore is expected to validate graph compatibility before importing state.
- MCP servers are untrusted for sampling; the Python MCP wrapper denies server-initiated sampling by default and filters tool-call kwargs by declared schema plus construction-time extras.
- File access/memory harnesses are intentionally model-facing tools, but the disk-backed store is scoped by a root and rejects traversal/symlink escapes.

## .NET

- Workflow/session checkpoint state uses JSON/System.Text.Json rather than BinaryFormatter-style unsafe polymorphic deserialization in the reviewed paths.
- `FileSystemJsonCheckpointStore` derives file names from session/checkpoint IDs and URL-encodes path-sensitive characters before writing/reading checkpoint files.
- `FileSystemAgentSessionStore` stores hosted/local sessions under `.checkpoints` or `/.checkpoints` and sanitizes agent/conversation IDs before composing paths.
- Declarative HTTP and MCP actions are trust-sensitive: workflow model expressions can evaluate to outbound URLs, headers, tool names, server URLs, and arguments. The default HTTP handler does not appear to implement SSRF policy; hosts should supply policy in admission control or custom handlers.
- MCP approval in declarative workflows is model-controlled via `requireApproval`; if an untrusted author can set it false, policy must be enforced outside the executor.

## Manual validation priority

1. Confirm whether declarative workflows are intended to be accepted from untrusted tenants in any default hosting surface.
2. Confirm whether hosted declarative HTTP uses a hardened handler or egress policy that supersedes `DefaultHttpRequestHandler`.
3. Confirm whether declarative MCP server URLs are restricted by `DefaultMcpToolHandler`, connection registry policy, or Foundry hosting before network/tool invocation.
4. Confirm whether any non-file Python checkpoint backend calls `decode_checkpoint_value` with `allowed_types=None` on externally writable storage.
