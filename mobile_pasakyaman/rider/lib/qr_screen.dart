import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import 'session.dart';
import 'theme.dart';

final _tagged = RegExp(
  r'^(?:yapasakay|pasakyaman):customer:([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$',
  caseSensitive: false,
);
final _bare = RegExp(
  r'^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$',
  caseSensitive: false,
);

String? parseCustomerHail(String raw) {
  final text = raw.trim();
  return _tagged.firstMatch(text)?.group(1) ?? (_bare.hasMatch(text) ? text : null);
}

class QrScreen extends StatefulWidget {
  const QrScreen({super.key, required this.session});

  final RiderSession session;

  @override
  State<QrScreen> createState() => _QrScreenState();
}

class _QrScreenState extends State<QrScreen> {
  bool _busy = false;
  String? _hint;

  Future<void> _lock(String customerId) async {
    try {
      await widget.session.hail(customerId);
      if (!mounted) {
        return;
      }
      Navigator.pop(context);
    } catch (ex) {
      if (mounted) {
        setState(() {
          _busy = false;
          _hint = '$ex';
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Scan customer QR')),
      body: Column(
        children: [
          Expanded(
            child: Stack(
              fit: StackFit.expand,
              children: [
                MobileScanner(
                  onDetect: (capture) {
                    if (_busy || capture.barcodes.isEmpty) {
                      return;
                    }
                    final raw = capture.barcodes.first.rawValue;
                    if (raw == null) {
                      return;
                    }
                    final customerId = parseCustomerHail(raw);
                    if (customerId == null) {
                      setState(() => _hint = 'That QR is not a Pasakya Man customer.');
                      return;
                    }
                    setState(() => _busy = true);
                    _lock(customerId);
                  },
                ),
                IgnorePointer(
                  child: Center(
                    child: Container(
                      width: 240,
                      height: 240,
                      decoration: BoxDecoration(
                        border: Border.all(color: Colors.white, width: 3),
                        borderRadius: BorderRadius.circular(24),
                      ),
                    ),
                  ),
                ),
                if (_busy)
                  const ColoredBox(
                    color: Color(0x88000000),
                    child: Center(child: CircularProgressIndicator(color: Colors.white)),
                  ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
            child: Text(
              _hint ?? 'Point your camera at the customer QR.',
              textAlign: TextAlign.center,
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: _hint == null ? brandMuted : brandRed,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
