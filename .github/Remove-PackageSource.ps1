[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)][string]$ConfigFile,
    [Parameter(Mandatory = $true)][string]$Source
)
$doc = New-Object System.Xml.XmlDocument
$filename = (Get-Item $ConfigFile).FullName
$doc.Load($filename)

$sources = $doc.DocumentElement.SelectSingleNode("packageSources")
if ($sources -ne $null)
{
    $localSource = $sources.SelectSingleNode("add[@key='$Source']")
    if ($localSource -ne $null) {
        $sources.REmoveChild($localSource)
    }
}

$doc.Save($filename)

Get-Content $filename