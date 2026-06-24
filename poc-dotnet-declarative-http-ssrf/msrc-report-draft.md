# Draft report: .NET declarative HTTP request sink reachability

## Summary

The .NET declarative workflow HTTP action can carry workflow-controlled URL and header values to a host-configured HTTP request sink. In a deployment where untrusted users can provide declarative workflow definitions and the host explicitly enables HTTP dispatch without egress policy, those values may target loopback, link-local, private network, or metadata-looking URLs.

This is **not** a default SSRF claim. The host must configure an `IHttpRequestHandler` for declarative HTTP actions to dispatch. Without a configured handler, the expected behavior is fail closed.

## Impact framing

Potential impact is limited to hosts that both:

1. accept declarative workflow definitions or formula inputs from an untrusted boundary, and
2. configure HTTP execution with insufficient URL/header/redirect policy.

The PoC does not access public internet targets, the real `169.254.169.254` metadata endpoint, real credentials, or real tokens.

## Evidence planned by PoC

The no-network harness demonstrates:

- missing handler fails closed;
- a `RecordingHttpRequestHandler` receives a workflow-controlled localhost URL;
- a metadata-looking URL is captured only as a string by a fake handler;
- declarative headers reach the request object;
- `DefaultHttpRequestHandler` constructs an `HttpRequestMessage` visible to a fake `HttpMessageHandler`;
- redirect behavior is documented with a fake 302 response only and not tested against the network.

## Suggested remediation

For hosts that expose declarative workflow authoring across trust boundaries:

- require a policy-enforcing `IHttpRequestHandler` for declarative HTTP actions;
- allow only approved schemes and hostnames;
- block loopback, link-local, private address ranges, and metadata endpoints by default;
- bind credentials/headers to approved connections instead of accepting arbitrary workflow-authored secrets;
- disable or revalidate redirects;
- document the trust boundary for workflow definitions and HTTP action enablement.
