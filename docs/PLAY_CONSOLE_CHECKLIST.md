# Play Console — что осталось вручную

Автоматика в репозитории уже готова: **AAB**, **метаданные fastlane**, **секреты подписи** в GitHub Actions.

## 1. Дорегистрировать аккаунт (≈5 мин + $25)

В [Play Console](https://play.google.com/console/signup) под `yaylymchik@gmail.com`:

1. **Для себя** → имя разработчика **YayLym**.
2. **Платежный профиль** → привязать карту и оплатить **$25**.
3. Пройти **подтверждение личности** и остальные шаги мастера.

Вкладка консоли уже открыта на шаге «Платежный профиль».

## 2. Создать приложение

1. **Create app** → **Sumrak VPN**, язык **Russian**, бесплатное.
2. Package: **`com.sumrax.app`** (должен совпасть с AAB).
3. Политика: URL из `PRIVACY_POLICY.md` на GitHub.
4. Категория: **Tools** / VPN-клиент (без собственной инфраструктуры).
5. Заполнить **Data safety** и декларацию VPN.

## 3. API для автозаливки

1. **Setup → API access** → привязать Google Cloud project.
2. **Create service account** → скачать JSON.
3. В Play Console дать аккаунту роль **Admin** или **Release manager**.
4. В GitHub репозитории добавить секрет:

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\play-store-credentials.json")) | Set-Clipboard
```

Имя секрета: **`PLAY_STORE_JSON_BASE64`**.

## 4. Первый релиз

**Actions → Publish Play Internal → Run workflow** (track: `internal`).

Или push тега `v2.2.21` повторно / новый тег — workflow соберёт signed AAB и зальёт метаданные.

Секреты подписи **`APP_KEYSTORE_*`** уже в GitHub (upload keystore). Пароль хранится локально в `.build-cache/play-signing.local.txt` (не коммитится).

## 5. Листинг

Тексты: `fastlane/metadata/android/ru-RU` и `en-US`.  
Иконку 512 и скриншоты — в консоли вручную (fastlane images пока пропускаются).