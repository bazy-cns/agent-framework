# SSRF default exposure analysis

## Finding summary

The declarative HTTP candidate is **not** a default SSRF exposure. HTTP dispatch is host mediated: a host must explicitly configure an `IHttpRequestHandler` for declarative HTTP actions to execute.

## Default / no-handler behavior

The expected safe default is fail closed when no handler is configured. The PoC harness records this as check A: missing `IHttpRequestHandler` fails before dispatch.

## Exposure condition

Potential exposure begins only when all of the following are true:

1. A host accepts declarative workflows, workflow inputs, or formula-derived HTTP fields from an untrusted boundary.
2. The host enables declarative HTTP dispatch by configuring an `IHttpRequestHandler`.
3. The configured handler is the built-in/default handler or another handler without equivalent URL, header, egress, and redirect policy.
4. The host lacks compensating network egress controls.

## Observed default-handler policy gap

Static review of the default-handler path did not identify framework-level checks for:

- host allowlist;
- scheme allowlist;
- loopback/private/link-local/metadata denylist;
- redirect target revalidation;
- blocking declarative headers from entering the request object.

## Severity framing

Recommended severity remains **Medium / defense-in-depth** unless a Microsoft first-party host enables this handler for untrusted workflows without compensating controls.
