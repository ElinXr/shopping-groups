# Shopping Groups V1

**Подготвено обновяване v2:** [push известия с автор и час](docs/PUSH-NOTIFICATIONS.md), с включване от „Профил“, тестово известие и запазени ключове в Neon. Показва първоначалния автор и часа на добавяне по българско време. Локално минават 98 теста и production клиентският build; новият API/Docker набор остава за изпълнение в GitHub. Публикуването на тази версия в сайта още не е потвърдено.

Мобилно приложение за частни групи и съвместно пазаруване: React + TypeScript PWA, ASP.NET Core **10**, PostgreSQL, EF Core Identity и SignalR. Интерфейсът е на български.

**Предходна версия:** локално са минали 72 теста и клиентският build. На 25 октомври потребителят предостави успешна GitHub Actions проверка за commit `fa44122`; зададеният pipeline включва API build и Docker интеграционните проверки. Публичната страница за вход на [Shopping Groups](https://shopping-groups-test.onrender.com/) зарежда според предоставената снимка; предходният Render лог потвърждава достъп до Neon базата. Самостоятелният публичен smoke test е блокиран от браузърното разрешение, а локално NuGet и Docker остават недостъпни. Доставката на имейли и двете npm находки от предишния лог остават за проверка. Вижте [точния отчет](docs/VERIFICATION.md).

## Стартиране с Docker

Необходими са Docker с Linux containers, Docker Compose v2 и мрежов достъп до npm, NuGet и регистрите за Docker образи.

```powershell
Copy-Item .env.example .env
# Редактирайте .env: парола за базата, администратор, публичен адрес и SMTP.
docker compose up --build -d
docker compose logs -f api
```

- Приложение: [http://localhost:8080](http://localhost:8080)
- Локална поща Mailpit: [http://localhost:8025](http://localhost:8025)
- Готовност на API: [http://localhost:8080/health/ready](http://localhost:8080/health/ready)

Първото стартиране прилага схемата. Създайте профил, група и списък. От „Хора и покани“ собственикът създава линк/QR или имейл покана. За имейл покана получателят трябва да потвърди адреса си чрез писмото в Mailpit. Публичната регистрация не дава администраторски права. Профилът от `ADMIN_EMAIL`/`ADMIN_PASSWORD` получава отделен административен панел.

Локалната Compose конфигурация използва Development за HTTP cookies и публикува портовете само на `127.0.0.1`. За интернет използвайте производствената конфигурация по-долу.

## Локална разработка без контейнер за API

Необходими са .NET SDK 10, Node.js 24 и PostgreSQL. EF Core/Npgsql/Identity пакетите са версия 10, а не подменени с по-стара версия заради локалните ограничения.

```powershell
# Терминал 1, от корена на проекта
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
$env:ConnectionStrings__Database = 'Host=localhost;Database=shopping;Username=shopping;Password=YOUR_PASSWORD'
$env:PublicUrl = 'http://localhost:5173'
dotnet run --project src/ShoppingGroups.Api

# Терминал 2, от корена на проекта
npm ci --prefix web --ignore-scripts
npm run dev --prefix web
```

За локална поща задайте `Smtp__Host`, `Smtp__Port` и `Smtp__From`, или стартирайте Mailpit отделно. При липса на SMTP писмата чакат в опашката, а известията в приложението продължават да работят. Vite препраща API и WebSocket заявките към порт 5080.

При настройки, изтеглени от Neon CLI, използвайте `npm run dev:api` от корена: стартиращият скрипт зарежда частния `.env` или `.env.local` за сървъра. За клиента използвайте `npm run dev:web` в друг терминал. API предпочита `DATABASE_URL_UNPOOLED` пред `DATABASE_URL`; системният `ConnectionStrings__Database` запазва предимство. Не поставяйте database URL във frontend/Vite променливи. [Neon CLI и ограниченията на последния опит](docs/FREE-HOSTING.md).

## Проверки

```powershell
dotnet run --project tests/ShoppingGroups.Domain.Tests
npm test --prefix web
npm run build --prefix web
dotnet build src/ShoppingGroups.Api
docker compose -f compose.tests.yaml up --build --abort-on-container-exit --exit-code-from tests
docker compose -f compose.tests.yaml down --volumes
```

За целия набор: `./scripts/check.ps1` или `sh scripts/check.sh`. Интеграционната конфигурация е отделна от нормалната база и използва временен PostgreSQL, Mailpit, истински HTTP cookies и SignalR WebSocket. Тестовите проекти са изпълними програми; `dotnet test` не е техният вход. CI изпълнява същите проверки.

## Публично разгръщане

За желания безплатен тест текущият [render.yaml](render.yaml) използва **Render Free + външна Neon Free PostgreSQL база**. Той не създава платен диск или Render база. [Стъпките и действителните адреси](docs/FREE-HOSTING.md) включват онлайн панела на базата и поддръжката на бъдещи корекции. Безплатните планове имат квоти и заспиване при неактивност; не се обещава неограничена услуга завинаги. Платеният стар вариант е запазен отделно в `deploy/render-paid.yaml`.

## Документация

- [Правила, допускания и граници на V1](docs/SPECIFICATION.md)
- [Архитектура, схема, транзакции и експлоатация](docs/ARCHITECTURE.md)
- [API маршрути](docs/API.md)
- [Проверки и действително получени резултати](docs/VERIFICATION.md)
- [Разгръщане и достъп](docs/DEPLOYMENT.md)

Продуктите са свободен текст. Всички участници могат да ги променят, изтриват и купуват. Дублирането се предупреждава, но се разрешава след потвърждение. Няколко участници могат да пазаруват едновременно. Завършването и началото изпращат автоматични известия; „Извести групата“ показва обобщение преди изпращане. Изтрит списък се възстановява до 90 дни, а архивът е личен за всеки участник.
