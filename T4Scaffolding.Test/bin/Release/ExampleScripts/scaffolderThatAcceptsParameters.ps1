[T4Scaffolding.Scaffolder(Description = "My description")][CmdletBinding()]
param(
	[parameter()]$SomeCustomParam,
	[switch]$SomeCustomSwitch,
	[string[]]$TemplateFolders
)

return "Hello"