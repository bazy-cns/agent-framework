# SSRF policy and redirect analysis

## Policy checks not observed in the reviewed path

For the default-handler path reviewed for this candidate, no framework-level policy was observed for:

- approved host allowlisting;
- approved scheme allowlisting;
- loopback/private/link-local/metadata target denylisting;
- DNS or address-family pinning;
- binding workflow-authored headers to an approved connection policy;
- per-redirect target revalidation.

## Header behavior

Declarative headers can enter the request object. The PoC uses only the fake value `Authorization: Bearer fake-token-do-not-send` and verifies header reachability without transmitting any real token.

## Redirect behavior

The PoC does not perform real redirect following. It models a fake `302` response through a fake `HttpMessageHandler` only.

The observed conclusion is limited: in the fake chain, no framework-level redirect target revalidation was observed. If a host uses an `HttpClientHandler` configuration that follows redirects, redirect following should be treated as handler/platform behavior and should be disabled or revalidated by host policy for untrusted workflows.

## Safe host policy

Hosts that enable declarative HTTP for untrusted workflows should:

1. allow only approved schemes and hosts;
2. deny loopback, private, link-local, and metadata targets by default;
3. bind credentials and headers to approved connections;
4. disable redirects or revalidate each redirect target;
5. apply network egress controls outside the process as defense in depth.
