import 'dart:async';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';
import 'package:geolocator/geolocator.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'alerts.dart';
import 'api.dart';
import 'chat_hub.dart';
import 'desk_hub.dart';
import 'models.dart';

class RiderSession extends ChangeNotifier {
  RiderSession(this.api)
      : _deskHub = DeskHubClient(api),
        _chatHub = TripChatHub(api) {
    _deskHub.onLiveChanged = _onDeskHubLive;
    _chatHub.onState = (state, error) {
      chatLink = state;
      chatLinkError = error;
      if (desk != null && !busy) {
        notifyListeners();
      }
      if (state != ChatLinkState.live) {
        unawaited(_loadChat(silent: true));
      }
      _scheduleChatPoll();
    };
  }

  final RiderApi api;
  final DeskHubClient _deskHub;
  final TripChatHub _chatHub;
  RiderDesk? desk;
  String? error;
  bool busy = false;
  RiderEarningsSummary? earningsSummary;
  List<RiderTripListItem> recentTrips = const [];
  DateTime? _homeExtrasAt;
  Timer? _poll;
  Timer? _gps;
  Timer? _chatPoll;
  Timer? _hubDebounce;
  String? _shownOfferId;
  String? _alarmingOfferId;
  String? _chatTripId;
  String? _deskSignature;
  RiderTrip? lastTrip;
  final List<ChatMessage> chatMessages = [];
  int chatUnread = 0;
  bool chatOpen = false;
  ChatLinkState chatLink = ChatLinkState.offline;
  String? chatLinkError;
  bool offerAlarmEnabled = true;
  bool _keepAlive = false;
  bool _deskHubLive = false;
  bool _refreshQueued = false;
  Future<void>? _refreshInFlight;
  DateTime? _lastGpsAt;
  double? _lastGpsLat;
  double? _lastGpsLng;
  final AudioPlayer _offerAlarm = AudioPlayer();
  static const _alarmKey = 'offerAlarmEnabled';

  /// Fast fallback when live desk hub is down.
  static const _pollFallback = Duration(seconds: 8);

  /// Safety net while SignalR is connected (offers still push via hub).
  static const _pollWhenLive = Duration(seconds: 30);

  static const _gpsInterval = Duration(seconds: 25);
  static const _chatPollFallback = Duration(seconds: 10);
  static const _minGpsMoveMeters = 35.0;

  bool get loggedIn => api.accessToken != null && api.accessToken!.isNotEmpty;

  Future<void> restore() async {
    await api.load();
    await _loadAlarmPref();
    notifyListeners();
    if (!loggedIn) {
      return;
    }
    try {
      desk = await api.desk().timeout(const Duration(seconds: 12));
      _deskSignature = _signature(desk);
      error = null;
    } on ApiException catch (ex) {
      if (ex.message.toLowerCase().contains('unauthorized') ||
          ex.message.toLowerCase().contains('account')) {
        await logout();
      } else {
        error = ex.message;
      }
    } on TimeoutException {
      error = 'Cannot reach TryGoRide right now.';
    } catch (_) {
      error = 'Cannot reach TryGoRide right now.';
    }
    notifyListeners();
    if (desk != null) {
      unawaited(_startLoops());
    }
  }

  Future<void> login(String phone, String password) async {
    busy = true;
    error = null;
    notifyListeners();
    try {
      await _loadAlarmPref();
      await api.ping();
      await api.login(phone, password);
      desk = await api.desk();
      _deskSignature = _signature(desk);
      error = null;
      busy = false;
      notifyListeners();
      try {
        await _startLoops();
      } catch (_) {
        // Home is already shown; GPS/chat can catch up on the next refresh.
      }
    } on ApiException catch (ex) {
      error = ex.message;
    } catch (_) {
      error = 'Cannot reach TryGoRide right now.';
    } finally {
      busy = false;
      notifyListeners();
    }
  }

  Future<void> logout() async {
    _poll?.cancel();
    _gps?.cancel();
    _chatPoll?.cancel();
    _hubDebounce?.cancel();
    try {
      if (desk?.isOnline == true) {
        await api.setOnline(false);
      }
    } catch (_) {}
    await _deskHub.disconnect();
    await _chatHub.disconnect();
    _shownOfferId = null;
    _alarmingOfferId = null;
    _chatTripId = null;
    _deskSignature = null;
    lastTrip = null;
    chatMessages.clear();
    chatUnread = 0;
    chatOpen = false;
    chatLink = ChatLinkState.offline;
    chatLinkError = null;
    desk = null;
    _keepAlive = false;
    _deskHubLive = false;
    _lastGpsAt = null;
    _lastGpsLat = null;
    _lastGpsLng = null;
    await api.clear();
    await _stopOfferAlarm();
    await RiderAlerts.stopOnline();
    notifyListeners();
  }

  Future<void> refresh() async {
    if (!loggedIn) {
      return;
    }
    if (_refreshInFlight != null) {
      _refreshQueued = true;
      return _refreshInFlight!;
    }
    _refreshInFlight = _refreshNow();
    try {
      await _refreshInFlight;
    } finally {
      _refreshInFlight = null;
      if (_refreshQueued) {
        _refreshQueued = false;
        unawaited(refresh());
      }
    }
  }

  Future<void> _refreshNow() async {
    final previous = desk;
    final previousSig = _deskSignature;
    final previousError = error;
    try {
      desk = await api.desk();
      error = null;
      _deskSignature = _signature(desk);
      _rememberTrip();
      await _syncChat();
      await _syncOnlineService();
      await _syncOfferAlarm(previous);
      if (desk?.isOnline == true) {
        _scheduleGps();
      } else {
        _gps?.cancel();
      }
    } on ApiException catch (ex) {
      error = ex.message;
    } catch (_) {
      error = 'Cannot reach TryGoRide right now.';
    }
    if (_deskSignature != previousSig || error != previousError) {
      notifyListeners();
    }
  }

  Future<void> setOnline(bool online) async {
    try {
      desk = await api.setOnline(online);
      _deskSignature = _signature(desk);
      error = null;
      if (online) {
        await RiderAlerts.prepare(true);
        _keepAlive = await RiderAlerts.startOnline();
        await _pingGps(force: true);
        _scheduleGps();
      } else {
        await _stopOfferAlarm();
        await RiderAlerts.stopOnline();
        _keepAlive = false;
        _gps?.cancel();
      }
    } on ApiException catch (ex) {
      error = ex.message;
    }
    notifyListeners();
  }

  /// Force a location heartbeat (e.g. when the app returns from idle).
  Future<void> pingLocationKeepAlive() => _pingGps(force: true);

  Future<void> setPaymentMethods(List<String> methods) async {
    desk = await api.setPayments(methods.map(paymentCode).toList());
    _deskSignature = _signature(desk);
    error = null;
    notifyListeners();
  }

  Future<void> accept(String offerId) async {
    await _stopOfferAlarm();
    desk = await api.accept(offerId);
    _deskSignature = _signature(desk);
    _shownOfferId = null;
    await _syncChat();
    notifyListeners();
  }

  Future<void> decline(String offerId) async {
    await _stopOfferAlarm();
    desk = await api.decline(offerId);
    _deskSignature = _signature(desk);
    _shownOfferId = null;
    notifyListeners();
  }

  Future<void> startTrip(String tripId) async {
    desk = await api.startTrip(tripId);
    _deskSignature = _signature(desk);
    await _syncChat();
    notifyListeners();
  }

  Future<void> completeTrip(String tripId) async {
    _rememberTrip();
    desk = await api.completeTrip(tripId);
    _deskSignature = _signature(desk);
    await _syncChat();
    await loadHomeExtras(force: true);
    notifyListeners();
  }

  Future<void> cancelTrip(String tripId) async {
    _rememberTrip();
    desk = await api.cancelTrip(tripId);
    _deskSignature = _signature(desk);
    await _syncChat();
    notifyListeners();
  }

  /// Earnings + recent trips — cached; not refetched on every desk SignalR tick.
  Future<void> loadHomeExtras({bool force = false}) async {
    if (!loggedIn) {
      return;
    }
    if (!force &&
        earningsSummary != null &&
        _homeExtrasAt != null &&
        DateTime.now().difference(_homeExtrasAt!) < const Duration(minutes: 2)) {
      return;
    }
    try {
      final summary = await api.earningsSummary();
      final trips = await api.trips();
      earningsSummary = summary;
      recentTrips = trips.where((t) => t.status == 'Completed').take(5).toList();
      _homeExtrasAt = DateTime.now();
      notifyListeners();
    } catch (_) {
      /* keep prior cache */
    }
  }

  Future<void> refreshHome({bool forceExtras = true}) async {
    await refresh();
    await loadHomeExtras(force: forceExtras);
  }

  Future<void> hail(String customerId) async {
    desk = await api.hail(customerId);
    _deskSignature = _signature(desk);
    _shownOfferId = null;
    notifyListeners();
  }

  Future<void> cancelHail() async {
    desk = await api.cancelHail();
    _deskSignature = _signature(desk);
    notifyListeners();
  }

  JobOffer? takeIncomingOffer() {
    final offers = desk?.offers ?? const <JobOffer>[];
    if (offers.isEmpty) {
      _shownOfferId = null;
      return null;
    }
    final next = offers.first;
    if (_shownOfferId == next.offerId) {
      return null;
    }
    _shownOfferId = next.offerId;
    return next;
  }

  Future<void> setOfferAlarmEnabled(bool enabled) async {
    offerAlarmEnabled = enabled;
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_alarmKey, enabled);
    if (!enabled) {
      await _stopOfferAlarm();
    }
    notifyListeners();
  }

  Future<void> _loadAlarmPref() async {
    final prefs = await SharedPreferences.getInstance();
    offerAlarmEnabled = prefs.getBool(_alarmKey) ?? true;
  }

  Future<void> _syncOnlineService() async {
    final online = desk?.isOnline == true;
    if (online) {
      if (!_keepAlive) {
        _keepAlive = await RiderAlerts.startOnline();
      }
      return;
    }
    if (_keepAlive) {
      await RiderAlerts.stopOnline();
      _keepAlive = false;
    }
  }

  Future<void> _syncOfferAlarm(RiderDesk? previous) async {
    final offers = desk?.offers ?? const <JobOffer>[];
    if (offers.isEmpty) {
      await _stopOfferAlarm();
      return;
    }
    final prevIds = {for (final offer in previous?.offers ?? const <JobOffer>[]) offer.offerId};
    final fresh = offers.where((offer) => !prevIds.contains(offer.offerId)).toList();
    if (fresh.isEmpty) {
      return;
    }
    await _playOfferAlarm(fresh.first);
  }

  Future<void> _playOfferAlarm(JobOffer offer) async {
    if (!offerAlarmEnabled) {
      await _stopOfferAlarm();
      return;
    }
    if (_alarmingOfferId == offer.offerId) {
      return;
    }
    _alarmingOfferId = offer.offerId;
    final native = await RiderAlerts.ringOffer(
      title: 'New job offer',
      body: '${offer.reference} · ${offer.pickup}',
    );
    if (native) {
      return;
    }
    try {
      await _offerAlarm.stop();
      await _offerAlarm.setAudioContext(
        AudioContext(
          android: AudioContextAndroid(
            stayAwake: true,
            contentType: AndroidContentType.sonification,
            usageType: AndroidUsageType.alarm,
            audioFocus: AndroidAudioFocus.gainTransientMayDuck,
          ),
        ),
      );
      await _offerAlarm.setReleaseMode(ReleaseMode.release);
      await _offerAlarm.setVolume(1);
      await _offerAlarm.play(AssetSource('offer-alarm.wav'));
    } catch (_) {}
  }

  Future<void> _stopOfferAlarm() async {
    _alarmingOfferId = null;
    try {
      await _offerAlarm.stop();
    } catch (_) {}
    await RiderAlerts.stopRing();
  }

  Future<void> _startLoops() async {
    _poll?.cancel();
    _gps?.cancel();
    _chatPoll?.cancel();
    _hubDebounce?.cancel();
    try {
      await _deskHub.connect(_onHubDeskChanged, onChat: _onIncomingChat);
    } catch (_) {
      // Poll still keeps the desk current if the hub cannot connect.
    }
    _deskHubLive = _deskHub.isLive;
    _schedulePoll();
    _scheduleGps();
    _scheduleChatPoll();
    unawaited(_syncOnlineService());
    unawaited(RiderAlerts.prepare(desk?.isOnline == true));
    unawaited(_syncOfferAlarm(null));
    unawaited(_pingGps(force: true));
    unawaited(_syncChat());
  }

  void _onHubDeskChanged(String? reason) {
    _hubDebounce?.cancel();
    _hubDebounce = Timer(const Duration(milliseconds: 280), () {
      unawaited(refresh());
    });
  }

  void _onDeskHubLive(bool live) {
    if (_deskHubLive == live) {
      return;
    }
    _deskHubLive = live;
    _schedulePoll();
  }

  void _schedulePoll() {
    _poll?.cancel();
    final interval = _deskHubLive ? _pollWhenLive : _pollFallback;
    _poll = Timer.periodic(interval, (_) => unawaited(refresh()));
  }

  void _scheduleGps() {
    _gps?.cancel();
    if (desk?.isOnline != true) {
      return;
    }
    _gps = Timer.periodic(_gpsInterval, (_) => unawaited(_pingGps()));
  }

  void _scheduleChatPoll() {
    _chatPoll?.cancel();
    if (chatLink == ChatLinkState.live) {
      return;
    }
    _chatPoll = Timer.periodic(_chatPollFallback, (_) {
      if (chatLink != ChatLinkState.live) {
        unawaited(_loadChat(silent: true));
      }
    });
  }

  RiderTrip? get chatTrip => desk?.activeTrip;

  bool get canViewChat => chatTrip?.canChat == true;

  bool get canSendChat => chatTrip?.canChat == true;

  void _rememberTrip() {
    final trip = desk?.activeTrip;
    if (trip != null) {
      lastTrip = trip;
    }
  }

  void _onIncomingChat(ChatMessage message) {
    _upsertChat(message);
    if (!chatOpen && !message.fromRider) {
      unawaited(RiderAlerts.pingChat(
        title: chatTrip == null ? 'New chat' : 'Chat · ${chatTrip!.reference}',
        body: message.body.trim().isEmpty ? 'Sent a photo' : message.body.trim(),
      ));
    }
  }

  void _upsertChat(ChatMessage message) {
    final exists = chatMessages.any((m) => m.id == message.id);
    if (exists) {
      return;
    }
    chatMessages.add(message);
    if (!chatOpen && !message.fromRider) {
      chatUnread += 1;
    }
    notifyListeners();
  }

  Future<void> _syncChat() async {
    _rememberTrip();
    final trip = chatTrip;
    if (trip == null || !canSendChat) {
      await _chatHub.disconnect();
      _chatTripId = null;
      if (chatMessages.isNotEmpty || chatUnread > 0) {
        chatMessages.clear();
        chatUnread = 0;
        notifyListeners();
      }
      _scheduleChatPoll();
      return;
    }
    if (_chatTripId != trip.tripId) {
      _chatTripId = trip.tripId;
      chatMessages.clear();
      chatUnread = 0;
      await _loadChat();
    }
    if (!_chatHub.connectedTo(trip.tripId)) {
      await _chatHub.connect(trip.tripId, _onIncomingChat);
    }
    _scheduleChatPoll();
  }

  Future<void> refreshChat(String tripId, {bool silent = true}) =>
      _loadChat(tripId: tripId, silent: silent);

  Future<void> _loadChat({String? tripId, bool silent = true}) async {
    final targetTripId = tripId ?? chatTrip?.tripId ?? _chatTripId;
    if (targetTripId == null) {
      return;
    }
    final activeTripId = desk?.activeTrip?.tripId;
    if (activeTripId != null && activeTripId != targetTripId) {
      return;
    }
    try {
      final rows = await api.chat(targetTripId);
      final previous = chatMessages.map((m) => m.id).toSet();
      final firstLoad = previous.isEmpty;
      final same =
          previous.length == rows.length && rows.every((m) => previous.contains(m.id));
      if (same && !firstLoad) {
        return;
      }
      chatMessages
        ..clear()
        ..addAll(rows);
      if (firstLoad) {
        notifyListeners();
        return;
      }
      final incoming = rows.where((m) => !previous.contains(m.id) && !m.fromRider).toList();
      if (!chatOpen) {
        chatUnread += incoming.length;
        if (incoming.isNotEmpty && chatLink != ChatLinkState.live) {
          final last = incoming.last;
          unawaited(RiderAlerts.pingChat(
            title: chatTrip == null ? 'New chat' : 'Chat · ${chatTrip!.reference}',
            body: last.body.trim().isEmpty ? 'Sent a photo' : last.body.trim(),
          ));
        }
      }
      notifyListeners();
    } catch (_) {
      if (!silent) {
        rethrow;
      }
    }
  }

  Future<ChatMessage> sendChat(String tripId, String text) async {
    final sent = await api.sendChat(tripId, text);
    _upsertChat(sent);
    return sent;
  }

  Future<ChatMessage> sendChatPhoto(String tripId, String filePath, {String? body}) async {
    final sent = await api.sendChatPhoto(tripId, filePath, body: body);
    _upsertChat(sent);
    return sent;
  }

  void markChatRead() {
    if (chatUnread == 0) {
      return;
    }
    chatUnread = 0;
    notifyListeners();
  }

  Future<void> _pingGps({bool force = false}) async {
    if (desk?.isOnline != true) {
      return;
    }
    try {
      var permission = await Geolocator.checkPermission();
      if (permission == LocationPermission.denied) {
        permission = await Geolocator.requestPermission();
      }
      if (permission == LocationPermission.denied ||
          permission == LocationPermission.deniedForever) {
        return;
      }

      final lastKnown = await Geolocator.getLastKnownPosition();
      final Position position;
      if (lastKnown != null &&
          DateTime.now().difference(lastKnown.timestamp) < const Duration(seconds: 45)) {
        position = lastKnown;
      } else {
        position = await Geolocator.getCurrentPosition(
          locationSettings: const LocationSettings(
            accuracy: LocationAccuracy.medium,
            timeLimit: Duration(seconds: 5),
          ),
        );
      }

      // Stationary riders: skip network ping briefly, but heartbeat at least every 2 minutes
      // so idle Online status is not treated as abandoned.
      if (!force && _lastGpsLat != null && _lastGpsLng != null && _lastGpsAt != null) {
        final age = DateTime.now().difference(_lastGpsAt!);
        final moved = Geolocator.distanceBetween(
          _lastGpsLat!,
          _lastGpsLng!,
          position.latitude,
          position.longitude,
        );
        if (moved < _minGpsMoveMeters && age < const Duration(minutes: 2)) {
          return;
        }
      }

      final previousSig = _deskSignature;
      desk = await api.pingLocation(position.latitude, position.longitude);
      _deskSignature = _signature(desk);
      _lastGpsAt = DateTime.now();
      _lastGpsLat = position.latitude;
      _lastGpsLng = position.longitude;
      if (_deskSignature != previousSig) {
        notifyListeners();
      }
    } catch (_) {
      // Keep the desk usable if GPS is off.
    }
  }

  static String _signature(RiderDesk? desk) {
    if (desk == null) {
      return '';
    }
    final trip = desk.activeTrip;
    final offers = desk.offers.map((o) => '${o.offerId}:${o.fare}').join(',');
    final hail = desk.pendingHail;
    return [
      desk.isOnline,
      desk.walletBalance.toStringAsFixed(2),
      desk.canReceiveBookings,
      desk.walletLow,
      desk.walletHighlight,
      desk.paymentMethods.join(','),
      trip?.tripId,
      trip?.status,
      trip?.fare,
      trip?.canChat,
      offers,
      hail?.customerId,
      desk.credibilityScore,
      desk.riderCancelCount,
    ].join('|');
  }

  @override
  void dispose() {
    _poll?.cancel();
    _gps?.cancel();
    _chatPoll?.cancel();
    _hubDebounce?.cancel();
    unawaited(_deskHub.disconnect());
    unawaited(_chatHub.disconnect());
    unawaited(_offerAlarm.dispose());
    super.dispose();
  }
}
