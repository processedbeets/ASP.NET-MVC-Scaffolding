[T4Scaffolding.Scaffolder(Description = "Creates a LINQ to SQL DataContext class")][CmdletBinding()]
param(        
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$ModelType,
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$DbContextType,
	[string]$Area,
    [string]$Project,
	[string]$CodeLanguage,
	[string[]]$TemplateFolders
)

# Ensure you've referenced System.Data.Linq
(Get-Project $Project).Object.References.Add("System.Data.Linq") | Out-Null

# Inherit all other logic from T4Scaffolding.EFDbContext
Scaffold T4Scaffolding.EFDbContext -ModelType $ModelType -DbContextType $DbContextType -Area $Area -Project $Project -CodeLanguage $CodeLanguage -OverrideTemplateFolders $TemplateFolders