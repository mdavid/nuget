using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NuGet.Test {
    [TestClass]
    public class PathResolverTest {
        private const string _basePath = @"X:\abc\efg";

        [TestMethod]
        public void PathWithLocalFileReturnSingleResult() {
            // Arrange
            var path = "foo.txt";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "foo.txt", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act 
            PathSearchFilter searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithLocalFileAndLeadingDirectorySepReturnSingleResult() {
            // Arrange
            var path = "\\foo.txt";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "foo.txt", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act 
            PathSearchFilter searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void RelativeFilePathReturnSingleResult() {
            // Arrange
            var path = "..\\..\\foo.txt";
            var directory = Path.GetDirectoryName(Path.GetDirectoryName(_basePath));
            var expectedFilter = new PathSearchFilter { SearchDirectory = directory, SearchPattern = "foo.txt", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act 
            PathSearchFilter searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }


        [TestMethod]
        public void RootedFilePathReturnSingleResult() {
            // Arrange
            var path = "Y:\\foo\\bar.txt";
            var expectedFilter = new PathSearchFilter { SearchDirectory = "Y:\\foo", SearchPattern = "bar.txt", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act 
            PathSearchFilter searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithSingleWildCard() {
            // Arrange
            var path = "*";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "*", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithWildCardFileName() {
            // Arrange
            var path = "*.foo";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "*.foo", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithWildCardExtension() {
            // Arrange
            var path = "jquery.*";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "jquery.*", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithWildCardFileNameAndExtension() {
            // Arrange
            var path = "*.*";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "*.*", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithLeadingDirectorySepDirectoryAndWildCardFileNameAndExtension() {
            // Arrange
            var path = "\\foo\\*";
            var expectedFilter = new PathSearchFilter { SearchDirectory = Path.Combine(_basePath, "foo"), SearchPattern = "*", SearchOption = SearchOption.TopDirectoryOnly };

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithDirectoryAndWildCardFileNameAndExtension() {
            // Arrange
            var path = "foo\\*";
            var expectedFilter = new PathSearchFilter { SearchDirectory = Path.Combine(_basePath, "foo"), SearchPattern = "*", SearchOption = SearchOption.TopDirectoryOnly };

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithDirectoryAndWildCardFileName() {
            // Arrange
            var path = "\\bar\\foo\\*.baz";
            var expectedFilter = new PathSearchFilter { SearchDirectory = Path.Combine(_basePath, "bar\\foo"), SearchPattern = "*.baz", SearchOption = SearchOption.TopDirectoryOnly };

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void RelativePathWildCardExtension() {
            // Arrange
            var path = "..\\..\\bar\\baz\\*.foo";
            var directory = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(_basePath)), "bar\\baz");
            var expectedFilter = new PathSearchFilter { SearchDirectory = directory, SearchPattern = "*.foo", SearchOption = SearchOption.TopDirectoryOnly }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithRecursiveWildCardSearch() {
            // Arrange
            var path = @"foo\**\.bar";
            var expectedFilter = new PathSearchFilter { SearchDirectory = Path.Combine(_basePath, "foo"), SearchPattern = ".bar", SearchOption = SearchOption.AllDirectories };

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithRecursiveWildCardSearchContainingDirectoryStructure() {
            // Arrange
            var path = @"foo\**\baz\.bar";
            var expectedFilter = new PathSearchFilter { SearchDirectory = Path.Combine(_basePath, "foo"), SearchPattern = "baz\\.bar", SearchOption = SearchOption.AllDirectories };

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithRecursiveWildCardSearchContainingNoExtension() {
            // Arrange
            var path = @"foo\**\*";
            var expectedFilter = new PathSearchFilter { SearchDirectory = Path.Combine(_basePath, "foo"), SearchPattern = "*", SearchOption = SearchOption.AllDirectories };

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithRecursiveWildCardSearchContainingNoLeadingDirectory() {
            // Arrange
            var path = @"**\*.txt";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "*.txt", SearchOption = SearchOption.AllDirectories }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithOnlyRecursiveWildCardSearchChars() {
            // Arrange
            var path = @"**";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "*", SearchOption = SearchOption.AllDirectories }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void PathWithInvalidRecursiveWildCardSearch() {
            // Arrange
            var path = @"**foo/bar";
            var expectedFilter = new PathSearchFilter { SearchDirectory = _basePath, SearchPattern = "foo/bar", SearchOption = SearchOption.AllDirectories }; ;

            // Act
            var searchFilter = PathResolver.ResolvePath(_basePath, path);

            // Assert
            AssertEqual(expectedFilter, searchFilter);
        }

        [TestMethod]
        public void DestinationPathResolverGeneratesRelativePaths() {
            // Arrange
            var path = @"root\sub-dir\foo\bar.txt";
            var basePath = @"root\sub-dir";

            // Act
            var result = PathResolver.ResolvePackagePath("*.*", basePath, path, String.Empty);

            // Assert
            Assert.AreEqual(@"foo\bar.txt", result);
        }

        [TestMethod]
        public void DestinationPathResolverGeneratesRelativePathsPrependedWithTargetPath() {
            // Arrange
            var path = @"dir\subdir\foo\bar.txt";
            var basePath = @"dir\subdir";
            var targetPath = @"\abc\cdf";

            // Act
            var result = PathResolver.ResolvePackagePath("*.*", basePath, path, targetPath);

            // Assert
            Assert.AreEqual(Path.Combine(targetPath, @"foo\bar.txt"), result);
        }

        [TestMethod]
        public void DestinationPathResolverReturnsFileNamesForNonRelativePaths() {
            // Arrange
            var searchPath = @"z:\bar\something.txt";
            var path = searchPath;
            var basePath = String.Empty;

            // Act
            var result = PathResolver.ResolvePackagePath(searchPath, basePath, path, String.Empty);

            // Assert
            Assert.AreEqual(@"something.txt", result);
        }

        [TestMethod]
        public void DestinationPathResolverReturnsFileNamesForPathsInBasePath() {
            // Arrange
            var path = @"dir\subdir\something.txt";
            var basePath = @"dir\subdir";

            // Act
            var result = PathResolver.ResolvePackagePath("*.*", basePath, path, String.Empty);

            // Assert
            Assert.AreEqual(@"something.txt", result);
        }

        [TestMethod]
        public void DestinationPathResolverPrependsTargetPath() {
            // Arrange
            var path = @"dir\subdir\something.txt";
            var basePath = @"dir\subdir";
            var targetPath = "foo";
            // Act
            var result = PathResolver.ResolvePackagePath("*.*", basePath, path, targetPath);

            // Assert
            Assert.AreEqual(Path.Combine(targetPath, "something.txt"), result);
        }

        [TestMethod]
        public void PathResolverTruncatesRecursiveWildCardInSearchPathWhenNoTargetPathSpecified() {
            // Arrange
            var path = @"root\folder\sub-folder\somefile.txt";
            var basePath = "root";
            var targetPath = String.Empty;

            // Act
            var result = PathResolver.ResolvePackagePath(@"folder\**", basePath, path, targetPath);

            // Assert
            Assert.AreEqual(@"sub-folder\somefile.txt", result);
        }

        [TestMethod]
        public void PathResolverTruncatesRecursiveWildCardInSearchPathWhenTargetPathSpecified() {
            // Arrange
            var path = @"root\dir\subdir\pack.dll";
            var basePath = @"";
            var targetPath = @"lib\sl4";

            // Act
            var result = PathResolver.ResolvePackagePath(@"root\dir\**", basePath, path, targetPath);

            // Assert
            Assert.AreEqual(@"lib\sl4\subdir\pack.dll", result);
        }

        [TestMethod]
        public void PathResolverTruncatesWildCardInSearchPathWhenNoTargetPathSpecified() {
            // Arrange
            var path = @"root\dir\subdir\pack.dll";
            var basePath = @"";
            var targetPath = String.Empty;

            // Act
            var result = PathResolver.ResolvePackagePath(@"root\dir\subdir\**\*.dll", basePath, path, targetPath);

            // Assert
            Assert.AreEqual(@"pack.dll", result);
        }

        [TestMethod]
        public void PathResolverTruncatesWildCardInSearchPathWhenTargetPathSpecified() {
            // Arrange
            var path = @"root\dir\subdir\pack.dll";
            var basePath = @"root";
            var targetPath = @"lib\sl4";

            // Act
            var result = PathResolver.ResolvePackagePath(@"dir\subdir\*.dll", basePath, path, targetPath);

            // Assert
            Assert.AreEqual(@"lib\sl4\pack.dll", result);
        }

        [TestMethod]
        public void PathResolverUsesTargetPathWhenFileExtensionMatchesAndSearchPathDoesNotContainWildcard() {
            // Arrange
            var path = @"root\dir\subdir\foo.dll";
            var basePath = @"root";
            var targetPath = @"lib\sl4\bar.dll";

            // Act
            var result = PathResolver.ResolvePackagePath(@"dir\subdir\foo.dll", basePath, path, targetPath);

            // Assert
            Assert.AreEqual(targetPath, result);
        }

        [TestMethod]
        public void PathResolverDoesNotLookForSearchTermThatOccurInBasePath() {
            // Arrange
            var actualPath = @"X:\drops\bin\Release\Output\bin\release\myfile.dll";
            var basePath = @"X:\drops\bin\Release\Output\";
            var searchString = @"bin\release\*.dll";
            var targetPath = @"lib\net40";

            // Act
            var result = PathResolver.ResolvePackagePath(searchString: searchString, basePath: basePath, actualPath: actualPath, targetPath: targetPath);

            // Assert
            Assert.AreEqual(@"lib\net40\myfile.dll", result);
        }

        private void AssertEqual(PathSearchFilter expected, PathSearchFilter actual) {
            Assert.AreEqual(expected.SearchDirectory, actual.SearchDirectory);
            Assert.AreEqual(expected.SearchOption, actual.SearchOption);
            Assert.AreEqual(expected.SearchPattern, actual.SearchPattern);
        }
    }
}