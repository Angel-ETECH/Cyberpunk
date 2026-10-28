$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$src=$PSScriptRoot
$output=Join-Path (Split-Path $src -Parent) 'EDGERUNNER.exe'
& "$framework\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/out:$output" "/win32manifest:$src\app.manifest" "/win32icon:$src\edgerunner.ico" "/resource:$src\Interface.xaml,Interface.xaml" "/resource:$src\night-city.png,night-city.png" "/resource:$src\edgerunner.ico,edgerunner.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Xml.dll /reference:System.Xml.Linq.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$framework\WPF\WindowsBase.dll" "/reference:$framework\WPF\PresentationCore.dll" "/reference:$framework\WPF\PresentationFramework.dll" /reference:System.Xaml.dll "$src\Edgerunner.cs"
if($LASTEXITCODE -ne 0){throw 'No se pudo compilar.'}
Write-Output $output
