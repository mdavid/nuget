﻿using System;
using System.Net;
using System.Net.Cache;

namespace NuGet {
    public class HttpClient : IHttpClient {
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
            request.CachePolicy = new HttpRequestCachePolicy();
            request.UseDefaultCredentials = true;
            if (request.Proxy != null) {
                // If we are going through a proxy then just set the default credentials
                request.Proxy.Credentials = CredentialCache.DefaultCredentials;
            }
        }

        public Uri GetRedirectedUri(Uri uri) {
            WebRequest request = CreateRequest(uri);
            try {
                WebResponse response = request.GetResponse();
                if (response == null) {
                    return null;
                }
                return response.ResponseUri;
            }
            catch (WebException e) {
                return e.Response.ResponseUri;
            }
        }
    }
}
