param([switch]$Test)
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$outDir = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$references = @('System.Net.Http.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xaml.dll', 'WPF/WindowsBase.dll', 'WPF/PresentationCore.dll', 'WPF/PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$sources = @('App.cs', 'TodoStore.cs', 'MainWindow.cs', 'Docking.cs', 'DailyQuote.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
$common = @('/nologo', '/utf8output', '/optimize+', ('/win32icon:' + (Join-Path $PSScriptRoot 'Daily.ico')), ('/resource:' + (Join-Path $PSScriptRoot 'MainWindow.xaml') + ',DailyTodo.MainWindow.xaml'))
& $compiler @common @references /target:winexe /main:DailyTodo.Program ('/out:' + (Join-Path $outDir 'DailyTodo.exe')) @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
if ($Test) {
    & $compiler @common @references /target:exe /main:DailyTodo.Tests ('/out:' + (Join-Path $outDir 'DailyTodo.Tests.exe')) @sources (Join-Path $PSScriptRoot 'Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed' }
    & (Join-Path $outDir 'DailyTodo.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
}
Write-Output "Built: $outDir/DailyTodo.exe"



