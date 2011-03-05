using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NuGet.Common;

namespace NuGet.Commands {
    [Command(typeof(NuGetResources), "pack", "PackageCommandDescription", MaxArgs = 1,
        UsageSummaryResourceName = "PackageCommandUsageSummary", UsageDescriptionResourceName = "PackageCommandUsageDescription")]
    public class PackCommand : Command {
        private static readonly Regex[] _defaultExcludes = new [] {
            // Exclude previous package files
            new Regex(Regex.Escape(Constants.PackageExtension) + '$', RegexOptions.IgnoreCase), 
            // Exclude the nuspec file
            new Regex(Regex.Escape(Constants.ManifestExtension) + '$', RegexOptions.IgnoreCase), 
            // Exclude all files and directories that begin with "."
            new Regex(@"(^|[/\\])\..+$", RegexOptions.IgnoreCase)
        };
        private readonly HashSet<string> _excludes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> _allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {  
            Constants.ManifestExtension,
            ".csproj",
            ".vbproj",
            ".fsproj",
        };

        [Option(typeof(NuGetResources), "PackageCommandOutputDirDescription")]
        public string OutputDirectory { get; set; }

        [Option(typeof(NuGetResources), "PackageCommandBasePathDescription")]
        public string BasePath { get; set; }

        [Option(typeof(NuGetResources), "PackageCommandVerboseDescription")]
        public bool Verbose { get; set; }

        [Option(typeof(NuGetResources), "PackageCommandVersionDescription")]
        public string Version { get; set; }

        [Option(typeof(NuGetResources), "PackageCommandDebugDescription")]
        public bool Debug { get; set; }

        [Option(typeof(NuGetResources), "PackageCommandSourcesDescription")]
        [Option(typeof(NuGetResources), "PackageCommandExcludeDescription")]
        public ICollection<string> Exclude {
            get { return _excludes; }
        }
        public bool Sources { get; set; }

        [Option(typeof(NuGetResources), "PackageCommandToolDescription")]
        public bool Tool { get; set; }

        public override void ExecuteCommand() {
            // Get the input file
            string path = GetInputFile();

            Console.WriteLine(NuGetResources.PackageCommandAttemptingToBuildPackage, Path.GetFileName(path));


            var basePath = BasePath ?? Path.GetDirectoryName(nuspecFile);
            PackageBuilder builder = new PackageBuilder(nuspecFile, basePath);
            
            if (!String.IsNullOrEmpty(Version)) {
                builder.Version = new Version(Version);
            }

            // Remove the output file or the package spec might try to include it (which is default behavior)
            ExcludeFiles(builder.Files, basePath, _excludes);

            // Get the output path
            string outputPath = GetOutputPath(builder);

            try {
                using (Stream stream = File.Create(outputPath)) {
                    builder.Save(stream);
                }
            }
            catch {
                if (File.Exists(outputPath)) {
                    File.Delete(outputPath);
                }
                throw;
            }

            if (Verbose) {
                PrintVerbose(outputPath);
            }


            Console.WriteLine(NuGetResources.PackageCommandSuccess, outputPath);
        }

        private void PrintVerbose(string outputPath) {
            Console.WriteLine();
            var package = new ZipPackage(outputPath);

            Console.WriteLine("Id: {0}", package.Id);
            Console.WriteLine("Version: {0}", package.Version);
            Console.WriteLine("Authors: {0}", String.Join(", ", package.Authors));
            Console.WriteLine("Description: {0}", package.Description);
            if (package.LicenseUrl != null) {
                Console.WriteLine("License Url: {0}", package.LicenseUrl);
            }
            if (package.ProjectUrl != null) {
                Console.WriteLine("Project Url: {0}", package.ProjectUrl);
            }
            if (!String.IsNullOrEmpty(package.Tags)) {
                Console.WriteLine("Tags: {0}", package.Tags.Trim());
            }
            if (package.Dependencies.Any()) {
                Console.WriteLine("Dependencies: {0}", String.Join(", ", package.Dependencies.Select(d => d.ToString())));
            }
            else {
                Console.WriteLine("Dependencies: None");
            }

            Console.WriteLine();

            foreach (var file in package.GetFiles().OrderBy(p => p.Path)) {
                Console.WriteLine(NuGetResources.PackageCommandAddedFile, file.Path);
            }

            Console.WriteLine();
        }

        internal static void ExcludeFiles(ICollection<IPackageFile> packageFiles, string basePath, IEnumerable<string> excludeFilters) {
            var filters = excludeFilters.Select(WildCardToRegex).Concat(_defaultExcludes);
            packageFiles.RemoveAll(item => {
                // The Path property of IPackageFile returns the target path, the path inside the package.
                // Since excludes would need to be performed against the physical path on disk, cast to it and use the SourcePath property
                Debug.Assert(item is PhysicalPackageFile);
                string path = (item as PhysicalPackageFile).SourcePath;

                // Trim out the basePath portion of the path since wildcards would be relative to the basePath.
                int index = path.IndexOf(basePath, StringComparison.OrdinalIgnoreCase);
                if (index != -1) {
                    path = path.Substring(index + basePath.Length);
                }
                return (filters.Any(f => f.IsMatch(path)));
            });
        }

        private static Regex WildCardToRegex(string wildCard) {
            return new Regex('^' + Regex.Escape(wildCard).Replace(@"\*", ".*").Replace(@"\?", ".") + '$', RegexOptions.IgnoreCase);
        }

        private string GetOutputPath(PackageBuilder builder) {
            // Output file is {id}.{version}
            string outputFile = builder.Id + "." + builder.Version;


            // If this is a source package then add .Sources to the package file name
            if (Sources) {
                outputFile += ".sources";
            }

            // Add the extension
            outputFile += Constants.PackageExtension;

            string outputDirectory = OutputDirectory ?? Directory.GetCurrentDirectory();
            return Path.Combine(outputDirectory, outputFile);
        }

        private PackageBuilder GetPackageBuilder(string path) {
            string extension = Path.GetExtension(path).ToLowerInvariant();

            switch (extension) {
                case ".nuspec":
                    return BuildFromNuspec(path);
                default:
                    return BuildFromProjectFile(path);
            }
        }

        private PackageBuilder BuildFromNuspec(string path) {
            if (String.IsNullOrEmpty(BasePath)) {
                return new PackageBuilder(path);
            }

            return new PackageBuilder(path, BasePath);
        }

        private PackageBuilder BuildFromProjectFile(string path) {
            var projectBuilder = new ProjectPackageBuilder(path, Console) {
                IncludeSources = Sources,
                Debug = Debug,
                IsTool = Tool
            };

            return projectBuilder.BuildPackage();
        }

        private string GetInputFile() {
            IEnumerable<string> files = null;

            if (Arguments.Any()) {
                files = Arguments;
            }
            else {
                files = Directory.GetFiles(Directory.GetCurrentDirectory());
            }

            var candidates = files.Where(file => _allowedExtensions.Contains(Path.GetExtension(file)))
                                  .ToList();

            switch (candidates.Count) {
                case 1:
                    return candidates.Single();
                default:
                    throw new CommandLineException(NuGetResources.PackageCommandSpecifyInputFileError);
            }
        }
    }
}
