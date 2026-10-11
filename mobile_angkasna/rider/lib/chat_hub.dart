import 'dart:async';

import 'package:signalr_netcore/signalr_client.dart';

import 'api.dart';
import 'models.dart';

enum ChatLinkState { offline, connecting, live }

class TripChatHub {
  TripChatHub(this.api);

  final RiderApi api;
  HubConnection? _connection;
  String? _tripId;
  String? _wantedTripId;
  void Function(ChatMessage message)? _onMessage;
  void Function(ChatLinkState state, String? error)? onState;
  Timer? _retry;
  int _attempt = 0;
  ChatLinkState state = ChatLinkState.offline;
  String? lastError;

  bool connectedTo(String tripId) =>
      _tripId == tripId && _connection?.state == HubConnectionState.Connected;

  Future<void> connect(String tripId, void Function(ChatMessage message) onMessage) async {
    _wantedTripId = tripId;
    _onMessage = onMessage;
    _retry?.cancel();
    _attempt = 0;
    await _connectNow();
  }

  Future<void> _connectNow() async {
    final tripId = _wantedTripId;
    final onMessage = _onMessage;
    if (tripId == null || onMessage == null) {
      return;
    }
    if (connectedTo(tripId)) {
      _setState(ChatLinkState.live, null);
      return;
    }

    await _tearDown();
    _setState(ChatLinkState.connecting, null);

    final token = api.accessToken;
    if (token == null || token.isEmpty) {
      _setState(ChatLinkState.offline, 'Sign in again to use live chat.');
      return;
    }

    final connection = HubConnectionBuilder()
        .withUrl(
          '${api.baseUrl}/hubs/chat',
          options: HttpConnectionOptions(
            accessTokenFactory: () async => api.accessToken ?? token,
            skipNegotiation: false,
          ),
        )
        .withAutomaticReconnect(retryDelays: [0, 1000, 2000, 5000, 10000])
        .build();

    connection.on('chatMessage', (args) {
      if (args == null || args.isEmpty) {
        return;
      }
      final raw = args.first;
      if (raw is Map) {
        onMessage(ChatMessage.fromJson(Map<String, dynamic>.from(raw)));
      }
    });

    connection.onreconnecting(({error}) {
      _setState(ChatLinkState.connecting, 'Reconnecting to live chat…');
    });

    connection.onreconnected(({connectionId}) {
      final id = _wantedTripId;
      if (id != null) {
        connection.invoke('JoinTrip', args: <Object>[id]).then((_) {
          _setState(ChatLinkState.live, null);
        }).catchError((_) {
          _setState(ChatLinkState.offline, 'Could not rejoin live chat.');
          _scheduleRetry();
        });
      }
    });

    connection.onclose(({error}) {
      if (_wantedTripId == null) {
        return;
      }
      _connection = null;
      _tripId = null;
      _setState(ChatLinkState.offline, error?.toString() ?? 'Live chat disconnected.');
      _scheduleRetry();
    });

    try {
      await connection.start()?.timeout(const Duration(seconds: 8));
      await connection.invoke('JoinTrip', args: <Object>[tripId]).timeout(const Duration(seconds: 8));
    } catch (ex) {
      try {
        await connection.stop();
      } catch (_) {}
      _setState(ChatLinkState.offline, 'Could not connect to live chat. Retrying…');
      _scheduleRetry();
      return;
    }

    _connection = connection;
    _tripId = tripId;
    _attempt = 0;
    _setState(ChatLinkState.live, null);
  }

  void _scheduleRetry() {
    if (_wantedTripId == null) {
      return;
    }
    _retry?.cancel();
    final wait = Duration(seconds: [2, 3, 5, 8, 12][_attempt.clamp(0, 4)]);
    _attempt += 1;
    _retry = Timer(wait, () {
      if (_wantedTripId != null && !connectedTo(_wantedTripId!)) {
        unawaited(_connectNow());
      }
    });
  }

  Future<void> disconnect() async {
    _wantedTripId = null;
    _onMessage = null;
    _retry?.cancel();
    _retry = null;
    await _tearDown();
    _setState(ChatLinkState.offline, null);
  }

  Future<void> _tearDown() async {
    final connection = _connection;
    final tripId = _tripId;
    _connection = null;
    _tripId = null;
    if (connection == null) {
      return;
    }
    try {
      if (tripId != null && connection.state == HubConnectionState.Connected) {
        await connection.invoke('LeaveTrip', args: <Object>[tripId]);
      }
    } catch (_) {}
    try {
      await connection.stop();
    } catch (_) {}
  }

  void _setState(ChatLinkState next, String? error) {
    state = next;
    lastError = error;
    onState?.call(next, error);
  }
}
