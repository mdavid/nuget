namespace NuGet {
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Cache;
    using System.Reflection;
    using NuGet.Resources;

    // REVIEW: This class isn't super clean. Maybe this object should be passed around instead
    // of being static
    internal static class HttpWebRequestor {
        private const string UserAgent = "Package-Installer/{0} ({1})";

        public static ZipPackage DownloadPackage(Uri uri) {
            return DownloadPackage(uri, verifyCallback: null, useCache: true);
        }

        [SuppressMessage(
            "Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "We can't dispose an object if we want to return it.")]
        public static ZipPackage DownloadPackage(Uri uri, Func<byte[], bool> verifyCallback, bool useCache) {
            byte[] cachedBytes = null;
            return new ZipPackage(() => {
                if (useCache && cachedBytes != null) {
                    return new MemoryStream(cachedBytes);
                }

                using (Stream responseStream = GetResponseStream(uri)) {
                    // ZipPackages require a seekable stream
                    var memoryStream = new MemoryStream();
                    // Copy the stream
                    responseStream.CopyTo(memoryStream);
                    // Call the validator if its available
                    if (verifyCallback != null && !verifyCallback(memoryStream.ToArray())) {
                        throw new InvalidDataException(NuGetResources.PackageContentsVerifyError);
                    }

                    // Move it back to the beginning
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    if (useCache) {
                        // Cache the bytes for this package
                        cachedBytes = memoryStream.ToArray();
                    }
                    return memoryStream;
                }
            });
        }

        public static Stream GetResponseStream(Uri uri) {
            WebResponse response = GetResponse(uri);

            return response.GetResponseStream();
        }

        public static WebResponse GetResponse(Uri uri) {
            WebRequest request = WebRequest.Create(uri);
            InitializeRequest(request);

            return request.GetResponse();
        }

        public static Uri GetRedirectedUri(Uri uri) {
            WebResponse response = GetResponse(uri);
            return response.ResponseUri;
        }

        internal static void InitializeRequest(WebRequest request) {
            var httpRequest = request as HttpWebRequest;
            if (httpRequest != null) {
                var version = typeof(HttpWebRequestor).Assembly.GetNameSafe().Version;
                string userAgent = String.Format(CultureInfo.InvariantCulture, UserAgent, version, Environment.OSVersion);
                httpRequest.UserAgent = userAgent;
            }
            request.CachePolicy = new HttpRequestCachePolicy();
            request.UseDefaultCredentials = true;
            if (request.Proxy != null) {
                // If we are going through a proxy then just set the default credentials
                request.Proxy.Credentials = CredentialCache.DefaultCredentials;
            }
        }
    }
}
