function Get-TableModifications([string]$filename)
{
     .\GetTableModifications.exe $filename | ConvertFrom-Json | %{ $_ | Add-Member @{Filename=$filename} -PassThru }
}