# Ya! Pasakay Passenger

Flutter passenger app aligned with the web customer experience (Home book, Booking, Rental, Favorites, Account, SOS). **Pabili is not included.**

## Setup

- Flutter SDK (this machine: `C:\Users\Lenovo\flutter\bin\flutter.bat`)
- Package id: `com.yapasakay.passenger`
- Default API: `https://yapasakay.com`

### Google Maps (Android)

Set a Maps SDK key when building:

```bash
# local.properties or env
MAPS_API_KEY=your_android_maps_key
```

Or pass Gradle property: `-PMAPS_API_KEY=...`

### Google Sign-In

Uses the web OAuth client id from `GET /api/public/auth` (`googleClientId`) as `serverClientId` so the backend can verify the ID token.

## Run / build

```bash
flutter pub get
flutter run --dart-define=API_BASE=https://yapasakay.com
flutter build apk --dart-define=API_BASE=https://yapasakay.com
```

## Feature flags

- **Rental** tab appears when `GET /api/customer/services` returns `rentalEnabled: true` for the current location.
