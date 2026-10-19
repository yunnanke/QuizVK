# ПЗ-3: воспроизводимый запуск QuizVK

Этот документ дополняет [ADR-001](../adr/ADR-001.md) и [декларацию инструментального контура](../toolchain.md). Он описывает маршрут запуска QuizVK из чистого клона. Доказательством независимого воспроизведения становится только заполненный протокол другим студентом или преподавателем, а не этот шаблон.

## Предварительные условия

- Windows 10/11 или другая среда с совместимым .NET SDK 8.x;
- Git;
- права на запись в каталог запуска: SQLite создаёт файл `quizapp.db`.

Перед началом следует зафиксировать среду:

```powershell
dotnet --version
git rev-parse HEAD
git status --short
```

`git status --short` не должен выводить строк: запуск относится к зафиксированному состоянию.

## Чистый запуск

```powershell
git clone https://github.com/yunnanke/QuizVK.git
cd QuizVK
dotnet restore QuizMvp.sln
dotnet build QuizMvp.sln --configuration Debug
dotnet run --project QuizMvp --configuration Debug
```

После запуска сервер останавливается сочетанием `Ctrl+C`. Локальная база появляется в рабочем каталоге приложения; для повторной проверки исходного состояния её удаляют только после остановки процесса:

```powershell
Remove-Item .\QuizMvp\quizapp.db -ErrorAction SilentlyContinue
```

## Поставочный режим

```powershell
dotnet build QuizMvp.sln --configuration Release
dotnet publish QuizMvp --configuration Release --output C:\temp\quizvk-release
Set-Location C:\temp\quizvk-release
.\QuizMvp.exe
```

Запуск из `C:\temp\quizvk-release` подтверждает, что используется подготовленный результат, а не запуск из исходного каталога. В этом каталоге должны быть права на запись для SQLite.

## Проверка логики

```powershell
dotnet run --project QuizMvp.LogicTests\QuizMvp.LogicTests.csproj --configuration Release
dotnet run --project QuizMvp.LogicTests\QuizMvp.LogicTests.csproj --configuration Release -- --mutation-check
```

Первый запуск должен вывести девять строк `PASS`; второй — `MUTATION KILLED by TC-SCORE-04`. Эти тесты проверяют только `AnswerScoringService.Score`, а не UI, SQLite, авторизацию или SignalR.

## Ограничения текущей версии

- Прямая зависимость пакетов фиксируется версиями `PackageReference`; отдельный `packages.lock.json` пока отсутствует.
- Workflow GitHub Actions настроен, но подтверждением служит только конкретный запуск на commit сдаваемой версии.
- Проверка чужим студентом должна быть проведена по этой инструкции в отдельном чистом окружении и записана в [протокол](independent-reproduction.md).
