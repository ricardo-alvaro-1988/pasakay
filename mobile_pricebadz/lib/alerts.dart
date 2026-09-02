import 'package:flutter/services.dart';

class RiderAlerts {
  static const _channel = MethodChannel('pricebadz.rider/alerts');
  static bool _batteryAsked = false;

  static Future<bool> startOnline() async {
    try {
      return await _channel.invokeMethod<bool>('startOnline') == true;
    } catch (_) {
      return false;
    }
  }

  static Future<void> stopOnline() async {
    try {
      await _channel.invokeMethod('stopOnline');
    } catch (_) {}
  }

  static Future<bool> ringOffer({required String title, required String body}) async {
    try {
      return await _channel.invokeMethod<bool>('ringOffer', {'title': title, 'body': body}) == true;
    } catch (_) {
      return false;
    }
  }

  static Future<void> stopRing() async {
    try {
      await _channel.invokeMethod('stopRing');
    } catch (_) {}
  }

  static Future<bool> pingChat({required String title, required String body}) async {
    try {
      return await _channel.invokeMethod<bool>('pingChat', {'title': title, 'body': body}) == true;
    } catch (_) {
      return false;
    }
  }

  static Future<void> prepare(bool askBattery) async {
    try {
      await _channel.invokeMethod('requestNotify');
    } catch (_) {}
    if (askBattery && !_batteryAsked) {
      _batteryAsked = true;
      Future<void>.delayed(const Duration(seconds: 2), () async {
        try {
          await _channel.invokeMethod('requestBattery');
        } catch (_) {}
      });
    }
  }
}
