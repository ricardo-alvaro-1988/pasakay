import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

import 'alerts.dart';
import 'api.dart';
import 'desk_hub.dart';
import 'models.dart';

class CustomerSession extends ChangeNotifier {
  CustomerSession(this.api) : _deskHub = DeskHubClient(api) {
    _deskHub.onLiveChanged = _onDeskHubLive;
  }

  final CustomerApi api;
  final DeskHubClient _deskHub;

  Desk? desk;
  String? error;
  bool busy = false;
  bool rentalEnabled = false;
  bool pabiliEnabled = false;
  String? googleClientId;
  String? mapsKey;
  String? hailBookRiderId;
  String? hailBookVehicleType;
  int unreadChatCount = 0;
  String? unreadChatTripId;
  bool chatSheetOpen = false;
  String? chatSheetTripId;
  final List<ChatMessage> _liveChat = [];

  Timer? _poll;
  Timer? _hubDebounce;
  Timer? _hubRetry;
  Timer? _pickupAlarm;
  bool _deskHubLive = false;
  bool _loopsStarted = false;
  Future<void>? _refreshInFlight;
  bool _refreshQueued = false;
  String? _deskSignature;

  /// Fast enough that accept/cancel feels live even if SignalR drops.
  static const _pollActiveTrip = Duration(seconds: 2);
  static const _pollFallback = Duration(seconds: 4);
  static const _pollWhenLive = Duration(seconds: 6);
  static const _hubRetryDelay = Duration(seconds: 8);

  bool get loggedIn => accessTokenPresent;

  bool get accessTokenPresent =>
      api.accessToken != null && api.accessToken!.isNotEmpty;

  bool get needsMobile => desk?.needsMobile == true;

  bool get hasActiveTrip => desk?.activeTrip?.isActive == true;

  bool get deskHubLive => _deskHubLive;

  void setHailIntent({String? riderId, String? vehicleType}) {
    hailBookRiderId = riderId;
    hailBookVehicleType = vehicleType;
    notifyListeners();
  }

  void clearHailIntent() {
    hailBookRiderId = null;
    hailBookVehicleType = null;
    notifyListeners();
  }

  void clearChatUnread() {
    if (unreadChatCount == 0 && unreadChatTripId == null) return;
    unreadChatCount = 0;
    unreadChatTripId = null;
    notifyListeners();
  }

  void markChatOpen(String tripId) {
    chatSheetOpen = true;
    chatSheetTripId = tripId;
    unreadChatCount = 0;
    unreadChatTripId = null;
    notifyListeners();
  }

  void markChatClosed() {
    chatSheetOpen = false;
    chatSheetTripId = null;
    notifyListeners();
  }

  List<ChatMessage> takeLiveChat(String tripId) {
    if (_liveChat.isEmpty) return const [];
    final rows = _liveChat.where((m) {
      final activeId = desk?.activeTrip?.id;
      return activeId == null || activeId == tripId || chatSheetTripId == tripId;
    }).toList();
    _liveChat.clear();
    return rows;
  }

  void _onChatMessage(ChatMessage message) {
    if (!message.fromRider) return;
    final tripId = desk?.activeTrip?.id;
    if (tripId == null || tripId.isEmpty) return;

    _liveChat.removeWhere((m) => m.id == message.id);
    _liveChat.add(message);

    final openHere = chatSheetOpen && chatSheetTripId == tripId;
    if (!openHere) {
      unreadChatCount += 1;
      unreadChatTripId = tripId;
    }
    notifyListeners();
    // Rider chat means the trip is live — pull desk so canChat/status catch up.
    unawaited(refreshDesk(silent: true));
  }

  void updateDesk(Desk next) {
    final hadActive = hasActiveTrip;
    final previousTripId = desk?.activeTrip?.id;
    desk = next;
    error = null;
    _deskSignature = _signature(next);
    final activeId = next.activeTrip?.id;
    if (activeId == null || (unreadChatTripId != null && unreadChatTripId != activeId)) {
      unreadChatCount = 0;
      unreadChatTripId = null;
    } else if (previousTripId != null && previousTripId != activeId) {
      unreadChatCount = 0;
      unreadChatTripId = null;
    }
    notifyListeners();
    if (hadActive != hasActiveTrip) {
      _schedulePoll();
    }
  }

  Future<void> restore() async {
    await api.load();
    notifyListeners();
    if (!loggedIn) return;
    try {
      await _loadPublicConfig();
      desk = await api.desk().timeout(const Duration(seconds: 12));
      _deskSignature = _signature(desk);
      await _loadServices();
      error = null;
    } on ApiException catch (ex) {
      if (ex.message.toLowerCase().contains('session expired') ||
          ex.message.toLowerCase().contains('sign in')) {
        await logout(silent: true);
      } else {
        error = ex.message;
      }
    } on TimeoutException {
      error = 'Cannot reach Ya! Pasakay right now.';
    } catch (_) {
      error = 'Cannot reach Ya! Pasakay right now.';
    }
    notifyListeners();
    if (desk != null && !needsMobile) {
      unawaited(_startLoops());
    }
  }

  Future<void> _loadPublicConfig() async {
    try {
      final auth = await api.authConfig();
      googleClientId = asTextOrNull(auth['googleClientId']);
      final maps = await api.mapsConfig();
      mapsKey = asTextOrNull(maps['googleMapsBrowserKey']);
    } catch (_) {}
  }

  Future<void> _loadServices() async {
    try {
      final lat = desk?.mapLat;
      final lng = desk?.mapLng;
      final services = await api.customerServices(
        lat: lat,
        lng: lng,
      );
      rentalEnabled = services.rentalEnabled;
      pabiliEnabled = services.pabiliEnabled;
    } catch (_) {
      rentalEnabled = false;
      pabiliEnabled = false;
    }
  }

  Future<void> googleLogin(String idToken) async {
    busy = true;
    error = null;
    notifyListeners();
    try {
      await api.googleSignIn(idToken);
      await _loadPublicConfig();
      desk = await api.desk();
      _deskSignature = _signature(desk);
      await _loadServices();
      error = null;
      busy = false;
      notifyListeners();
      if (!needsMobile) {
        unawaited(_startLoops());
      }
    } on ApiException catch (ex) {
      busy = false;
      error = ex.message;
      notifyListeners();
      throw ApiException(ex.message);
    } catch (ex) {
      busy = false;
      error = 'Could not sign in.';
      notifyListeners();
      if (ex is ApiException) rethrow;
      throw ApiException(error!);
    }
  }

  Future<void> refreshDesk({bool silent = false}) async {
    if (!loggedIn) return;
    if (_refreshInFlight != null) {
      _refreshQueued = true;
      return _refreshInFlight!;
    }
    _refreshInFlight = _refreshNow(silent: silent);
    try {
      await _refreshInFlight;
    } finally {
      _refreshInFlight = null;
      if (_refreshQueued) {
        _refreshQueued = false;
        unawaited(refreshDesk(silent: true));
      }
    }
  }

  Future<void> cancelTrip(String tripId) async {
    ApiException? apiError;
    try {
      final next = await api.cancel(tripId);
      updateDesk(next);
    } on ApiException catch (ex) {
      apiError = ex;
    } catch (_) {
      apiError = ApiException('Could not cancel.');
    }
    await refreshDesk(silent: true);
    final stillActive = desk?.activeTrip?.id == tripId ||
        desk?.scheduled.any((t) => t.id == tripId && t.canCancel) == true;
    if (stillActive && apiError != null) {
      throw apiError;
    }
  }

  Future<void> _refreshNow({bool silent = false}) async {
    final hadActive = hasActiveTrip;
    final previousSig = _deskSignature;
    final previousTripId = desk?.activeTrip?.id;
    try {
      final next = await api.desk().timeout(const Duration(seconds: 12));
      desk = next;
      _deskSignature = _signature(next);
      final activeId = next.activeTrip?.id;
      if (activeId == null || (unreadChatTripId != null && unreadChatTripId != activeId)) {
        unreadChatCount = 0;
        unreadChatTripId = null;
      } else if (previousTripId != null && previousTripId != activeId) {
        unreadChatCount = 0;
        unreadChatTripId = null;
      }
      if (!silent) error = null;
    } on ApiException catch (ex) {
      if (!silent) error = ex.message;
    } catch (_) {
      if (!silent) error = 'Could not refresh.';
    } finally {
      // Always notify on active trips so map/status keep ticking; otherwise only on change.
      if (hasActiveTrip || hadActive || _deskSignature != previousSig || !silent) {
        notifyListeners();
      }
      if (hadActive != hasActiveTrip) {
        _schedulePoll();
      }
      if (!_loopsStarted && desk != null && !needsMobile && loggedIn) {
        unawaited(_startLoops());
      }
      _armPickupAlarm();
    }
  }

  String _signature(Desk? d) {
    if (d == null) return '';
    final t = d.activeTrip;
    return [
      d.customerId,
      t?.id ?? '',
      t?.status ?? '',
      t?.riderName ?? '',
      t?.riderLat?.toStringAsFixed(5) ?? '',
      t?.riderLng?.toStringAsFixed(5) ?? '',
      t?.canCancel == true ? '1' : '0',
      d.scheduled.length.toString(),
      d.recent.isEmpty ? '' : d.recent.first.id,
      d.recent.isEmpty ? '' : d.recent.first.status,
      d.pendingRating?.id ?? '',
    ].join('|');
  }

  Future<void> logout({bool silent = false}) async {
    _stopLoops();
    await _deskHub.disconnect();
    await api.clear();
    desk = null;
    _deskSignature = null;
    rentalEnabled = false;
    pabiliEnabled = false;
    hailBookRiderId = null;
    hailBookVehicleType = null;
    unreadChatCount = 0;
    unreadChatTripId = null;
    chatSheetOpen = false;
    chatSheetTripId = null;
    _liveChat.clear();
    if (!silent) error = null;
    notifyListeners();
  }

  Future<void> _startLoops() async {
    _loopsStarted = true;
    _poll?.cancel();
    _hubDebounce?.cancel();
    _hubRetry?.cancel();
    try {
      await _deskHub.connect(_onHubDeskChanged, onChat: _onChatMessage);
    } catch (_) {}
    _deskHubLive = _deskHub.isLive;
    _schedulePoll();
    _scheduleHubRetry();
    // Immediate pull so we never wait a full poll interval after login/book.
    unawaited(refreshDesk(silent: true));
  }

  void _onHubDeskChanged(String? reason) {
    if (reason == 'schedule-alarm') {
      unawaited(_ringPickupAlarm());
    }
    _hubDebounce?.cancel();
    // Short debounce collapses double-fires (accepted + push) without feeling laggy.
    _hubDebounce = Timer(const Duration(milliseconds: 80), () {
      unawaited(refreshDesk(silent: true));
    });
  }

  void _onDeskHubLive(bool live) {
    if (_deskHubLive == live) return;
    _deskHubLive = live;
    _schedulePoll();
    _scheduleHubRetry();
    if (live) {
      unawaited(refreshDesk(silent: true));
    }
  }

  Duration get _pollInterval {
    if (hasActiveTrip) return _pollActiveTrip;
    return _deskHubLive ? _pollWhenLive : _pollFallback;
  }

  void _schedulePoll() {
    _poll?.cancel();
    if (!loggedIn || desk == null || needsMobile) return;
    final interval = _pollInterval;
    _poll = Timer.periodic(interval, (_) {
      unawaited(refreshDesk(silent: true));
    });
  }

  void _scheduleHubRetry() {
    _hubRetry?.cancel();
    if (!loggedIn || desk == null || needsMobile || _deskHubLive) return;
    _hubRetry = Timer(_hubRetryDelay, () async {
      if (!loggedIn || _deskHubLive) return;
      try {
        await _deskHub.connect(_onHubDeskChanged, onChat: _onChatMessage);
      } catch (_) {}
      _deskHubLive = _deskHub.isLive;
      _schedulePoll();
      _scheduleHubRetry();
      unawaited(refreshDesk(silent: true));
    });
  }

  void _stopLoops() {
    _loopsStarted = false;
    _poll?.cancel();
    _poll = null;
    _hubDebounce?.cancel();
    _hubDebounce = null;
    _hubRetry?.cancel();
    _hubRetry = null;
    _pickupAlarm?.cancel();
    _pickupAlarm = null;
  }

  Future<void> _ringPickupAlarm() async {
    await HapticFeedback.heavyImpact();
    SystemSound.play(SystemSoundType.alert);
    await PassengerAlerts.pingNotice(
      title: 'Pickup in 10 minutes',
      body: 'Your scheduled ride pickup is soon.',
    );
  }

  void _armPickupAlarm() {
    _pickupAlarm?.cancel();
    _pickupAlarm = null;
    final trips = <CustomerTrip>[
      ...?desk?.scheduled,
      if (desk?.activeTrip != null) desk!.activeTrip!,
    ];
    DateTime? fireAt;
    CustomerTrip? soonest;
    for (final trip in trips) {
      if (trip.status != 'Scheduled' && trip.status != 'ScheduledAccepted') continue;
      final pickup = DateTime.tryParse(trip.scheduledAtUtc ?? '');
      if (pickup == null) continue;
      final candidate = pickup.toUtc().subtract(const Duration(minutes: 10));
      if (fireAt == null || candidate.isBefore(fireAt)) {
        fireAt = candidate;
        soonest = trip;
      }
    }
    if (fireAt == null || soonest == null) {
      unawaited(PassengerAlerts.syncPickupAlarms(const []));
      return;
    }
    unawaited(PassengerAlerts.requestNotify());
    unawaited(PassengerAlerts.syncPickupAlarms([
      {
        'id': 'pk|${soonest.id}',
        'title': 'Pickup in 10 minutes',
        'body': '${soonest.reference} · ${soonest.pickup}',
        'at': fireAt.millisecondsSinceEpoch,
      }
    ]));
    final wait = fireAt.difference(DateTime.now().toUtc());
    if (wait.inSeconds <= 2) {
      if (wait.inMinutes >= -2) unawaited(_ringPickupAlarm());
      return;
    }
    if (wait.inDays > 2) return;
    _pickupAlarm = Timer(wait, () => unawaited(_ringPickupAlarm()));
  }

  /// Call when the app returns to foreground so accept/cancel catch up immediately.
  Future<void> onAppResumed() async {
    if (!loggedIn || desk == null || needsMobile) return;
    if (!_deskHubLive) {
      unawaited(_startLoops());
    } else {
      unawaited(refreshDesk(silent: true));
    }
  }

  @override
  void dispose() {
    _stopLoops();
    unawaited(_deskHub.disconnect());
    super.dispose();
  }
}
