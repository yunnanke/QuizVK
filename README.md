# QuizVK

[![HTML](https://img.shields.io/badge/HTML-55.4%25-orange)](https://developer.mozilla.org/en-US/docs/Web/HTML)
[![C#](https://img.shields.io/badge/C%23-24.7%25-purple)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![JavaScript](https://img.shields.io/badge/JavaScript-12.7%25-yellow)](https://developer.mozilla.org/en-US/docs/Web/JavaScript)
[![CSS](https://img.shields.io/badge/CSS-7.2%25-blue)](https://developer.mozilla.org/en-US/docs/Web/CSS)

Веб-приложение для создания и прохождения викторин. 

## Описание

**QuizVK** — это интерактивное веб-приложение, которое позволяет пользователям проходить тесты и викторины. Проект построен на базе **ASP.NET Core**, что обеспечивает полноценную серверную логику и динамический клиентский интерфейс.

Основная цель проекта — предоставить удобную платформу для проверки знаний в игровой форме.

## Основные возможности

*   **Прохождение викторин**: Пользователи могут отвечать на вопросы из различных категорий.
*   **Управление вопросами**: Администраторы могут создавать, редактировать и удалять вопросы и варианты ответов.
*   **Визуальный интерфейс**: Современный и адаптивный интерфейс, созданный с использованием HTML, CSS и JavaScript.

## Технологии

Проект использует следующие технологии и языки:

*   **Backend**: C# (ASP.NET Core / .NET)
*   **Frontend**: HTML, CSS, JavaScript
*   **Среда разработки**: Microsoft Visual Studio

## Установка и запуск

### Требования
*   .NET SDK 10.0.401 (версия закреплена в `global.json`)
*   .NET 8 runtime для запуска приложения с целевой платформой `net8.0`
*   Visual Studio 2022 или более новая версия (необязательно)

### Инструкция
1. **Клонировать репозиторий и перейти в корень:**
    ```bash
    git clone https://github.com/yunnanke/QuizVK.git
    cd QuizVK
    ```
2. **Перейти на ветку с готовой практической работой** (после её публикации):
    ```bash
    git checkout <ветка-практики>
    ```
3. **Восстановить зафиксированные версии зависимостей и собрать приложение:**
```bash
dotnet restore QuizMvp.sln --locked-mode
dotnet build QuizMvp.sln --no-restore --configuration Release
dotnet test QuizMvp.sln --no-build --configuration Release
```
4. **Запустить веб-приложение:**
```bash
dotnet run --project QuizMvp/QuizMvp.csproj --configuration Release
```
В консоли появится адрес локального сервера. Версию компонента и состояние настройки регистрации можно получить запросом `/_diagnostics/authentication`. Переключатель `Authentication:RegistrationEnabled` принимает только `true` или `false`; пример безопасной конфигурации находится в `QuizMvp/appsettings.json`.

Команды сравнения Debug и Release, проверки отключённой регистрации и запуска результата вне каталога исходников приведены в [описании инструментального контура](docs/toolchain.md).
```

## Автор

**yunnanke** — [GitHub профиль](https://github.com/yunnanke)

