﻿using System;
using Microsoft.VisualStudio.Shell.Interop;
using Moq;
using NuGet.Test.Mocks;
using Xunit;

namespace NuGet.VisualStudio.Test
{
    public class VsSourceControlTrackerTest
    {
        [Fact]
        public void ConstructorStartTrackingIfSolutionIsOpen()
        {
            // Arrange
            var solutionManager = new Mock<ISolutionManager>();
            var fileSystemProvider = new Mock<IFileSystemProvider>();
            var projectDocumentsEvents = new Mock<IVsTrackProjectDocuments2>();

            solutionManager.Setup(s => s.IsSolutionOpen).Returns(true);
            solutionManager.Setup(s => s.SolutionDirectory).Returns("foo:\\bar");

            // Act
            var scTracker = new VsSourceControlTracker(
                solutionManager.Object, fileSystemProvider.Object, projectDocumentsEvents.Object);

            // Assert
            uint cookie;
            projectDocumentsEvents.Verify(
                p => p.AdviseTrackProjectDocumentsEvents(It.IsAny<IVsTrackProjectDocumentsEvents2>(), out cookie),
                Times.Once());
        }

        [Fact]
        public void StartTrackingIfNewSolutionIsOpen()
        {
            // Arrange
            var solutionManager = new Mock<ISolutionManager>();
            var fileSystemProvider = new Mock<IFileSystemProvider>();
            var projectDocumentsEvents = new Mock<IVsTrackProjectDocuments2>();
            solutionManager.Setup(s => s.IsSolutionOpen).Returns(false);
            solutionManager.Setup(s => s.SolutionDirectory).Returns("baz:\\foo");

            var fileSystem = new Mock<IFileSystem>();
            fileSystemProvider.Setup(f => f.GetFileSystem("baz:\\foo\\.nuget")).Returns(fileSystem.Object);

            // Act
            var scTracker = new VsSourceControlTracker(
                solutionManager.Object, fileSystemProvider.Object, projectDocumentsEvents.Object);
            solutionManager.Raise(s => s.SolutionOpened += (o, e) => { }, EventArgs.Empty);

            // Assert
            uint cookie;
            projectDocumentsEvents.Verify(
                p => p.AdviseTrackProjectDocumentsEvents(It.IsAny<IVsTrackProjectDocumentsEvents2>(), out cookie),
                Times.Once());
        }

        [Fact]
        public void DoNotTrackIfNewSourceControlIntegrationIsDisabled()
        {
            // Arrange
            var solutionManager = new Mock<ISolutionManager>();
            var fileSystemProvider = new Mock<IFileSystemProvider>();
            var projectDocumentsEvents = new Mock<IVsTrackProjectDocuments2>();

            solutionManager.Setup(s => s.IsSolutionOpen).Returns(true);
            solutionManager.Setup(s => s.SolutionDirectory).Returns("baz:\\foo");

            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("nuget.config", @"<?xml version=""1.0"" ?><configuration><solution><add key=""disableSourceControlIntegration"" value=""true"" /></solution></configuration>");

            fileSystemProvider.Setup(f => f.GetFileSystem("baz:\\foo\\.nuget")).Returns(fileSystem);

            // Act
            var scTracker = new VsSourceControlTracker(
                solutionManager.Object, fileSystemProvider.Object, projectDocumentsEvents.Object);

            // Assert
            uint cookie;
            projectDocumentsEvents.Verify(
                p => p.AdviseTrackProjectDocumentsEvents(It.IsAny<IVsTrackProjectDocumentsEvents2>(), out cookie),
                Times.Never());
        }

        [Fact]
        public void DoNotTrackWhenSolutionIsOpenButSourceControlIntegrationIsDisabled()
        {
            // Arrange
            var solutionManager = new Mock<ISolutionManager>();
            var fileSystemProvider = new Mock<IFileSystemProvider>();
            var projectDocumentsEvents = new Mock<IVsTrackProjectDocuments2>();

            solutionManager.Setup(s => s.IsSolutionOpen).Returns(false);
            solutionManager.Setup(s => s.SolutionDirectory).Returns("baz:\\foo");

            var fileSystem = new MockFileSystem();
            fileSystem.AddFile("nuget.config", @"<?xml version=""1.0"" ?><configuration><solution><add key=""disableSourceControlIntegration"" value=""true"" /></solution></configuration>");

            fileSystemProvider.Setup(f => f.GetFileSystem("baz:\\foo\\.nuget")).Returns(fileSystem);

            var scTracker = new VsSourceControlTracker(
                solutionManager.Object, fileSystemProvider.Object, projectDocumentsEvents.Object);

            // Act
            solutionManager.Raise(s => s.SolutionOpened += (o, e) => { }, EventArgs.Empty);

            // Assert
            uint cookie;
            projectDocumentsEvents.Verify(
                p => p.AdviseTrackProjectDocumentsEvents(It.IsAny<IVsTrackProjectDocumentsEvents2>(), out cookie),
                Times.Never());
        }

        [Fact]
        public void StopTrackingWhenSolutionIsClosed()
        {
            // Arrange
            var solutionManager = new Mock<ISolutionManager>();
            var fileSystemProvider = new Mock<IFileSystemProvider>();
            var projectDocumentsEvents = new Mock<IVsTrackProjectDocuments2>();

            solutionManager.Setup(s => s.IsSolutionOpen).Returns(true);
            solutionManager.Setup(s => s.SolutionDirectory).Returns("baz:\\foo");

            var fileSystem = new MockFileSystem();
            fileSystemProvider.Setup(f => f.GetFileSystem("baz:\\foo\\.nuget")).Returns(fileSystem);

            var scTracker = new VsSourceControlTracker(
                solutionManager.Object, fileSystemProvider.Object, projectDocumentsEvents.Object);

            // Act
            solutionManager.Raise(s => s.SolutionClosed += (o, e) => { }, EventArgs.Empty);

            // Assert
            projectDocumentsEvents.Verify(
                p => p.UnadviseTrackProjectDocumentsEvents(It.IsAny<uint>()),
                Times.Once());
        }


    }
}