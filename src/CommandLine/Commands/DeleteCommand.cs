﻿using System;
using System.ComponentModel.Composition;
using NuGet.Common;

namespace NuGet.Commands {
    [Command(typeof(NuGetResources), "delete", "DeleteCommandDescription",
        MinArgs = 2, MaxArgs = 3, UsageDescriptionResourceName = "DeleteCommandUsageDescription",
        UsageSummaryResourceName = "DeleteCommandUsageSummary")]
    public class DeleteCommand : Command {
        
        [Option(typeof(NuGetResources), "DeleteCommandSourceDescription", AltName = "src")]
        public string Source { get; set; }

        [Option(typeof(NuGetResources), "DeleteCommandNoPromptDescription", AltName = "np")]
        public bool NoPrompt { get; set; }

        public override void ExecuteCommand() {

            //First argument should be the package ID
            string packageId = Arguments[0];
            //Second argument should be the package Version
            string packageVersion = Arguments[1];
            //Third argument, if present, should be the API Key
            string userSetApiKey = null;
            if (Arguments.Count > 1) {
                userSetApiKey = Arguments[1];
            }

            //If the user passed a source use it for the gallery location
            GalleryServer gallery;
            if (String.IsNullOrEmpty(Source)) {
                gallery = new GalleryServer();
            }
            else {
                gallery = new GalleryServer(Source);
            }

            //If the user did not pass an API Key look in the config file
            string apiKey;
            ISettings settings = new UserSettings(new PhysicalFileSystem(Environment.CurrentDirectory));
            if (String.IsNullOrEmpty(userSetApiKey)) {
                var value = settings.GetDecryptedValue("apiKeys", gallery.Source);
                if (string.IsNullOrEmpty(value)) {
                    throw new CommandLineException(NuGetResources.NoApiKeyFound);
                }
                apiKey = value;

            }
            else {
                apiKey = userSetApiKey;
            }


            if (NoPrompt || Console.Confirm(String.Format(NuGetResources.DeleteCommandConfirm, packageId, packageVersion))) {
                Console.WriteLine(NuGetResources.DeleteCommandDeletingPackage, packageId, packageVersion);
                gallery.DeletePackage(apiKey, packageId, packageVersion);
                Console.WriteLine(NuGetResources.DeleteCommandDeletedPackage, packageId, packageVersion);
            }
            else {
                Console.WriteLine(NuGetResources.DeleteCommandCanceled);
            }

        }
    }
}