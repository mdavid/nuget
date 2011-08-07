﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using NuGet.Resources;

namespace NuGet {
    public class PackageBuilder : IPackageBuilder {
        private const string DefaultContentType = "application/octet";
        internal const string ManifestRelationType = "manifest";

        public PackageBuilder(string path)
            : this(path, NullPropertyProvider.Instance) {
        }

        public PackageBuilder(string path, IPropertyProvider propertyProvider)
            : this(path, Path.GetDirectoryName(path), propertyProvider) {
        }

        public PackageBuilder(string path, string basePath)
            : this(path, basePath, NullPropertyProvider.Instance) {
        }

        public PackageBuilder(string path, string basePath, IPropertyProvider propertyProvider)
            : this() {
            using (Stream stream = File.OpenRead(path)) {
                ReadManifest(stream, basePath, propertyProvider);
            }
        }

        public PackageBuilder(Stream stream, string basePath)
            : this(stream, basePath, NullPropertyProvider.Instance) {
        }

        public PackageBuilder(Stream stream, string basePath, IPropertyProvider propertyProvider)
            : this() {
            ReadManifest(stream, basePath, propertyProvider);
        }

        public PackageBuilder() {
            Files = new Collection<IPackageFile>();
            Dependencies = new Collection<PackageDependency>();
            FrameworkReferences = new Collection<FrameworkAssemblyReference>();
            PackageAssemblyReferences = new Collection<string>();
            Authors = new HashSet<string>();
            Owners = new HashSet<string>();
            Tags = new HashSet<string>();
        }

        public string Id {
            get;
            set;
        }

        public Version Version {
            get;
            set;
        }

        public string Title {
            get;
            set;
        }

        public ISet<string> Authors {
            get;
            private set;
        }

        public ISet<string> Owners {
            get;
            private set;
        }

        public Uri IconUrl {
            get;
            set;
        }

        public Uri LicenseUrl {
            get;
            set;
        }

        public Uri ProjectUrl {
            get;
            set;
        }

        public bool RequireLicenseAcceptance {
            get;
            set;
        }

        public string Description {
            get;
            set;
        }

        public string Summary {
            get;
            set;
        }

        public string ReleaseNotes {
            get;
            set;
        }

        public string Language {
            get;
            set;
        }

        public ISet<string> Tags {
            get;
            private set;
        }

        public string Copyright {
            get;
            set;
        }

        public Collection<PackageDependency> Dependencies {
            get;
            private set;
        }

        public Collection<IPackageFile> Files {
            get;
            private set;
        }

        public Collection<FrameworkAssemblyReference> FrameworkReferences {
            get;
            private set;
        }

        public Collection<string> PackageAssemblyReferences {
            get;
            private set;
        }

        IEnumerable<string> IPackageMetadata.Authors {
            get {
                return Authors;
            }
        }

        IEnumerable<string> IPackageMetadata.Owners {
            get {
                return Owners;
            }
        }

        string IPackageMetadata.Tags {
            get {
                return String.Join(" ", Tags);
            }
        }

        IEnumerable<PackageDependency> IPackageMetadata.Dependencies {
            get {
                return Dependencies;
            }
        }

        IEnumerable<FrameworkAssemblyReference> IPackageMetadata.FrameworkAssemblies {
            get {
                return FrameworkReferences;
            }
        }

        public void Save(Stream stream) {
            // Make sure we're saving a valid package id
            PackageIdValidator.ValidatePackageId(Id);

            // Throw if the package doesn't contain any dependencies nor content
            if (!Files.Any() && !Dependencies.Any() && !FrameworkReferences.Any()) {
                throw new InvalidOperationException(NuGetResources.CannotCreateEmptyPackage);
            }

            ValidateReferenceAssemblies();

            using (Package package = Package.Open(stream, FileMode.Create)) {
                // Validate and write the manifest
                WriteManifest(package);

                // Write the files to the package
                WriteFiles(package);

                // Copy the metadata properties back to the package
                package.PackageProperties.Creator = String.Join(",", Authors);
                package.PackageProperties.Description = Description;
                package.PackageProperties.Identifier = Id;
                package.PackageProperties.Version = Version.ToString();
                package.PackageProperties.Language = Language;
                package.PackageProperties.Keywords = ((IPackageMetadata)this).Tags;
            }
        }

        private void ValidateReferenceAssemblies() {
            var libFiles = new HashSet<string>(from file in Files
                                               where file.Path.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
                                               select Path.GetFileName(file.Path), StringComparer.OrdinalIgnoreCase);

            foreach (var reference in PackageAssemblyReferences) {
                if (!libFiles.Contains(reference) && !libFiles.Contains(reference + ".dll") && !libFiles.Contains(reference + ".dll")) {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_InvalidReference, reference));
                }
            }
        }

        private void ReadManifest(Stream stream, string basePath, IPropertyProvider propertyProvider) {
            // Deserialize the document and extract the metadata
            Manifest manifest = Manifest.ReadFrom(stream, propertyProvider);

            Populate(manifest.Metadata);

            // If there's no base path then ignore the files node
            if (basePath != null) {
                if (manifest.Files == null) {
                    AddFiles(basePath, @"**\*.*", null);
                }
                else {
                    PopulateFiles(basePath, manifest.Files);
                }
            }
        }

        public void Populate(ManifestMetadata manifestMetadata) {
            IPackageMetadata metadata = manifestMetadata;
            Id = metadata.Id;
            Version = metadata.Version;
            Title = metadata.Title;
            Authors.AddRange(metadata.Authors);
            Owners.AddRange(metadata.Owners);
            IconUrl = metadata.IconUrl;
            LicenseUrl = metadata.LicenseUrl;
            ProjectUrl = metadata.ProjectUrl;
            RequireLicenseAcceptance = metadata.RequireLicenseAcceptance;
            Description = metadata.Description;
            Summary = metadata.Summary;
            ReleaseNotes = metadata.ReleaseNotes;
            Language = metadata.Language;
            Copyright = metadata.Copyright;

            if (metadata.Tags != null) {
                Tags.AddRange(ParseTags(metadata.Tags));
            }

            Dependencies.AddRange(metadata.Dependencies);
            FrameworkReferences.AddRange(metadata.FrameworkAssemblies);
            PackageAssemblyReferences.AddRange(manifestMetadata.References.Select(reference => reference.File));
        }

        public void PopulateFiles(string basePath, IEnumerable<ManifestFile> files) {
            foreach (var file in files) {
                AddFiles(basePath, file.Source, file.Target, file.Exclude);
            }
        }

        private void WriteManifest(Package package) {
            Uri uri = UriUtility.CreatePartUri(Id + Constants.ManifestExtension);

            // Create the manifest relationship
            package.CreateRelationship(uri, TargetMode.Internal, Constants.SchemaNamespace + ManifestRelationType);

            // Create the part
            PackagePart packagePart = package.CreatePart(uri, DefaultContentType, CompressionOption.Maximum);

            using (Stream stream = packagePart.GetStream()) {
                Manifest manifest = Manifest.Create(this);
                if (!PackageAssemblyReferences.IsEmpty()) {
                    manifest.Metadata.References = new List<ManifestReference>(PackageAssemblyReferences.Select(reference => new ManifestReference { File = reference }));
                }
                manifest.Save(stream);
            }
        }

        private void WriteFiles(Package package) {
            // Add files that might not come from expanding files on disk
            foreach (IPackageFile file in new HashSet<IPackageFile>(Files)) {
                using (Stream stream = file.GetStream()) {
                    CreatePart(package, file.Path, stream);
                }
            }
        }

        private void AddFiles(string basePath, string source, string destination, string exclude = null) {
            List<PhysicalPackageFile> searchFiles = PathResolver.ResolveSearchPattern(basePath, source, destination).ToList();
            ExcludeFiles(searchFiles, basePath, exclude);

            if (!PathResolver.IsWildcardSearch(source) && !searchFiles.Any()) {
                throw new FileNotFoundException(String.Format(CultureInfo.CurrentCulture, NuGetResources.PackageAuthoring_FileNotFound,
                    source));
            }
            Files.AddRange(searchFiles);
        }

        private static void ExcludeFiles(List<PhysicalPackageFile> searchFiles, string basePath, string exclude) {
            if (String.IsNullOrEmpty(exclude)) {
                return;
            }

            // One or more exclusions may be specified in the file. Split it and prepend the base path to the wildcard provided.
            var exclusions = exclude.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in exclusions) {
                string wildCard = PathResolver.NormalizeWildcard(basePath, item);
                PathResolver.FilterPackageFiles(searchFiles, p => p.SourcePath, new[] { wildCard });
            }
        }

        private static void CreatePart(Package package, string path, Stream sourceStream) {
            if (PackageUtility.IsManifest(path)) {
                return;
            }

            Uri uri = UriUtility.CreatePartUri(path);

            // Create the part
            PackagePart packagePart = package.CreatePart(uri, DefaultContentType, CompressionOption.Maximum);
            using (Stream stream = packagePart.GetStream()) {
                sourceStream.CopyTo(stream);
            }
        }

        /// <summary>
        /// Tags come in this format. tag1 tag2 tag3 etc..
        /// </summary>
        private static IEnumerable<string> ParseTags(string tags) {
            Debug.Assert(tags != null);
            return from tag in tags.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                   select tag.Trim();
        }
    }
}