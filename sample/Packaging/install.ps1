param($installPath, $toolsPath, $package, $project)

[Reflection.Assembly]::LoadFile($toolsPath + "\OneNorth.TDSProjectMerge.dll")

try {
	
	$tdsProject = Get-TDSProject("Master")
	if ($tdsProject) {
		Merge-TDSProject $tdsProject $installPath "TDS.Master" "TDS.Master.scproj"
	}
	
} catch {
	Write-Error $_.Exception.Message
	throw
}

function Get-TDSProject([string]$type) {
	$matchString = "." + $type.ToLower()

	$solution = Get-Interface $dte.Solution ([EnvDTE80.Solution2])
	foreach ($project in $solution.Projects) {
        if (($project.Kind -eq "{caa73bb0-ef22-4d79-a57e-df67b3ba9c80}") -and ($project.Name.ToLower().EndsWith($matchString))) {
			return $project
		}
		if (($project.Project.ProjectType -eq "TDS Project") -and ($project.Name.ToLower().EndsWith($matchString))) {
			return $project
		}
	}
    Write-Host "Could not find TDS Project '$($type)'. Existing projects:"
    foreach ($project in $solution.Projects) {
        $name = $project.Name
        $kind = $project.Kind
        $type = $project.Project.ProjectType
        Write-Host "  Name: '$($name)', Kind: '$($kind)', Type: '$($type)'"
	}
}

function Merge-TDSProject($tdsProject, [string]$installPath, [string]$sourceFolder, [string]$sourceProject) {
	$contentProject = $installPath + "\" + $sourceFolder + "\" + $sourceProject

	Write-Host "Merging $($contentProject) into TDS project at: $($tdsProject.FullName)"

	$merger = New-Object OneNorth.TDSProjectMerge.MergeTask
	$merger.MergeProjects($tdsProject.FullName, $contentProject)

	$sourceFiles = $installPath + "\" + $sourceFolder
	$targetFiles = [System.IO.Path]::GetDirectoryName($tdsProject.FullName)

	Write-Host "Copying from '$($sourceFiles)' to '$($targetFiles)'"

	$merger.CopyFiles($targetFiles, $sourceFiles);
}