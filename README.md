# QuizVK

[![HTML](https://img.shields.io/badge/HTML-55.4%25-orange)](https://developer.mozilla.org/en-US/docs/Web/HTML)
[![C#](https://img.shields.io/badge/C%23-24.7%25-purple)](https://learn.microsoft.com/dotnet/csharp/)
[![JavaScript](https://img.shields.io/badge/JavaScript-12.7%25-yellow)](https://developer.mozilla.org/en-US/docs/Web/JavaScript)
[![CSS](https://img.shields.io/badge/CSS-7.2%25-blue)](https://developer.mozilla.org/en-US/docs/Web/CSS)

Веб-приложение для создания и прохождения интерактивных викторин.

## Описание

**QuizVK** — веб-приложение на базе **ASP.NET Core**. Автор создаёт квиз с вопросами и вариантами ответов, запускает сессию и получает код комнаты. Участник подключается по коду, отвечает на вопросы и получает автоматически рассчитанный результат.

Проект реализуется как MVP. Для хранения данных используется локальная SQLite, а для событий сессии — SignalR. PostgreSQL и отдельный WebSocket-сервер находятся за границами текущей версии продукта.

## Основные возможности

- **Регистрация и вход** пользователей.
- **Создание квизов** с вопросами, вариантами и баллами.
- **Запуск сессии** с уникальным кодом комнаты.
- **Прохождение викторин** участниками.
- **Проверка ответов**: баллы начисляются только при полном совпадении выбранных и правильных вариантов.
- **Таблица лидеров** и базовые результаты сессии.
- **Адаптивный интерфейс** на HTML, CSS и JavaScript.

## Технологии

- **Backend:** C#, ASP.NET Core MVC, .NET 8.
- **Хранение данных:** Entity Framework Core и SQLite.
- **События сессии:** SignalR.
- **Frontend:** HTML, CSS, JavaScript.
- **Зависимости:** NuGet.
- **Среда разработки:** Visual Studio 2022 или PowerShell.

## Структура проекта

```text
QuizMvp/                  ASP.NET Core веб-приложение
QuizMvp.LogicTests/       Консольные проверки логики начисления баллов
QuizMvp.sln               Файл решения
.github/workflows/        Workflow GitHub Actions
```

Логика начисления баллов находится в:

```text
QuizMvp/Services/AnswerScoringService.cs
```

Она используется в `QuizMvp/Controllers/QuizController.cs`, метод `SubmitAnswer`.

## Установка и запуск

### Требования

- [.NET SDK 8.x](https://dotnet.microsoft.com/download)
- Git
- Visual Studio 2022 или новее — необязательно

Проверка версии SDK:

```powershell
dotnet --version
```

### Клонирование проекта

```powershell
git clone https://github.com/yunnanke/QuizVK.git
cd QuizVK
dotnet restore QuizMvp.sln
```

Версии пакетов NuGet указаны в `QuizMvp/QuizMvp.csproj`.

### Запуск в режиме разработки

```powershell
dotnet build QuizMvp.sln --configuration Debug
dotnet run --project QuizMvp --configuration Debug
```

Приложение использует настройки из `QuizMvp/appsettings.json`. По умолчанию SQLite подключается так:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=quizapp.db"
  }
}
```

Файл `quizapp.db` создаётся при первом запуске приложения. Остановить сервер можно сочетанием `Ctrl+C`.

Не следует добавлять в `appsettings.json` пароли, токены или строки подключения к рабочей БД. Для закрытых данных используются переменные окружения или User Secrets.

## Сборка и публикация Release

```powershell
dotnet build QuizMvp.sln --configuration Release
```

Публикация в отдельную папку:

```powershell
dotnet publish QuizMvp --configuration Release --output C:\temp\quizvk-release
cd C:\temp\quizvk-release
.\QuizMvp.exe
```

В каталоге публикации находятся исполняемый файл, DLL-библиотеки, настройки приложения и манифест зависимостей. Для работы SQLite в каталоге должны быть права на запись.

## Проверки логики

Проект `QuizMvp.LogicTests` проверяет требование FR-08: баллы начисляются только при полном совпадении выбранных и правильных вариантов.

```powershell
dotnet run --project QuizMvp.LogicTests\QuizMvp.LogicTests.csproj --configuration Release
```

Ожидаемый результат: девять строк `PASS` и код завершения `0`.

Проверка чувствительности набора тестов к искусственному дефекту:

```powershell
dotnet run --project QuizMvp.LogicTests\QuizMvp.LogicTests.csproj --configuration Release -- --mutation-check
```

Ожидаемый результат:

```text
MUTATION KILLED by TC-SCORE-04
```

Этот режим не изменяет рабочую реализацию модуля.

## Автоматические проверки

Workflow `.github/workflows/quality-gate.yml` запускается при `push` и `pull_request`:

1. Восстанавливает NuGet-зависимости.
2. Собирает решение в режиме Release.
3. Запускает `QuizMvp.LogicTests`.

Перед созданием Pull Request рекомендуется выполнить:

```powershell
dotnet restore QuizMvp.sln
dotnet build QuizMvp.sln --no-restore --configuration Release
dotnet run --project QuizMvp.LogicTests\QuizMvp.LogicTests.csproj --no-build --configuration Release
```

## Правила внесения изменений

Для каждого изменения создаётся отдельная ветка:

```powershell
git switch -c feature/short-change-name
```

Изменение поведения должно включать обновление требований, реализации, тестов и инструкции. После локальной проверки создаётся Pull Request.

В репозиторий нельзя добавлять пароли, токены, файлы SQLite, результаты сборки и персональные настройки.

## Границы MVP

В текущую версию входят:

- регистрация и вход;
- создание квизов и вопросов;
- запуск сессии и код комнаты;
- отправка ответа участником;
- начисление баллов при полном совпадении;
- SQLite;
- SignalR;
- таблица лидеров и базовые результаты.

В резерве остаются:

- миграция на PostgreSQL;
- отдельная WebSocket-инфраструктура;
- частичное начисление баллов;
- расширенная аналитика;
- импорт квизов.

## Автор

**yunnanke** — [GitHub-профиль](https://github.com/yunnanke)
**shirley** — [GitHub-профиль](https://github.com/shirley0sh)