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
*   [.NET SDK](https://dotnet.microsoft.com/download) (версия, совместимая с проектом)
*   Visual Studio 2022 или более новая версия (рекомендуется)

### Инструкция
1.  **Клонировать репозиторий:**
    ```bash
    git clone https://github.com/yunnanke/QuizVK.git
    cd QuizVK
    ```
2.  **Открыть решение:** Дважды щелкните по файлу `QuizMvp.sln`, чтобы открыть проект в Visual Studio.
3.  **Восстановить зависимости:** В Visual Studio выберите `Собрать` > `Восстановить пакеты NuGet` или выполните в терминале:
    ```bash
    dotnet restore
    ```
4.  **Собрать и запустить:** Нажмите `F5` или выберите `Запустить` в меню отладки.

Также можно собрать и запустить проект из командной строки из папки с проектом (`QuizMvp`):
```bash
dotnet build
dotnet run
```

## Автор

**yunnanke** — [GitHub профиль](https://github.com/yunnanke)

