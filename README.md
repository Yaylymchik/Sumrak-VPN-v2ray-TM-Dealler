# Sumrak VPN (SumraX)

Android VPN client based on [v2rayNG](https://github.com/2dust/v2rayNG), branded as **SumraX**.

Repository: [Yaylymchik/Sumrak-VPN-v2ray-TM-Dealler](https://github.com/Yaylymchik/Sumrak-VPN-v2ray-TM-Dealler)

## Build

1. Open the `V2rayNG` folder in Android Studio (or use the Gradle wrapper there).
2. Sync Gradle and build the `playstoreDebug` / release variant.
3. Core library: `V2rayNG/app/libs/libv2ray.aar` (rebuild from [AndroidLibXrayLite](https://github.com/2dust/AndroidLibXrayLite) if needed).

## Google Play

Package **`com.sumrax.app`**. Release bundle:

```bash
cd V2rayNG && ./gradlew :app:bundlePlaystoreRelease
```

Output: `app/build/outputs/bundle/playstoreRelease/app-playstore-release.aab`. Signing: see `V2rayNG/gradle.properties.example`.

Step-by-step (RU): [docs/PLAYSTORE.md](docs/PLAYSTORE.md). Store listing texts: `fastlane/metadata/android/`. Privacy: [PRIVACY_POLICY.md](PRIVACY_POLICY.md).

**Google Search / landing page** (enable GitHub Pages from `/docs`): [docs/index.html](docs/index.html) · [GOOGLE_SEARCH.md](docs/GOOGLE_SEARCH.md)

## App updates

In-app update checks use GitHub Releases API:

`https://api.github.com/repos/Yaylymchik/Sumrak-VPN-v2ray-TM-Dealler/releases`

Publish a Release with the APK attached (tag like `v2.2.4`) so users can update from the app.

## License

See [LICENSE](LICENSE) (upstream v2rayNG / related licenses).
