﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Test;

namespace NuGet.VisualStudio.Test {

    using PackageUtility = NuGet.Test.PackageUtility;
    

    [TestClass]
    public class RecentPackageRepositoryTest {

        [TestMethod]
        public void RemovePackageMethodThrow() {
            // Arrange
            var repository = CreateRecentPackageRepository();

            // Act & Assert
            ExceptionAssert.Throws<NotSupportedException>(() => repository.RemovePackage(null));
        }

        [TestMethod]
        public void TestGetPackagesReturnNoPackageIfThereIsNoPackageMetadata() {
            // Arrange
            var repository = CreateRecentPackageRepository(empty: true);

            // Act
            var packages = repository.GetPackages().ToList();

            // Assert
            Assert.AreEqual(0, packages.Count);
        }

        [TestMethod]
        public void TestGetPackagesReturnCorrectNumberOfPackages() {
            // Scenario: The remote repository contains package A and C
            // Recent settings store contains metadata for A and B
            // Calling GetPackages() should return package A.

            // Arrange
            var repository = CreateRecentPackageRepository();

            // Act
            var packages = repository.GetPackages().ToList();

            // Assert
            Assert.AreEqual(1, packages.Count);
            Assert.AreEqual("A", packages[0].Id);
            Assert.AreEqual(new Version("1.0"), packages[0].Version);
        }

        [TestMethod]
        public void TestGetPackagesReturnPackagesSortedByDateByDefault() {
            // Scenario: The remote repository contains package A, B and C
            // Recent settings store contains metadata for A and B
            // Calling GetPackages() should return package A and B, sorted by date (B goes before A)

            // Arrange
            var packageA = PackageUtility.CreatePackage("A", "1.0");
            var packageB = PackageUtility.CreatePackage("B", "2.0");
            var packageC = PackageUtility.CreatePackage("C", "2.0");

            var packagesList = new[] { packageA, packageB, packageC };
            var repository = CreateRecentPackageRepository(packagesList: packagesList);

            // Act
            var packages = repository.GetPackages().ToList();

            // Assert
            Assert.AreEqual(2, packages.Count);
            Assert.AreEqual("B", packages[0].Id);
            Assert.AreEqual(new Version("2.0"), packages[0].Version);

            Assert.AreEqual("A", packages[1].Id);
            Assert.AreEqual(new Version("1.0"), packages[1].Version);
        }

        [TestMethod]
        public void TestGetPackagesReturnCorrectNumberOfPackagesAfterAddingPackage() {
            // Scenario: The remote repository contains package A and C
            // Recent settings store contains metadata for A and B
            // Calling AddPackage(packageC)
            // Now GetPackages() should return A and C

            // Arrange
            var repository = CreateRecentPackageRepository();
            var packageC = PackageUtility.CreatePackage("C", "2.0");
            var recentPackageC = packageC;

            repository.AddPackage(recentPackageC);

            // Act
            var packages = repository.GetPackages().ToList();

            // Assert
            Assert.AreEqual(2, packages.Count);
            Assert.AreEqual("A", packages[1].Id);
            Assert.AreEqual(new Version("1.0"), packages[1].Version);

            Assert.AreEqual("C", packages[0].Id);
            Assert.AreEqual(new Version("2.0"), packages[0].Version);
        }

        [TestMethod]
        public void CallingClearMethodClearsAllPackagesFromSettingsStore() {
            // Arrange
            var repository = CreateRecentPackageRepository();

            // Act
            repository.Clear();
            var packages = repository.GetPackages().ToList();

            // Assert
            Assert.AreEqual(0, packages.Count);
        }

        private RecentPackagesRepository CreateRecentPackageRepository(bool empty = false, IPackage[] packagesList = null) {
            if (packagesList == null) {
                var packageA = PackageUtility.CreatePackage("A", "1.0");
                var packageB = PackageUtility.CreatePackage("C", "2.0");

                packagesList = new[] { packageA, packageB };
            }

            var mockRepository = new Mock<IPackageRepository>();
            mockRepository.Setup(p => p.GetPackages()).Returns(packagesList.AsQueryable());

            var mockRepositoryFactory = new Mock<IPackageRepositoryFactory>();
            mockRepositoryFactory.Setup(f => f.CreateRepository(It.IsAny<PackageSource>())).Returns(mockRepository.Object);

            var mockSettingsManager = new MockSettingsManager();

            if (!empty) {
                var A = new PersistencePackageMetadata("A", "1.0", new DateTime(2010, 8, 12));
                var B = new PersistencePackageMetadata("B", "2.0", new DateTime(2011, 3, 2));

                mockSettingsManager.SavePackageMetadata(new PersistencePackageMetadata[] { A, B });
            }

            var mockPackageSourceProvider = new Mock<IPackageSourceProvider>();
            mockPackageSourceProvider.Setup(p => p.AggregateSource).Returns(new PackageSource("All") { IsAggregate = true });

            return new RecentPackagesRepository(null, mockRepositoryFactory.Object, mockPackageSourceProvider.Object, mockSettingsManager);
        }

        private class MockSettingsManager : IPersistencePackageSettingsManager {

            List<PersistencePackageMetadata> _items = new List<PersistencePackageMetadata>();

            public System.Collections.Generic.IEnumerable<PersistencePackageMetadata> LoadPackageMetadata(int maximumCount) {
                return _items.Take(maximumCount);
            }

            public void SavePackageMetadata(System.Collections.Generic.IEnumerable<PersistencePackageMetadata> packageMetadata) {
                _items.Clear();
                _items.AddRange(packageMetadata);
            }

            public void ClearPackageMetadata() {
                _items.Clear();
            }
        }
    }
}