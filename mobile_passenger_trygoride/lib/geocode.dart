import 'dart:convert';

import 'package:http/http.dart' as http;

import 'models.dart';

class PlacePrediction {
  PlacePrediction({required this.placeId, required this.description, required this.mainText});

  final String placeId;
  final String description;
  final String mainText;
}

Map<String, String> _mapsHeaders(String apiKey, {String? referer, String? fieldMask}) {
  final headers = <String, String>{
    'Content-Type': 'application/json',
    'X-Goog-Api-Key': apiKey,
  };
  if (fieldMask != null && fieldMask.isNotEmpty) {
    headers['X-Goog-FieldMask'] = fieldMask;
  }
  final origin = (referer ?? '').trim();
  if (origin.isNotEmpty) {
    headers['Referer'] = origin.endsWith('/') ? origin : '$origin/';
  }
  return headers;
}

Future<List<PlacePrediction>> autocompletePlaces(
  String query,
  String apiKey, {
  double? biasLat,
  double? biasLng,
  String? referer,
}) async {
  final trimmed = query.trim();
  if (trimmed.length < 2 || apiKey.isEmpty) return const [];
  final body = <String, dynamic>{
    'input': trimmed,
    'includedRegionCodes': ['ph'],
  };
  if (biasLat != null && biasLng != null) {
    body['locationBias'] = {
      'circle': {
        'center': {'latitude': biasLat, 'longitude': biasLng},
        'radius': 25000.0,
      },
    };
  }
  final uri = Uri.https('places.googleapis.com', '/v1/places:autocomplete');
  final response = await http
      .post(uri, headers: _mapsHeaders(apiKey, referer: referer), body: jsonEncode(body))
      .timeout(const Duration(seconds: 10));
  if (response.statusCode != 200) return const [];
  final decoded = jsonDecode(response.body);
  if (decoded is! Map) return const [];
  final suggestions = decoded['suggestions'];
  if (suggestions is! List) return const [];
  return suggestions.map((raw) {
    final row = asJsonMap(raw) ?? {};
    final prediction = asJsonMap(row['placePrediction']) ?? {};
    final text = asJsonMap(prediction['text']);
    final structured = asJsonMap(prediction['structuredFormat']);
    final mainObj = asJsonMap(structured?['mainText']);
    final description = asText(text?['text'], asText(prediction['placeId']));
    final main = asText(mainObj?['text'], description);
    return PlacePrediction(
      placeId: asText(prediction['placeId']),
      description: description,
      mainText: main,
    );
  }).where((p) => p.placeId.isNotEmpty).take(8).toList();
}

Future<Stop?> placeDetails(String placeId, String apiKey, {String? referer}) async {
  if (placeId.isEmpty || apiKey.isEmpty) return null;
  final id = placeId.startsWith('places/') ? placeId.substring('places/'.length) : placeId;
  final uri = Uri.https('places.googleapis.com', '/v1/places/$id');
  final response = await http
      .get(
        uri,
        headers: _mapsHeaders(
          apiKey,
          referer: referer,
          fieldMask: 'id,displayName,formattedAddress,location',
        ),
      )
      .timeout(const Duration(seconds: 10));
  if (response.statusCode != 200) return null;
  final decoded = jsonDecode(response.body);
  if (decoded is! Map) return null;
  final location = asJsonMap(decoded['location']);
  if (location == null) return null;
  final lat = asDouble(location['latitude']);
  final lng = asDouble(location['longitude']);
  final name = asJsonMap(decoded['displayName']);
  final formatted = asText(decoded['formattedAddress'], asText(name?['text']));
  final label = formatted.split(',').first.trim();
  return Stop(label: label.isEmpty ? 'Selected place' : label, details: formatted, lat: lat, lng: lng);
}

Future<Stop?> geocodeAddress(
  String query,
  String apiKey, {
  double? biasLat,
  double? biasLng,
  String? referer,
}) async {
  final hints = await autocompletePlaces(
    query,
    apiKey,
    biasLat: biasLat,
    biasLng: biasLng,
    referer: referer,
  );
  if (hints.isEmpty) return null;
  return placeDetails(hints.first.placeId, apiKey, referer: referer);
}

Future<Stop?> reverseGeocode(double lat, double lng, String apiKey, {String? referer}) async {
  if (apiKey.isEmpty) return null;
  final uri = Uri.https('places.googleapis.com', '/v1/places:searchNearby');
  final body = jsonEncode({
    'maxResultCount': 3,
    'locationRestriction': {
      'circle': {
        'center': {'latitude': lat, 'longitude': lng},
        'radius': 80.0,
      },
    },
  });
  final response = await http
      .post(
        uri,
        headers: _mapsHeaders(
          apiKey,
          referer: referer,
          fieldMask: 'places.formattedAddress,places.displayName',
        ),
        body: body,
      )
      .timeout(const Duration(seconds: 10));
  if (response.statusCode != 200) return null;
  final decoded = jsonDecode(response.body);
  if (decoded is! Map) return null;
  final places = decoded['places'];
  if (places is! List || places.isEmpty) return null;
  final first = asJsonMap(places.first);
  if (first == null) return null;
  final name = asJsonMap(first['displayName']);
  final formatted = asText(first['formattedAddress'], asText(name?['text'], 'Current location'));
  final label = formatted.split(',').first.trim();
  return Stop(
    label: label.isEmpty ? 'Current location' : label,
    details: formatted,
    lat: lat,
    lng: lng,
  );
}
