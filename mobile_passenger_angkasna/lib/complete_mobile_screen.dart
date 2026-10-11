import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'api.dart';
import 'session.dart';
import 'theme.dart';

class CompleteMobileScreen extends StatefulWidget {
  const CompleteMobileScreen({super.key, required this.session});

  final CustomerSession session;

  @override
  State<CompleteMobileScreen> createState() => _CompleteMobileScreenState();
}

class _CompleteMobileScreenState extends State<CompleteMobileScreen> {
  final _phone = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _phone.dispose();
    super.dispose();
  }

  String _normalizePhone(String raw) {
    var digits = raw.replaceAll(RegExp(r'\D'), '');
    if (digits.startsWith('63') && digits.length >= 12) {
      digits = '0${digits.substring(2)}';
    }
    if (digits.startsWith('9') && digits.length == 10) {
      digits = '0$digits';
    }
    return digits;
  }

  Future<void> _submit() async {
    final normalized = _normalizePhone(_phone.text);
    if (!RegExp(r'^09\d{9}$').hasMatch(normalized)) {
      setState(() => _error = 'Enter a valid PH mobile number (09XXXXXXXXX).');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final desk = await widget.session.api.updateMobile(normalized);
      widget.session.updateDesk(desk);
      if (desk.needsMobile != true) {
        await widget.session.restore();
      }
    } on ApiException catch (ex) {
      setState(() => _error = ex.message);
    } catch (_) {
      setState(() => _error = 'Could not save mobile number.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Mobile number')),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: BrandPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text(
                  'We need your mobile number so riders can reach you.',
                  style: TextStyle(fontWeight: FontWeight.w600, color: brandMuted),
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: _phone,
                  keyboardType: TextInputType.phone,
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[\d+\s-]'))],
                  decoration: const InputDecoration(labelText: 'PH mobile', hintText: '09XX XXX XXXX'),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 8),
                  Text(_error!, style: const TextStyle(color: brandSos)),
                ],
                const SizedBox(height: 20),
                FilledButton(
                  onPressed: _busy ? null : _submit,
                  child: _busy
                      ? const SizedBox(
                          width: 22,
                          height: 22,
                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                        )
                      : const Text('Continue'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
