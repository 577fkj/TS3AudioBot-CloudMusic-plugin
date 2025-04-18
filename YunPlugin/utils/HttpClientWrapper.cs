using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TS3AudioBot;
using TSLib.Helper;

namespace YunPlugin.utils
{
    [Serializable]
    public class HttpClientException : Exception
    {
        public HttpClientException(Exception? ex = null) : base(null, ex) { }
        public HttpClientException(string message, Exception? ex = null) : base(message, ex) { }
        protected HttpClientException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    public static class Error
    {
        public static HttpClientException Exception(Exception ex) => new HttpClientException(ex);
        public static HttpClientException Exception(string message, Exception ex = null) => new HttpClientException(message, ex);
    }

    public static class HttpUtils
    {
        public static async Task<T> AsJson<T>(this HttpResponseMessage response)
        {
            try
            {
                using (response)
                {
                    using Stream stream = await response.Content.ReadAsStreamAsync();
                    return (await JsonSerializer.DeserializeAsync<T>(stream)) ?? throw Error.Exception("Request got empty response.");
                }
            }
            catch (JsonException exception)
            {
                throw Error.Exception("Invalid or malformed response parts (json-request)", exception);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw ex;
            }
        }
    }

    public class HttpRequest : HttpRequestMessage
    {
        private const string TimeoutPropertyKey = "RequestTimeout";

        private Func<HttpResponseMessage, Task<Exception>> action;
        private NLog.Logger Log;

        private HttpClient httpClient;
        public HttpRequest(HttpClient httpClient, NLog.Logger log, HttpMethod method, Uri uri, Func<HttpResponseMessage, Task<Exception>> action = null) : base(method, uri)
        {
            this.httpClient = httpClient;
            Log = log;
            this.action = action;
        }

        public HttpRequest WithMethod(HttpMethod method)
        {
            Method = method;
            return this;
        }

        public HttpRequest WithHeader(string name, string value)
        {
            Headers.Add(name, value);
            return this;
        }

        public HttpRequest WithTimeout(TimeSpan timeout)
        {
            Properties[TimeoutPropertyKey] = timeout;
            return this;
        }

        public HttpRequest SetHttpCallback(Func<HttpResponseMessage, Task<Exception>> action)
        {
            this.action = action;
            return this;
        }

        private async Task<Exception> CheckOkReturnCodeOrThrow(HttpResponseMessage response)
        {
            if (action != null)
            {
                return await action.Invoke(response);
            }
            else if (!response.IsSuccessStatusCode)
            {
                return Error.Exception($"Request failed with status code {response.StatusCode}.");
            }
            return null;
        }

        private async Task<HttpResponseMessage> SendDefaultAsync()
        {
            var response = await httpClient.SendAsync(this, HttpCompletionOption.ResponseHeadersRead);
            var ex = await CheckOkReturnCodeOrThrow(response);
            if (ex != null)
            {
                throw ex;
            }
            return response;
        }

        public async Task<T> AsJson<T>()
        {
            try
            {
                using (this)
                {
                    using HttpResponseMessage response = await SendDefaultAsync();
                    using Stream stream = await response.Content.ReadAsStreamAsync();
                    return (await JsonSerializer.DeserializeAsync<T>(stream)) ?? throw Error.Exception("Request got empty response.");
                }
            }
            catch (JsonException exception)
            {
                throw Error.Exception("Invalid or malformed response parts (json-request)", exception);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw ToLoggedError(ex);
            }
        }

        public async Task<HttpResponseMessage> Send()
        {
            try
            {
                    return await SendDefaultAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw ToLoggedError(ex);
            }
        }

        public async Task<string> AsString()
        {
            try
            {
                using (this)
                {
                    using var response = await SendDefaultAsync();
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw ToLoggedError(ex);
            }
        }

        public async Task ToAction(Func<HttpResponseMessage, Task> body)
        {
            try
            {
                using (this)
                {
                    using var response = await SendDefaultAsync();
                    await body.Invoke(response);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw ToLoggedError(ex);
            }
        }

        public async Task<T> ToAction<T>(Func<HttpResponseMessage, Task<T>> body)
        {
            try
            {
                using (this)
                {
                    using var response = await SendDefaultAsync();
                    return await body.Invoke(response);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw ToLoggedError(ex);
            }
        }

        public Task ToStream(Func<Stream, Task> body) => ToAction(async response => await body(await response.Content.ReadAsStreamAsync()));

        public async Task<HttpResponseMessage> UnsafeResponse()
        {
            try
            {
                using (this)
                {
                    var response = await SendDefaultAsync();
                    return response;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException)
            {
                throw ToLoggedError(ex);
            }
        }

        public async Task<Stream> UnsafeStream() => await (await UnsafeResponse()).Content.ReadAsStreamAsync();

        private HttpClientException ToLoggedError(Exception ex)
        {
            if (ex is OperationCanceledException webEx)
            {
                Log.Debug(webEx, "Request timed out");
                throw Error.Exception("Request timed out.", ex);
            }

            Log.Debug(ex, "Unknown request error");
            throw Error.Exception("Unknown request error.", ex);
        }
    }

    public class HttpClientWrapper
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        private readonly HttpClient HttpClient = new HttpClient(new RedirectHandler(new HttpClientHandler()));
        private readonly NLog.Logger Log;

        private string BaseUrl;
        private Dictionary<string, string> Headres;
        private bool AutoAddTimestamp;

        private Func<HttpResponseMessage, Task<Exception>> OnResponse;

        public void SetHttpCallback(Func<HttpResponseMessage, Task<Exception>> action)
        {
            OnResponse = action;
        }

        public HttpClientWrapper(string baseUrl = "", Dictionary<string, string> headres = null, bool autoAddTimestamp = false, NLog.Logger log = null)
        {
            if (baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl[..^1];
            }
            BaseUrl = baseUrl;
            Headres = headres;
            AutoAddTimestamp = autoAddTimestamp;
            if (log != null)
            {
                Log = log;
            }
            else
            {
                Log = YunPlugin.GetLogger(GetType().Name);
            }

            HttpClient.Timeout = DefaultTimeout;
        }

        public HttpRequest Request(string? link) => Request(CreateUri(link));
        public HttpRequest Request(Uri uri) => new HttpRequest(HttpClient, Log, HttpMethod.Get, uri, OnResponse);

        private Uri CreateUri(string? link)
        {
            if (!Uri.TryCreate(link, UriKind.RelativeOrAbsolute, out var uri))
                throw Error.Exception("Invalid uri.");
            return uri;
        }

        public async Task<HttpResponseMessage> GetHttpResponse(string path, Dictionary<string, string> param = null)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.StartsWith("/"))
                {
                    path = "/" + path;
                }
            }
            if (param != null && (AutoAddTimestamp || param.Keys.Count != 0))
            {
                List<string> paramList = new List<string>();
                foreach (var item in param)
                {
                    paramList.Add($"{item.Key}={item.Value}");
                }
                if (AutoAddTimestamp)
                {
                    paramList.Add($"timestamp={Utils.GetTimeStamp()}");
                }
                path += "?" + string.Join("&", paramList);
            }

            var request = Request(BaseUrl + path);
            if (Headres != null)
            {
                foreach (var item in Headres)
                {
                    request.WithHeader(item.Key, item.Value);
                }
            }
            return await request.Send();
        }

        public async Task<T> Get<T>(string path, Dictionary<string, string> param = null, bool encode = true)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.StartsWith("/"))
                {
                    path = "/" + path;
                }
            }
            if (param != null && (AutoAddTimestamp || param.Keys.Count != 0))
            {
                List<string> paramList = new List<string>();
                foreach (var item in param)
                {
                    if (encode)
                    {
                        paramList.Add($"{item.Key}={WebUtility.UrlEncode(item.Value)}");
                    }
                    else
                    {
                        paramList.Add($"{item.Key}={item.Value}");
                    }
                }
                if (AutoAddTimestamp)
                {
                    paramList.Add($"timestamp={Utils.GetTimeStamp()}");
                }
                path += "?" + string.Join("&", paramList);
            }

            var request = Request(BaseUrl + path);
            if (Headres != null)
            {
                foreach (var item in Headres)
                {
                    request.WithHeader(item.Key, item.Value);
                }
            }
            return await request.AsJson<T>();
        }

        public async Task<T> Post<T>(string path, Dictionary<string, string> param = null, bool json = false)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.StartsWith("/"))
                {
                    path = "/" + path;
                }
            }

            if (AutoAddTimestamp)
            {
                if (path.Contains("?"))
                {
                    path += "&timestamp=" + Utils.GetTimeStamp();
                }
                else
                {
                    path += "?timestamp=" + Utils.GetTimeStamp();
                }
            }

            var request = Request(BaseUrl + path);

            request.WithMethod(HttpMethod.Post);

            if (Headres != null)
            {
                foreach (var item in Headres)
                {
                    request.WithHeader(item.Key, item.Value);
                }
            }
            if (param != null)
            {
                if (json)
                {
                    var jsonData = JsonSerializer.Serialize(param);
                    request.Content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(param);
                }
            }

            return await request.AsJson<T>();
        }

        public async Task<HttpResponseMessage> PostHttpResponse(string path, Dictionary<string, string> param = null, bool json = false)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.StartsWith("/"))
                {
                    path = "/" + path;
                }
            }

            if (AutoAddTimestamp)
            {
                if (path.Contains("?"))
                {
                    path += "&timestamp=" + Utils.GetTimeStamp();
                }
                else
                {
                    path += "?timestamp=" + Utils.GetTimeStamp();
                }
            }

            var request = Request(BaseUrl + path);

            request.WithMethod(HttpMethod.Post);

            if (Headres != null)
            {
                foreach (var item in Headres)
                {
                    request.WithHeader(item.Key, item.Value);
                }
            }
            if (param != null)
            {
                if (json)
                {
                    var jsonData = JsonSerializer.Serialize(param);
                    request.Content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                }
                else
                {
                    request.Content = new FormUrlEncodedContent(param);
                }
            }

            return await request.Send();
        }

        public void SetHeader(Dictionary<string, string> header)
        {
            Headres = header;
        }

        public void SetBaseUrl(string baseUrl)
        {
            this.BaseUrl = baseUrl;
        }

        public void Dispose()
        {
            HttpClient.Dispose();
        }
    }

    // HttpClient does not allow unsafe HTTPS->HTTP redirects.
    // But we don't care because audio streaming is not security critical
    // This loop implements a simple redirect following on 301/302 with at most 5 redirects.
    public class RedirectHandler : DelegatingHandler
    {
        private const int MaxRedirects = 5;

        public RedirectHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            for (int i = 0; i < MaxRedirects; i++)
            {
                response = await base.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Moved || response.StatusCode == HttpStatusCode.Redirect)
                {
                    request.RequestUri = response.Headers.Location;
                }
                else
                {
                    return response;
                }
            }

            throw Error.Exception("Invalid or malformed response parts. (Max redirects reached)");
        }
    }
}
