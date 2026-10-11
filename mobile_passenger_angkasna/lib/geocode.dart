import 'dart:convert';

import 'package:http/http.dart' as http;

import 'models.dart';

class PlacePrediction {
  PlacePrediction({required this.placeId, required this.description, required this.mainText});

  final String placeId;
  final String description;
  final String mainText;
}

Future<List<PlacePrediction>> autocompletePlaces(
  String query,
  String apiKey, {
  double? biasLat,
  double? biasLng,
}) async {
  final trimmed = query.trim();
  if (trimmed.length < 2 || apiKey.isEmpty) return const [];
  final params = <String, String>{
    'input': trimmed,
    'key': apiKey,
    'components': 'country:ph',
    'language': 'en',
  };
  if (biasLat != null && biasLng != null) {
    params['location'] = '$biasLat,$biasLng';
    params['radius'] = '25000';
  }
  final uri = Uri.https('maps.googleapis.com', '/maps/api/place/autocomplete/json', params);
  final response = await http.get(uri).timeout(const Duration(seconds: 10));
  if (response.statusCode != 200) return const [];
  final decoded = jsonDecode(response.body);
  if (decoded is! Map) return const [];
  final predictions = decoded['predictions'];
  if (predictions is! List) return const [];
  return predictions.map((raw) {
    final row = asJsonMap(raw) ?? {};
    final structured = asJsonMap(row['structured_formatting']);
    final main = asText(structured?['main_text'], asText(row['description']));
    return PlacePrediction(
      placeId: asText(row['place_id']),
      description: asText(row['description']),
      mainText: main,
    );
  }).where((p) => p.placeId.isNotEmpty).take(8).toList();
}

Future<Stop?> placeDetails(String placeId, String apiKey) async {
  if (placeId.isEmpty || apiKey.isEmpty) return null;
  final uri = Uri.https('maps.googleapis.com', '/maps/api/place/details/json', {
    'place_id': placeId,
    'fields': 'geometry,formatted_address,name',
    'key': apiKey,
  });
  final response = await http.get(uri).timeout(const Duration(seconds: 10));
  if (response.statusCode != 200) return null;
  final decoded = jsonDecode(response.body);
  if (decoded is! Map) return null;
  final result = asJsonMap(decoded['result']);
  if (result == null) return null;
  final geometry = asJsonMap(result['geometry']);
  final location = geometry == null ? null : asJsonMap(geometry['location']);
  if (location == null) return null;
  final lat = asDouble(location['lat']);
  final lng = asDouble(location['lng']);
  final formatted = asText(result['formatted_address'], asText(result['name']));
  final label = formatted.split(',').first.trim();
  return Stop(label: label.isEmpty ? 'Selected place' : label, details: formatted, lat: lat, lng: lng);
}

Future<Stop?> geocodeAddress(
  String query,
  String apiKey, {
  double? biasLat,
  double? biasLng,
}) async {
  final trimmed = query.trim();
  if (trimmed.isEmpty || apiKey.isEmpty) return null;
  final params = <String, String>{
    'address': trimmed,
    'key': apiKey,
    'region': 'ph',
  };
  if (biasLat != null && biasLng != null) {
    params['bounds'] =
        '${biasLat - 0.5},${biasLng - 0.5}|${biasLat + 0.5},${biasLng + 0.5}';
  }
  final uri = Uri.https('maps.googleapis.com', '/maps/api/geocode/json', params);
  final response = await http.get(uri).timeout(const Duration(seconds: 10));
  if (response.statusCode != 200) return null;
  final decoded = jsonDecode(response.body);
  if (decoded is! Map) return null;
  final results = decoded['results'];
  if (results is! List || results.isEmpty) return null;
  final first = asJsonMap(results.first);
  if (first == null) return null;
  final geometry = asJsonMap(first['geometry']);
  final location = geometry == null ? null : asJsonMap(geometry['location']);
  if (location == null) return null;
  final lat = asDouble(location['lat']);
  final lng = asDouble(location['lng']);
  final formatted = asText(first['formatted_address'], trimmed);
  final label = formatted.split(',').first.trim();
  return Stop(label: label.isEmpty ? trimmed : label, details: formatted, lat: lat, lng: lng);
}

Future<Stop?> reverseGeocode(double lat, double lng, String apiKey) async {
  if (apiKey.isEmpty) return null;
  final uri = Uri.https('maps.googleapis.com', '/maps/api/geocode/json', {
    'latlng': '$lat,$lng',
    'key': apiKey,
  });
  final response = await http.get(uri).timeout(const Duration(seconds: 10));
  if (response.statusCode != 200) return null;
  final decoded = jsonDecode(response.body);
  if (decoded is! Map) return null;
  final results = decoded['results'];
  if (results is! List || results.isEmpty) return null;
  final first = asJsonMap(results.first);
  if (first == null) return null;
  final formatted = asText(first['formatted_address'], 'Current location');
  final label = formatted.split(',').first.trim();
  return Stop(
    label: label.isEmpty ? 'Current location' : label,
    details: formatted,
    lat: lat,
    lng: lng,
  );
}
