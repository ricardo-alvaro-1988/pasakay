import 'package:flutter/material.dart';
import 'package:qr_flutter/qr_flutter.dart';

import 'theme.dart';

String customerQrPayload(String customerId) => 'yapasakay:customer:$customerId';

class ShowQrButton extends StatelessWidget {
  const ShowQrButton({super.key, required this.onPressed});

  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(999),
      elevation: 2,
      shadowColor: const Color(0x3316181D),
      child: InkWell(
        onTap: onPressed,
        borderRadius: BorderRadius.circular(999),
        child: const Padding(
          padding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(Icons.qr_code_scanner, color: brandRed, size: 20),
              SizedBox(width: 6),
              Text('Show QR', style: TextStyle(fontWeight: FontWeight.w800, color: brandInk)),
            ],
          ),
        ),
      ),
    );
  }
}

Future<void> showCustomerQrOverlay(BuildContext context, {required String customerId}) {
  return showPassengerDialog<void>(
    context: context,
    builder: (ctx) => Padding(
      padding: const EdgeInsets.fromLTRB(20, 22, 20, 16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('Show this to the rider', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
          const SizedBox(height: 16),
          QrImageView(
            data: customerQrPayload(customerId),
            version: QrVersions.auto,
            size: 240,
            backgroundColor: Colors.white,
            eyeStyle: const QrEyeStyle(eyeShape: QrEyeShape.square, color: brandInk),
            dataModuleStyle: const QrDataModuleStyle(
              dataModuleShape: QrDataModuleShape.square,
              color: brandInk,
            ),
          ),
          const SizedBox(height: 12),
          const Text(
            'Let the rider scan this. Then set pickup and drop-off.',
            textAlign: TextAlign.center,
            style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: FilledButton(onPressed: () => Navigator.pop(ctx), child: const Text('Close')),
          ),
        ],
      ),
    ),
  );
}
