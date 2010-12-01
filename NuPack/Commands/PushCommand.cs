﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Net;
using NuGet.Common;

namespace NuGet.Commands {

    [Export(typeof(ICommand))]
    [Command(typeof(NuGetResources), "push", "PushCommandDescription", AltName = "p",
        MinArgs = 2, MaxArgs = 2, UsageDescriptionResourceName = "PushCommandUsageDescription",
        UsageSummaryResourceName = "PushCommandUsageSummary")]
    public class PushCommand : ICommand {
        private const string _createPackageService = "PackageFiles";
        private const string _publichPackageService = "PublishedPackages";
        private const string _userAgentPattern = "CommandLine/{0} ({1})";
        private const string _baseGalleryServerUrlFWLink = "http://go.microsoft.com/fwlink/?LinkID=207106";

        private string _apiKey;
        private string _packagePath;
        private string _userAgent;
        private Uri _baseGalleryServerUrl;

        public List<string> Arguments { get; set; }

        public IConsole Console { get; set; }

        [Option(typeof(NuGetResources), "PushCommandPublishDescription", AltName = "pub")]
        public bool Publish { get; set; }

        [ImportingConstructor]
        public PushCommand(IConsole console) {
            Console = console;
            Publish = true;
        }

        public void Execute() {
            //Frist argument should be the package
            _packagePath = Arguments[0];
            //Second argument should be the API Key
            _apiKey = Arguments[1];

            var client = new HttpClient();
            _baseGalleryServerUrl = client.GetRedirectedUri(new Uri(_baseGalleryServerUrlFWLink));

            var version = typeof(PushCommand).Assembly.GetNameSafe().Version;
            _userAgent = String.Format(CultureInfo.InvariantCulture, _userAgentPattern, version, Environment.OSVersion);

            PushPackage();
        }

        private void PushPackage() {

            var url = new Uri(string.Format("{0}/{1}/{2}/nupkg", _baseGalleryServerUrl, _createPackageService, _apiKey));

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.ContentType = "application/octet-stream";
            request.Method = "POST";
            request.UserAgent = _userAgent;

            ZipPackage pkg = new ZipPackage(_packagePath);

            using (Stream pkgStream = pkg.GetStream()) {
                byte[] file = pkgStream.ReadAllBytes();
                request.ContentLength = file.Length;
                var requestStream = request.GetRequestStream();
                requestStream.Write(file, 0, file.Length);
            }

            Console.WriteLine(NuGetResources.PushCommandCreatingPackage, pkg.Id, pkg.Version);

            var response = SafeGetResponse(request);

            if (response.StatusCode != HttpStatusCode.OK) {
                string errorMessage = String.Empty;
                using (var stream = response.GetResponseStream()) {
                    errorMessage = stream.ReadToEnd();
                }

                throw new CommandLineException(NuGetResources.PushCommandInvalidResponse, response.StatusCode, errorMessage);
            }

            Console.WriteLine(NuGetResources.PushCommandPackageCreated);

            if (Publish) {
                PublishPackage(pkg.Id, pkg.Version.ToString());
            }

        }

        private void PublishPackage(string id, string version) {
            Console.WriteLine(NuGetResources.PushCommandPublishingPackage, id, version);

            var url = new Uri(string.Format("{0}/{1}/{2}/{3}/{4}", _baseGalleryServerUrl, _publichPackageService, _apiKey, id, version));

            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = _userAgent;

            var response = SafeGetResponse(request);

            if (response.StatusCode != HttpStatusCode.OK) {
                string errorMessage = String.Empty;
                using (var stream = response.GetResponseStream()) {
                    errorMessage = stream.ReadToEnd();
                }

                throw new CommandLineException(NuGetResources.PushCommandInvalidResponse, response.StatusCode, errorMessage);
            }
            Console.WriteLine(NuGetResources.PushCommandPackagePublished);
        }

        private HttpWebResponse SafeGetResponse(HttpWebRequest request) {
            try {
                return (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e) {
                return (HttpWebResponse)e.Response;
            }
        }
    }
}