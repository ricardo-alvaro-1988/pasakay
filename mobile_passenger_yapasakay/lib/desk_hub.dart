import 'package:shared_preferences/shared_preferences.dart';
import 'package:signalr_netcore/signalr_client.dart';

import 'api.dart';
import 'models.dart';

class DeskHubClient {
  DeskHubClient(this.api);

  final CustomerApi api;
  HubConnection? _connection;
  void Function(bool live)? onLiveChanged;

  bool get isLive => _connection?.state == HubConnectionState.Connected;

  Future<void> connect(
    void Function(String? reason) onChanged, {
    void Function(ChatMessage message)? onChat,
  }) async {
    await disconnect();
    final token = api.accessToken;
    if (token == null || token.isEmpty) {
      onLiveChanged?.call(false);
      return;
    }

    final connection = HubConnectionBuilder()
        .withUrl(
          '${api.baseUrl}/hubs/desk',
          options: HttpConnectionOptions(
            accessTokenFactory: () async => api.accessToken ?? token,
            skipNegotiation: false,
            // Package default is 2000ms — too short on mobile and drops the hub.
            requestTimeout: 15000,
          ),
        )
        .withAutomaticReconnect(retryDelays: [0, 1000, 2000, 5000, 10000, 15000])
        .build();

    // Always fire onChanged — payload shape varies by SignalR client version.
    connection.on('deskChanged', (args) {
      String? reason;
      if (args != null && args.isNotEmpty) {
        final first = args.first;
        final map = asJsonMap(first);
        if (map != null) {
          reason = asTextOrNull(map['reason'] ?? map['Reason']);
        } else if (first is String) {
          reason = first;
        }
      }
      onChanged(reason);
    });

    if (onChat != null) {
      connection.on('chatMessage', (args) {
        if (args == null || args.isEmpty) return;
        final map = asJsonMap(args.first);
        if (map != null && map['id'] != null) {
          onChat(ChatMessage.fromJson(map));
        }
      });
    }

    connection.onreconnecting(({error}) {
      onLiveChanged?.call(false);
    });
    connection.onreconnected(({connectionId}) {
      onLiveChanged?.call(true);
      onChanged('reconnected');
    });
    connection.onclose(({error}) {
      onLiveChanged?.call(false);
    });

    _connection = connection;
    try {
      await connection.start()?.timeout(const Duration(seconds: 15));
      if (_connection != connection) return;
      onLiveChanged?.call(true);
    } catch (_) {
      if (_connection == connection) {
        _connection = null;
        try {
          await connection.stop();
        } catch (_) {}
      }
      onLiveChanged?.call(false);
      return;
    }

    final prefs = await SharedPreferences.getInstance();
    var device = prefs.getString('deviceToken');
    if (device == null || device.length < 8) {
      device = 'passenger-${DateTime.now().millisecondsSinceEpoch}-${token.hashCode.abs()}';
      await prefs.setString('deviceToken', device);
    }
    try {
      await api.registerDevice(device, platform: 'Android');
    } catch (_) {}
  }

  Future<void> disconnect() async {
    final connection = _connection;
    _connection = null;
    onLiveChanged?.call(false);
    if (connection == null) return;
    try {
      await connection.stop();
    } catch (_) {}
  }
}
