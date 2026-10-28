$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$src=$PSScriptRoot
$metadata=Join-Path $env:WINDIR "System32\WinMetadata"
$runtime=(Get-ChildItem (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_MSIL\System.Runtime") -Recurse -Filter System.Runtime.dll | Select-Object -First 1).FullName
$output=Join-Path (Split-Path $src -Parent) 'EDGERUNNER.exe'
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/out:$output" "/win32manifest:$src\app.manifest" "/win32icon:$src\edgerunner.ico" "/resource:$src\Interface.xaml,Interface.xaml" "/resource:$src\night-city.png,night-city.png" "/resource:$src\edgerunner.ico,edgerunner.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$framework\WPF\WindowsBase.dll" "/reference:$framework\WPF\PresentationCore.dll" "/reference:$framework\WPF\PresentationFramework.dll" /reference:System.Xaml.dll "/reference:$runtime" "/reference:$metadata\Windows.Media.winmd" "/reference:$metadata\Windows.Foundation.winmd" "/reference:$metadata\Windows.Storage.winmd" "$src\Edgerunner.cs" "$src\Media.cs"
if($LASTEXITCODE -ne 0){throw 'No se pudo compilar.'}
Write-Output $output
