param($rootPath, $toolsPath, $package, $project)

# Try to delete InstallationDummyFile.txt
if ($project) {
	$project.ProjectItems | ?{ $_.Name -eq "InstallationDummyFile.txt" } | %{ $_.Delete() }
}

# Bail out if scaffolding is disabled (probably because you're running an incompatible version of T4Scaffolding.dll)
if (-not (Get-Module T4Scaffolding)) { return }

Set-DefaultScaffolder -Name LinqToSqlDataContext -Scaffolder LinqToSqlScaffolding.DataContext -SolutionWide -DoNotOverwriteExistingSetting