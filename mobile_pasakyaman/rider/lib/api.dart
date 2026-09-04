import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;
import 'package:http/io_client.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'models.dart';

class ApiException implements Exception {
  ApiException(this.message);
  final String message;

  @override
  String toString() => message;
}

class RiderApi {
  RiderApi();

  static const _tokenKey = 'accessToken';
  static const _baseKey = 'apiBase';
  static const _timeout = Duration(seconds: 12);

  String baseUrl = defaultBaseUrl();
  String? accessToken;
  http.Client? _client;

  http.Client get _http {
    return _client ??= IOClient(
      HttpClient()
        ..connectionTimeout = const Duration(seconds: 8)
        ..idleTimeout = const Duration(seconds: 15)
        ..findProxy = (_) => 'DIRECT',
    );
  }

  static const productionBaseUrl = 'https://pasakyaman.com';
  static const _definedBase = String.fromEnvironment('API_BASE');

  static String defaultBaseUrl() {
    final defined = _definedBase.trim().replaceAll(RegExp(r'/$'), '');
    if (defined.isNotEmpty) {
      return defined;
    }
    return productionBaseUrl;
  }

  static bool _isDevHost(String url) {
    final host = Uri.tryParse(url)?.host.toLowerCase() ?? '';
    return host == '127.0.0.1' ||
        host == 'localhost' ||
        host == '10.0.2.2' ||
        host == '::1' ||
        host.startsWith('192.168.') ||
        host.startsWith('10.') ||
        url.contains(':5088');
  }

  Future<void> load() async {
    final prefs = await SharedPreferences.getInstance();
    accessToken = prefs.getString(_tokenKey);
    final saved = prefs.getString(_baseKey)?.trim() ?? '';
    if (saved.isEmpty || _isDevHost(saved)) {
      baseUrl = defaultBaseUrl();
      await prefs.setString(_baseKey, baseUrl);
      return;
    }
    baseUrl = saved.replaceAll(RegExp(r'/$'), '');
  }

  Future<void> saveBase(String url) async {
    baseUrl = url.trim().replaceAll(RegExp(r'/$'), '');
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_baseKey, baseUrl);
  }

  Future<void> saveToken(String token) async {
    accessToken = token;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, token);
  }

  Future<void> clear() async {
    accessToken = null;
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
  }

  String? mediaUrl(String? path) {
    final value = path?.trim() ?? '';
    if (value.isEmpty) {
      return null;
    }
    if (value.startsWith('http://') || value.startsWith('https://')) {
      return value;
    }
    return '$baseUrl$value';
  }

  Uri _uri(String path) => Uri.parse('$baseUrl$path');

  Future<http.Response> _get(Uri uri, {Map<String, String>? headers}) {
    return _http.get(uri, headers: headers).timeout(_timeout);
  }

  Future<http.Response> _post(Uri uri, {Map<String, String>? headers, Object? body}) {
    return _http.post(uri, headers: headers, body: body).timeout(_timeout);
  }

  Future<void> ping() async {
    final response = await _get(_uri('/health'), headers: _headers(auth: false, json: false));
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException('Cannot reach Pasakya Man right now.');
    }
  }

  Map<String, String> _headers({bool auth = true, bool json = true}) {
    return {
      if (json) 'Content-Type': 'application/json',
      if (auth && accessToken != null) 'Authorization': 'Bearer $accessToken',
    };
  }

  Future<Map<String, dynamic>> _json(
    http.Response response, {
    String fallback = 'Request failed.',
  }) async {
    Map<String, dynamic>? body;
    if (response.body.isNotEmpty) {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) {
        body = decoded;
      }
    }
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return body ?? {};
    }
    throw ApiException(asTextOrNull(body?['message']) ?? fallback);
  }

  Future<void> login(String phone, String password) async {
    final response = await _post(
      _uri('/api/auth/login'),
      headers: _headers(auth: false),
      body: jsonEncode({'phone': phone, 'password': password}),
    );
    final body = await _json(response, fallback: 'Could not sign in.');
    final role = (body['user'] as Map?)?['role']?.toString();
    if (role != 'Rider' && role != '3') {
      throw ApiException('This login is for riders only.');
    }
    final token = body['accessToken']?.toString();
    if (token == null || token.isEmpty) {
      throw ApiException('Login did not return a token.');
    }
    await saveToken(token);
  }

  Future<void> changePassword(String currentPassword, String newPassword) async {
    final response = await _post(
      _uri('/api/rider/password'),
      headers: _headers(),
      body: jsonEncode({
        'currentPassword': currentPassword,
        'newPassword': newPassword,
      }),
    );
    await _json(response, fallback: 'Could not change password.');
  }

  Future<RiderDesk> desk() async {
    final response = await _get(_uri('/api/rider/desk'), headers: _headers());
    final body = await _json(response, fallback: 'Could not load rider desk.');
    return RiderDesk.fromJson(body);
  }

  Future<RiderDesk> setOnline(bool online) async {
    final response = await _post(
      _uri('/api/rider/online'),
      headers: _headers(),
      body: jsonEncode({'online': online}),
    );
    return RiderDesk.fromJson(await _json(response, fallback: 'Could not update online status.'));
  }

  Future<RiderDesk> setPayments(List<String> methods) async {
    final response = await _post(
      _uri('/api/rider/payments'),
      headers: _headers(),
      body: jsonEncode({'paymentMethods': methods}),
    );
    return RiderDesk.fromJson(await _json(response, fallback: 'Could not update payments.'));
  }

  Future<RiderDesk> pingLocation(double lat, double lng) async {
    final response = await _post(
      _uri('/api/rider/location'),
      headers: _headers(),
      body: jsonEncode({'lat': lat, 'lng': lng}),
    );
    return RiderDesk.fromJson(await _json(response));
  }

  Future<RiderDesk> accept(String offerId) async {
    final response = await _post(
      _uri('/api/rider/offers/$offerId/accept'),
      headers: _headers(),
    );
    return RiderDesk.fromJson(await _json(response, fallback: 'Could not accept this job.'));
  }

  Future<RiderDesk> decline(String offerId) async {
    final response = await _post(
      _uri('/api/rider/offers/$offerId/decline'),
      headers: _headers(),
    );
    return RiderDesk.fromJson(await _json(response));
  }

  Future<RiderDesk> startTrip(String tripId) async {
    final response = await _post(
      _uri('/api/rider/trips/$tripId/start'),
      headers: _headers(),
    );
    return RiderDesk.fromJson(await _json(response, fallback: 'Could not start trip.'));
  }

  Future<RiderDesk> completeTrip(String tripId) async {
    final response = await _post(
      _uri('/api/rider/trips/$tripId/complete'),
      headers: _headers(),
    );
    return RiderDesk.fromJson(await _json(response, fallback: 'Could not complete trip.'));
  }

  Future<RiderDesk> hail(String customerId) async {
    final response = await _post(
      _uri('/api/rider/hail'),
      headers: _headers(),
      body: jsonEncode({'customerId': customerId}),
    );
    return RiderDesk.fromJson(await _json(response, fallback: 'Could not lock this customer.'));
  }

  Future<RiderDesk> cancelHail() async {
    final response = await _post(
      _uri('/api/rider/hail/cancel'),
      headers: _headers(),
    );
    return RiderDesk.fromJson(await _json(response, fallback: 'Could not clear this hail.'));
  }

  Future<List<ChatMessage>> chat(String tripId) async {
    final response = await _get(
      _uri('/api/rider/trips/$tripId/chat'),
      headers: _headers(),
    );
    if (response.statusCode < 200 || response.statusCode >= 300) {
      String message = 'Could not load chat.';
      try {
        final decoded = jsonDecode(response.body);
        if (decoded is Map && decoded['message'] is String) {
          message = decoded['message'] as String;
        }
      } catch (_) {}
      throw ApiException(message);
    }
    final decoded = response.body.isEmpty ? const [] : jsonDecode(response.body);
    final rows = decoded is List ? decoded : const [];
    return rows.map(asJsonMap).whereType<Map<String, dynamic>>().map(ChatMessage.fromJson).toList();
  }

  Future<ChatMessage> sendChat(String tripId, String text) async {
    final response = await _post(
      _uri('/api/rider/trips/$tripId/chat'),
      headers: _headers(),
      body: jsonEncode({'body': text}),
    );
    return ChatMessage.fromJson(await _json(response, fallback: 'Could not send message.'));
  }

  Future<ChatMessage> sendChatPhoto(String tripId, String filePath, {String? body}) async {
    final request = http.MultipartRequest('POST', _uri('/api/rider/trips/$tripId/chat/photo'));
    if (accessToken != null) {
      request.headers['Authorization'] = 'Bearer $accessToken';
    }
    final caption = body?.trim() ?? '';
    if (caption.isNotEmpty) {
      request.fields['body'] = caption;
    }
    request.files.add(await http.MultipartFile.fromPath('photo', filePath));
    final streamed = await _http.send(request).timeout(_timeout);
    final response = await http.Response.fromStream(streamed);
    return ChatMessage.fromJson(await _json(response, fallback: 'Could not send photo.'));
  }

  Future<void> sos(String tripId, {String? message, double? lat, double? lng}) async {
    final response = await _post(
      _uri('/api/sos'),
      headers: _headers(),
      body: jsonEncode({
        'tripId': tripId,
        'message': message,
        'lat': lat,
        'lng': lng,
      }),
    );
    await _json(response, fallback: 'Could not send SOS.');
  }

  Future<void> registerDevice(String token, {String platform = 'Android'}) async {
    final response = await _post(
      _uri('/api/devices/register'),
      headers: _headers(),
      body: jsonEncode({'token': token, 'platform': platform}),
    );
    await _json(response, fallback: 'Could not register device.');
  }

  Future<WalletSummary> wallet() async {
    final response = await _get(_uri('/api/rider/wallet'), headers: _headers());
    return WalletSummary.fromJson(await _json(response, fallback: 'Could not load wallet.'));
  }

  Future<List<RiderTripListItem>> trips() async {
    final response = await _get(_uri('/api/rider/trips'), headers: _headers());
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException('Could not load trips.');
    }
    final decoded = response.body.isEmpty ? const [] : jsonDecode(response.body);
    final rows = decoded is List ? decoded : const [];
    return rows.map(asJsonMap).whereType<Map<String, dynamic>>().map(RiderTripListItem.fromJson).toList();
  }

  Future<RiderTripDetail> tripDetail(String tripId) async {
    final response = await _get(_uri('/api/rider/trips/$tripId'), headers: _headers());
    return RiderTripDetail.fromJson(await _json(response, fallback: 'Could not load trip details.'));
  }

  Future<void> walletRequest(String kind, double amount, String paymentMethod, String? note) async {
    final response = await _post(
      _uri('/api/rider/wallet/$kind'),
      headers: _headers(),
      body: jsonEncode({
        'amount': amount,
        'paymentMethod': paymentMethod,
        'note': note,
      }),
    );
    await _json(response, fallback: 'Could not submit wallet request.');
  }
}
