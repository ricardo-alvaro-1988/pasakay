import 'package:geolocator/geolocator.dart';

import 'api.dart';

Future<(double? lat, double? lng)> sosLastKnown() async {
  try {
    final last = await Geolocator.getLastKnownPosition();
    if (last == null) return (null, null);
    return (last.latitude, last.longitude);
  } catch (_) {
    return (null, null);
  }
}

Future<void> refineSosLocation(RiderApi api, String tripId) async {
  try {
    final position = await Geolocator.getCurrentPosition(
      locationSettings: const LocationSettings(
        accuracy: LocationAccuracy.high,
        timeLimit: Duration(seconds: 8),
      ),
    );
    await api.sos(tripId, message: 'Rider SOS', lat: position.latitude, lng: position.longitude);
  } catch (_) {}
}
