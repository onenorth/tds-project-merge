

Sitecore: Using NuGet to add items to TDS Projects
==================================================

# Overview
At One North, we wanted to improve development efficiencies by providing a starting point for our Sitecore projects.  Based on the domain or industry we were doing a Sitecore implementation for, we could provide an initial data model to start with.  This would not only decrease the time to setup the initial data model, but it would allow us to carry forward what we have learned on a domain before.

Sitecore provides a way to export items from one system and import them into another through their Package Designer.  The Package Designer is one way to provide a pre-canned data model to new projects.  The drawback of this approach is that it is difficult to keep track of the items that go into or come out of the packages.  Items could be missed as items are packaged. 

We have been using TDS (Team Development for Sitecore) to manage our Sitecore items in Visual Studio.  TDS provides a way to synchronize items in Sitecore with Visual Studio and version control.  TDS essentially gives you full visibility and control into what items need to be migrated from environment to environment.  What we wanted to try was to use TDS as a way to get the initial data model into a project.  We were already using TDS to manage our items and the initial data model needed to end up in TDS, so this felt like the best approach.

NuGet is the package manager for Visual Studio and .Net projects.  NuGet has the ability to not only add assemblies to projects, but it also has the ability to add files to projects.  For example, a NuGet project could contain sample c# code files that can be added to a project.  TDS projects in Visual Studio are essentially the same as code based projects as they have files and a project file.  In theory, NuGet could be a way to add items to an existing TDS project through the mechanisms that NuGet already supports.

Our idea was to use NuGet to add items to existing TDS projects as a way to get our initial data model into a new build.

# Implementation

Upon trying to implement this, we quickly realized that NuGet only supports a fixed set of project types that NuGet packages can target.  These projects include web projects or class library projects, the TDS project type was not supported.  Because of this, we could not use the out of the box support NuGet provided for adding items to projects and updating the respective .scproj project file.  We would need to create our own way to handle this.

The primary hurtle was how do we support adding a NuGet package to a project that is unsupported.  The NuGet package manager in Visual Studio would not allow us to pick the TDS project as the target project.

NuGet supports 2 types of packages.  The first type is a project-level package, which can update a Visual Studio project and run PowerShell scripts upon install.  This is unsupported for installing directly into the TDS project.  The second type of package is a solution-level package.  This package is simply meant to run PowerShell scripts when the package is first installed and every time the solution is opened.  This is typically used to add new commands to PowerShell for use in the Package Manager Console. 

We can tap into the ability for NuGet packages to run PowerShell scripts to execute whatever code we need to programmatically update the TDS Projects.  This script should only be executed once upon install.  Solution-level packages execute PowerShell scripts each time the solution is opened, which does not work for us.  Project-level packages execute PowerShell scripts whenever the package is installed, which is what we want.  TDS Projects for the most part always live alongside website projects.  With this idea, we can install the NuGet package into the website project and have it find the TDS project and update it programmatically.  Because we have full control of our internal projects, so we decided to standardize our TDS project names using “**TDS.Master**” and “**TDS.Core**”.  With standard names, the Website NuGet package PowerShell script could target the known TDS project names. 

## .nuspec

NuGet packages become project-level packages if they include content that targets the **/content** folder or the **/lib** folder.   We essentially need to put something in these folders so it is installed as a project-level package.  If the items in TDS are dependent on any configuration changes, the **/content/App_Config/Include/** folder would be a perfect spot to include the necessary configuration and files, otherwise including an empty readme works.   We can put the actual TDS files outside of these folders because they are not meant to be copied into the Web project.  The folder structure can match the TDS Project folder structure.

Using what we know from above, here is an example .nuspec file that defines the content that would go into the NuGet package.

    <?xml version="1.0" encoding="utf-8"?>
    <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
      <metadata>
        <id>OneNorth.TDSProjectMerge.Sample</id>
        <version>0.0.1</version>
        <title />
        <authors>One North</authors>
        <description>Sample TDS Project Merge .nuspec file</description>
        <projectUrl>https://github.com/onenorth/tds-project-merge</projectUrl>
      </metadata>
      <files>
        <file src="install.ps1" target="tools" />
        <file src="OneNorth.TDSProjectMerge.dll" target="tools" />
        <file src="..\Web\App_Config\**\*" target="content\App_Config\" />
        <file src="..\TDS.Master\sitecore\**\*" target="TDS.Master\sitecore" />
        <file src="..\TDS.Master\*.scproj" target="TDS.Master" />
        <file src="..\TDS.Core\sitecore\**\*" target="TDS.Core\sitecore" />
        <file src="..\TDS.Core\*.scproj" target="TDS.Core" />
      </files>
    </package>

> **Notes:**
> 
> - The folders **TDS.Master** and **TDS.Core** contain the TDS content that will be merged into our respective target projects in our target solution. 
> - The **Web\App_Config** includes configuration file updates related to the items added.  This is important to include because the package will be treated as a project-level package.  If this is not included, another file such as a text file should be added to target=”content”.
> - The “tools” target contains the scripts required to merge TDS projects together.  We will talk about this next.

##Scripts  

The piece we have not talked about yet are the custom PowerShell scripts that are needed for all of this to work.  The above .nuspec file includes files in the “tools” folder.  NuGet will automatically execute the **install.ps1** in the tools folder when the package is installed into a project.  We can use this hook to programmatically update, via PowerShell, the TDS projects and merge the items into them.  To make the creation of the PowerShell scripts easier, we made a helper assembly **OneNorth.TDSProjectMerge.dll**.

The helper assembly **OneNorth.TDSProjectMerge.dll** was developed with consultation from the TDS architect/developer.  This assembly contains all of the logic required to merge two TDS projects together.  It mainly performs xml manipulation to combine two TDS .scproj project files together.  I will not go into the details of the logic for this assembly.  The source code for this assembly is located at: https://github.com/onenorth/tds-project-merge/tree/master/src/OneNorth.TDSProjectMerge

The **install.ps1** hooks into the NuGet package install process.   This script orchestrates updating the TDS project with the items from the NuGet package.   Initially the script loads the helper assembly so it can be referenced.  The script then calls a few helper methods to locate the respective TDS project and then to merge the content from the NuGet package into the target TDS project.

    param($installPath, $toolsPath, $package, $project)
    
    [Reflection.Assembly]::LoadFile($toolsPath + "\OneNorth.TDSProjectMerge.dll")
    
    try {
    	
    	$tdsProject = Get-TDSProject("Master")
    	if ($tdsProject) {
    		Merge-TDSProject $tdsProject $installPath "TDS.Master" "TDS.Master.scproj"
    	}
    	
    	$tdsProject = Get-TDSProject("Core")
    	if ($tdsProject) {
    		Merge-TDSProject $tdsProject $installPath "TDS.Core" "TDS.Core.scproj"
    	}
    
    } catch {
    	Write-Error $_.Exception.Message
    	throw
    } 
    
> **Notes:**
> 
> - First the script loads the helper assembly.  We need to know the path to the helper assembly.  The passed **$toolsPath** variable provides the path to the content of the tools folder.  NuGet takes care of passing the parameters into the install.ps1 script for us.
> - It then calls the **Get-TDSProject** and **Merge-TDSProject** helper methods for each desired TDS project. In this particular case, the script merges both a **Core** and **Master** TDS project. If **TDS.Master** or **TDS.Core** do not need to be updated, they can be removed from the script.

####Get-TDSProject

The function **Get-TDSProject** is used to locate the TDS project based on the project kind GUID or by project type name.  It then matches the end of the project name to determine if it is a match with the desired project.  With this approach, as long as the TDS project names either end with **Master** or **Core** a match will be successful.  If a match is not found, the script lists out all of the projects.  This is mainly used to debug the powershell script.

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


#### Merge-TDSProject

The **Merge-TDSProject** function handles the actual merging of the TDS project in the NuGet package with the target TDS project in the Visual Studio solution.  The function takes the TDS project returned from the **Get-TDSProject** method, as well as the location of the TDS files to merge.   The method then calls methods in the helper assembly to perform the actual merge.  This method assumes the helper assembly is loaded.

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

> All of the code from above is located in the following GitHub location: https://github.com/onenorth/tds-project-merge/tree/master/src

# Creating your own TDS NuGet package

Now that we know how the tooling was created, how do we build our own NuGet package to deploy items to TDS?  Here are the recommended steps for creating your own project/package:

 1. Setup a Visual Studio solution with a TDS project(s) to contain all of the items that you want to distribute via the NuGet package.  Name the project TDS.Master and/or TDS.Core.  Do a Get Sitecore Items or Sync with Sitecore to get the items into the TDS project(s).  Optionally create a Web project to contain whatever /App_Config/Include configuration files you may need.  Save the solution and projects.
 1. Clone the project at https://github.com/onenorth/tds-project-merge. 
 1. Copy the **OneNorth.TDSProjectMerge.dll**, **OneNorth.TDSProjectMerge.Sample.nuspec**, and **install.ps1** from the **release** folder to a **Packaging** sub-folder of the Visual Studio solution you just created.
 1. Make the following updates to the **install.ps1**
	 1. Remove the logic related to **Master** or **Core** if they are not used.
 1. Make the following updates to the **.nuspec** file
	 1. Rename the .nuspec file to a custom name.
	 1. Update the metadata to reflect your information.
	 1. Remove the **TDS.Master** or **TDS.Core** file entries if you are not using them.
	 1. If you do not have any **/App_Config/Include** configuration, remove this node.  Note: it is important to include an alternate file that has a target in the content folder.  This file can be a simple text file.  If the file is not there, the NuGet package will not install correctly.
 1. Build the NuGet package using the .nuspec file using your favorite tool.
 1.	Distribute the NuGet package using http://nuget.org or privately.
 1.	Install the NuGet package into a target Web project that has an already existing TDS.Master and/or TDS.Core sibling project. Note: you may need to close and re-open the TDS projects for the changes to take effect after installed
 1.	 Perform a **Sync with Sitecore** to migrate the changes into Sitecore.	

> A sample project that demonstrates the above steps has been created here: https://github.com/onenorth/tds-project-merge/tree/master/sample

#Conclusion

Through the use of NuGet packages and some custom script, we were able to deploy an initial data model to our TDS Projects.  This approach can be expanded beyond the initial setup of projects to adding features/modules where the associated items need to be tracked in TDS.  All of the sample code is located here: https://github.com/onenorth/tds-project-merge.

#License

The associated code is released under the terms of the [MIT license](http://onenorth.mit-license.org).