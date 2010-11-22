using System;

namespace NuGet {
    public interface IProjectManager {
        IPackageRepository LocalRepository { get; }
        ILogger Logger { get; set; }
        IProjectSystem Project { get; }
        IPackageRepository SourceRepository { get; }

        event EventHandler<PackageOperationEventArgs> PackageReferenceAdded;
        event EventHandler<PackageOperationEventArgs> PackageReferenceAdding;
        event EventHandler<PackageOperationEventArgs> PackageReferenceRemoved;
        event EventHandler<PackageOperationEventArgs> PackageReferenceRemoving;

        void AddPackageReference(string packageId, Version version, bool ignoreDependencies);      
        void RemovePackageReference(string packageId, bool forceRemove, bool removeDependencies);
        void UpdatePackageReference(string packageId, Version version, bool updateDependencies);

        bool IsInstalled(IPackage package);
    }
}
