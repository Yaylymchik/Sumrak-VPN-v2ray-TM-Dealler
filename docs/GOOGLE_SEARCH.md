# Sumrak VPN в Google Search и Play

## GitHub Pages (лендинг для Google)

1. Репозиторий **Settings → Pages** → Source: **Deploy from a branch** → Branch **main** → Folder **`/docs`** → Save.
2. Через несколько минут сайт: `https://yaylymchik.github.io/Sumrak-VPN-v2ray-TM-Dealler/`
3. Файлы: `docs/index.html`, `docs/robots.txt`, `docs/sitemap.xml`.

## Статус

- **GitHub Pages:** https://yaylymchik.github.io/Sumrak-VPN-v2ray-TM-Dealler/ (включён, деплой через Actions)
- **IndexNow:** после каждого деплоя workflow шлёт URL в Bing / Yandex (см. `.github/workflows/pages.yml`)

## Google Search Console (нужен ваш Google-аккаунт)

1. [Google Search Console](https://search.google.com/search-console)
2. Добавьте ресурс (URL префикс): `https://yaylymchik.github.io/Sumrak-VPN-v2ray-TM-Dealler/`
3. Подтвердите владение (HTML-файл или meta-тег в `docs/index.html` в `<head>`):

   ```html
   <meta name="google-site-verification" content="ВАШ_КОД" />
   ```

4. **Sitemaps** → отправьте: `https://yaylymchik.github.io/Sumrak-VPN-v2ray-TM-Dealler/sitemap.xml`
5. Запросите индексирование главной страницы (URL inspection → Request indexing).

Репозиторий GitHub тоже индексируется; в README и в описании Play указаны те же ключевые слова: **Sumrak VPN**, **SumraX**, v2ray, Xray.

## Google Play (поиск в магазине)

Тексты листинга: `fastlane/metadata/android/` (ru-RU, en-US).

Загрузка метаданных без APK (или с AAB):

```powershell
cd fastlane
bundle exec fastlane upload_internal
```

Или вручную в Play Console вставьте **краткое** и **полное** описание из `full_description.txt` / `short_description.txt`.

## После релиза в Play

Добавьте ссылку на карточку приложения в `docs/index.html` и в `full_description.txt`:

`https://play.google.com/store/apps/details?id=com.sumrax.app`