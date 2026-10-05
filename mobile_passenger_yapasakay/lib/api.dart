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

class CustomerApi {
  CustomerApi();

  static const _tokenKey = 'yapasakay-customer-access';
  static const _refreshKey = 'yapasakay-customer-refresh';
  static const _baseKey = 'apiBase';
  static const _timeout = Duration(seconds: 12);

  String baseUrl = defaultBaseUrl();
  String? accessToken;
  String? refreshToken;
  http.Client? _client;
  Future<bool>? _refreshInFlight;

  http.Client get _http {
    return _client ??= IOClient(
      HttpClient()
        ..connectionTimeout = const Duration(seconds: 8)
        ..idleTimeout = const Duration(seconds: 15)
        ..findProxy = (_) => 'DIRECT',
    );
  }

  static const productionBaseUrl = 'https://yapasakay.com';
  static const _definedBase = String.fromEnvironment('API_BASE');

  static String defaultBaseUrl() {
    final defined = _definedBase.trim().replaceAll(RegExp(r'/$'), '');
    if (defined.isNotEmpty) return defined;
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
    refreshToken = prefs.getString(_refreshKey);
    final saved = prefs.getString(_baseKey)?.trim() ?? '';
    if (saved.isEmpty || _isDevHost(saved)) {
      baseUrl = defaultBaseUrl();
      await prefs.setString(_baseKey, baseUrl);
      return;
    }
    baseUrl = saved.replaceAll(RegExp(r'/$'), '');
  }

  Future<void> saveAuth(AuthResponse auth) async {
    accessToken = auth.accessToken;
    refreshToken = auth.refreshToken;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, auth.accessToken);
    await prefs.setString(_refreshKey, auth.refreshToken);
  }

  Future<void> clear() async {
    accessToken = null;
    refreshToken = null;
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_tokenKey);
    await prefs.remove(_refreshKey);
  }

  String? mediaUrl(String? path) {
    final value = path?.trim() ?? '';
    if (value.isEmpty) return null;
    if (value.startsWith('http://') || value.startsWith('https://')) return value;
    if (value.startsWith('/')) return '$baseUrl$value';
    return '$baseUrl/uploads/$value';
  }

  Uri _uri(String path) => Uri.parse('$baseUrl$path');

  Map<String, String> _headers({bool auth = true, bool json = true}) {
    return {
      if (json) 'Content-Type': 'application/json',
      if (auth && accessToken != null) 'Authorization': 'Bearer $accessToken',
    };
  }

  Future<Map<String, dynamic>> _decodeJson(http.Response response, {String fallback = 'Request failed.'}) async {
    Map<String, dynamic>? body;
    if (response.body.isNotEmpty) {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) body = decoded;
    }
    if (response.statusCode >= 200 && response.statusCode < 300) {
      return body ?? {};
    }
    throw ApiException(asTextOrNull(body?['message']) ?? asTextOrNull(body?['detail']) ?? fallback);
  }

  bool _shouldAttemptRefresh(String path) {
    return !path.startsWith('/api/auth/refresh') &&
        !path.startsWith('/api/auth/google') &&
        !path.startsWith('/api/auth/login') &&
        !path.startsWith('/api/auth/customer-pin-login');
  }

  Future<bool> _refreshSession() async {
    if (_refreshInFlight != null) return _refreshInFlight!;
    final token = refreshToken;
    if (token == null || token.isEmpty) return false;
    _refreshInFlight = () async {
      try {
        final response = await _http
            .post(
              _uri('/api/auth/refresh'),
              headers: _headers(auth: false),
              body: jsonEncode({'refreshToken': token}),
            )
            .timeout(_timeout);
        if (response.statusCode < 200 || response.statusCode >= 300) {
          await clear();
          return false;
        }
        final body = await _decodeJson(response, fallback: 'Could not refresh session.');
        final auth = AuthResponse.fromJson(body);
        if (auth.accessToken.isEmpty || auth.refreshToken.isEmpty) {
          await clear();
          return false;
        }
        await saveAuth(auth);
        return true;
      } catch (_) {
        return false;
      } finally {
        _refreshInFlight = null;
      }
    }();
    return _refreshInFlight!;
  }

  Future<http.Response> _send(
    Future<http.Response> Function() call,
    String path, {
    bool retried = false,
    bool auth = true,
  }) async {
    final response = await call().timeout(_timeout);
    final isSignIn = path.startsWith('/api/auth/google') ||
        path.startsWith('/api/auth/login') ||
        path.startsWith('/api/auth/customer-pin-login') ||
        path.startsWith('/api/auth/verify-otp') ||
        path.startsWith('/api/auth/request-otp');
    if (response.statusCode == 401 && !isSignIn) {
      if (!retried && _shouldAttemptRefresh(path) && await _refreshSession()) {
        return _send(call, path, retried: true, auth: auth);
      }
      await clear();
      throw ApiException('Session expired. Sign in again.');
    }
    return response;
  }

  Future<Map<String, dynamic>> _request(
    String method,
    String path, {
    Object? body,
    bool auth = true,
  }) async {
    Future<http.Response> call() {
      final uri = _uri(path);
      final headers = _headers(auth: auth);
      switch (method) {
        case 'GET':
          return _http.get(uri, headers: headers);
        case 'POST':
          return _http.post(uri, headers: headers, body: body);
        case 'PUT':
          return _http.put(uri, headers: headers, body: body);
        case 'DELETE':
          return _http.delete(uri, headers: headers);
        default:
          throw ApiException('Unsupported method.');
      }
    }

    final response = await _send(call, path, auth: auth);
    if (response.statusCode == 204) return {};
    return _decodeJson(response);
  }

  Future<List<T>> _requestList<T>(String path, T Function(Map<String, dynamic>) fromJson) async {
    Future<http.Response> call() => _http.get(_uri(path), headers: _headers());
    final response = await _send(call, path);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw ApiException('Request failed.');
    }
    final decoded = response.body.isEmpty ? const [] : jsonDecode(response.body);
    if (decoded is! List) return const [];
    return decoded.map(asJsonMap).whereType<Map<String, dynamic>>().map(fromJson).toList();
  }

  Future<Map<String, dynamic>> authConfig() async {
    return _request('GET', '/api/public/auth', auth: false);
  }

  Future<Branding> branding() async {
    return Branding.fromJson(await _request('GET', '/api/public/branding', auth: false));
  }

  Future<Map<String, dynamic>> mapsConfig() async {
    return _request('GET', '/api/public/maps', auth: false);
  }

  Future<AuthResponse> googleSignIn(String idToken) async {
    final body = await _request(
      'POST',
      '/api/auth/google',
      auth: false,
      body: jsonEncode({'idToken': idToken}),
    );
    final auth = AuthResponse.fromJson(body);
    await saveAuth(auth);
    return auth;
  }

  Future<AuthResponse> redeemMobileAuthTicket(String ticket) async {
    final body = await _request(
      'POST',
      '/api/auth/google/mobile-ticket/redeem',
      auth: false,
      body: jsonEncode({'ticket': ticket}),
    );
    final auth = AuthResponse.fromJson(body);
    await saveAuth(auth);
    return auth;
  }

  Future<AuthResponse> customerPinLogin({required String phone, required String pin}) async {
    final body = await _request(
      'POST',
      '/api/auth/customer-pin-login',
      auth: false,
      body: jsonEncode({'phone': phone, 'pin': pin}),
    );
    final auth = AuthResponse.fromJson(body);
    await saveAuth(auth);
    return auth;
  }

  Future<Desk> desk() async {
    return Desk.fromJson(await _request('GET', '/api/customer/desk'));
  }

  Future<CustomerServices> customerServices({double? lat, double? lng, String? barangayId}) async {
    final params = <String, String>{};
    if (lat != null) params['lat'] = lat.toString();
    if (lng != null) params['lng'] = lng.toString();
    if (barangayId != null && barangayId.isNotEmpty) params['barangayId'] = barangayId;
    final q = params.isEmpty ? '' : '?${Uri(queryParameters: params).query}';
    return CustomerServices.fromJson(await _request('GET', '/api/customer/services$q'));
  }

  Future<Quote> quote(BookBody body) async {
    return Quote.fromJson(await _request('POST', '/api/customer/quote', body: jsonEncode(body.toJson())));
  }

  Future<Desk> book(BookBody body) async {
    return Desk.fromJson(await _request('POST', '/api/customer/book', body: jsonEncode(body.toJson())));
  }

  Future<Desk> cancel(String tripId) async {
    return Desk.fromJson(await _request('POST', '/api/customer/trips/$tripId/cancel'));
  }

  Future<CustomerTripDetail> tripDetail(String tripId) async {
    return CustomerTripDetail.fromJson(await _request('GET', '/api/customer/trips/$tripId'));
  }

  Future<Desk> rate(String tripId, int rating, {String? comment}) async {
    return Desk.fromJson(await _request(
      'POST',
      '/api/customer/trips/$tripId/rate',
      body: jsonEncode({'rating': rating, if (comment != null && comment.isNotEmpty) 'comment': comment}),
    ));
  }

  Future<List<FavoriteRider>> favorites() async {
    return _requestList('/api/customer/favorites', FavoriteRider.fromJson);
  }

  Future<FavoriteRider> addFavorite(String riderId) async {
    return FavoriteRider.fromJson(await _request('POST', '/api/customer/favorites/$riderId'));
  }

  Future<void> removeFavorite(String riderId) async {
    await _request('DELETE', '/api/customer/favorites/$riderId');
  }

  Future<Desk> clearHail() async {
    return Desk.fromJson(await _request('POST', '/api/customer/hail/clear'));
  }

  Future<List<HailRider>> availableRiders({
    required String vehicleType,
    required String paymentMethod,
    required double pickupLat,
    required double pickupLng,
    required String pickupDetails,
    String? pickupBarangayId,
    String? vehicleCategoryId,
  }) async {
    final params = {
      'vehicleType': vehicleType,
      'paymentMethod': paymentMethod,
      'pickupLat': pickupLat.toString(),
      'pickupLng': pickupLng.toString(),
      'pickupDetails': pickupDetails,
    };
    if (pickupBarangayId != null && pickupBarangayId.isNotEmpty) {
      params['pickupBarangayId'] = pickupBarangayId;
    }
    if (vehicleCategoryId != null && vehicleCategoryId.isNotEmpty) {
      params['vehicleCategoryId'] = vehicleCategoryId;
    }
    final q = Uri(queryParameters: params).query;
    return _requestList('/api/customer/riders/available?$q', HailRider.fromJson);
  }

  Future<ServiceCheckResult> serviceCheck({
    String? pickupBarangayId,
    required String pickupDetails,
    required double pickupLat,
    required double pickupLng,
    String? dropoffBarangayId,
    String? dropoffDetails,
  }) async {
    return ServiceCheckResult.fromJson(await _request(
      'POST',
      '/api/customer/service-check',
      body: jsonEncode({
        if (pickupBarangayId != null && pickupBarangayId.isNotEmpty) 'pickupBarangayId': pickupBarangayId,
        'pickupDetails': pickupDetails,
        'pickupLat': pickupLat,
        'pickupLng': pickupLng,
        if (dropoffBarangayId != null && dropoffBarangayId.isNotEmpty) 'dropoffBarangayId': dropoffBarangayId,
        if (dropoffDetails != null && dropoffDetails.isNotEmpty) 'dropoffDetails': dropoffDetails,
      }),
    ));
  }

  Future<Desk> updateProfile({
    required String firstName,
    required String lastName,
    required String gender,
    required String email,
  }) async {
    return Desk.fromJson(await _request(
      'PUT',
      '/api/customer/account/profile',
      body: jsonEncode({'firstName': firstName, 'lastName': lastName, 'gender': gender, 'email': email}),
    ));
  }

  Future<Desk> setPin(String pin, {String? currentPin}) async {
    return Desk.fromJson(await _request(
      'POST',
      '/api/customer/account/pin',
      body: jsonEncode({'pin': pin, if (currentPin != null && currentPin.isNotEmpty) 'currentPin': currentPin}),
    ));
  }

  Future<Desk> uploadProfilePhoto(String filePath) async {
    final request = http.MultipartRequest('POST', _uri('/api/customer/account/photo'));
    if (accessToken != null) {
      request.headers['Authorization'] = 'Bearer $accessToken';
    }
    request.files.add(await http.MultipartFile.fromPath('photo', filePath));
    final streamed = await _http.send(request).timeout(_timeout);
    final response = await http.Response.fromStream(streamed);
    return Desk.fromJson(await _decodeJson(response, fallback: 'Could not upload photo.'));
  }

  Future<Desk> updateMobile(String newPhone) async {
    return Desk.fromJson(await _request(
      'PUT',
      '/api/customer/account/mobile',
      body: jsonEncode({'newPhone': newPhone}),
    ));
  }

  Future<Desk> deleteAccount(String reason, {String? pin}) async {
    return Desk.fromJson(await _request(
      'POST',
      '/api/customer/account/delete',
      body: jsonEncode({'reason': reason, if (pin != null && pin.isNotEmpty) 'pin': pin}),
    ));
  }

  Future<void> registerDevice(String token, {String platform = 'Android'}) async {
    await _request(
      'POST',
      '/api/devices/register',
      body: jsonEncode({'token': token, 'platform': platform}),
    );
  }

  Future<void> sos(String tripId, {double? lat, double? lng}) async {
    await _request(
      'POST',
      '/api/sos',
      body: jsonEncode({'tripId': tripId, 'message': 'Customer SOS', 'lat': lat, 'lng': lng}),
    );
  }

  Future<List<ChatMessage>> chat(String tripId) async {
    return _requestList('/api/customer/trips/$tripId/chat', ChatMessage.fromJson);
  }

  Future<ChatMessage> sendChat(String tripId, String body) async {
    return ChatMessage.fromJson(await _request(
      'POST',
      '/api/customer/trips/$tripId/chat',
      body: jsonEncode({'body': body}),
    ));
  }
}
