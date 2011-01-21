function Test-RemovingPackageFromProjectDoesNotRemoveIfInUse {
    # Arrange
    $p1 = New-ClassLibrary
    $p2 = New-ClassLibrary
    
    Install-Package Ninject -Project $p1.Name
    Assert-Reference $p1 Ninject
    
    Install-Package Ninject -Project $p2.Name
    Assert-Reference $p2 Ninject
    
    Uninstall-Package Ninject -Project $p1.Name
    
    Assert-Null (Get-ProjectPackage $p1 Ninject)
    Assert-Null (Get-AssemblyReference $p1 Ninject)
    Assert-SolutionPackage Ninject
}

function Test-RemovePackageRemovesPackageFromSolutionIfNotInUse {
    # Arrange
    $p1 = New-WebApplication
    
    Install-Package elmah -Project $p1.Name
    Assert-Reference $p1 elmah
    Assert-SolutionPackage elmah
    
    Uninstall-Package elmah -Project $p1.Name
    Assert-Null (Get-AssemblyReference $p1 elmah)
    Assert-Null (Get-ProjectPackage $p1 elmah)
    Assert-Null (Get-SolutionPackage elmah)
}

function Test-UninstallingPackageWithConfigTransformWhenConfigReadOnly {
    # Arrange
    $p1 = New-WebApplication
    
    Install-Package elmah -Project $p1.Name    
    Assert-Reference $p1 elmah
    Assert-SolutionPackage elmah
    attrib +R (Get-ProjectItemPath $p1 web.config)
    
    Uninstall-Package elmah -Project $p1.Name
    Assert-Null (Get-AssemblyReference $p1 elmah)
    Assert-Null (Get-ProjectPackage $p1 elmah)
    Assert-Null (Get-SolutionPackage elmah)
}

function Test-UninstallPackageInvokeUninstallScript {
    param(
        $context
    )
    
    # Arrange
    $p = New-ConsoleApplication

    # Act
    Install-Package Moq -Project $p.Name -Source $context.RepositoryRoot
    Uninstall-Package Moq -Project $p.Name

    # Assert

    # This asserts uninstall.ps1 gets called
    Assert-NotNull (Get-ChildItem function:\TestFunction)
}

function Test-UninstallPackageWithNestedContentFiles {
    param(
        $context
    )

    # Arrange
    $p = New-WebApplication
    Install-Package NestedFolders -Project $p.Name -Source $context.RepositoryRoot

    # Act    
    Uninstall-Package NestedFolders -Project $p.Name

    # Assert
    Assert-Null (Get-ProjectItem $p a)
    Assert-Null (Get-ProjectItem $p a\b)
    Assert-Null (Get-ProjectItem $p a\b\c)
    Assert-Null (Get-ProjectItem $p a\b\c\test.txt)
}

function Test-SimpleFSharpUninstall {
    # Arrange
    $p = New-FSharpLibrary
    
    # Act
    Install-Package Ninject -Project $p.Name 
    Assert-Reference $p Ninject
    Assert-Package $p Ninject
    Assert-SolutionPackage Ninject
    Uninstall-Package Ninject -Project $p.Name
    
    # Assert
    Assert-Null (Get-ProjectPackage $p Ninject)
    Assert-Null (Get-AssemblyReference $p Ninject)
    Assert-Null (Get-SolutionPackage Ninject)
}
