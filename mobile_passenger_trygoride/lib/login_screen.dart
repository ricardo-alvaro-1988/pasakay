import 'package:flutter/material.dart';
import 'package:flutter_web_auth_2/flutter_web_auth_2.dart';
import 'package:google_sign_in/google_sign_in.dart';

import 'api.dart';
import 'models.dart';
import 'session.dart';
import 'theme.dart';
import 'vehicle_art.dart';

/// Same pattern as Ya Pabili / TryGoRide passenger:
/// 1) Native Google Sign-In (needs Android OAuth client + SHA-1 in Google Cloud)
/// 2) Only if that returns developer error 10 → browser OAuth once
class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.session});

  final CustomerSession session;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  bool _loading = false;
  String? _localError;
  GoogleSignIn? _google;
  static const _callbackScheme = 'trygoride-passenger';

  CustomerApi get _api => widget.session.api;

  Future<String> _ensureGoogleClientId() async {
    var clientId = widget.session.googleClientId;
    if (clientId == null || clientId.isEmpty) {
      final auth = await _api.authConfig();
      clientId = asTextOrNull(auth['googleClientId']);
      widget.session.googleClientId = clientId;
    }
    if (clientId == null || clientId.isEmpty) {
      throw Exception('Google sign-in is not configured on the server.');
    }
    return clientId;
  }

  GoogleSignIn _googleClient(String clientId) {
    return _google ??= GoogleSignIn(
      scopes: const ['email', 'profile'],
      serverClientId: clientId,
    );
  }

  bool _isCancel(Object ex) {
    final message = ex.toString().toLowerCase();
    return message.contains('canceled') ||
        message.contains('cancelled') ||
        message.contains('sign_in_canceled') ||
        message.contains('sign_in_cancelled') ||
        message.contains('12501');
  }

  /// ApiException: 10 = DEVELOPER_ERROR (SHA-1 / Android OAuth client missing).
  bool _needsBrowserFallback(Object ex) {
    final message = ex.toString().toLowerCase();
    if (message.contains('developer_error')) return true;
    if (message.contains('q1: 10')) return true;
    if (message.contains('sign_in_failed') && message.contains('10')) return true;
    return false;
  }

  Future<void> _finishWithIdToken(String idToken) async {
    await widget.session.googleLogin(idToken);
    if (!widget.session.loggedIn) {
      throw ApiException(widget.session.error ?? 'Could not sign in with Google.');
    }
  }

  Future<GoogleSignInAccount?> _interactiveAccount(GoogleSignIn google) async {
    var account = await google.signIn();
    if (account != null) return account;
    // Common Android quirk: first picker select returns null.
    try {
      await google.signOut();
    } catch (_) {}
    return google.signIn();
  }

  Future<String?> _idTokenFor(GoogleSignIn google, GoogleSignInAccount account) async {
    var idToken = (await account.authentication).idToken;
    if (idToken != null && idToken.isNotEmpty) return idToken;
    try {
      await google.disconnect();
    } catch (_) {
      try {
        await google.signOut();
      } catch (_) {}
    }
    final again = await google.signIn();
    if (again == null) return null;
    idToken = (await again.authentication).idToken;
    if (idToken != null && idToken.isNotEmpty) return idToken;
    return null;
  }

  Future<void> _browserSignIn() async {
    final base = _api.baseUrl.trim().replaceAll(RegExp(r'/$'), '');
    final authUrl = Uri.parse('$base/mobile-auth').replace(
      queryParameters: {
        'scheme': _callbackScheme,
        'package': 'com.trygoride.passenger',
      },
    );
    final result = await FlutterWebAuth2.authenticate(
      url: authUrl.toString(),
      callbackUrlScheme: _callbackScheme,
    );
    final returned = Uri.parse(result);
    var ticket = returned.queryParameters['ticket']?.trim() ?? '';
    if (ticket.isEmpty && returned.fragment.isNotEmpty) {
      ticket = Uri.splitQueryString(returned.fragment)['ticket']?.trim() ?? '';
    }
    if (ticket.isEmpty) {
      throw Exception(
        'Sign-in did not return to the app. On the browser page tap “Open TryGoRide app”.',
      );
    }
    await _api.redeemMobileAuthTicket(ticket);
    await widget.session.restore();
    if (!widget.session.loggedIn) {
      throw ApiException(widget.session.error ?? 'Could not finish Google sign-in.');
    }
  }

  Future<void> _signIn() async {
    if (_loading) return;
    setState(() {
      _loading = true;
      _localError = null;
      widget.session.error = null;
    });
    try {
      final clientId = await _ensureGoogleClientId();
      final google = _googleClient(clientId);

      // --- Native first (retry once on null account / missing id token) ---
      try {
        final account = await _interactiveAccount(google);
        if (account == null) return; // user cancelled
        final idToken = await _idTokenFor(google, account);
        if (idToken == null || idToken.isEmpty) {
          throw Exception('Google sign-in failed. Try again.');
        }
        await _finishWithIdToken(idToken);
        return;
      } catch (ex) {
        if (_isCancel(ex)) return;
        if (ex is ApiException) rethrow;
        if (!_needsBrowserFallback(ex)) {
          throw Exception('Google sign-in failed. Try again.');
        }
        // SHA-1 / Android OAuth client missing → browser once.
      }

      await _browserSignIn();
    } on ApiException catch (ex) {
      if (!mounted) return;
      setState(() => _localError = ex.message);
    } catch (ex) {
      if (!mounted) return;
      if (_isCancel(ex)) return;
      final message = ex.toString().replaceFirst('Exception: ', '').trim();
      final friendly = message.contains('PlatformException') || message.contains('sign_in_failed')
          ? 'Google sign-in failed. Try again.'
          : (message.isEmpty ? 'Google sign-in failed. Try again.' : message);
      setState(() => _localError = friendly);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final error = _localError ?? widget.session.error;
    return Scaffold(
      backgroundColor: brandCanvas,
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            Container(
              padding: const EdgeInsets.fromLTRB(18, 20, 18, 22),
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(28),
                gradient: const LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [brandRed, Color(0xFF7A030C)],
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        width: 48,
                        height: 48,
                        padding: const EdgeInsets.all(3),
                        decoration: const BoxDecoration(color: Colors.white, shape: BoxShape.circle),
                        child: ClipOval(
                          child: Image.asset('assets/logo-circle.png', fit: BoxFit.cover),
                        ),
                      ),
                      const SizedBox(width: 10),
                      const Text(
                        'TryGoRide',
                        style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 22),
                      ),
                    ],
                  ),
                  const SizedBox(height: 18),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                    decoration: BoxDecoration(
                      color: Colors.black.withValues(alpha: 0.22),
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: const Text(
                      'TRICYCLE  ·  SEDAN  ·  MORE',
                      style: TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 12),
                    ),
                  ),
                  const SizedBox(height: 16),
                  const Text(
                    'Go where you need to go.',
                    style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 28, height: 1.15),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Book a ride in seconds, watch your rider on the map, and get there with TryGoRide.',
                    style: TextStyle(color: Colors.white.withValues(alpha: 0.9), fontWeight: FontWeight.w600, height: 1.35),
                  ),
                  const SizedBox(height: 16),
                  Row(
                    children: [
                      Expanded(child: vehicleArtImage('Tricycle', height: 56)),
                      Expanded(child: vehicleArtImage('Motorcycle', height: 56)),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),
            BrandPanel(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text('Welcome back', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800)),
                  const SizedBox(height: 6),
                  const Text(
                    'Sign in with Google to book rides and track your driver live.',
                    style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                  ),
                  if (error != null && error.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    Text(error, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
                  ],
                  const SizedBox(height: 18),
                  SizedBox(
                    width: double.infinity,
                    child: OutlinedButton(
                      onPressed: _loading || widget.session.busy ? null : _signIn,
                      style: OutlinedButton.styleFrom(
                        side: const BorderSide(color: brandRed, width: 2),
                        foregroundColor: brandInk,
                        minimumSize: const Size(64, 52),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                      ),
                      child: _loading || widget.session.busy
                          ? const SizedBox(
                              width: 22,
                              height: 22,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Row(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                _GoogleMark(),
                                SizedBox(width: 10),
                                Text('Sign in with Google', style: TextStyle(fontWeight: FontWeight.w800)),
                              ],
                            ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'Your account stays on this device until you sign out.',
                    textAlign: TextAlign.center,
                    style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 14),
            BrandPanel(
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceAround,
                children: const [
                  _TrustItem(icon: Icons.payments_outlined, label: 'Cash'),
                  _TrustItem(icon: Icons.account_balance_wallet_outlined, label: 'GCash'),
                  _TrustItem(icon: Icons.credit_card, label: 'Maya'),
                  _TrustItem(icon: Icons.my_location, label: 'Live Tracking'),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _GoogleMark extends StatelessWidget {
  const _GoogleMark();

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 22,
      height: 22,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(11),
        border: Border.all(color: brandLine),
      ),
      child: const Text(
        'G',
        style: TextStyle(
          color: Color(0xFF4285F4),
          fontWeight: FontWeight.w900,
          fontSize: 13,
          height: 1,
        ),
      ),
    );
  }
}

class _TrustItem extends StatelessWidget {
  const _TrustItem({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Icon(icon, color: brandRed, size: 22),
        const SizedBox(height: 4),
        Text(label, style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w700, color: brandMuted)),
      ],
    );
  }
}
