# SSRF value and severity decision

## Decision

Recommended severity: **Medium / defense-in-depth**.

## Rationale

The candidate is valuable because workflow-controlled declarative HTTP fields can reach a host-enabled HTTP request sink. This matters for hosts that accept untrusted workflow definitions or formula-controlled URL/header values.

The candidate should not be rated as default SSRF because declarative HTTP dispatch requires a host to configure an `IHttpRequestHandler`; without that handler, expected behavior is fail closed.

## Factors increasing severity

Severity may increase if a Microsoft first-party host:

- accepts declarative workflows from an untrusted or cross-tenant boundary;
- enables the built-in/default HTTP handler for those workflows;
- lacks host/scheme allowlists, private-address denylisting, metadata endpoint blocking, credential binding, redirect revalidation, and egress controls.

## Factors limiting severity

Severity is limited because:

- this is not default-on network behavior;
- the PoC is no-network and uses only fake/recording handlers;
- no real metadata endpoint, public target, credential, or token is accessed;
- hosts can mitigate by not enabling HTTP for untrusted workflows or by supplying a policy-enforcing handler.

## Recommended report framing

Frame as a conditional SSRF defense-in-depth issue in host-enabled declarative HTTP execution, not as an unconditional vulnerability in all Agent Framework applications.
