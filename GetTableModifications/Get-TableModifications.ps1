[cmdletbinding()]
Param(
	[Parameter(ValueFromPipeline=$true)]
	[string]
	$Filename
)

Process {
	.\GetTableModifications.exe $Filename | Out-String | ConvertFrom-Json | %{ $_ | Add-Member @{Filename=$filename} -PassThru }
}