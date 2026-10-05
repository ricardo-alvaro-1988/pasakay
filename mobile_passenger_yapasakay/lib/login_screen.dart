import 'package:flutter/material.dart';
import 'package:flutter_web_auth_2/flutter_web_auth_2.dart';

import 'api.dart';
import 'session.dart';
import 'theme.dart';
import 'vehicle_art.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.session});

  final CustomerSession session;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  bool _loading = false;
  static const _callbackScheme = 'yapasakay-passenger';

  Future<String> _browserTicket() async {
    final authUrl = Uri.parse('${CustomerApi.productionBaseUrl}/mobile-auth').replace(
      queryParameters: {
        'scheme': _callbackScheme,
        'package': 'com.yapasakay.passenger',
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
      throw Exception('Google sign-in did not return to the app. Tap Open Ya! Pasakay app on the browser page.');
    }
    return ticket;
  }

  Future<void> _signIn() async {
    if (_loading) return;
    setState(() => _loading = true);
    try {
      final ticket = await _browserTicket();
      await widget.session.api.redeemMobileAuthTicket(ticket);
      await widget.session.restore();
    } catch (ex) {
      if (!mounted) return;
      final message = ex.toString().replaceFirst('Exception: ', '').trim();
      if (message.toLowerCase().contains('canceled') || message.toLowerCase().contains('cancelled')) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(message.isEmpty ? 'Google sign-in failed.' : message)),
      );
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final error = widget.session.error;
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
                        'Ya! Pasakay',
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
                    'Book a ride in seconds, watch your rider on the map, and get there with Ya! Pasakay.',
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
                  OutlinedButton(
                    onPressed: _loading || widget.session.busy ? null : _signIn,
                    style: OutlinedButton.styleFrom(
                      side: const BorderSide(color: brandRed, width: 2),
                      foregroundColor: brandInk,
                      minimumSize: const Size.fromHeight(52),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                    ),
                    child: _loading || widget.session.busy
                        ? const SizedBox(
                            width: 22,
                            height: 22,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              _GoogleMark(),
                              const SizedBox(width: 10),
                              const Text('Sign in with Google', style: TextStyle(fontWeight: FontWeight.w800)),
                            ],
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
  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 20,
      height: 20,
      child: CustomPaint(painter: _GoogleGPainter()),
    );
  }
}

class _GoogleGPainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()..style = PaintingStyle.fill;
    paint.color = const Color(0xFF4285F4);
    canvas.drawCircle(Offset(size.width * 0.5, size.height * 0.5), size.width * 0.48, paint);
    paint.color = Colors.white;
    canvas.drawCircle(Offset(size.width * 0.5, size.height * 0.5), size.width * 0.28, paint);
    paint.color = const Color(0xFF4285F4);
    canvas.drawRect(Rect.fromLTWH(size.width * 0.48, size.height * 0.42, size.width * 0.4, size.height * 0.16), paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class _TrustItem extends StatelessWidget {
  const _TrustItem({required this.icon, required this.label});

  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        CircleAvatar(
          radius: 18,
          backgroundColor: brandChip,
          child: Icon(icon, size: 18, color: brandInk),
        ),
        const SizedBox(height: 6),
        Text(label, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 11)),
      ],
    );
  }
}
