# Публикация Sumrak VPN в Google Play

Пакет для магазина: **`com.sumrax.app`** (flavor `playstore`, без суффикса `.fdroid`).

## 1. Аккаунт и приложение в Play Console

1. [Google Play Console](https://play.google.com/console) — регистрация разработчика (разовый взнос).
2. **Create app** → название **Sumrak VPN**, язык по умолчанию, тип **App**, бесплатное.
3. Заполните обязательные разделы чеклиста (политика, контент, доступ к приложению).

## 2. Подпись релиза

Создайте release-keystore (один раз) и добавьте в `V2rayNG/gradle.properties` (файл не коммитить):

```properties
SUMRAX_STORE_FILE=D:/path/to/sumrax-release.jks
SUMRAX_STORE_PASSWORD=***
SUMRAX_KEY_ALIAS=sumrax
SUMRAX_KEY_PASSWORD=***
```

В Play Console включите **Play App Signing** (рекомендуется): загрузите первый AAB, Google хранит ключ подписи дистрибуции.

## 3. Сборка AAB (обязательный формат Play)

```powershell
cd V2rayNG
.\gradlew.bat :app:bundlePlaystoreRelease
```

Артефакт:

`V2rayNG/app/build/outputs/bundle/playstoreRelease/app-playstore-release.aab`

Для push уведомлений положите `app/google-services.json` до сборки (см. Firebase).

## 4. Политика конфиденциальности

В консоли укажите URL:

`https://github.com/Yaylymchik/Sumrak-VPN-v2ray-TM-Dealler/blob/main/PRIVACY_POLICY.md`

Тот же URL используется в приложении (`AppConfig.APP_PRIVACY_POLICY`).

## 5. Категория VPN (важно)

В анкете укажите, что приложение — **VPN-клиент для пользовательских конфигураций**, без собственной VPN-инфраструктуры.

- **Функция VPN:** `VpnService` в манифесте.
- **QUERY_ALL_PACKAGES:** для режима прокси по приложениям — в декларации Play укажите «per-app proxy / app list for split tunneling».
- **Foreground service:** VPN и фоновые задачи (обновление подписок, проверка обновлений).

Ознакомьтесь с [политикой VPN в Play](https://support.google.com/googleplay/android-developer/answer/9888170).

## 6. Data safety

Примерно:

| Тип | Собирается | Передаётся | Цель |
|-----|------------|------------|------|
| Диагностика (логи) | По действию пользователя | Нет (локально) | Отладка |
| Идентификаторы устройства (FCM) | Опционально, если включён Firebase | Google FCM | Push |

Трафик VPN **не** отправляется разработчику приложения.

## 7. Метаданные и fastlane

Тексты листинга: `fastlane/metadata/android/` (`en-US`, `ru-RU`).

Загрузка метаданных и AAB (нужен JSON service account из Play Console → API access):

```powershell
cd fastlane
# положите play-store-credentials.json (в .gitignore)
bundle exec fastlane upload_internal
```

Или вручную: **Release** → **Production / Internal testing** → **Create new release** → загрузить AAB.

## 8. Графика для листинга (вручную)

- Иконка 512×512 (из `mipmap` / бренд)
- Feature graphic 1024×500
- Скриншоты телефона (мин. 2), при необходимости 7″ / 10″

Положить в `fastlane/metadata/android/en-US/images/` при использовании fastlane supply.

## 9. Треки релиза

Рекомендуемый порядок: **Internal testing** → **Closed testing** → **Production**.

После первой публикации смените стратегию обновлений: можно оставить GitHub-releases для sideload и дублировать релизы в Play.