[T4Scaffolding.Scaffolder(Description = "Creates a repository")][CmdletBinding()]
param(        
    [parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$ModelType,
    [string]$DbContextType,
	[string]$Area,
    [string]$Project,
    [string]$CodeLanguage,
	[switch]$NoChildItems = $false,
	[string[]]$TemplateFolders,
	[switch]$Force = $false
)

# Inherit all logic from T4Scaffolding.EFRepository. Override the template by passing $TemplateFolders from this scaffolder.
Scaffold T4Scaffolding.EFRepository -ModelType $ModelType -DbContextType $DbContextType -Area $Area -Project $Project -CodeLanguage $CodeLanguage -NoChildItems:$true -OverrideTemplateFolders $TemplateFolders -Force:$Force

if(!$NoChildItems) {
	if(!$DbContextType) { $DbContextType = [System.Text.RegularExpressions.Regex]::Replace((Get-Project $Project).Name, "[^a-zA-Z0-9]", "") + "Context" }
	Scaffold LinqToSqlDataContext -ModelType $ModelType -DbContextType $DbContextType -Area $Area -Project $Project -CodeLanguage $CodeLanguage
}