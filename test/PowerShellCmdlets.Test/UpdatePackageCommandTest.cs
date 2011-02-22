using System;
using System.Management.Automation;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Test;
using NuGet.VisualStudio;
using NuGet.VisualStudio.Test;

namespace NuGet.PowerShell.Commands.Test {
    [TestClass]
    public class UpdatePackageCommandTest {
        [TestMethod]
        public void UpdatePackageCmdletThrowsWhenSolutionIsClosed() {
            // Arrange
            var packageManagerFactory = new Mock<IVsPackageManagerFactory>();
            packageManagerFactory.Setup(m => m.CreatePackageManager()).Returns((IVsPackageManager)null);
            var cmdlet = new UpdatePackageCommand(TestUtils.GetSolutionManager(isSolutionOpen: false), packageManagerFactory.Object, null);

            // Act and Assert
            ExceptionAssert.Throws<InvalidOperationException>(() => cmdlet.GetResults(),
                "The current environment doesn't have a solution open.");
        }

        [TestMethod]
        public void UpdatePackageCmdletUsesPackageManangerWithSourceIfSpecified() {
            // Arrange
            var packageManagerFactory = new Mock<IVsPackageManagerFactory>();
            var vsPackageManager = new MockVsPackageManager();
            var sourceVsPackageManager = new MockVsPackageManager();
            packageManagerFactory.Setup(m => m.CreatePackageManager()).Returns(vsPackageManager);
            packageManagerFactory.Setup(m => m.CreatePackageManager("somesource")).Returns(sourceVsPackageManager);
            var cmdlet = new Mock<UpdatePackageCommand>(TestUtils.GetSolutionManager(), packageManagerFactory.Object, null) { CallBase = true };
            cmdlet.Object.Source = "somesource";
            cmdlet.Object.Id = "my-id";
            cmdlet.Object.Version = new Version("2.8");

            // Act
            cmdlet.Object.Execute();

            // Assert
            Assert.AreSame(sourceVsPackageManager, cmdlet.Object.PackageManager);
        }

        [TestMethod]
        public void UpdatePackageCmdletPassesParametersCorrectlyWhenIdAndVersionAreSpecified() {
            // Arrange
            var vsPackageManager = new MockVsPackageManager();
            var packageManagerFactory = new Mock<IVsPackageManagerFactory>();
            packageManagerFactory.Setup(m => m.CreatePackageManager()).Returns(vsPackageManager);
            var cmdlet = new Mock<UpdatePackageCommand>(TestUtils.GetSolutionManager(), packageManagerFactory.Object, null) { CallBase = true };
            cmdlet.Object.Id = "my-id";
            cmdlet.Object.Version = new Version("2.8");

            // Act
            cmdlet.Object.Execute();

            // Assert
            Assert.AreEqual("my-id", vsPackageManager.PackageId);
            Assert.AreEqual(new Version("2.8"), vsPackageManager.Version);
        }

        [TestMethod]
        public void UpdatePackageCmdletPassesIgnoreDependencySwitchCorrectly() {
            // Arrange
            var vsPackageManager = new MockVsPackageManager();
            var packageManagerFactory = new Mock<IVsPackageManagerFactory>();
            packageManagerFactory.Setup(m => m.CreatePackageManager()).Returns(vsPackageManager);
            var cmdlet = new Mock<UpdatePackageCommand>(TestUtils.GetSolutionManager(), packageManagerFactory.Object, null) { CallBase = true };
            cmdlet.Object.Id = "my-id";
            cmdlet.Object.Version = new Version("2.8");

            // Act
            cmdlet.Object.Execute();

            // Assert
            Assert.AreEqual("my-id", vsPackageManager.PackageId);
            Assert.AreEqual(new Version("2.8"),  vsPackageManager.Version);
            Assert.IsTrue(vsPackageManager.UpdateDependencies);
        }

        [TestMethod]
        public void UpdatePackageCmdletPassesIgnoreDependencySwitchCorrectlyWhenPresent() {
            // Arrange
            var vsPackageManager = new MockVsPackageManager();
            var packageManagerFactory = new Mock<IVsPackageManagerFactory>();
            packageManagerFactory.Setup(m => m.CreatePackageManager()).Returns(vsPackageManager);
            var cmdlet = new Mock<UpdatePackageCommand>(TestUtils.GetSolutionManager(), packageManagerFactory.Object, null) { CallBase = true };
            cmdlet.Object.Id = "my-id";
            cmdlet.Object.Version = new Version("2.8");
            cmdlet.Object.IgnoreDependencies = new SwitchParameter(isPresent: true);

            // Act
            cmdlet.Object.Execute();

            // Assert
            Assert.AreEqual("my-id", vsPackageManager.PackageId);
            Assert.AreEqual(new Version("2.8"), vsPackageManager.Version);
            Assert.IsFalse(vsPackageManager.UpdateDependencies);
        }

        private class MockVsPackageManager : VsPackageManager {
            public MockVsPackageManager()
                : base(new Mock<ISolutionManager>().Object, new Mock<IPackageRepository>().Object, new Mock<IFileSystem>().Object, new Mock<ISharedPackageRepository>().Object, new Mock<IRecentPackageRepository>().Object) {
            }

            public IProjectManager ProjectManager { get; set; }

            public string PackageId { get; set; }

            public Version Version { get; set; }

            public bool UpdateDependencies { get; set; }

            public override void UpdatePackage(IProjectManager projectManager, string packageId, Version version, bool updateDependencies, ILogger logger) {
                ProjectManager = projectManager;
                PackageId = packageId;
                Version = version;
                UpdateDependencies = updateDependencies;
            }

            public override IProjectManager GetProjectManager(Project project) {
                return new Mock<IProjectManager>().Object;
            }
        }

    }
}
