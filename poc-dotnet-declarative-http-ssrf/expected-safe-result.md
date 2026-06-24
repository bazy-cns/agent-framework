# Expected safe result

This candidate should be interpreted narrowly: declarative HTTP fields can reach a configured HTTP request sink. It is not a claim that default Agent Framework execution automatically performs SSRF.

## Safe behavior for hosts

A host that accepts workflow definitions from an untrusted or cross-tenant boundary should apply policy before enabling HTTP dispatch. Safe behavior includes one or more of:

- fail closed when no `IHttpRequestHandler` is configured;
- require explicit allowlists for scheme, host, port, and connection name;
- reject loopback, link-local, private-network, and cloud metadata address ranges unless the host has a very specific trusted use case;
- avoid sending user-controlled authorization headers or bind credentials to predeclared connection policy;
- disable automatic redirect following or revalidate each redirect target with the same policy;
- use egress controls outside the process as defense in depth.

## Expected PoC observations

- `workflow-localhost.*` must be captured only by `RecordingHttpRequestHandler` or the fake `HttpMessageHandler`.
- `workflow-metadata-string-only.yaml` must remain a string-only fixture. The real `169.254.169.254` endpoint must not be contacted.
- The fake `Authorization: Bearer fake-token-do-not-send` header proves header reachability only. It is not a real token and must not be replaced with one.
- Redirect behavior must not be tested with real network I/O. If a real `HttpClientHandler` is later used by an application, redirect following is handler behavior and should be controlled by host policy.
