# Автопубликация в Google Play (GitHub Actions)

Workflow: [`.github/workflows/play-publish.yml`](../.github/workflows/play-publish.yml)

## Секреты репозитория

| Secret | Статус | Описание |
|--------|--------|----------|
| `PLAY_STORE_JSON_BASE64` | **нужно добавить** | Service account JSON из Play Console → API access, base64 |
| `APP_KEYSTORE_BASE64` | **уже в GitHub** | Upload keystore `.jks` |
| `APP_KEYSTORE_PASSWORD` | **уже в GitHub** | Пароль keystore |
| `APP_KEYSTORE_ALIAS` | **уже в GitHub** (`sumrax`) | Alias ключа |
| `APP_KEY_PASSWORD` | **уже в GitHub** | Пароль ключа |

Локально: `scripts/setup-play-signing.ps1` и `.build-cache/play-signing.local.txt`.

PowerShell (пример):

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes("D:\path\play-store-credentials.json")) | Set-Clipboard
[Convert]::ToBase64String([IO.File]::ReadAllBytes("D:\path\sumrax-release.jks")) | Set-Clipboard
```

В GitHub: **Settings → Secrets and variables → Actions → New repository secret**.

## Первый раз в Play Console

1. Завершить регистрацию аккаунта разработчика ($25).
2. **Create app** → Sumrak VPN, `com.sumrax.app` уже в APK/AAB.
3. Заполнить обязательные разделы (контент, Data safety, политика).
4. **Setup → API access** → связать Google Cloud project, создать service account, дать права **Release manager** (или Admin).

## Запуск

- Вручную: **Actions → Publish Play Internal → Run workflow** (track `internal`).
- С тега: `git tag v2.2.22 && git push origin v2.2.22` — соберёт AAB и зальёт на `internal`.

Метаданные (ru/en) берутся из `fastlane/metadata/android/`.