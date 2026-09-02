import 'package:signalr_netcore/signalr_client.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'api.dart';
import 'models.dart';

class DeskHubClient {
  DeskHubClient(this.api);

  final RiderApi api;
  HubConnection? _connection;

  Future<void> connect(
    void Function(String? reason) onChanged, {
    void Function(ChatMessage message)? onChat,
  }) async {
    await disconnect();
    final token = api.accessToken;
    if (token == null || token.isEmpty) {
      return;
    }

    final connection = HubConnectionBuilder()
        .withUrl(
          '${api.baseUrl}/hubs/desk',
          options: HttpConnectionOptions(
            accessTokenFactory: () async => api.accessToken ?? token,
            skipNegotiation: false,
          ),
        )
        .withAutomaticReconnect(retryDelays: [0, 1000, 2000, 5000, 10000])
        .build();

    connection.on('deskChanged', (args) {
      String? reason;
      if (args != null && args.isNotEmpty && args.first is Map) {
        reason = (args.first as Map)['reason']?.toString();
      }
      onChanged(reason);
    });

    if (onChat != null) {
      connection.on('chatMessage', (args) {
        if (args == null || args.isEmpty) {
          return;
        }
        final map = asJsonMap(args.first);
        if (map != null && map['id'] != null) {
          onChat(ChatMessage.fromJson(map));
        }
      });
    }

    try {
      await connection.start()?.timeout(const Duration(seconds: 6));
    } catch (_) {
      return;
    }
    _connection = connection;

    final prefs = await SharedPreferences.getInstance();
    var device = prefs.getString('deviceToken');
    if (device == null || device.length < 8) {
      device = 'rider-${DateTime.now().millisecondsSinceEpoch}-${token.hashCode.abs()}';
      await prefs.setString('deviceToken', device);
    }
    try {
      await api.registerDevice(device, platform: 'Android');
    } catch (_) {}
  }

  Future<void> disconnect() async {
    final connection = _connection;
    _connection = null;
    if (connection == null) {
      return;
    }
    try {
      await connection.stop();
    } catch (_) {}
  }
}
