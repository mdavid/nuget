﻿using System;
using System.Globalization;
using System.IO;
using System.Net;
using Microsoft.Internal.Web.Utils;
using NuGet.Resources;

namespace NuGet
{
    public class PackageServer : IPackageServer
    {
        private const string ServiceEndpoint = "/api/v2/package";
        private const string ApiKeyHeader = "X-NuGet-ApiKey";

        private readonly Lazy<Uri> _baseUri;
        private readonly string _source;
        private readonly string _userAgent;

        public PackageServer(string source, string userAgent)
        {
            if (String.IsNullOrEmpty(source))
            {
                throw new ArgumentException(CommonResources.Argument_Cannot_Be_Null_Or_Empty, "source");
            }
            _source = source;
            _userAgent = userAgent;
            _baseUri = new Lazy<Uri>(ResolveBaseUrl);
        }

        public string Source
        {
            get { return _source; }
        }

        /// <summary>
        /// Pushes a package to the server that is represented by the stream.
        /// </summary>
        /// <param name="apiKey">API key to be used to push the package.</param>
        /// <param name="packageStream">Stream representing the package.</param>
        /// <param name="timeout">Time in milliseconds to timeout the server request.</param>
        public void PushPackage(string apiKey, Stream packageStream, int timeout)
        {
            HttpClient client = GetClient("", "PUT", "application/octet-stream");

            client.SendingRequest += (sender, e) =>
            {
                var request = (HttpWebRequest)e.Request;

                // Set the timeout
                if (timeout <= 0)
                {
                    timeout = request.ReadWriteTimeout; // Default to 5 minutes if the value is invalid.
                }

                request.Timeout = timeout;
                request.ReadWriteTimeout = timeout;
                request.Headers.Add(ApiKeyHeader, apiKey);

                var multiPartRequest = new MultipartWebRequest();
                multiPartRequest.AddFile(() => packageStream, "package");

                multiPartRequest.CreateMultipartRequest(request);
            };

            EnsureSuccessfulResponse(client, HttpStatusCode.Created);
        }

        public void DeletePackage(string apiKey, string packageId, string packageVersion)
        {
            // Review: Do these values need to be encoded in any way?
            var url = String.Join("/", packageId, packageVersion);
            HttpClient client = GetClient(url, "DELETE", "text/html");

            client.SendingRequest += (sender, e) =>
            {
                var request = (HttpWebRequest)e.Request;
                request.Headers.Add(ApiKeyHeader, apiKey);
            };
            EnsureSuccessfulResponse(client);
        }

        private HttpClient GetClient(string path, string method, string contentType)
        {
            var baseUrl = _baseUri.Value;
            Uri requestUri = GetServiceEndpointUrl(baseUrl, path);

            var client = new HttpClient(requestUri)
            {
                ContentType = contentType,
                Method = method
            };

            if (!String.IsNullOrEmpty(_userAgent))
            {
                client.UserAgent = HttpUtility.CreateUserAgentString(_userAgent);
            }

            return client;
        }

        internal static Uri GetServiceEndpointUrl(Uri baseUrl, string path)
        {
            Uri requestUri;
            if (String.IsNullOrEmpty(baseUrl.AbsolutePath.TrimStart('/')))
            {
                // If there's no host portion specified, append the url to the client.
                requestUri = new Uri(baseUrl, ServiceEndpoint + '/' + path);
            }
            else
            {
                requestUri = new Uri(baseUrl, path);
            }
            return requestUri;
        }

        private static void EnsureSuccessfulResponse(HttpClient client, HttpStatusCode expectedStatusCode = HttpStatusCode.OK)
        {
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)client.GetResponse();
                if (response != null && expectedStatusCode != response.StatusCode)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.PackageServerError, response.StatusDescription, String.Empty));
                }
            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw;
                }
                response = (HttpWebResponse)e.Response;
                if (expectedStatusCode != response.StatusCode)
                {
                    throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, NuGetResources.PackageServerError, response.StatusDescription, e.Message), e);
                }
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                    response = null;
                }
            }
        }

        private Uri ResolveBaseUrl()
        {
            Uri uri;

            try
            {
                var client = new RedirectedHttpClient(new Uri(Source));
                uri = client.Uri;
            }
            catch (WebException ex)
            {
                var response = (HttpWebResponse)ex.Response;
                if (response == null)
                {
                    throw;
                }

                uri = response.ResponseUri;
            }

            return EnsureTrailingSlash(uri);
        }

        private static Uri EnsureTrailingSlash(Uri uri)
        {
            string value = uri.OriginalString;
            if (!value.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                value += "/";
            }
            return new Uri(value);
        }
    }
}
