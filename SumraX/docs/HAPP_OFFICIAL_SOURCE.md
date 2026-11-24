# Happ — где «исходник»

Официально **Happ не выкладывает исходный код приложения** (ни Qt для PC, ни Kotlin для Android).

| Репозиторий | URL | Что внутри |
|-------------|-----|------------|
| Desktop | https://github.com/Happ-proxy/happ-desktop | Только `README.md` + `release` (ссылки на `.exe`/`.dmg`/`.deb`) |
| Android | https://github.com/Happ-proxy/happ-android | Только `README.md` + `release` (ссылки на `Happ.apk`) |
| iOS | https://github.com/Happ-proxy/happ-ios | Пустой, без сборок |

Локальные зеркала (скачать метаданные релизов):

```powershell
git clone --depth 1 https://github.com/Happ-proxy/happ-desktop.git tools\happ-desktop-mirror
git clone --depth 1 https://github.com/Happ-proxy/happ-android.git tools\happ-android-src
```

Бинарники: вкладка **Releases** у тех же репозиториев (например `setup-Happ.x64.exe`, `Happ.apk`).

## Что использовать у вас вместо исходника Happ

| Платформа | Ваш открытый код |
|-----------|------------------|
| **Windows PC** | `SumraX/` — WPF, имя **Sumrak VPN** |
| **Android** | `V2rayNG/` — форк с Sumrak UI |

Получить закрытый Qt/QML Happ с GitHub **нельзя** — только у разработчиков Happ (happ.su) или реверс бинарника (отдельная тема).