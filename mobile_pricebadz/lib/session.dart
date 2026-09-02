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
    _chatHub.onState = (state, error) {
      chatLink = state;
      chatLinkError = error;
      if (desk != null && !busy) {
        notifyListeners();
      }
      if (state != ChatLinkState.live) {
        unawaited(_loadChat(silent: true));
      }
    };
  }

  final RiderApi api;
  final DeskHubClient _deskHub;
  final TripChatHub _chatHub;
  RiderDesk? desk;
  String? error;
  bool busy = false;
  Timer? _poll;
  Timer? _gps;
  Timer? _chatPoll;
  String? _shownOfferId;
  String? _alarmingOfferId;
  String? _chatTripId;
  RiderTrip? lastTrip;
  final List<ChatMessage> chatMessages = [];
  int chatUnread = 0;
  bool chatOpen = false;
  ChatLinkState chatLink = ChatLinkState.offline;
  String? chatLinkError;
  bool offerAlarmEnabled = true;
  bool _keepAlive = false;
  final AudioPlayer _offerAlarm = AudioPlayer();
  static const _alarmKey = 'offerAlarmEnabled';

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
      error = null;
    } on ApiException catch (ex) {
      if (ex.message.toLowerCase().contains('unauthorized') ||
          ex.message.toLowerCase().contains('account')) {
        await logout();
      } else {
        error = ex.message;
      }
    } on TimeoutException {
      error = 'Cannot reach Pricebadz right now.';
    } catch (_) {
      error = 'Cannot reach Pricebadz right now.';
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
      error = 'Cannot reach Pricebadz right now.';
    } finally {
      busy = false;
      notifyListeners();
    }
  }

  Future<void> logout() async {
    _poll?.cancel();
    _gps?.cancel();
    _chatPoll?.cancel();
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
    lastTrip = null;
    chatMessages.clear();
    chatUnread = 0;
    chatOpen = false;
    chatLink = ChatLinkState.offline;
    chatLinkError = null;
    desk = null;
    _keepAlive = false;
    await api.clear();
    await _stopOfferAlarm();
    await RiderAlerts.stopOnline();
    notifyListeners();
  }

  Future<void> refresh() async {
    if (!loggedIn) {
      return;
    }
    try {
      final previous = desk;
      desk = await api.desk();
      error = null;
      _rememberTrip();
      await _syncChat();
      await _syncOnlineService();
      await _syncOfferAlarm(previous);
    } on ApiException catch (ex) {
      error = ex.message;
    } catch (_) {
      error = 'Cannot reach Pricebadz right now.';
    }
    notifyListeners();
  }

  Future<void> setOnline(bool online) async {
    try {
      desk = await api.setOnline(online);
      error = null;
      if (online) {
        await RiderAlerts.prepare(true);
        _keepAlive = await RiderAlerts.startOnline();
        await _pingGps();
      } else {
        await _stopOfferAlarm();
        await RiderAlerts.stopOnline();
        _keepAlive = false;
      }
    } on ApiException catch (ex) {
      error = ex.message;
    }
    notifyListeners();
  }

  Future<void> setPaymentMethods(List<String> methods) async {
    desk = await api.setPayments(methods.map(paymentCode).toList());
    error = null;
    notifyListeners();
  }

  Future<void> accept(String offerId) async {
    await _stopOfferAlarm();
    desk = await api.accept(offerId);
    _shownOfferId = null;
    await _syncChat();
    notifyListeners();
  }

  Future<void> decline(String offerId) async {
    await _stopOfferAlarm();
    desk = await api.decline(offerId);
    _shownOfferId = null;
    notifyListeners();
  }

  Future<void> startTrip(String tripId) async {
    desk = await api.startTrip(tripId);
    await _syncChat();
    notifyListeners();
  }

  Future<void> completeTrip(String tripId) async {
    _rememberTrip();
    desk = await api.completeTrip(tripId);
    await _syncChat();
    notifyListeners();
  }

  Future<void> hail(String customerId) async {
    desk = await api.hail(customerId);
    _shownOfferId = null;
    notifyListeners();
  }

  Future<void> cancelHail() async {
    desk = await api.cancelHail();
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
    if (fresh.isNotEmpty || _alarmingOfferId == null) {
      await _playOfferAlarm(fresh.isNotEmpty ? fresh.first : offers.first);
    }
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
      await _offerAlarm.setReleaseMode(ReleaseMode.loop);
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
    try {
      await _deskHub.connect((_) => refresh(), onChat: _onIncomingChat);
    } catch (_) {
      // Poll still keeps the desk current if the hub cannot connect.
    }
    _poll = Timer.periodic(const Duration(seconds: 8), (_) => refresh());
    _gps = Timer.periodic(const Duration(seconds: 12), (_) => _pingGps());
    _chatPoll = Timer.periodic(const Duration(seconds: 3), (_) {
      if (chatLink != ChatLinkState.live) {
        unawaited(_loadChat(silent: true));
      }
    });
    unawaited(_syncOnlineService());
    unawaited(RiderAlerts.prepare(desk?.isOnline == true));
    unawaited(_syncOfferAlarm(null));
    unawaited(_pingGps());
    unawaited(_syncChat());
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

  Future<void> _pingGps() async {
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
      final position = await Geolocator.getCurrentPosition(
        locationSettings: const LocationSettings(
          accuracy: LocationAccuracy.high,
          timeLimit: Duration(seconds: 8),
        ),
      );
      desk = await api.pingLocation(position.latitude, position.longitude);
      notifyListeners();
    } catch (_) {
      // Keep the desk usable if GPS is off.
    }
  }

  @override
  void dispose() {
    _poll?.cancel();
    _gps?.cancel();
    _chatPoll?.cancel();
    unawaited(_deskHub.disconnect());
    unawaited(_chatHub.disconnect());
    unawaited(_offerAlarm.dispose());
    super.dispose();
  }
}
