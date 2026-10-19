# Инструментальный контур QuizVK

- Проект: QuizVK (MVP Quizzy)
- Авторы проекта: Матвеева Ю. П., Голубев Е. Е.
- Индивидуальная часть Голубева Е. Е.: регистрация и авторизация (`AccountController`)
- Версия документа: 1.1
- Дата: 19.10.2026

## Назначение и границы

Документ описывает получение, сборку, проверку и запуск веб-приложения QuizVK. В этой практической проверяется компонент регистрации и авторизации: MVC-маршруты `AccountController`, переключатель регистрации `Authentication:RegistrationEnabled`, версия исходного состояния и безопасная диагностика запуска. Пароли и пользовательские данные в диагностический ответ и журнал запуска не включаются.

Архитектурное решение и общая граница инфраструктуры проекта описаны в [ADR-001](adr/ADR-001.md). Практика не меняет выбранные ASP.NET Core MVC, SQLite, EF Core и SignalR.

## Позиции инструментального контура

| Позиция | Средство | Применение в проекте | Состояние |
|---|---|---|---|
| Сборка | .NET SDK 10.0.401, MSBuild; целевая платформа `net8.0` | `dotnet restore QuizMvp.sln`, `dotnet build QuizMvp.sln -c Debug/Release` | Зафиксирована `global.json`; CI устанавливает SDK 10 и runtime .NET 8 |
| Управление зависимостями | NuGet, точные `PackageReference`, `packages.lock.json` | `dotnet restore QuizMvp.sln --locked-mode` | Прямые и транзитивные версии зафиксированы lock-файлами |
| Тестирование | xUnit, `Microsoft.AspNetCore.Mvc.Testing`, `QuizMvp.LogicTests` | `dotnet test QuizMvp.sln -c Release`; запуск LogicTests | Работает локально и включено в CI |
| Статический анализ | Roslyn, nullable-аннотации и предупреждения компилятора | Сборка решения; CI собирает Release | Используется; отдельный анализатор стиля не подключён |
| Отладка и диагностика | ASP.NET Core logging, Visual Studio / .NET debugger, диагностический HTTP-маршрут | Стартовое сообщение без секретов; `GET /_diagnostics/authentication` | Реализовано для модуля авторизации |
| Профилирование | `dotnet-counters`, `dotnet-trace` | Исследование процесса при необходимости, не критерий этой практики | Инструменты доступны для отдельного профилирования; замеры производительности здесь не заявляются |
| CI | GitHub Actions | `.github/workflows/quality-gate.yml`: restore, Release build, xUnit, LogicTests | Настроено; ссылка на запуск конкретного workflow добавляется после push ветки |

## Фиксированное окружение

- SDK: `10.0.401` — файл `global.json` запрещает незаметно выбрать другой SDK.
- Целевая платформа и runtime: `net8.0` / .NET 8.
- На машине сборки одновременно нужны SDK 10.0.401 и runtime .NET 8; workflow устанавливает SDK 8 и 10.0.401.
- Пакеты NuGet зафиксированы в `.csproj`; транзитивное дерево — в `packages.lock.json` соответствующих проектов.
- Список разрешённых прямых и транзитивных пакетов проверяется командой `dotnet list QuizMvp/QuizMvp.csproj package --include-transitive --no-restore`.
- Локальная база для приложения задаётся `ConnectionStrings:DefaultConnection` в конфигурации. Не добавляйте в репозиторий пароли, реальные пользовательские данные или рабочие секреты.

## Конфигурация модуля

В `QuizMvp/appsettings.json` содержится безопасное демонстрационное значение:

```json
{
  "Authentication": {
    "RegistrationEnabled": true
  }
}
```

Допустимы только `true` и `false`. Отсутствующее или некорректное значение останавливает запуск с понятным сообщением о ключе `Authentication:RegistrationEnabled`. Значение `false` отключает GET- и POST-обработчики регистрации: они возвращают HTTP 503 и уведомление на странице. Вход в существующую учётную запись не отключается.

В Windows PowerShell временное переопределение без редактирования файла:

```powershell
$env:Authentication__RegistrationEnabled = "false"
dotnet run --project QuizMvp/QuizMvp.csproj --no-build --configuration Release
Remove-Item Env:Authentication__RegistrationEnabled
```

## Повторяемое получение результата

Из корня репозитория выполните:

```powershell
git checkout <ветка-практики>
git rev-parse HEAD
dotnet --version
dotnet restore QuizMvp.sln --locked-mode
dotnet build QuizMvp.sln --no-restore --configuration Debug
dotnet test QuizMvp.sln --no-build --configuration Debug
dotnet build QuizMvp.sln --no-restore --configuration Release
dotnet test QuizMvp.sln --no-build --configuration Release
dotnet run --project QuizMvp.LogicTests/QuizMvp.LogicTests.csproj --no-build --configuration Release
```

Для запуска опубликованного Release-результата вне папки исходников сначала опубликуйте приложение в отдельный каталог, затем запускайте DLL из другого рабочего каталога:

```powershell
$publish = Join-Path $env:TEMP "QuizVK-Auth-Release"
dotnet publish QuizMvp/QuizMvp.csproj --no-restore --configuration Release --output $publish
Push-Location $publish
dotnet (Join-Path $publish "QuizMvp.dll")
Pop-Location
```

При необходимости задайте временный путь базы, чтобы не менять рабочую БД. Версия приложения выдаётся в `/_diagnostics/authentication`; она включает informational version и revision исходного состояния. Стартовый журнал содержит только флаг регистрации, версию и конфигурацию сборки.

Сборка и содержимое управляемого артефакта проверяются отдельно:

```powershell
$publish = Join-Path $env:TEMP "QuizVK-Auth-Release"
dotnet publish QuizMvp/QuizMvp.csproj --no-restore --configuration Release --output $publish
ildasm.exe /text "/out=$env:TEMP\QuizVK-Authentication.il" (Join-Path $publish "QuizMvp.dll")
Select-String -Path "$env:TEMP\QuizVK-Authentication.il" -Pattern "AuthenticationModuleSettings|/_diagnostics/authentication|RegistrationUnavailable"
Get-Item (Join-Path $publish "QuizMvp.dll") | Select-Object Name, Length, LastWriteTime
```

`ildasm` используется только для чтения опубликованной сборки. В листинге подтверждаются тип `AuthenticationModuleSettings`, маршрут диагностики и ветви обработчика регистрации.

## Проверка поведения и сохранение свидетельств

1. Обычный запуск с `RegistrationEnabled=true`: откройте `/Account/Register` и `/_diagnostics/authentication`; сохраните вывод, версию, revision и код ответа.
2. Отрицательный сценарий конфигурации: задайте `RegistrationEnabled=false`; проверьте HTTP 503 для GET и POST регистрации и флаг `false` в диагностике.
3. Некорректная конфигурация: задайте значение `yes`; приложение должно завершиться при старте с сообщением о неверном ключе.
4. Для контроля установленного дерева зависимостей сохраните вывод `dotnet list ... --include-transitive`; для инспекции Release-артефакта сохраните список типов из `ildasm`.
5. Сравнение Debug и Release выполняется на одном фиксированном commit и одинаковом сценарии. В отчёт вносятся фактические команды и результаты двух запусков.
6. Проверку с чистого клона и независимое воспроизведение выполняет другой участник/назначенный проверяющий; его имя, commit, команды, результат и скриншот добавляются только после реального выполнения.
7. До push невозможно получить ссылку на проверку этой ветки в GitHub Actions. После согласования push ссылка на workflow дополняется в отчёте.
8. Финальный тег создаётся только после принятия проверенного состояния: `0104-pz01-<код>-v1`. При повторной сдаче используется новый тег; существующий тег не перемещается.

Скриншоты отчёта должны показывать фактические окно приложения/терминал, команду и результат. Не подменяйте результаты реального запуска макетами. Независимую проверку и CI нельзя отмечать как выполненные до появления соответствующего результата.
