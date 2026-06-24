// Copyright (c) Microsoft. All rights reserved.
// No-network integration check that compiles the real source files for
// Microsoft.Agents.AI.Workflows.Declarative.IHttpRequestHandler,
// HttpRequestInfo, HttpRequestResult, and DefaultHttpRequestHandler.

using System.Net;
using System.Text;
using RealDeclarative = Microsoft.Agents.AI.Workflows.Declarative;

internal static class RealSourceIntegrationHarness
{
    public static async Task<IReadOnlyList<string>> RunAsync()
    {
        List<string> results = [];

        results.Add(await TestRealDefaultHandlerBuildsRequestWithFakeHandlerAsync().ConfigureAwait(false));
        results.Add(await TestRealDefaultHandlerMetadataStringOnlyAsync().ConfigureAwait(false));
        results.Add(await TestRealDefaultHandlerFakeRedirectAsync().ConfigureAwait(false));

        return results;
    }

    private static async Task<string> TestRealDefaultHandlerBuildsRequestWithFakeHandlerAsync()
    {
        RealCapturingHttpMessageHandler fake = new(HttpStatusCode.OK, "{\"ok\":true}");
        using HttpClient client = new(fake);
        await using RealDeclarative.DefaultHttpRequestHandler handler = new(client);

        RealDeclarative.HttpRequestInfo request = new()
        {
            Method = "GET",
            Url = "http://127.0.0.1:65535/declarative-poc",
            QueryParameters = new Dictionary<string, string> { ["from"] = "real-source" },
            Headers = new Dictionary<string, string>
            {
                ["X-Poc-Marker"] = "real-source-default-handler",
                ["Authorization"] = "Bearer fake-token-do-not-send",
            },
        };

        RealDeclarative.HttpRequestResult result = await handler.SendAsync(request).ConfigureAwait(false);

        bool ok = result.IsSuccessStatusCode &&
            fake.Requests.Count == 1 &&
            fake.Requests[0].RequestUri?.ToString() == "http://127.0.0.1:65535/declarative-poc?from=real-source" &&
            fake.Requests[0].Headers.TryGetValues("X-Poc-Marker", out IEnumerable<string>? markerValues) &&
            markerValues.Single() == "real-source-default-handler" &&
            fake.Requests[0].Headers.TryGetValues("Authorization", out IEnumerable<string>? authValues) &&
            authValues.Single() == "Bearer fake-token-do-not-send";

        return ok
            ? "PASS G: real source DefaultHttpRequestHandler + IHttpRequestHandler types constructed the expected request through a fake HttpMessageHandler."
            : "FAIL G: real source DefaultHttpRequestHandler did not construct the expected fake-handler request.";
    }

    private static async Task<string> TestRealDefaultHandlerMetadataStringOnlyAsync()
    {
        RealCapturingHttpMessageHandler fake = new(HttpStatusCode.OK, "{}");
        using HttpClient client = new(fake);
        await using RealDeclarative.DefaultHttpRequestHandler handler = new(client);

        RealDeclarative.HttpRequestInfo request = new()
        {
            Method = "GET",
            Url = "http://169.254.169.254/latest/meta-data/iam/security-credentials/",
            Headers = new Dictionary<string, string>
            {
                ["X-Poc-Marker"] = "real-source-metadata-string-only",
            },
        };

        await handler.SendAsync(request).ConfigureAwait(false);

        bool ok = fake.Requests.Count == 1 &&
            fake.Requests[0].RequestUri?.ToString() == "http://169.254.169.254/latest/meta-data/iam/security-credentials/" &&
            fake.NetworkSendCount == 0;

        return ok
            ? "PASS H: real source DefaultHttpRequestHandler saw metadata-looking URL only through the fake HttpMessageHandler; no real network handler was used."
            : "FAIL H: real source metadata-string-only check behaved unexpectedly.";
    }

    private static async Task<string> TestRealDefaultHandlerFakeRedirectAsync()
    {
        RealCapturingHttpMessageHandler fake = new(
            HttpStatusCode.Redirect,
            string.Empty,
            location: "http://127.0.0.1:65535/redirect-target");
        using HttpClient client = new(fake);
        await using RealDeclarative.DefaultHttpRequestHandler handler = new(client);

        RealDeclarative.HttpRequestResult result = await handler.SendAsync(new RealDeclarative.HttpRequestInfo
        {
            Method = "GET",
            Url = "http://127.0.0.1:65535/declarative-poc",
        }).ConfigureAwait(false);

        bool ok = result.StatusCode == 302 && fake.Requests.Count == 1;
        return ok
            ? "PASS I: real source DefaultHttpRequestHandler fake redirect remained a fake 302 response; no real redirect/network follow was performed."
            : "FAIL I: real source fake redirect check behaved unexpectedly.";
    }

    private sealed class RealCapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;
        private readonly string? _location;

        public RealCapturingHttpMessageHandler(HttpStatusCode statusCode, string body, string? location = null)
        {
            _statusCode = statusCode;
            _body = body;
            _location = location;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        public int NetworkSendCount => 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(CloneRequestWithoutBody(request));

            HttpResponseMessage response = new(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };

            if (_location is not null)
            {
                response.Headers.Location = new Uri(_location);
            }

            return Task.FromResult(response);
        }

        private static HttpRequestMessage CloneRequestWithoutBody(HttpRequestMessage request)
        {
            HttpRequestMessage clone = new(request.Method, request.RequestUri);
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            return clone;
        }
    }
}
