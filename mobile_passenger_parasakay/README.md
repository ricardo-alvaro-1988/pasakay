# ParaSakay Passenger

Flutter passenger app aligned with the web customer experience (Home book, Booking, Rental, Favorites, Account, SOS). **Pabili is not included.**

## Setup

- Flutter SDK (this machine: `C:\Users\Lenovo\flutter\bin\flutter.bat`)
- Package id: `com.parasakay.passenger`
- Default API: `https://parasakay.com`

### Google Maps (Android)

Set a Maps SDK key when building:

```bash
# local.properties or env
MAPS_API_KEY=your_android_maps_key
```

Or pass Gradle property: `-PMAPS_API_KEY=...`

### Google Sign-In

Uses the web OAuth client id from `GET /api/public/auth` (`googleClientId`) as `serverClientId` so the backend can verify the ID token.

Native Google Sign-In on Android also needs an **Android OAuth client** (same as Ya Pabili). Without it you get `ApiException: 10` (DEVELOPER_ERROR).

**One-time Google Cloud setup** (same project as `GoogleAuth__ClientId`):

1. Open [Google Cloud Console → Credentials](https://console.cloud.google.com/apis/credentials)
2. **Create credentials → OAuth client ID → Android**
3. Package name: `com.parasakay.passenger`
4. SHA-1 (debug / current APK signing key on this PC):

   `B2:E7:DA:9F:56:16:2D:38:20:06:73:01:48:F5:22:51:73:40:68:68`

5. Save. Wait ~5 minutes, reinstall the APK, then **Sign in with Google** works in-app like Pabili.

If SHA-1 is missing, the app falls back to browser `/mobile-auth` once (no error 10 shown).

## Run / build

```bash
flutter pub get
flutter run --dart-define=API_BASE=https://parasakay.com
flutter build apk --dart-define=API_BASE=https://parasakay.com
```

## Feature flags

- **Rental** tab appears when `GET /api/customer/services` returns `rentalEnabled: true` for the current location.
