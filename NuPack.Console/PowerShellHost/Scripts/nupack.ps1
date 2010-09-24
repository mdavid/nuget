# make sure we stop on exceptions
$ErrorActionPreference = "Stop"

$global:DefaultProjectName = $null

function global:New-Package {
    [CmdletBinding()]
    param(
        [string]$Project = $DefaultProjectName,
        [string]$Spec,
        [string]$TargetFile
    )
        
    Process {
    
        if (!$Project) {
            Write-Error "Missing -Project parameter and the default project is not set."
            return
        }
        
        $ProjectIns = Get-Project $Project
        
        if ($ProjectIns -eq $null) {
            Write-Error "Project '$Project' is not found."
            return
        }
        
        $SpecItem = _LookForSpecFile $ProjectIns $Spec
        if ($SpecItem -eq $null) {
            Write-Error "Unable to locate the nuspec file."
            return
        }
        
        # $SpecItem.FileNames returns an instance of PSParameterizedProperty.
        $SpecFilePath = $SpecItem.FileNames.Invoke(0)
        
        # Prepare the builder
        $builder = [NuPack.PackageBuilder]::ReadFrom($SpecFilePath)
        $builder.Created = [System.DateTime]::Now
        $builder.Modified = $builder.Created
        $builder.Files.RemoveAll( { param($file) (".nupack", ".nuspec") -contains [System.IO.Path]::GetExtension($file.SourcePath) } ) | out-null
        
        if (!$TargetFile){
            $TargetFile = Join-Path (Split-Path $ProjectIns.FullName) ($builder.Id + '.' + $builder.Version + '.nupack')
        }
        
        Write-Output "Creating package at '$TargetFile'..."
        
        try { 
            $stream = [System.IO.File]::Create($TargetFile)
            $builder.Save($stream)
        }
        finally {
            $stream.Close()
        }
        
        Write-Output "Package file successfully created..."
    }
}

function global:Add-Package {
    [CmdletBinding()]    
    param(
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Id,
        
        [string]$Project,
        
        [Version]$Version,
      
        [switch]$IgnoreDependencies
    )
    Begin {
        $packageManager = _GetPackageManager
    }
    Process {
        try {
            $isSolutionLevel = _IsSolutionOnlyPackage $packageManager $Id $Version
        
            if ($isSolutionLevel) {
                if ($Project) {
                    Write-Error "The package '$Id' only applies to the solution and not to a project. Remove the -Project parameter."
                    return
                }
                else {
                    # Only set the logger for solution level packages
                    $packageManager.Logger = _CreateLogger
                    $packageManager.InstallPackage($Id, $Version, $IgnoreDependencies)
                    $packageManager.Logger = $null
                }
            }
            else {
                if (!$Project) {
                    $Project = $DefaultProjectName
                }
                
                if ($Project) {
                    $projectManager = _GetProjectManager $packageManager $Project
                    $projectManager.AddPackageReference($Id, $Version, $IgnoreDependencies)
                }
                else {
                    Write-Error "Missing project parameter and the default project is not set."
                }
            }
        }
        catch {
            _WriteError $_.Exception
        }
    }
}

function global:Remove-Package {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true, Position=0)]
        [string]$Id,
        
        [string]$Project,

        [switch]$Force,

        [switch]$RemoveDependencies
    )
    Begin {
        $packageManager = _GetPackageManager
    }
    Process {
        try {
            $isSolutionLevel = _IsSolutionOnlyPackage $packageManager $Id $Version
             
            if ($isSolutionLevel) {
                if ($Project) {
                     Write-Error "The package '$Id' only applies to the solution and not to a project. Remove the -Project parameter."
                     return
                }
                else {
                     # Only set the logger for solution level packages
                     $packageManager.Logger = _CreateLogger
                     $packageManager.UninstallPackage($Id, $null, $Force, $RemoveDependencies)
                     $packageManager.Logger = $null
                }
            } 
            else {
        
                if (!$Project) {
                    $Project = $DefaultProjectName
                }
        
                if ($Project) {
					$projectManager = _GetProjectManager $packageManager $Project
					$projectManager.RemovePackageReference($Id, $Force, $RemoveDependencies)
                }
                else {
                    Write-Error "Missing project parameter and the default project is not set."
                }
            }
        }
        catch {
            _WriteError $_.Exception
        }
    }
}

function global:Update-Package {
    [CmdletBinding()]
    param(
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Id,
        
        [string]$Project,

        [Version]$Version,

        [switch]$UpdateDependencies = $true
    )
    Begin {
        $packageManager = _GetPackageManager
    }
    Process {
        try {
            $isSolutionLevel = _IsSolutionOnlyPackage $packageManager $Id $Version
             
            if ($isSolutionLevel) {
                
                if ($Project) {
                     Write-Error "The package '$Id' only applies to the solution and not to a project. Remove the -Project parameter."
                     return
                }
                else {
                     $packageManager.UpdatePackage($Id, $Version, $UpdateDependencies)
                }
            }
            else {
                
                if (!$Project) {
                    $Project = $DefaultProjectName
                }
            
                if ($Project) {
                    $projectManager = _GetProjectManager $packageManager $Project
                    $projectManager.UpdatePackageReference($Id, $Version, $IgnoreDependencies)
                }
                else {
                    $packageManager.UpdatePackage($Id, $Version, $UpdateDependencies)
                }
            }
        }
        catch {
            _WriteError $_.Exception
        }
    }
}

function global:List-Package {
    [CmdletBinding()]
    param(
        [switch]$Installed,
        
        [switch]$Updates
    )

    # check if there is a solution open in the IDE
    if ($dte -and $dte.Solution -and $dte.Solution.IsOpen) {   
        $packageManager = _GetPackageManager

        if($Updates) {
            $solutionPackages = $packageManager.SolutionRepository.GetPackages()
            $externalPackages = $packageManager.ExternalRepository.GetPackages()
            return  $solutionPackages | Select-Object Id, Version, @{Name="UpdateAvailable";E={ $packageId = $_.Id; $version = $_.Version;
                                                                                                $packages = @($externalPackages | Where-Object { $packageId -eq $_.Id -and $_.Version -gt $version }) ;
                                                                                                $packages.Count -gt 0
                                                                                              }}
        }
        elseif($Installed) {            
            $repository = $packageManager.SolutionRepository
        }
        else {
            $repository = $packageManager.ExternalRepository
        }
    }
    else {
        if ($Local -or $Updates) {
            Write-Error "No solution is found in the current environment."
            return
        }
        
        $DefaultPackageSource = _GetDefaultPackageSource

        if ($DefaultPackageSource) {
            $repository = [NuPack.PackageRepositoryFactory]::CreateRepository($DefaultPackageSource)
        }
        else {
            []
        }
    }

    return $repository.GetPackages() | Select-Object Id, Version, Description
}

# This is called directly by the combobox in VS.NET
# When I tried calling Change-DefaultProject directly from NuPack Console, I got an cryptic COM exception
function global:_SetDefaultProjectInternal($Project) {
    $global:DefaultProjectName = $Project
}

# Helper functions

# Get the solution events
$global:packageManagerInitialized = $false
$global:solutionEvents = Get-Interface $dte.Events.SolutionEvents ([EnvDTE._dispSolutionEvents_Event])

# Clear the cache when the solution is closed

$global:solutionEvents.add_BeforeClosing([EnvDTE._dispSolutionEvents_BeforeClosingEventHandler]{
    $global:projectCache = $null
    $global:DefaultProjectName = $null
    $global:packageManagerInitialized = $false
})

$global:solutionEvents.add_Opened([EnvDTE._dispSolutionEvents_OpenedEventHandler]{
    $packageManager = _GetPackageManager
    $repository = $packageManager.SolutionRepository
    $localPackages = $repository.GetPackages()

    $localPackages | ForEach-Object {
        $path = $packageManager.PathResolver.GetInstallPath($_)

        _AddToolsFolderToEnv $path
        _ExecuteScript $path "tools\init.ps1" $_
    }
})

# Update the cache when a project is added/removed/renamed
$global:solutionEvents.add_ProjectAdded([EnvDTE._dispSolutionEvents_ProjectAddedEventHandler]{
    param($project)

    if(_IsSupportedProject $project) {
        $global:projectCache[$project.Name] = $project
    }
})

$global:solutionEvents.add_ProjectRenamed([EnvDTE._dispSolutionEvents_ProjectRenamedEventHandler]{
    param($project, $oldName)

    if(_IsSupportedProject $project) {
        $global:projectCache[$project.Name] = $project
        $global:projectCache.Remove($oldName)
    }
})

$global:solutionEvents.add_ProjectRemoved([EnvDTE._dispSolutionEvents_ProjectRemovedEventHandler]{
    param($project) 

    $global:projectCache.Remove($project.Name)
})

function global:Get-Project {
    [CmdletBinding()]
    param(
        [string]$ProjectName
    )

    _EnsureProjectCache    

    # No project specified and there is only one project then just return it
    if(!$ProjectName) {
        if($global:projectCache.Count -eq 1) {
            return $global:projectCache.Values | Select-Object -First 1
        }
        throw "Unable to determine project. Try specifying the full name."
    }

    # Check the cache for the project
    $project = $global:projectCache[$ProjectName]

    if($project) {
        return $project
    }

    throw "Project '$ProjectName' not found. Try specifying the full name."
}

function global:GetProjects() {
    _EnsureProjectCache

    return $global:projectCache.Values
}

function global:GetProjectNames() {
    GetProjects | ForEach-Object { $_.Name }
}

# Backup the original tab expansion function
if ((Test-Path Function:\DefaultTabExpansion) -eq $false) {
    Rename-Item Function:\TabExpansion global:DefaultTabExpansion
}

function global:TabExpansion($line, $lastWord) {
    $tokens = $line.Split(@(' '), 'RemoveEmptyEntries')
    $filter = $lastWord.Trim()

    if (!$filter) {
        $tokens = $tokens + $filter
    }

    if ($tokens.length -gt 2) {
        $secondLastToken = $tokens[-2]
    }
    else {
        $secondLastToken = ''
    }
    
    switch ($tokens[0]) {
        { $_ -eq 'New-Package' -or $_ -eq 'nnp' } {
            $choices = _TabExpansionForNewPackage $secondLastToken $tokens.length $filter
        }
    
        { $_ -eq 'Add-Package' -or $_ -eq 'nap' } {
            $choices = _TabExpansionForAddPackage $secondLastToken $tokens.length $filter
        }

        { $_ -eq 'Remove-Package' -or $_ -eq 'nrp' } {
            $choices = _TabExpansionForRemovePackage $secondLastToken $tokens.length $filter
        }

        { $_ -eq 'Update-Package' -or $_ -eq 'nup' } {
            $choices = _TabExpansionForRemovePackage $secondLastToken $tokens.length $filter
        }
        
        'Update-DefaultProject' {
          $choices = _TabExpansionForChangeDefaultProject $secondLastToken $filter
        }
    }
    
    if($choices) {                
        # Return all the choices, do some filtering based on the last word and sort them
        $choices | Where-Object { $_.StartsWith($filter, "OrdinalIgnoreCase") } | Sort-Object
    }
    else {
        # Fallback the to default tab expansion
        DefaultTabExpansion $line $lastWord 
    }
}

function _TabExpansionForNewPackage([string]$secondLastWord, [int]$tokenCount, [string]$filter) {
    if ($filter.StartsWith('-')) {
       # if this is a parameter, do not return anything so that the default PS tab expansion can supply the list of parameters
    }
    elseif (($secondLastWord -eq '-project') -or 
            ($tokenCount -eq 2 -and !$secondLastWord.StartsWith('-'))) {
        GetProjectNames
    }
}

function _TabExpansionForAddPackage([string]$secondLastWord, [int]$tokenCount, [string]$filter) {
    if ($filter.StartsWith('-')) {
       # if this is a parameter, do not return anything so that the default PS tab expansion can supply the list of parameters
    }
    elseif (($secondLastWord -eq '-id') -or ($secondLastWord -eq '')) {
        List-Package | Group-Object ID | ForEach-Object { $_.Name }
    }
    elseif (($secondLastWord -eq '-project') -or 
            ($tokenCount -eq 3 -and !$secondLastWord.StartsWith('-'))) {
        GetProjectNames
    }
}

function _TabExpansionForRemovePackage([string]$secondLastWord, [int]$tokenCount, [string]$filter) {
    if ($filter.StartsWith('-')) {
       # if this is a parameter, do not return anything so that the default PS tab expansion can supply the list of parameters
    }
    elseif (($secondLastWord -eq '-id') -or ($secondLastWord -eq '')) {
        (List-Package -local) | Group-Object ID | ForEach-Object { $_.Name }
    }
    elseif (($secondLastWord -eq '-project') -or 
            ($tokenCount -eq 3 -and !$secondLastWord.StartsWith('-'))) {
        GetProjectNames
    }
}

function _TabExpansionForChangeDefaultProject([string]$secondLastWord, [string]$filter) {
    if ($filter.StartsWith('-')) {
       # if this is a parameter, do not return anything so that the default PS tab expansion can supply the list of parameters
    }
    elseif ($secondLastWord -eq '' -or $secondLastWord -eq '-project') {
        GetProjectNames
    }
}

function global:_EnsureProjectCache {
    if($global:projectCache) {
        return
    }

    # Create a new cache object
    $global:projectCache = @{}               
                
    $projectStack = New-Object System.Collections.Stack
                
    foreach($project in $dte.Solution.Projects) {
        $projectStack.Push($project)
    }    

    while($projectStack.Count -gt 0) {
        $project = $projectStack.Pop()
                        
        if(_IsSupportedProject $project) {
            # Add it to our list of projects
            $global:projectCache[$project.Name] = $project
        }

        foreach($projectItem in $project.ProjectItems) {
            if($projectItem.SubProject) {
                $projectStack.Push($projectItem.SubProject)
            }
        }
    }
}

# Private functions (meant to be private not actually private)
function global:_WriteError($exception) {
    if($exception.InnerException) {
        $message = $exception.InnerException.Message
    }
    else {
        $message = $exception.Message
    }
    Write-Error $message
}

function global:_GetProjectManager($packageManager, $projectName) {
    $project = Get-Project $projectName
    $projectManager = $packageManager.GetProjectManager($project)

    if(!$projectManager.Logger) {
        # Initialize the project manager properties if they are set
        $projectManager.Logger = _CreateLogger

        # REVIEW: We really want to do this once per project manager instance
        $projectManager.add_PackageReferenceAdded({ 
            param($sender, $e)
        
            Write-Verbose "Executing install script after adding package $($e.Package.Id)..."
            _ExecuteScript $e.InstallPath "tools\install.ps1" $e.Package $project
        }.GetNewClosure());

        $projectManager.add_PackageReferenceRemoving({
            param($sender, $e)

            Write-Verbose "Executing uninstall script before removing package $($e.Package.Id)..."
            _ExecuteScript $e.InstallPath "tools\uninstall.ps1" $e.Package $project
        }.GetNewClosure());
    }

    return $projectManager;
}

function global:_GetPackageManager {
    if(!$dte) {
        throw "DTE isn't loaded"
    }

    # Create a visual studio package manager
    $packageManager = [NuPack.VisualStudio.VsPackageManager]::GetPackageManager([object]$dte)

    if(!$global:packageManagerInitialized) {
        # Add an event for when packages are installed
        $packageManager.add_PackageInstalled($function:_OnPackageInstalled)
        $packageManager.add_PackageUninstalling($function:_OnPackageUninstalling)

        $global:packageManagerInitialized = $true
    }

    return $packageManager
}

function global:_OnPackageInstalled($sender, $e) {
    $path = $e.TargetPath

    _AddToolsFolderToEnv $path
    
    Write-Verbose "Executing init script after installing package $($e.Package.Id)..."
    _ExecuteScript $path "tools\init.ps1"
}

function global:_OnPackageUninstalling($sender, $e) {    
    # TODO: remove tools path from the environment varible
}

function global:_CreateLogger {
    function Log($level, $statusMessage) {
        if ($level -eq 'Info') {
            Write-Host $statusMessage
        }
        elseif ($level -eq 'Debug') {
            Write-Verbose $statusMessage
        }
        elseif ($level -eq 'Warning') {
            Write-Warning $statusMessage
        }
    }

    return New-Object NuPack.CallbackLogger($function:Log)
}

function global:_ExecuteScript([string]$rootPath, [string]$scriptFile, $package, $project) {
    $fullPath = (Join-Path $rootPath $scriptFile)
        
    if (Test-Path $fullPath) {
        $folder = Split-Path $fullPath
        & $fullPath $rootPath $folder $package $project
    }
}

function global:_AddToolsFolderToEnv([string]$rootPath) {
    # add tools path to the environment
    $toolsPath = (Join-Path $rootPath 'tools')
    if (Test-Path $toolsPath) {
        if (!$env:path.EndsWith(';')) {
            $toolsPath = ';' + $toolsPath
        }
        # add the tools folder to the environment path
        $env:path = $env:path + $toolsPath
    }
}

function global:_IsSupportedProject($project) {
    # Hard coded list of project types we support
    $supportedProjectTypes = @("{E24C65DC-7377-472B-9ABA-BC803B73C61A}", # Website project                                
                                "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}", # CSharp project
                                "{F184B08F-C81C-45F6-A57F-5ABD9991F28F}") # VB Project
    
    return $project.Kind -and $supportedProjectTypes -contains $project.Kind
}	

function global:_IsSolutionOnlyPackage($packageManager, $id, $version) {    
    $repository = $packageManager.ExternalRepository
    $package = [NuPack.PackageRepositoryExtensions]::FindPackage($repository, $id, $null, $null, $version)

    return $package -and ![NuPack.PackageExtensions]::HasProjectContent($package)
}

function global:_LookForSpecFile($projectIns, $spec) {
    if ($spec) {
        $projectIns.ProjectItems.Item($spec)
    }
    else {
        $allNuspecs = @($projectIns.ProjectItems | Where-Object { $_.Name.EndsWith('.nuspec') })
        if ($allNuspecs.length -eq 1) {
            $allNuspecs[0]
        }
    }
}

function global:_GetDefaultPackageSource() {
    $ActivePackageSource = $PackageSourceStore.ActivePackageSource
    if ($ActivePackageSource) {
        $ActivePackageSource.Source
    }
}

# assign aliases to package cmdlets

New-Alias 'nnp' 'New-Package'
New-Alias 'nlp' 'List-Package'
New-Alias 'nap' 'Add-Package'
New-Alias 'nrp' 'Remove-Package'
New-Alias 'nup' 'Update-Package'