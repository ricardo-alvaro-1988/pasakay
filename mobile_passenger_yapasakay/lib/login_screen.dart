import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_web_auth_2/flutter_web_auth_2.dart';

import 'api.dart';
import 'pin_entry.dart';
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
  bool _pinStep = false;
  String? _phone;
  String? _pinError;
  bool _pinBusy = false;
  final _phoneCtrl = TextEditingController();
  static const _callbackScheme = 'yapasakay-passenger';

  @override
  void dispose() {
    _phoneCtrl.dispose();
    super.dispose();
  }

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

  Future<void> _signInGoogle() async {
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

  void _continueToPin() {
    final phone = _phoneCtrl.text.trim();
    if (phone.replaceAll(RegExp(r'\D'), '').length < 10) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Enter a valid Philippine mobile number.')),
      );
      return;
    }
    setState(() {
      _phone = phone;
      _pinStep = true;
      _pinError = null;
    });
  }

  Future<void> _submitPin(String pin) async {
    final phone = _phone;
    if (phone == null || _pinBusy) return;
    setState(() {
      _pinBusy = true;
      _pinError = null;
    });
    try {
      await widget.session.api.customerPinLogin(phone: phone, pin: pin);
      await widget.session.restore();
    } on ApiException catch (ex) {
      if (mounted) setState(() => _pinError = ex.message);
    } catch (_) {
      if (mounted) setState(() => _pinError = 'Could not sign in with PIN.');
    } finally {
      if (mounted) setState(() => _pinBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_pinStep) {
      return PinEntryPage(
        title: 'Enter PIN',
        subtitle: 'Sign in as ${_phone ?? 'your number'}',
        onBack: _pinBusy
            ? null
            : () => setState(() {
                  _pinStep = false;
                  _pinError = null;
                }),
        busy: _pinBusy,
        error: _pinError,
        actionLabel: 'Sign in',
        autoSubmitAt: 6,
        onSubmit: _submitPin,
      );
    }

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
                    'Sign in with Google to book rides, or use your phone and PIN after you set one in Account.',
                    style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                  ),
                  if (error != null && error.isNotEmpty) ...[
                    const SizedBox(height: 12),
                    Text(error, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
                  ],
                  const SizedBox(height: 18),
                  OutlinedButton(
                    onPressed: _loading || widget.session.busy ? null : _signInGoogle,
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
                        : const Row(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              _GoogleMark(),
                              SizedBox(width: 10),
                              Text('Sign in with Google', style: TextStyle(fontWeight: FontWeight.w800)),
                            ],
                          ),
                  ),
                  const SizedBox(height: 18),
                  Row(
                    children: [
                      Expanded(child: Divider(color: brandLine)),
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 10),
                        child: Text('OR', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w800, fontSize: 12)),
                      ),
                      Expanded(child: Divider(color: brandLine)),
                    ],
                  ),
                  const SizedBox(height: 14),
                  TextField(
                    controller: _phoneCtrl,
                    keyboardType: TextInputType.phone,
                    inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[0-9+\s-]'))],
                    decoration: const InputDecoration(
                      labelText: 'Mobile number',
                      hintText: '09XX XXX XXXX',
                    ),
                  ),
                  const SizedBox(height: 12),
                  FilledButton(
                    onPressed: _loading || widget.session.busy ? null : _continueToPin,
                    child: const Text('Continue with PIN'),
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'First time? Sign in with Google, then Set PIN in Account.',
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
