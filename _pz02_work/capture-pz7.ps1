$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Pz7WindowCapture {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
'@
$ready = 'C:\Users\eg0rg\Desktop\quizzy\_pz02_work\evidence-p7-ready.txt'
Remove-Item -LiteralPath $ready -Force -ErrorAction SilentlyContinue
$console = Start-Process -FilePath "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File','C:\Users\eg0rg\Desktop\quizzy\_pz02_work\pz7-console.ps1') -PassThru -WindowStyle Normal
$window = $null
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    $window = Get-Process WindowsTerminal -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -like '*PZ7 AnswerScoringService cca508c verified*' } | Select-Object -First 1
    if ($null -ne $window -and $window.MainWindowHandle -ne [IntPtr]::Zero) { break }
}
if ($null -eq $window -or $window.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Test console window was not found.' }
for ($i = 0; $i -lt 40 -and -not (Test-Path -LiteralPath $ready); $i++) { Start-Sleep -Milliseconds 500 }
if (-not (Test-Path -LiteralPath $ready)) { throw 'Test output did not reach the completed state.' }
[Pz7WindowCapture]::ShowWindow($window.MainWindowHandle, 3) | Out-Null
[Pz7WindowCapture]::SetForegroundWindow($window.MainWindowHandle) | Out-Null
Start-Sleep -Seconds 2
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
$path = 'C:\Users\eg0rg\Desktop\quizzy\_pz02_work\evidence-p7-answer-scoring.png'
$bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
Write-Output "SCREENSHOT=$path"
Write-Output "WINDOW=$($window.MainWindowTitle)"
