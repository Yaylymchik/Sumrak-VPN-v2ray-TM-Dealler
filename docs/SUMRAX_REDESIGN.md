# SumraX — полный редизайн

Версия спека: **2.2.18+**. Референс: тёмный UI, жёлтые акценты. Ядро: v2rayNG без смены VPN/Xray логики.

## Принципы

1. Один главный экран: подключение + список серверов.
2. Жёлтый — только CTA, выбор, акцентные обводки.
3. Короткие подписи узлов (`ServerDisplayName`), не URL подписки.
4. HWID / Proxy·VPN — в настройках, не на Home.
5. Touch targets ≥ 44dp.

## Карта экранов

| Экран | Файлы | Статус |
|-------|--------|--------|
| Home | `jumpjump_home.xml`, `MainActivity.kt`, `SumrakPowerButtonView.kt` | Редизайн v2 |
| Импорт (шторка) | `overlay_server_picker.xml` | sheet |
| Меню | `nav_sumrax_*.xml` | Glass drawer |
| Онбординг | `activity_onboarding.xml` | Стилизация |
| Настройки | `fragment_sumrax_settings_host.xml`, `pref_settings.xml` | HWID здесь |
| Legacy списки | `activity_main` ViewPager | Скрыт, через drawer при необходимости |

## Home (wireframe)

```
[≡]      SumraX      [⚙]
      (chip дней)
        ( power )
     Не подключено
        🇩🇪 Berlin
      [ Smart | Manual ]
┌─── sheet ─────────────┐
│ ручка │ Серверы │ ⟳ ⧉+ │
│ Места │ Избранное │ ... │
│  список узлов         │
└───────────────────────┘
```

## Фазы

- **Phase 1 (сделано):** Home layout, power icon, server sheet, top bar, display names, list refresh, days cap ≤999.
- **Phase 2:** Settings + все editor activity в единой теме `SumraX.*`.
- **Phase 3:** Анимации, empty states, release APK CI.

## QA (1080×2400)

- [ ] Нет верхнего legacy-toolbar на Home
- [ ] Список «Места» не пуст при наличии профилей
- [ ] Имя сервера без `workers.dev`
- [ ] Нет «8831 дн.» из `[8831]`
- [ ] Порядок иконок: пинг → обновить → импорт