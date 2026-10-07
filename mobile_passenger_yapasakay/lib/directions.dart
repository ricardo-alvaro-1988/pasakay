import 'dart:convert';
import 'dart:math' as math;

import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:http/http.dart' as http;

import 'models.dart';

/// Driving route via Google Directions API (same idea as web `drawDrivingRoute`).
Future<List<LatLng>> fetchDrivingRoute({
  required LatLng origin,
  required LatLng destination,
  required String apiKey,
}) async {
  if (apiKey.isEmpty) return _straight(origin, destination);
  final uri = Uri.https('maps.googleapis.com', '/maps/api/directions/json', {
    'origin': '${origin.latitude},${origin.longitude}',
    'destination': '${destination.latitude},${destination.longitude}',
    'mode': 'driving',
    'key': apiKey,
  });
  try {
    final response = await http.get(uri).timeout(const Duration(seconds: 12));
    if (response.statusCode != 200) return _straight(origin, destination);
    final decoded = jsonDecode(response.body);
    if (decoded is! Map) return _straight(origin, destination);
    if (asText(decoded['status']) != 'OK') return _straight(origin, destination);
    final routes = decoded['routes'];
    if (routes is! List || routes.isEmpty) return _straight(origin, destination);
    final route = asJsonMap(routes.first);
    final overview = asJsonMap(route?['overview_polyline']);
    final encoded = asText(overview?['points']);
    if (encoded.isEmpty) return _straight(origin, destination);
    final points = decodePolyline(encoded);
    return points.length >= 2 ? points : _straight(origin, destination);
  } catch (_) {
    return _straight(origin, destination);
  }
}

List<LatLng> _straight(LatLng a, LatLng b) => [a, b];

/// Google encoded polyline decoder.
List<LatLng> decodePolyline(String encoded) {
  final points = <LatLng>[];
  var index = 0;
  var lat = 0;
  var lng = 0;
  while (index < encoded.length) {
    var shift = 0;
    var result = 0;
    int b;
    do {
      b = encoded.codeUnitAt(index++) - 63;
      result |= (b & 0x1f) << shift;
      shift += 5;
    } while (b >= 0x20);
    final dlat = (result & 1) != 0 ? ~(result >> 1) : (result >> 1);
    lat += dlat;

    shift = 0;
    result = 0;
    do {
      b = encoded.codeUnitAt(index++) - 63;
      result |= (b & 0x1f) << shift;
      shift += 5;
    } while (b >= 0x20);
    final dlng = (result & 1) != 0 ? ~(result >> 1) : (result >> 1);
    lng += dlng;

    points.add(LatLng(lat / 1e5, lng / 1e5));
  }
  return points;
}

Future<void> fitMapToPoints(GoogleMapController? map, Iterable<LatLng> points) async {
  if (map == null) return;
  final list = points.toList();
  if (list.isEmpty) return;
  if (list.length == 1) {
    await map.animateCamera(CameraUpdate.newLatLngZoom(list.first, 15));
    return;
  }
  var minLat = list.first.latitude;
  var maxLat = list.first.latitude;
  var minLng = list.first.longitude;
  var maxLng = list.first.longitude;
  for (final p in list.skip(1)) {
    minLat = math.min(minLat, p.latitude);
    maxLat = math.max(maxLat, p.latitude);
    minLng = math.min(minLng, p.longitude);
    maxLng = math.max(maxLng, p.longitude);
  }
  await map.animateCamera(
    CameraUpdate.newLatLngBounds(
      LatLngBounds(
        southwest: LatLng(minLat, minLng),
        northeast: LatLng(maxLat, maxLng),
      ),
      72,
    ),
  );
}
