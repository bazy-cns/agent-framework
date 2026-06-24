// Copyright (c) Microsoft. All rights reserved.
// Minimal no-network PoC harness for declarative HTTP request sink reachability.
// This file intentionally uses only RecordingHttpRequestHandler and fake HttpMessageHandler.

using System.Net;
using System.Text;
using System.Text.Json;

internal static class HttpSsrSinkHarness
{
    private const string LocalhostWorkflow = "workflow-localhost.json";
    private const string MetadataWorkflow = "workflow-metadata-string-only.yaml";

    public static async Task<int> Main()
    {
        List<string> results = [];

        WorkflowHttpRequest localhost = WorkflowHttpRequest.Load(LocalhostWorkflow);
        WorkflowHttpRequest metadata = WorkflowHttpRequest.Load(MetadataWorkflow);

        results.Add(TestFailClosedWhenHandlerMissing(localhost));
        results.Add(await TestRecordingHandlerCapturesLocalhostAsync(localhost).ConfigureAwait(false));
        results.Add(await TestMetadataIsStringOnlyAsync(metadata).ConfigureAwait(false));
        results.Add(await TestHeadersReachRequestAsync(localhost).ConfigureAwait(false));
        results.Add(await TestDefaultHandlerUsesFakeHttpMessageHandlerAsync(localhost).ConfigureAwait(false));
        results.Add(await TestRedirectDocumentedWithFakeHandlerAsync(localhost).ConfigureAwait(false));
        results.AddRange(await RealSourceIntegrationHarness.RunAsync().ConfigureAwait(false));

        Console.WriteLine("No-network declarative HTTP sink PoC results:");
        foreach (string result in results)
        {
            Console.WriteLine(result);
        }

        bool failed = results.Any(static result => result.StartsWith("FAIL", StringComparison.Ordinal));
        return failed ? 1 : 0;
    }

    private static string TestFailClosedWhenHandlerMissing(WorkflowHttpRequest workflow)
    {
        try
        {
            _ = DispatchThroughHostConfiguredHandler(workflow, httpRequestHandler: null);
            return "FAIL A: missing IHttpRequestHandler unexpectedly dispatched.";
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("IHttpRequestHandler", StringComparison.Ordinal))
        {
            return "PASS A: missing IHttpRequestHandler fails closed before dispatch.";
        }
    }

    private static async Task<string> TestRecordingHandlerCapturesLocalhostAsync(WorkflowHttpRequest workflow)
    {
        RecordingHttpRequestHandler recording = new();
        HttpRequestResult result = await DispatchThroughHostConfiguredHandler(workflow, recording).ConfigureAwait(false);

        IReadOnlyDictionary<string, string>? queryParameters = recording.Requests.Count == 1
            ? recording.Requests[0].QueryParameters
            : null;
        bool ok = result.IsSuccessStatusCode &&
            recording.Requests.Count == 1 &&
            recording.Requests[0].Url == "http://127.0.0.1:65535/declarative-poc" &&
            queryParameters is not null &&
            queryParameters.TryGetValue("from", out string? from) &&
            from == "workflow";

        return ok
            ? "PASS B: RecordingHttpRequestHandler captured workflow-controlled localhost URL without network I/O."
            : "FAIL B: RecordingHttpRequestHandler did not capture expected localhost URL.";
    }

    private static async Task<string> TestMetadataIsStringOnlyAsync(WorkflowHttpRequest workflow)
    {
        RecordingHttpRequestHandler recording = new();
        await DispatchThroughHostConfiguredHandler(workflow, recording).ConfigureAwait(false);

        bool ok = recording.Requests.Count == 1 &&
            recording.Requests[0].Url == "http://169.254.169.254/latest/meta-data/iam/security-credentials/" &&
            recording.NetworkSendCount == 0;

        return ok
            ? "PASS C: metadata URL was captured only as a string by the fake handler; no real HttpClient/network path was used."
            : "FAIL C: metadata URL was not safely string-captured by the fake handler.";
    }

    private static async Task<string> TestHeadersReachRequestAsync(WorkflowHttpRequest workflow)
    {
        RecordingHttpRequestHandler recording = new();
        await DispatchThroughHostConfiguredHandler(workflow, recording).ConfigureAwait(false);

        IReadOnlyDictionary<string, string>? headers = recording.Requests[0].Headers;
        bool ok = headers is not null &&
            headers.TryGetValue("X-Poc-Marker", out string? marker) &&
            marker == "declarative-header-reached-sink" &&
            headers.TryGetValue("Authorization", out string? authorization) &&
            authorization == "Bearer fake-token-do-not-send";

        return ok
            ? "PASS D: declarative headers reached HttpRequestInfo in the recording handler; token value is fake and was not sent."
            : "FAIL D: declarative headers did not reach the recorded request.";
    }

    private static async Task<string> TestDefaultHandlerUsesFakeHttpMessageHandlerAsync(WorkflowHttpRequest workflow)
    {
        CapturingHttpMessageHandler fake = new(HttpStatusCode.OK, "{\"ok\":true}");
        using HttpClient client = new(fake) { BaseAddress = new Uri("http://example.invalid/") };
        await using DefaultHttpRequestHandler handler = new(client);

        HttpRequestResult result = await handler.SendAsync(workflow.ToHttpRequestInfo()).ConfigureAwait(false);

        bool ok = result.IsSuccessStatusCode &&
            fake.Requests.Count == 1 &&
            fake.Requests[0].RequestUri?.ToString() == "http://127.0.0.1:65535/declarative-poc?from=workflow" &&
            fake.Requests[0].Headers.TryGetValues("X-Poc-Marker", out IEnumerable<string>? values) &&
            values.Single() == "declarative-header-reached-sink";

        return ok
            ? "PASS E: fake HttpMessageHandler captured the request constructed by DefaultHttpRequestHandler."
            : "FAIL E: fake HttpMessageHandler did not capture the expected DefaultHttpRequestHandler request.";
    }

    private static async Task<string> TestRedirectDocumentedWithFakeHandlerAsync(WorkflowHttpRequest workflow)
    {
        CapturingHttpMessageHandler fakeRedirect = new(
            HttpStatusCode.Redirect,
            string.Empty,
            location: "http://127.0.0.1:65535/redirect-target");
        using HttpClient client = new(fakeRedirect);
        await using DefaultHttpRequestHandler handler = new(client);

        HttpRequestResult result = await handler.SendAsync(workflow.ToHttpRequestInfo()).ConfigureAwait(false);

        bool ok = result.StatusCode == 302 && fakeRedirect.Requests.Count == 1;
        return ok
            ? "PASS F: redirect was modeled with a fake handler only; no framework-level redirect revalidation was observed in this fake chain. Real redirect following, if enabled, is HttpClientHandler behavior."
            : "FAIL F: fake redirect chain behaved unexpectedly.";
    }

    private static Task<HttpRequestResult> DispatchThroughHostConfiguredHandler(
        WorkflowHttpRequest workflow,
        IHttpRequestHandler? httpRequestHandler)
    {
        if (httpRequestHandler is null)
        {
            throw new InvalidOperationException("Host did not configure IHttpRequestHandler; declarative HTTP action is fail-closed.");
        }

        // This mirrors the trust boundary being tested: workflow-authored URL/headers become HttpRequestInfo
        // and are passed to a host-supplied IHttpRequestHandler. It is not a claim of default SSRF.
        return httpRequestHandler.SendAsync(workflow.ToHttpRequestInfo());
    }

    private sealed class RecordingHttpRequestHandler : IHttpRequestHandler
    {
        public List<HttpRequestInfo> Requests { get; } = [];

        public int NetworkSendCount => 0;

        public Task<HttpRequestResult> SendAsync(HttpRequestInfo request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpRequestResult
            {
                StatusCode = 200,
                IsSuccessStatusCode = true,
                Body = "{\"recorded\":true}",
                Headers = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["X-Recorded-By"] = [nameof(RecordingHttpRequestHandler)],
                },
            });
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;
        private readonly string? _location;

        public CapturingHttpMessageHandler(HttpStatusCode statusCode, string body, string? location = null)
        {
            _statusCode = statusCode;
            _body = body;
            _location = location;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

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

            if (request.Content is not null)
            {
                clone.Content = new StringContent(string.Empty);
                foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                {
                    clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            return clone;
        }
    }

    private sealed record WorkflowHttpRequest(
        string Method,
        string Url,
        IReadOnlyDictionary<string, string> Headers,
        IReadOnlyDictionary<string, string>? QueryParameters)
    {
        public HttpRequestInfo ToHttpRequestInfo() => new()
        {
            Method = Method,
            Url = Url,
            Headers = Headers,
            QueryParameters = QueryParameters,
        };

        public static WorkflowHttpRequest Load(string path)
        {
            string text = File.ReadAllText(path);
            return Path.GetExtension(path).Equals(".json", StringComparison.OrdinalIgnoreCase)
                ? LoadJson(text)
                : LoadMinimalYaml(text);
        }

        private static WorkflowHttpRequest LoadJson(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement http = document.RootElement.GetProperty("actions")[0].GetProperty("httpRequest");
            string method = http.GetProperty("method").GetString() ?? "GET";
            string url = http.GetProperty("url").GetString() ?? string.Empty;
            Dictionary<string, string> headers = ReadStringMap(http.GetProperty("headers"));
            Dictionary<string, string>? query = http.TryGetProperty("queryParameters", out JsonElement queryElement)
                ? ReadStringMap(queryElement)
                : null;
            return new WorkflowHttpRequest(method, url, headers, query);
        }

        private static WorkflowHttpRequest LoadMinimalYaml(string yaml)
        {
            string method = ReadYamlScalar(yaml, "method") ?? "GET";
            string url = ReadYamlScalar(yaml, "url") ?? string.Empty;
            Dictionary<string, string> headers = ReadIndentedYamlMap(yaml, "headers");
            Dictionary<string, string> query = ReadIndentedYamlMap(yaml, "queryParameters");
            return new WorkflowHttpRequest(method, url, headers, query.Count == 0 ? null : query);
        }

        private static Dictionary<string, string> ReadStringMap(JsonElement element)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                map[property.Name] = property.Value.GetString() ?? string.Empty;
            }

            return map;
        }

        private static string? ReadYamlScalar(string yaml, string key)
        {
            foreach (string line in yaml.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(key + ":", StringComparison.Ordinal))
                {
                    return Unquote(trimmed[(key.Length + 1)..].Trim());
                }
            }

            return null;
        }

        private static Dictionary<string, string> ReadIndentedYamlMap(string yaml, string key)
        {
            Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
            string[] lines = yaml.Split('\n');
            bool inSection = false;
            int sectionIndent = -1;

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int indent = line.Length - line.TrimStart().Length;
                string trimmed = line.Trim();
                if (trimmed == key + ":")
                {
                    inSection = true;
                    sectionIndent = indent;
                    continue;
                }

                if (!inSection)
                {
                    continue;
                }

                if (indent <= sectionIndent)
                {
                    break;
                }

                int colon = trimmed.IndexOf(':', StringComparison.Ordinal);
                if (colon > 0)
                {
                    map[trimmed[..colon].Trim()] = Unquote(trimmed[(colon + 1)..].Trim());
                }
            }

            return map;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                return value[1..^1];
            }

            return value;
        }
    }
}

internal interface IHttpRequestHandler
{
    Task<HttpRequestResult> SendAsync(HttpRequestInfo request, CancellationToken cancellationToken = default);
}

internal sealed class HttpRequestInfo
{
    public string Method { get; init; } = "GET";

    public string Url { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    public IReadOnlyDictionary<string, string>? QueryParameters { get; init; }
}

internal sealed class HttpRequestResult
{
    public int StatusCode { get; init; }

    public bool IsSuccessStatusCode { get; init; }

    public string? Body { get; init; }

    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Headers { get; init; }
}

internal sealed class DefaultHttpRequestHandler : IHttpRequestHandler, IAsyncDisposable
{
    private readonly HttpClient _httpClient;

    public DefaultHttpRequestHandler(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<HttpRequestResult> SendAsync(HttpRequestInfo request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            throw new ArgumentException("Request URL must not be empty.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Method))
        {
            throw new ArgumentException("Request method must not be empty.", nameof(request));
        }

        using HttpRequestMessage message = BuildHttpRequestMessage(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        string body = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, IReadOnlyList<string>> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in response.Headers)
        {
            headers[header.Key] = header.Value.ToArray();
        }

        if (response.Content is not null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in response.Content.Headers)
            {
                headers[header.Key] = header.Value.ToArray();
            }
        }

        return new HttpRequestResult
        {
            StatusCode = (int)response.StatusCode,
            IsSuccessStatusCode = response.IsSuccessStatusCode,
            Body = body,
            Headers = headers,
        };
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static HttpRequestMessage BuildHttpRequestMessage(HttpRequestInfo request)
    {
        HttpRequestMessage message = new(new HttpMethod(request.Method.Trim()), ResolveRequestUri(request));

        if (request.Headers is not null)
        {
            foreach (KeyValuePair<string, string> header in request.Headers)
            {
                message.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return message;
    }

    private static string ResolveRequestUri(HttpRequestInfo request)
    {
        if (request.QueryParameters is null || request.QueryParameters.Count == 0)
        {
            return request.Url;
        }

        StringBuilder builder = new(request.Url);
        builder.Append(request.Url.Contains('?', StringComparison.Ordinal) ? '&' : '?');
        bool first = true;
        foreach (KeyValuePair<string, string> parameter in request.QueryParameters)
        {
            if (!first)
            {
                builder.Append('&');
            }

            first = false;
            builder.Append(Uri.EscapeDataString(parameter.Key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(parameter.Value));
        }

        return builder.ToString();
    }
}
