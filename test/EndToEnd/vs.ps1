param([parameter(Mandatory = $true)]
      [string]$OutputPath,
      [parameter(Mandatory = $true)]
      [string]$TemplatePath)

# Make sure we stop on exceptions
$ErrorActionPreference = "Stop"
$FileKind = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}"

function Get-SolutionDir {
    if($dte.Solution -and $dte.Solution.IsOpen) {
        return Split-Path $dte.Solution.FullName
    }
    else {
        throw "Solution not avaliable"
    }
}

function Ensure-Solution {
    if(!$dte.Solution -or !$dte.Solution.IsOpen) {
        New-Solution
    }
}

function Ensure-Dir {
    param(
        [string]
        [parameter(Mandatory = $true)]
        $Path
    )
    if(!(Test-Path $Path)) {
        mkdir $Path | Out-Null
    }
}

function New-Solution {
    $id = New-Guid
    $name = "Solution_$id"
    $solutionDir = Join-Path $OutputPath $name
    $solutionPath = Join-Path $solutionDir $name    
    
    Ensure-Dir $solutionDir
     
    $dte.Solution.Create($solutionDir, $name) | Out-Null
    $dte.Solution.SaveAs($solutionPath) | Out-Null    
}

function New-Project {
    param(
         [parameter(Mandatory = $true)]
         [string]$TemplateName
    )
    
    $id = New-Guid
    $projectName = $TemplateName + "_$id"
    
    # Make sure there is a solution
    Ensure-Solution
    
    # Get the zip file where the project template is located
    $projectTemplatePath = Join-Path $TemplatePath "$TemplateName.zip"
    
    # Find the vs template file
    $projectTemplateFilePath = @(Get-ChildItem $projectTemplatePath -Filter *.vstemplate)[0].FullName
    
    # Get the output path of the project
    $destPath = Join-Path (Get-SolutionDir) $projectName
    
    # Store the active window so that we can set focus to it after the command completes
    # When we add a project to VS it usually tries to set focus to some page
    $window = $dte.ActiveWindow
    
    # Add the project to the solution from th template file specified
    $dte.Solution.AddFromTemplate($projectTemplateFilePath, $destPath, $projectName, $false) | Out-Null
    
    # Close all active documents
    $dte.Documents | %{ $_.Close() }
    
    # Set the focus back on the shell
    $window.SetFocus()
    
    # Return the project
    Get-Project $projectName
}

function New-ClassLibrary { 
    New-Project ClassLibrary
}

function New-ConsoleApplication {
    New-Project ConsoleApplication
}

function New-WebApplication {
    New-Project EmptyWebApplicationProject40
}

function New-MvcApplication { 
    New-Project EmptyMvcWebApplicationProjectTemplatev2.0.cs
}

function Build-Project {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [string]$Configuration
    )    
    if(!$Configuration) {
        # If no configuration was specified then use
        $Configuration = $dte.Solution.SolutionBuild.ActiveConfiguration.Name
    }
    
    # Build the project and wait for it to complete
    $dte.Solution.SolutionBuild.BuildProject($Configuration, $Project.UniqueName, $true)
}

function Get-AssemblyReference {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$Reference
    )    
    try {
        return $Project.Object.References.Item($Reference)
    }
    catch {        
    }
    return $null
}

function Get-PropertyValue {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true)]
        [string]$PropertyName
    )    
    try {
        $property = $Project.Properties.Item($PropertyName)        
        if($property) {
            return $property.Value
        }
    }
    catch {        
    }
    return $null
}

function Get-ProjectDir {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    Get-PropertyValue $Project FullPath
}

function Get-OutputPath {
    param(
        [parameter(Mandatory = $true)]
        $Project
    )
    
    $outputPath = $Project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
    Join-Path (Get-ProjectDir) $outputPath
}

function Get-ProjectItem {
    param(
        [parameter(Mandatory = $true)]
        $Project,
        [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)]
        [string]$Path
    )
    Process {
        $pathParts = $Path.Split('\')
        $projectItems = $Project.ProjectItems
        
        foreach($part in $pathParts) {
            if(!$part -or $part -eq '') {
                continue
            }
            
            try {
                $subItem = $projectItems.Item($part)
            }
            catch {
                return $null
            }

            $projectItems = $subItem.ProjectItems
        }

        if($subItem.Kind -eq $FileKind) {
            return $subItem
        }
        
        # Force array
       return  ,$projectItems
    }
}