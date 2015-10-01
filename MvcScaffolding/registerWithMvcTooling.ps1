param($rootPath, $toolsPath)

# Ensure we're on the right version of the tooling. If not, there's nothing for us to do here.
$toolingExists = [System.AppDomain]::CurrentDomain.GetAssemblies() | ?{ $_.GetType("Microsoft.VisualStudio.Web.Mvc.Scaffolding.ScaffolderProviders") }
if (!$toolingExists) { return }

# Todo: Scope the following to this module if possible
$global:MvcScaffoldingRootPath = $rootPath
$global:MvcScaffoldingToolsPath = $toolsPath

function global:MvcScaffoldingHashTableToPsObject($hashOfScriptMethods) {
    $result = New-Object PSObject
	$hashOfScriptMethods.Keys | %{ Add-Member -InputObject $result -Member ScriptMethod -Name $_ -Value $hashOfScriptMethods[$_] }
	$result
}

function global:MvcScaffoldingInvokeViaScriptExecutor($scriptToExecute) {
	$tempScriptFileName = "scaffoldViaDialogTempScript.ps1"
	$tempScriptFilePath = Join-Path $global:MvcScaffoldingToolsPath $tempScriptFileName
	try {
		# Ensure we're on the right NuGet version
		$scriptExecutorExists = [System.AppDomain]::CurrentDomain.GetAssemblies() | ?{ $_.GetType("NuGet.VisualStudio.IScriptExecutor") }
		if (!$scriptExecutorExists) {
			[System.Windows.Forms.MessageBox]::Show("Sorry, this operation requires NuGet 1.2 or later.", "Scaffolding error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
			return
		}

		# Invoke via IScriptExecutor, then clean up
		$scriptExecutor = [NuGet.VisualStudio.ServiceLocator].GetMethods() | ?{ $_.Name -eq 'GetInstance' } | %{ $_.MakeGenericMethod([NuGet.VisualStudio.IScriptExecutor]).Invoke($null, [Array]$null) }
		
		# Write the script to disk
		Set-Content -Path $tempScriptFilePath -Value $scriptToExecute

		if ($host.Version.Major -lt 2) {
			$scriptExecutor.Execute($global:MvcScaffoldingRootPath, $tempScriptFileName, $null, $null, (New-Object NuGet.NullLogger))
		} else {
			# NuGet v2 introduced support for targeting PowerShell scripts to a target framework.
			# This causes $scriptExecuter.Execute to expect a framework version parameter not expected previously.
			# http://docs.nuget.org/docs/creating-packages/creating-and-publishing-a-package#New_in_NuGet_version_2.0_and_above
			$scriptExecutor.Execute($global:MvcScaffoldingRootPath, $tempScriptFileName, (Get-Package MvcScaffolding), $null, (New-Object System.Runtime.Versioning.FrameworkName "Fake,Version=v0.0"), (New-Object NuGet.NullLogger))
		}
	} catch {
		[System.Windows.Forms.MessageBox]::Show("An error occurred during scaffolding:`n`n$($_.Exception.ToString())`n`nYou may need to upgrade to a newer version of MvcScaffolding.", "Scaffolding error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
	}
	# PowerShell v2 for whatever reason cannot handle a finally block here.
	# This shouldn't be an issue since we catch all exception types and the catch block *shouldn't* throw.
	# If we don't remove the temp script, it's not the end of the world, but it would likely mess up a subsequent uninstall.
	Remove-Item $tempScriptFilePath -ErrorAction SilentlyContinue
}

$mvcScaffoldingProvider = global:MvcScaffoldingHashTableToPsObject @{
	ID = { "{9EC893D9-B925-403C-B785-A50545149521}" };
	GetControllerScaffolders = {
		param($project)
		$allScaffolders = Get-Scaffolder -Project $project.Name -IncludeHidden
		$allControllerScaffolders = $allScaffolders | ?{ $_.ScaffolderAttribute -is [T4Scaffolding.ControllerScaffolderAttribute] }
		if (!$allControllerScaffolders) { return @() }

		$result = $allControllerScaffolders | %{
			$thisControllerScaffolder = $_
			global:MvcScaffoldingHashTableToPsObject @{
				ID = { $thisControllerScaffolder.Name }.GetNewClosure();
				DisplayName = { "MvcScaffolding: " + $thisControllerScaffolder.ScaffolderAttribute.DisplayName }.GetNewClosure();
				SupportsModelType = { $thisControllerScaffolder.ScaffolderAttribute.SupportsModelType }.GetNewClosure();
				SupportsDataContextType = { $thisControllerScaffolder.ScaffolderAttribute.SupportsDataContextType }.GetNewClosure();
				ViewsScaffolders = { 
					if (!$thisControllerScaffolder.ScaffolderAttribute.SupportsViewScaffolder) { return @() }
					$viewScaffolderSelector = $thisControllerScaffolder.ScaffolderAttribute.ViewScaffolderSelector
					if (!$viewScaffolderSelector) { $viewScaffolderSelector = [T4Scaffolding.ViewScaffolderAttribute] }
					$viewScaffolders = $allScaffolders | ?{ $viewScaffolderSelector.IsAssignableFrom($_.ScaffolderAttribute.GetType()) }
						
					# Put default view engine at the top of the list so it's the default selection until you choose otherwise
					$defaultViewScaffolder = (Get-DefaultScaffolder View).ScaffolderName
					$viewScaffolders = $viewScaffolders | Sort-Object { if($_.Name -eq $defaultViewScaffolder) { "" } else { $_.Name } }
										
					$result = $viewScaffolders | %{
						$thisViewScaffolder = $_
						global:MvcScaffoldingHashTableToPsObject @{
							ID = { $thisViewScaffolder.Name }.GetNewClosure();
							DisplayName = { $thisViewScaffolder.ScaffolderAttribute.DisplayName }.GetNewClosure();
							LayoutPageFilter = { $thisViewScaffolder.ScaffolderAttribute.LayoutPageFilter }.GetNewClosure();
						}
					}
					return ,[Array]$result
				}.GetNewClosure();
				Execute = { 
					param($container, $controllerName, $modelType, $dataContextType, $viewsScaffolder, $options)

					# Infer possible area name from container location
					$areaName = $null
					if ($container -is [EnvDTE.ProjectItem]) {
						$containerNamespace = $container.Properties.Item("DefaultNamespace").Value
						$areaMatch = [System.Text.RegularExpressions.Regex]::Match($containerNamespace, "(^|\.)Areas\.(.*)\.Controllers($|\.)")
						$areaName = if ($areaMatch.Success) { $areaMatch.Groups[2].Value }
					}

					$scriptToExecute = @"
						try {
							# These are all the args we may pass to the target scaffolder...
							`$possibleArgs = @{
								ControllerName = "$controllerName";
								ModelType = "$modelType";
								DbContextType = "$dataContextType";
								Project = "$($project.Name)";
								Area = $(if($areaName) { "`"$areaName`"" } else { "`$null" });
								ViewScaffolder = $(if($viewsScaffolder) { "`"" + $viewsScaffolder.ID + "`"" } else { "`$null" });
								Force = $(if($options.OverwriteViews -or $options.OverwriteController) { "`$true" } else { "`$false" });
								ForceMode = $(if($options.OverwriteViews -and $options.OverwriteController) { "`$null" } else { if($options.OverwriteViews) { "`"PreserveController`"" } else { "`"ControllerOnly`"" } });
								Layout = $(if($options.UseLayout) { "`"" + $options.Layout + "`"" } else { "`$null" });
								PrimarySectionName = $(if($options.PrimarySectionName) { "`"" + $options.PrimarySectionName + "`"" } else { "`$null" });
								ReferenceScriptLibraries = $(if($options.ReferenceScriptLibraries) { "`$true" } else { "`$false" });
							}
							# ... but we only pass the ones it actually accepts
							`$actualArgs = @{}
							`$acceptedParameterNames = (Get-Command Invoke-Scaffolder -ArgumentList @("$($thisControllerScaffolder.Name)")).Parameters.Keys
							`$acceptedParameterNames | ?{ `$possibleArgs.ContainsKey(`"`$_`") } | %{ `$actualArgs.Add(`$_, `$possibleArgs[`$_]) }
							Invoke-Scaffolder "$($thisControllerScaffolder.Name)" @actualArgs
						} catch {
							[System.Windows.Forms.MessageBox]::Show("An error occurred during scaffolding:`n`n`$(`$_.Exception.ToString())`n`nYou may need to upgrade to a newer version of MvcScaffolding.", "Scaffolding error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
						}
"@

					$console = [NuGet.VisualStudio.ServiceLocator].GetMethod("GetInstance").MakeGenericMethod([NuGet.VisualStudio.IOutputConsoleProvider]).Invoke($null, $null).CreateOutputConsole(<#requirePowerShellHost:#> $true)
					$console.Clear()
					$packageManagerOutputPaneGuid = "{CEC55EC8-CC51-40E7-9243-57B87A6F6BEB}"
					$outputWindow = $dte.Windows.Item([EnvDTE.Constants]::vsWindowKindOutput)
					$packageManagerOutputPane = $outputWindow.Object.OutputWindowPanes.Item($packageManagerOutputPaneGuid)
					$packageManagerOutputPane.Activate()
					$outputWindow.Activate()
					# In PowerShell v2, $console.Host.Execute will fail the first time it is called. Exceptions like the following will be thrown:
					# Add-ProjectItemViaTemplate : The term 'Find-ScaffolderTemplate' resolved to a cmdlet name that is ambiguous.
					# Possible matches include: T4Scaffolding\Find-ScaffolderTemplate T4Scaffolding\Find-ScaffolderTemplate.
					if ($PSVersionTable.PSVersion.Major -lt 3) {
					    global:MvcScaffoldingInvokeViaScriptExecutor($scriptToExecute)
					} else {
						$console.Host.Initialize($console)
						$console.Host.Execute($console, $scriptToExecute, $null);
					}

					# Trick PowerShell into not unrolling the return collection by wrapping it in a further collection
					$result = [System.Activator]::CreateInstance(([System.Collections.Generic.List``1].MakeGenericType([System.Object])))
					$result.Add(([System.Activator]::CreateInstance(([System.Collections.Generic.List``1].MakeGenericType([EnvDTE.ProjectItem])))))
					return $result
				}.GetNewClosure();				
			}
		}
		return ,[Array]$result
	}
}

# Need to register with each ScaffolderProviders type
# There can be multiple, one per loaded ASP.NET MVC tooling DLL, if your VS instance has multiple versions of ASP.NET MVC tooling loaded
$scaffolderProviderTypes = [System.AppDomain]::CurrentDomain.GetAssemblies() | %{ $_.GetType("Microsoft.VisualStudio.Web.Mvc.Scaffolding.ScaffolderProviders") } | ?{ $_ }
$scaffolderProviderTypes | %{
	$allProviders = $_.GetProperty("Providers").GetValue($null, $null)

	# Remove existing MvcScaffolding providers
	$existingMvcScaffoldingProviders = $allProviders | ?{ $_.ID -eq $mvcScaffoldingProvider.ID() } 
	$existingMvcScaffoldingProviders | %{ $allProviders.Remove($_) } | Out-Null

	# Add new provider
	$powerShellScaffolderProviderType = $_.Assembly.GetType("Microsoft.VisualStudio.Web.Mvc.Scaffolding.PowerShell.PowerShellScaffolderProvider")
	$newProvider = New-Object $powerShellScaffolderProviderType($mvcScaffoldingProvider)
	$allProviders.Add($newProvider) | Out-Null
}