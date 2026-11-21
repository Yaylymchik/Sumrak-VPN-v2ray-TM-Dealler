# Автопубликация в Google Play (GitHub Actions)

Workflow: [`.github/workflows/play-publish.yml`](../.github/workflows/play-publish.yml)

## Секреты репозитория

| Secret | Описание |
|--------|----------|
| `PLAY_STORE_JSON_BASE64` | Service account JSON из Play Console → **Users and permissions** → **Invite user** (или API access), base64 одной строкой |
| `APP_KEYSTORE_BASE64` | Release `.jks` в base64 (первый AAB можно подписать upload-ключом) |
| `APP_KEYSTORE_PASSWORD` | Пароль keystore |
| `APP_KEYSTORE_ALIAS` | Alias ключа |
| `APP_KEY_PASSWORD` | Пароль ключа |

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