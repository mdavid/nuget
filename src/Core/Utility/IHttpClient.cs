﻿using System;
using System.Net;

namespace NuGet {
    public interface IHttpClient : IProgressProvider {
        string UserAgent {
            get;
            set;
        }

        WebRequest CreateRequest(Uri uri);
        void InitializeRequest(WebRequest request);
        Uri GetRedirectedUri(Uri uri);

        byte[] DownloadData(Uri uri);
    }
}