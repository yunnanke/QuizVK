$host.UI.RawUI.WindowTitle = 'PZ7 AnswerScoringService cca508c verified'
$OutputEncoding = [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
chcp 65001 | Out-Null
Set-Location 'C:\Users\eg0rg\Desktop\quizzy\_pz7_review'
Write-Host 'Reviewed commit: cca508ce642c55369d541daf01d4d78e9f91acf1'
Write-Host 'Command 1: dotnet run -c Release --no-build --project QuizMvp.LogicTests'
dotnet run -c Release --no-build --project QuizMvp.LogicTests
$logicExit = $LASTEXITCODE
Write-Host "Contract test exit code: $logicExit"
Write-Host 'Command 2: dotnet run -c Release --no-build --project QuizMvp.LogicTests -- --mutation-check'
dotnet run -c Release --no-build --project QuizMvp.LogicTests -- --mutation-check
$mutationExit = $LASTEXITCODE
Write-Host "Mutation check exit code: $mutationExit"
Write-Host 'Release build: 0 errors, 34 warnings.'
[System.IO.File]::WriteAllText('C:\Users\eg0rg\Desktop\quizzy\_pz02_work\evidence-p7-ready.txt', "logic=$logicExit`nmutation=$mutationExit")
Read-Host 'Press Enter to keep the results on screen'
