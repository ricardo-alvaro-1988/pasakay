import 'dart:convert';

import 'package:flutter/services.dart';

class PassengerAlerts {
  static const _channel = MethodChannel('yapasakay.passenger/alerts');

  static Future<bool> syncPickupAlarms(List<Map<String, dynamic>> items) async {
    try {
      return await _channel.invokeMethod<bool>('syncPickupAlarms', jsonEncode(items)) == true;
    } catch (_) {
      return false;
    }
  }

  static Future<void> pingNotice({required String title, required String body}) async {
    try {
      await _channel.invokeMethod('pingNotice', {'title': title, 'body': body});
    } catch (_) {}
  }

  static Future<void> requestNotify() async {
    try {
      await _channel.invokeMethod('requestNotify');
    } catch (_) {}
  }
}
