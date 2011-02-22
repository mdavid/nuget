﻿using System;
using System.IO;
using System.Net;

namespace NuGet {
    public class HttpClient : IHttpClient {
        private const int RequestTimeOut = 5000;
        private IObserver<int> _observer;

        public string UserAgent {
            get;
            set;
        }

        public WebRequest CreateRequest(Uri uri) {
            WebRequest request = WebRequest.Create(uri);
            InitializeRequest(request);
            return request;
        }

        public void InitializeRequest(WebRequest request) {
            var httpRequest = request as HttpWebRequest;
            if (httpRequest != null) {
                httpRequest.UserAgent = UserAgent;
            }

            request.UseDefaultCredentials = true;
            if (request.Proxy != null) {
                // If we are going through a proxy then just set the default credentials
                request.Proxy.Credentials = CredentialCache.DefaultCredentials;
            }

            // Don't do this in debug mode so we can debug requests without worrying about timeouts
#if !DEBUG
            request.Timeout = RequestTimeOut;
#endif
        }

        public Uri GetRedirectedUri(Uri uri) {
            WebRequest request = CreateRequest(uri);
            using (WebResponse response = request.GetResponse()) {
                if (response == null) {
                    return null;
                }
                return response.ResponseUri;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        public byte[] DownloadData(Uri uri) {
            const int ChunkSize = 1024 * 4; // 4KB

            byte[] buffer = null;
            try {
                WebRequest request = CreateRequest(uri);
                using (var response = request.GetResponse()) {

                    // total response length
                    int length = (int)response.ContentLength;
                    buffer = new byte[length];

                    // We read the response stream chunk by chunk (each chunk is 4KB). 
                    // After reading each chunk, we report the progress based on the total number bytes read so far.
                    int totalReadSoFar = 0;
                    using (Stream stream = response.GetResponseStream()) {
                        while (totalReadSoFar < length) {
                            int bytesRead = stream.Read(buffer, totalReadSoFar, Math.Min(length - totalReadSoFar, ChunkSize));
                            if (bytesRead == 0) {
                                break;
                            }
                            else {
                                totalReadSoFar += bytesRead;
                                NotifyObserver(o => o.OnNext((totalReadSoFar * 100) / length));
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                NotifyObserver(o => o.OnError(ex));
                throw;
            }

            NotifyObserver(o => o.OnCompleted());

            return buffer;
        }

        private void NotifyObserver(Action<IObserver<int>> action) {
            if (_observer != null) {
                action(_observer);
            }
        }

        public IDisposable Subscribe(IObserver<int> observer) {
            if (observer == null) {
                throw new ArgumentNullException("observer");
            }

            _observer = observer;
            return null;
        }
    }
}