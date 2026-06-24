# SSRF registration and callgraph notes

## Registration / enablement point

Declarative HTTP execution depends on a host-provided `IHttpRequestHandler`. The relevant option is `DeclarativeWorkflowOptions.HttpRequestHandler`.

If this option is not configured, declarative HTTP actions should fail closed instead of sending a request.

## Source-to-sink call chain

The reviewed source-to-sink path is:

1. Declarative workflow contains an HTTP action with workflow-controlled URL, query, headers, and optional body fields.
2. `HttpRequestExecutor.ExecuteAsync()` evaluates those fields.
3. `HttpRequestExecutor` constructs `HttpRequestInfo` containing the evaluated method, URL, headers, query parameters, body, timeout, and connection name.
4. `HttpRequestExecutor` calls `httpRequestHandler.SendAsync(requestInfo, cancellationToken)`.
5. With the built-in/default handler, `DefaultHttpRequestHandler.SendAsync()` builds an `HttpRequestMessage`.
6. `DefaultHttpRequestHandler` sends that message through `HttpClient.SendAsync()`.

## PoC mapping

The no-network PoC mirrors this callgraph with safe doubles:

- `RecordingHttpRequestHandler` verifies that workflow-controlled URL/query/header fields reach the handler boundary.
- A fake `HttpMessageHandler` verifies that the default-handler model constructs an `HttpRequestMessage` containing the expected URL/query/header values.

No public internet target, real metadata endpoint, real credential, or real token is used.
