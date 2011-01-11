using System;
using System.Diagnostics;
using System.IO;

namespace NuGet {
    internal static class PathResolver {
        public static PathSearchFilter ResolveSearchFilter(string basePath, string source) {
            basePath = basePath ?? String.Empty;
            string pathFromBase = Path.Combine(basePath, source.TrimStart(Path.DirectorySeparatorChar));

            if (IsWildCardSearch(pathFromBase)) {
                return GetPathSearchFilter(pathFromBase);
            }
            else {
                pathFromBase = Path.GetFullPath(pathFromBase.TrimStart(Path.DirectorySeparatorChar));
                string directory = Path.GetDirectoryName(pathFromBase);
                string searchFilter = Path.GetFileName(pathFromBase);
                return new PathSearchFilter {
                    SearchDirectory = NormalizeSearchDirectory(directory),
                    SearchPattern = NormalizeSearchFilter(searchFilter),
                    SearchOption = SearchOption.TopDirectoryOnly,
                    WildCardSearch = false
                };
            }
        }

        private static PathSearchFilter GetPathSearchFilter(string path) {
            Debug.Assert(IsWildCardSearch(path));

            var searchFilter = new PathSearchFilter { WildCardSearch = true };
            int recursiveSearchIndex = path.IndexOf("**", StringComparison.OrdinalIgnoreCase);

            if (recursiveSearchIndex != -1) {
                // Recursive searches are of the format /foo/bar/**/*[.abc]
                string searchPattern = path.Substring(recursiveSearchIndex + 2).TrimStart(Path.DirectorySeparatorChar);
                string searchDirectory = recursiveSearchIndex == 0 ? "." : path.Substring(0, recursiveSearchIndex - 1);

                searchFilter.SearchDirectory = NormalizeSearchDirectory(searchDirectory);
                searchFilter.SearchPattern = NormalizeSearchFilter(searchPattern);
                searchFilter.SearchOption = SearchOption.AllDirectories;
            }
            else {
                string searchDirectory = Path.GetDirectoryName(path);
                string searchPattern = Path.GetFileName(path);

                searchFilter.SearchDirectory = NormalizeSearchDirectory(searchDirectory);
                searchFilter.SearchPattern = NormalizeSearchFilter(searchPattern);
                searchFilter.SearchOption = SearchOption.TopDirectoryOnly;
            }

            return searchFilter;
        }

        /// <summary>
        /// Resolves the path of a file inside of a package 
        /// For paths that are relative, the destination path is resovled as the path relative to the basePath (path to the manifest file)
        /// For all other paths, the path is resolved as the first path portion that does not contain a wildcard character
        /// </summary>
        /// <param name="searchFilter">The search filter used to add the file.</param>
        /// <param name="path">The absolute path to the file being added.</param>
        /// <param name="targetPath">The target path prefix for the .</param>
        public static string ResolvePackagePath(PathSearchFilter searchFilter, string path, string targetPath) {
            string fileName = Path.GetFileName(path);
            string packagePath = null;

            if (!searchFilter.WildCardSearch && Path.GetExtension(searchFilter.SearchPattern).Equals(Path.GetExtension(targetPath), StringComparison.OrdinalIgnoreCase)) {
                // If the search does not contain wild cards, and the target path shares the same extension, copy it
                // e.g. <file src="ie\css\style.css" target="Content\css\ie.css" /> --> Content\css\ie.css
                return targetPath;
            }
            else if (path.StartsWith(searchFilter.SearchDirectory, StringComparison.OrdinalIgnoreCase)) {
                packagePath = path.Substring(searchFilter.SearchDirectory.Length).TrimStart(Path.DirectorySeparatorChar);
            }
            else {
                packagePath = fileName;
            }

            return Path.Combine(targetPath ?? String.Empty, packagePath);
        }

        private static string NormalizeSearchDirectory(string directory) {
            return Path.GetFullPath(String.IsNullOrEmpty(directory) ? "." : directory);
        }

        private static string NormalizeSearchFilter(string filter) {
            return String.IsNullOrEmpty(filter) ? "*" : filter;
        }

        /// <summary>
        /// Returns true if the path does not contain any wildcards.
        /// </summary>
        private static bool IsWildCardSearch(string filter) {
            return filter.IndexOf('*') != -1;
        }
    }
}
