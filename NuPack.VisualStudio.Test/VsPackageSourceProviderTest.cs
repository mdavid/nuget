﻿using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Test;
using System;

namespace NuGet.VisualStudio.Test {
    [TestClass]
    public class VsPackageSourceProviderTest {
        [TestMethod]
        public void AddSourceThrowsIfSourceIsNull() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            ExceptionAssert.ThrowsArgNull(() => provider.AddPackageSource(null), "source");
        }

        [TestMethod]
        public void CtorIfFirstRunningAddsDefaultSource() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            var sources = provider.GetPackageSources().ToList();


            // Assert
            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual(VsPackageSourceProvider.DefaultPackageSource, sources[1].Source);
        }

        [TestMethod]
        public void CtorAddsDefaultSourceIfAnotherDefaultWasPreviouslyRegistered() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            settingsManager.PackageSourcesString = String.Format("<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><Name>NuGet official package source</Name><Source>http://some/old/feed</Source></PackageSource></ArrayOfPackageSource>");
            settingsManager.ActivePackageSourceString = "<PackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><IsAggregate>true</IsAggregate><Name>All</Name><Source>(Aggregate source)</Source></PackageSource>";
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            var sources = provider.GetPackageSources().ToList();


            // Assert
            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual(VsPackageSourceProvider.DefaultPackageSource, sources[1].Source);
        }

        [TestMethod]
        public void CtorAddsAggregrateIfNothingWasPersistedIntoSettingsManager() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            var sources = provider.GetPackageSources().ToList();


            // Assert
            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual(VsPackageSourceProvider.AggregateSource, sources[0]);
        }

        [TestMethod]
        public void CtorAddsAggregrateIfAggregateWasPersistedIntoSettingsManager() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            settingsManager.PackageSourcesString = "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>false</IsAggregate><Name>a</Name><Source>a</Source></PackageSource></ArrayOfPackageSource>";
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            var sources = provider.GetPackageSources().ToList();


            // Assert
            Assert.AreEqual(3, sources.Count);
            Assert.AreEqual(VsPackageSourceProvider.AggregateSource, sources[0]);
            Assert.AreEqual(new PackageSource("a", "a"), sources[1]);
        }

        [TestMethod]
        public void CtorDoesNotAddNewAggregrateIfAggregatePersistedIntoSettingsManager() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            settingsManager.PackageSourcesString = "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>true</IsAggregate><Name>All</Name><Source>(Aggregate source)</Source></PackageSource></ArrayOfPackageSource>";
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            var sources = provider.GetPackageSources().ToList();


            // Assert
            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual(VsPackageSourceProvider.AggregateSource, sources[0]);
        }

        [TestMethod]
        public void CtorDoesNotAddNewAggregrateIfAggregatePersistedIntoSettingsManagerAndAggregateIsActivePackageSource() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            settingsManager.PackageSourcesString = "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>true</IsAggregate><Name>All</Name><Source>(Aggregate source)</Source></PackageSource></ArrayOfPackageSource>";
            settingsManager.ActivePackageSourceString = "<PackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><IsAggregate>true</IsAggregate><Name>All</Name><Source>(Aggregate source)</Source></PackageSource>";
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            var sources = provider.GetPackageSources().ToList();


            // Assert
            Assert.AreEqual(2, sources.Count);
            Assert.AreEqual(VsPackageSourceProvider.AggregateSource, sources[0]);
        }

        [TestMethod]
        public void AddSourceSetsPersistsSourcesToSettingsManager() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            var provider = new VsPackageSourceProvider(settingsManager);
            var source = new PackageSource("a", "a");

            // Act
            provider.AddPackageSource(source);

            // Assert
            Assert.AreEqual(
                String.Format(
                    "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>true</IsAggregate><Name>All</Name><Source>(Aggregate source)</Source></PackageSource><PackageSource><IsAggregate>false</IsAggregate><Name>{0}</Name><Source>{1}</Source></PackageSource><PackageSource><IsAggregate>false</IsAggregate><Name>a</Name><Source>a</Source></PackageSource></ArrayOfPackageSource>",
                    VsPackageSourceProvider.OfficialFeedName,
                    VsPackageSourceProvider.DefaultPackageSource),
                settingsManager.PackageSourcesString);
        }

        [TestMethod]
        public void RemoveSourceThrowsIfSourceIsNull() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            ExceptionAssert.ThrowsArgNull(() => provider.RemovePackageSource(null), "source");
        }

        [TestMethod]
        public void RemovingUnknownPackageSourceReturnsFalse() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            bool result = provider.RemovePackageSource(new PackageSource("a", "a"));

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RemovingExistingPackageSourceReturnsFalse() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            settingsManager.PackageSourcesString = "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>false</IsAggregate><Name>a</Name><Source>a</Source></PackageSource></ArrayOfPackageSource>";
            var provider = new VsPackageSourceProvider(settingsManager);
            var packageSource = new PackageSource("a", "a");

            // Act
            bool result = provider.RemovePackageSource(packageSource);

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual(
                String.Format(
                    "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>true</IsAggregate><Name>All</Name><Source>(Aggregate source)</Source></PackageSource><PackageSource><IsAggregate>false</IsAggregate><Name>{0}</Name><Source>{1}</Source></PackageSource></ArrayOfPackageSource>",
                    VsPackageSourceProvider.OfficialFeedName,
                    VsPackageSourceProvider.DefaultPackageSource),
                settingsManager.PackageSourcesString);
        }

        [TestMethod]
        public void RemovingActivePackageSourceSetsActivePackageSourceToNull() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            settingsManager.PackageSourcesString = "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>false</IsAggregate><Name>a</Name><Source>a</Source></PackageSource></ArrayOfPackageSource>";
            var provider = new VsPackageSourceProvider(settingsManager);
            var packageSource = new PackageSource("a", "a");
            provider.ActivePackageSource = packageSource;

            // Act
            bool result = provider.RemovePackageSource(packageSource);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNull(provider.ActivePackageSource);
            Assert.AreEqual(
                String.Format(
                    "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>true</IsAggregate><Name>All</Name><Source>(Aggregate source)</Source></PackageSource><PackageSource><IsAggregate>false</IsAggregate><Name>{0}</Name><Source>{1}</Source></PackageSource></ArrayOfPackageSource>",
                    VsPackageSourceProvider.OfficialFeedName,
                    VsPackageSourceProvider.DefaultPackageSource),
                settingsManager.PackageSourcesString);
        }

        [TestMethod]
        public void SettingActivePackageSourceToNonExistantSourceThrows() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            ExceptionAssert.ThrowsArgumentException(() => provider.ActivePackageSource = new PackageSource("a", "a"), "value", "The package source does not belong to the collection of available sources.");
        }

        [TestMethod]
        public void SettingActivePackageSourceSetsActivePackageStringOnSettingsManager() {
            // Arrange
            var settingsManager = new MockPackageSourceSettingsManager();
            settingsManager.PackageSourcesString = "<ArrayOfPackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><PackageSource><IsAggregate>false</IsAggregate><Name>a</Name><Source>a</Source></PackageSource></ArrayOfPackageSource>";
            var provider = new VsPackageSourceProvider(settingsManager);

            // Act
            provider.ActivePackageSource = new PackageSource("a", "a");

            // Assert
            Assert.AreEqual("<PackageSource xmlns=\"http://schemas.datacontract.org/2004/07/NuGet\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\"><IsAggregate>false</IsAggregate><Name>a</Name><Source>a</Source></PackageSource>", settingsManager.ActivePackageSourceString);
        }

        private class MockPackageSourceSettingsManager : IPackageSourceSettingsManager {
            public MockPackageSourceSettingsManager() {
                //OfficialFeed = VsPackageSourceProvider.DefaultPackageSource;
            }

            public string ActivePackageSourceString {
                get;
                set;
            }

            public string PackageSourcesString {
                get;
                set;
            }
        }
    }
}
