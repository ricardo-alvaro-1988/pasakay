import 'dart:async';

import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'api.dart';
import 'chat_action_button.dart';
import 'directions.dart';
import 'geocode.dart';
import 'models.dart';
import 'place_search.dart';
import 'rate_dialog.dart';
import 'session.dart';
import 'share_trip.dart';
import 'show_qr.dart';
import 'stop_rail.dart';
import 'theme.dart';
import 'trip_chat_sheet.dart';
import 'trip_status_banner.dart';
import 'vehicle_art.dart' hide vehicleLabel;

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, required this.session});

  final CustomerSession session;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  GoogleMapController? _map;
  Stop? _pickup;
  Stop? _dropoff;
  /// 1 = vehicle type, 2 = pickup/drop-off, 3 = choose ride + book
  int _bookStep = 1;
  String _preferredType = 'Motorcycle';
  String _vehicle = 'Motorcycle';
  String? _vehicleCategoryId;
  String _payment = 'Cash';
  String _paymentRef = '';
  int _passengers = 1;
  Quote? _quote;
  final Map<String, Quote> _quotes = {};
  bool _quoting = false;
  bool _booking = false;
  bool _locating = false;
  bool _sheetOpen = true;
  bool _cancelling = false;
  ServiceCheckResult? _serviceCheck;
  String? _error;
  Timer? _quoteDebounce;
  Set<Polyline> _polylines = {};
  String? _routeKey;
  String? _ratingDismissedId;
  bool _ratingPromptOpen = false;
  bool _sosBusy = false;
  static const _ratingDismissKey = 'trygoride.rating_dismissed_trip_id';

  @override
  void initState() {
    super.initState();
    widget.session.addListener(_onSessionChanged);
    unawaited(_initPickupGps());
    _applyHailIntent();
    unawaited(_syncRoute());
    unawaited(_loadRatingDismissedThenPrompt());
  }

  Future<void> _loadRatingDismissedThenPrompt() async {
    final prefs = await SharedPreferences.getInstance();
    _ratingDismissedId = prefs.getString(_ratingDismissKey);
    if (mounted) await _maybePromptRating();
  }

  Future<void> _persistRatingDismissed(String? tripId) async {
    _ratingDismissedId = tripId;
    final prefs = await SharedPreferences.getInstance();
    if (tripId == null || tripId.isEmpty) {
      await prefs.remove(_ratingDismissKey);
    } else {
      await prefs.setString(_ratingDismissKey, tripId);
    }
  }

  @override
  void didUpdateWidget(covariant HomeScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.session != widget.session) {
      oldWidget.session.removeListener(_onSessionChanged);
      widget.session.addListener(_onSessionChanged);
    }
    if (oldWidget.session.hailBookRiderId != widget.session.hailBookRiderId) {
      _applyHailIntent();
    }
    unawaited(_syncRoute());
  }

  void _onSessionChanged() {
    if (!mounted) return;
    final hasActive = widget.session.desk?.activeTrip != null;
    setState(() {
      // Never keep the Book spinner after a trip already exists on the desk.
      if (hasActive) {
        _booking = false;
        _bookStep = 1;
      }
    });
    unawaited(_syncRoute());
    _maybePromptRating();
  }

  Future<void> _maybePromptRating() async {
    if (!mounted || _ratingPromptOpen) return;
    final pending = widget.session.desk?.pendingRating;
    if (pending == null) return;
    if (pending.id == _ratingDismissedId) return;
    _ratingPromptOpen = true;
    final result = await showCompletedRideRateDialog(
      context,
      trip: pending,
      session: widget.session,
    );
    if (!mounted) return;
    _ratingPromptOpen = false;
    if (result == true) {
      await _persistRatingDismissed(null);
      await widget.session.refreshDesk(silent: true);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Thanks for your rating.')),
        );
      }
    } else {
      // Later / dismiss — persist so reopen does not re-spam the same trip.
      await _persistRatingDismissed(pending.id);
    }
  }

  Future<void> _sendSos(CustomerTrip trip) async {
    if (_sosBusy || !trip.canSos) return;
    final ok = await showPassengerConfirm(
      context,
      title: 'Send SOS?',
      message: 'Alert operators and share your location for this trip.',
      cancelLabel: 'Cancel',
      confirmLabel: 'Send SOS',
      confirmColor: brandSos,
    );
    if (ok != true || !mounted) return;
    setState(() => _sosBusy = true);
    try {
      double? lat;
      double? lng;
      try {
        final pos = await Geolocator.getCurrentPosition();
        lat = pos.latitude;
        lng = pos.longitude;
      } catch (_) {}
      await widget.session.api.sos(trip.id, lat: lat, lng: lng);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('SOS sent.')));
      }
    } on ApiException catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
      }
    } finally {
      if (mounted) setState(() => _sosBusy = false);
    }
  }

  void _applyHailIntent() {
    final riderId = widget.session.hailBookRiderId;
    final vehicle = widget.session.hailBookVehicleType;
    if (vehicle != null && vehicle.isNotEmpty) {
      setState(() {
        _preferredType = vehicle;
        _vehicle = vehicle;
        _bookStep = 2;
      });
    }
    if (riderId != null) {
      final hailed = widget.session.desk?.hailedRider;
      if (hailed != null) {
        setState(() {
          _preferredType = hailed.vehicleType;
          _vehicle = hailed.vehicleType;
          _bookStep = 2;
        });
      }
    }
  }

  void _selectPreferredType(String type, String? categoryId) {
    setState(() {
      _preferredType = type;
      _vehicle = type;
      _vehicleCategoryId = categoryId;
      _passengers = vehicleIsCargo(type) ? 1 : _passengers.clamp(1, vehicleMaxPassengers(type));
      _bookStep = 2;
      _sheetOpen = true;
      _error = null;
    });
  }

  void _goToChooseRide() {
    if (_pickup == null || _dropoff == null) {
      setState(() => _error = 'Choose pickup and drop-off.');
      return;
    }
    setState(() {
      _bookStep = 3;
      _sheetOpen = true;
      _error = null;
      // Keep preferred type selected when entering step 3.
      _vehicle = _preferredType;
    });
    _scheduleQuote();
    unawaited(_syncRoute());
  }

  Future<String> _mapsKey() async {
    var key = widget.session.mapsKey ?? '';
    if (key.isEmpty) {
      final maps = await widget.session.api.mapsConfig();
      key = asText(maps['googleMapsBrowserKey']);
      widget.session.mapsKey = key;
    }
    return key;
  }

  Future<void> _initPickupGps() async {
    if (_pickup != null) return;
    setState(() => _locating = true);
    try {
      var perm = await Geolocator.checkPermission();
      if (perm == LocationPermission.denied) {
        perm = await Geolocator.requestPermission();
      }
      if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) {
        _useDeskMapCenter();
        return;
      }
      final pos = await Geolocator.getCurrentPosition();
      final key = await _mapsKey();
      final stop = await reverseGeocode(pos.latitude, pos.longitude, key) ??
          Stop(
            label: 'Current location',
            details: 'Current location',
            lat: pos.latitude,
            lng: pos.longitude,
          );
      if (!mounted) return;
      setState(() {
        _pickup = stop;
        _locating = false;
      });
      _map?.animateCamera(CameraUpdate.newLatLngZoom(LatLng(stop.lat, stop.lng), 15));
      _scheduleQuote();
      unawaited(_runServiceCheck());
    } catch (_) {
      _useDeskMapCenter();
    }
  }

  void _useDeskMapCenter() {
    final desk = widget.session.desk;
    if (desk?.mapLat != null && desk?.mapLng != null) {
      setState(() {
        _pickup = Stop(
          label: 'Map center',
          details: 'Map center',
          lat: desk!.mapLat!,
          lng: desk.mapLng!,
        );
        _locating = false;
      });
    } else {
      setState(() => _locating = false);
    }
  }

  Future<void> _locateMe() async {
    setState(() => _locating = true);
    try {
      final pos = await Geolocator.getCurrentPosition();
      final key = await _mapsKey();
      final stop = await reverseGeocode(pos.latitude, pos.longitude, key) ??
          Stop(
            label: 'Current location',
            details: 'Current location',
            lat: pos.latitude,
            lng: pos.longitude,
          );
      if (!mounted) return;
      setState(() {
        _pickup = stop;
        _locating = false;
      });
      await _map?.animateCamera(CameraUpdate.newLatLngZoom(LatLng(stop.lat, stop.lng), 16));
      _scheduleQuote();
      unawaited(_runServiceCheck());
    } catch (_) {
      if (mounted) setState(() => _locating = false);
    }
  }

  Future<void> _pickStop(bool pickup) async {
    final key = await _mapsKey();
    final stop = await showPlaceSearchDialog(
      context,
      api: widget.session.api,
      mapsKey: key,
      title: pickup ? 'Pickup' : 'Drop-off',
      biasLat: _pickup?.lat ?? widget.session.desk?.mapLat,
      biasLng: _pickup?.lng ?? widget.session.desk?.mapLng,
      allowCurrentLocation: pickup,
    );
    if (stop == null || !mounted) return;
    setState(() {
      if (pickup) {
        _pickup = stop;
      } else {
        _dropoff = stop;
      }
      _sheetOpen = true;
    });
    _map?.animateCamera(CameraUpdate.newLatLngZoom(LatLng(stop.lat, stop.lng), 16));
    _scheduleQuote();
    unawaited(_runServiceCheck());
    unawaited(_syncRoute());
  }

  Future<void> _runServiceCheck() async {
    final pickup = _pickup;
    if (pickup == null) return;
    try {
      final result = await widget.session.api.serviceCheck(
        pickupDetails: pickup.details,
        pickupLat: pickup.lat,
        pickupLng: pickup.lng,
        pickupBarangayId: pickup.barangayId,
        dropoffDetails: _dropoff?.details,
        dropoffBarangayId: _dropoff?.barangayId,
      );
      if (!mounted) return;
      setState(() => _serviceCheck = result);
    } catch (_) {}
  }

  VehicleOffer? get _selectedOffer {
    final vehicles = _serviceCheck?.vehicles ?? const <VehicleOffer>[];
    for (final v in vehicles) {
      if (_vehicleCategoryId != null && v.id == _vehicleCategoryId) return v;
      if (_vehicleCategoryId == null && v.vehicleType == _vehicle) return v;
    }
    return null;
  }

  int get _maxPassengers =>
      vehicleMaxPassengers(_vehicle, offerMax: _selectedOffer?.maxPassengers);

  bool get _isCargo => vehicleIsCargo(_vehicle, offerCargo: _selectedOffer?.isCargo);

  bool get _showPassengerPicker => _pickup != null && !_isCargo && _maxPassengers > 1;

  BookBody? _bookBody({String? vehicleType, String? categoryId, int? passengers}) {
    final pickup = _pickup;
    final dropoff = _dropoff;
    if (pickup == null || dropoff == null) return null;
    final riderId = widget.session.hailBookRiderId ?? widget.session.desk?.hailedRider?.riderId;
    final type = vehicleType ?? _vehicle;
    return BookBody(
      vehicleType: type,
      vehicleCategoryId: categoryId ?? _vehicleCategoryId,
      pickupBarangayId: pickup.barangayId,
      pickupDetails: pickup.details.isNotEmpty ? pickup.details : pickup.label,
      pickupLat: pickup.lat,
      pickupLng: pickup.lng,
      dropoffBarangayId: dropoff.barangayId,
      dropoffDetails: dropoff.details.isNotEmpty ? dropoff.details : dropoff.label,
      dropoffLat: dropoff.lat,
      dropoffLng: dropoff.lng,
      paymentMethod: _payment,
      paymentMethodOther: _payment == 'Other' ? _paymentRef : null,
      riderId: riderId,
      passengerCount: passengers ?? _passengers,
    );
  }

  void _scheduleQuote() {
    _quoteDebounce?.cancel();
    _quoteDebounce = Timer(const Duration(milliseconds: 350), () {
      unawaited(_fetchQuotes());
    });
  }

  Future<void> _fetchQuotes() async {
    if (_pickup == null || _dropoff == null) {
      setState(() {
        _quote = null;
        _quotes.clear();
        _quoting = false;
      });
      return;
    }
    setState(() {
      _quoting = true;
      _error = null;
    });
    final vehicles = (_serviceCheck?.vehicles ?? const <VehicleOffer>[]).where((v) => v.available).toList();
    try {
      if (vehicles.isNotEmpty) {
        final entries = <String, Quote>{};
        for (final v in vehicles) {
          final body = _bookBody(
            vehicleType: v.vehicleType,
            categoryId: v.id,
            passengers: vehicleIsCargo(v.vehicleType, offerCargo: v.isCargo)
                ? 1
                : _passengers.clamp(1, vehicleMaxPassengers(v.vehicleType, offerMax: v.maxPassengers)),
          );
          if (body == null) continue;
          try {
            entries[v.id] = await widget.session.api.quote(body);
          } catch (_) {}
        }
        if (!mounted) return;
        setState(() {
          _quotes
            ..clear()
            ..addAll(entries);
          // Prefer the step-1 type (and current selection) when applying quotes.
          VehicleOffer? selected = _selectedOffer;
          if (selected == null || !entries.containsKey(selected.id)) {
            for (final v in vehicles) {
              if (v.vehicleType == _preferredType && entries.containsKey(v.id)) {
                selected = v;
                _vehicle = v.vehicleType;
                _vehicleCategoryId = v.id;
                break;
              }
            }
          }
          _quote = selected == null ? null : entries[selected.id];
          _quoting = false;
        });
      } else {
        final body = _bookBody();
        if (body == null) return;
        final quote = await widget.session.api.quote(body);
        if (!mounted) return;
        setState(() {
          _quote = quote;
          _quoting = false;
        });
      }
    } on ApiException catch (ex) {
      if (!mounted) return;
      setState(() {
        _quote = null;
        _quoting = false;
        _error = ex.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _quote = null;
        _quoting = false;
        _error = 'Could not get a fare. Try again.';
      });
    }
  }

  Future<void> _book() async {
    if (widget.session.desk?.activeTrip != null) {
      setState(() => _error = 'Finish or cancel your current booking first.');
      return;
    }
    final body = _bookBody();
    if (body == null || _quote == null) {
      setState(() => _error = 'Choose pickup and drop-off.');
      return;
    }
    if (_payment == 'Other' && _paymentRef.trim().isEmpty) {
      setState(() => _error = 'Enter payment reference for Other.');
      return;
    }
    setState(() {
      _booking = true;
      _error = null;
    });
    try {
      final desk = await widget.session.api.book(body);
      if (!mounted) return;
      // Stop the Book spinner as soon as the trip is created — do not wait for rider accept.
      setState(() {
        _booking = false;
        _quoting = false;
      });
      widget.session.updateDesk(desk);
      widget.session.clearHailIntent();
      setState(() => _bookStep = 1);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            desk.activeTrip?.status == 'Pending'
                ? 'Ride booked — finding a rider…'
                : 'Ride booked!',
          ),
        ),
      );
      unawaited(_syncRoute());
    } on ApiException catch (ex) {
      if (mounted) {
        setState(() {
          _booking = false;
          _error = ex.message;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _booking = false;
          _error = 'Could not book this ride.';
        });
      }
    }
  }

  Future<void> _cancelActive(CustomerTrip trip) async {
    final ok = await showPassengerConfirm(
      context,
      title: 'Cancel trip?',
      message: 'Cancel ${trip.reference}?',
      cancelLabel: 'No',
      confirmLabel: 'Cancel trip',
      confirmColor: brandSos,
    );
    if (ok != true || !mounted) return;
    setState(() => _cancelling = true);
    try {
      await widget.session.cancelTrip(trip.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Booking cancelled.')));
        unawaited(_syncRoute());
      }
    } on ApiException catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not cancel.')),
        );
      }
    } finally {
      if (mounted) setState(() => _cancelling = false);
    }
  }

  LatLng _initialTarget() {
    final desk = widget.session.desk;
    final active = desk?.activeTrip;
    if (active?.pickupLat != null && active?.pickupLng != null) {
      return LatLng(active!.pickupLat!, active.pickupLng!);
    }
    if (_pickup != null) return LatLng(_pickup!.lat, _pickup!.lng);
    if (desk?.mapLat != null && desk?.mapLng != null) {
      return LatLng(desk!.mapLat!, desk.mapLng!);
    }
    return const LatLng(10.3157, 123.8854);
  }

  ({LatLng? origin, LatLng? dest, List<LatLng> pins}) _routeEndpoints() {
    final trip = widget.session.desk?.activeTrip;
    final pickup = trip?.pickupLat != null && trip?.pickupLng != null
        ? LatLng(trip!.pickupLat!, trip.pickupLng!)
        : (_pickup == null ? null : LatLng(_pickup!.lat, _pickup!.lng));
    final dropoff = trip?.dropoffLat != null && trip?.dropoffLng != null
        ? LatLng(trip!.dropoffLat!, trip.dropoffLng!)
        : (_dropoff == null ? null : LatLng(_dropoff!.lat, _dropoff!.lng));
    final pins = <LatLng>[
      if (pickup != null) pickup,
      if (dropoff != null) dropoff,
      if (trip?.riderLat != null && trip?.riderLng != null) LatLng(trip!.riderLat!, trip.riderLng!),
    ];
    if (trip != null &&
        trip.status == 'Waiting' &&
        trip.riderLat != null &&
        trip.riderLng != null &&
        pickup != null) {
      return (origin: LatLng(trip.riderLat!, trip.riderLng!), dest: pickup, pins: pins);
    }
    return (origin: pickup, dest: dropoff, pins: pins);
  }

  Future<void> _syncRoute() async {
    final ends = _routeEndpoints();
    final origin = ends.origin;
    final dest = ends.dest;
    if (origin == null || dest == null) {
      if (_polylines.isNotEmpty || _routeKey != null) {
        if (!mounted) return;
        setState(() {
          _polylines = {};
          _routeKey = null;
        });
      }
      return;
    }
    final trip = widget.session.desk?.activeTrip;
    final key =
        '${trip?.id ?? 'draft'}:${trip?.status ?? ''}:${origin.latitude},${origin.longitude}->${dest.latitude},${dest.longitude}:${trip?.riderLat},${trip?.riderLng}';
    if (key == _routeKey) return;
    _routeKey = key;
    final mapsKey = await _mapsKey();
    final points = await fetchDrivingRoute(origin: origin, destination: dest, apiKey: mapsKey);
    if (!mounted || _routeKey != key) return;
    setState(() {
      _polylines = {
        Polyline(
          polylineId: const PolylineId('route'),
          points: points,
          color: brandRed.withValues(alpha: 0.55),
          width: 5,
        ),
      };
    });
    await fitMapToPoints(_map, {...ends.pins, ...points});
  }

  Set<Marker> _mapMarkers() {
    final trip = widget.session.desk?.activeTrip;
    final markers = <Marker>{};
    final pickupLat = trip?.pickupLat ?? _pickup?.lat;
    final pickupLng = trip?.pickupLng ?? _pickup?.lng;
    final dropoffLat = trip?.dropoffLat ?? _dropoff?.lat;
    final dropoffLng = trip?.dropoffLng ?? _dropoff?.lng;
    if (pickupLat != null && pickupLng != null) {
      markers.add(
        Marker(
          markerId: const MarkerId('pickup'),
          position: LatLng(pickupLat, pickupLng),
          icon: BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueGreen),
          infoWindow: const InfoWindow(title: 'Pickup'),
        ),
      );
    }
    if (dropoffLat != null && dropoffLng != null) {
      markers.add(
        Marker(
          markerId: const MarkerId('dropoff'),
          position: LatLng(dropoffLat, dropoffLng),
          icon: BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueRed),
          infoWindow: const InfoWindow(title: 'Drop-off'),
        ),
      );
    }
    if (trip?.riderLat != null && trip?.riderLng != null) {
      markers.add(
        Marker(
          markerId: const MarkerId('rider'),
          position: LatLng(trip!.riderLat!, trip.riderLng!),
          icon: BitmapDescriptor.defaultMarkerWithHue(BitmapDescriptor.hueAzure),
          infoWindow: InfoWindow(title: trip.riderName ?? 'Your rider'),
        ),
      );
    }
    return markers;
  }

  @override
  void dispose() {
    widget.session.removeListener(_onSessionChanged);
    _quoteDebounce?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final active = widget.session.desk?.activeTrip;
    final hailed = widget.session.desk?.hailedRider;
    final hailId = widget.session.hailBookRiderId;
    final vehicles = _serviceCheck?.vehicles ?? const <VehicleOffer>[];
    final motoOk = _serviceCheck?.motorcycleAvailable ?? true;
    final trikeOk = _serviceCheck?.tricycleAvailable ?? true;
    final customerId = widget.session.desk?.customerId ?? '';
    final topPad = MediaQuery.paddingOf(context).top;
    final bottomInset = shellContentBottomInset(context);

    // PAGE 1 — plain vehicle selection (no map)
    if (active == null && _bookStep == 1) {
      return Scaffold(
        backgroundColor: brandCanvas,
        body: SafeArea(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Padding(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 8),
                child: Row(
                  children: [
                    ClipOval(
                      child: Image.asset('assets/logo-circle.png', width: 36, height: 36, fit: BoxFit.cover),
                    ),
                    const SizedBox(width: 10),
                    const Expanded(
                      child: Text('Pick the type you want', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 22)),
                    ),
                    if (customerId.isNotEmpty)
                      ShowQrButton(
                        onPressed: () => showCustomerQrOverlay(context, customerId: customerId),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              if (hailed != null || hailId != null)
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 10),
                  child: BrandPanel(
                    child: Text(
                      hailed != null
                          ? 'Booking with ${hailed.fullName}'
                          : 'Booking your favorite rider',
                      style: const TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ),
                ),
              Expanded(
                child: ListView(
                  padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + bottomInset),
                  children: [
                    _VehicleGrid(
                      vehicles: vehicles,
                      selectedType: _preferredType,
                      selectedCategoryId: null,
                      quotes: const {},
                      motoOk: motoOk,
                      trikeOk: trikeOk,
                      preferType: _preferredType,
                      onSelect: _selectPreferredType,
                      plainLarge: true,
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      );
    }

    // PAGE 2 (map + stops) / PAGE 3 (fares) / active trip
    return Scaffold(
      body: Stack(
        children: [
          GoogleMap(
            initialCameraPosition: CameraPosition(target: _initialTarget(), zoom: 14),
            myLocationEnabled: true,
            myLocationButtonEnabled: false,
            zoomControlsEnabled: false,
            onMapCreated: (c) {
              _map = c;
              unawaited(_syncRoute());
            },
            markers: _mapMarkers(),
            polylines: _polylines,
          ),
          Positioned(
            top: topPad + 10,
            left: 12,
            right: 12,
            child: Row(
              children: [
                if (active == null && _bookStep > 1)
                  Material(
                    color: Colors.white,
                    shape: const CircleBorder(),
                    elevation: 2,
                    child: IconButton(
                      tooltip: 'Back',
                      onPressed: () => setState(() {
                        _bookStep -= 1;
                        _error = null;
                      }),
                      icon: const Icon(Icons.arrow_back, color: brandInk),
                    ),
                  )
                else
                  Material(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(999),
                    elevation: 2,
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          ClipOval(
                            child: Image.asset('assets/logo-circle.png', width: 28, height: 28, fit: BoxFit.cover),
                          ),
                          const SizedBox(width: 8),
                          const Text('TryGoRide', style: TextStyle(fontWeight: FontWeight.w800)),
                        ],
                      ),
                    ),
                  ),
                const Spacer(),
                if (active != null && active.canSos)
                  Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: Material(
                      color: brandSos,
                      borderRadius: BorderRadius.circular(999),
                      elevation: 3,
                      child: InkWell(
                        onTap: _sosBusy ? null : () => _sendSos(active),
                        borderRadius: BorderRadius.circular(999),
                        child: Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              if (_sosBusy)
                                const SizedBox(
                                  width: 16,
                                  height: 16,
                                  child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                                )
                              else
                                const Icon(Icons.warning_amber_rounded, color: Colors.white, size: 18),
                              const SizedBox(width: 6),
                              const Text(
                                'SOS',
                                style: TextStyle(color: Colors.white, fontWeight: FontWeight.w900, fontSize: 13),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                if (active == null && customerId.isNotEmpty)
                  ShowQrButton(
                    onPressed: () => showCustomerQrOverlay(context, customerId: customerId),
                  ),
              ],
            ),
          ),
          Positioned(
            right: 14,
            bottom: _sheetOpen
                ? MediaQuery.sizeOf(context).height * (_bookStep == 2 ? 0.52 : 0.78)
                : 110 + bottomInset,
            child: FloatingActionButton.small(
              heroTag: 'locate',
              backgroundColor: Colors.white,
              onPressed: _locating ? null : _locateMe,
              child: _locating
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.my_location, color: brandInk),
            ),
          ),
          DraggableScrollableSheet(
            key: ValueKey('book-sheet-${active?.id ?? 'none'}-$_bookStep'),
            initialChildSize: active != null ? 0.42 : (_bookStep == 2 ? 0.50 : 0.75),
            minChildSize: 0.22,
            maxChildSize: 0.92,
            builder: (ctx, scroll) {
              return NotificationListener<DraggableScrollableNotification>(
                onNotification: (n) {
                  final open = n.extent > 0.28;
                  if (open != _sheetOpen) setState(() => _sheetOpen = open);
                  return false;
                },
                child: Container(
                  decoration: const BoxDecoration(
                    color: brandSurface,
                    borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
                    boxShadow: [BoxShadow(color: Color(0x3316181D), blurRadius: 16, offset: Offset(0, -4))],
                  ),
                  child: ListView(
                    controller: scroll,
                    padding: EdgeInsets.fromLTRB(14, 10, 14, 16 + bottomInset),
                    children: [
                      Center(
                        child: Container(
                          width: 40,
                          height: 4,
                          decoration: BoxDecoration(color: brandLine, borderRadius: BorderRadius.circular(99)),
                        ),
                      ),
                      const SizedBox(height: 10),
                      Text(
                        active != null
                            ? 'Your ride'
                            : _bookStep == 2
                                ? 'Pickup and drop-off'
                                : 'Choose your ride',
                        style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18),
                      ),
                      if (active != null) ...[
                        const SizedBox(height: 10),
                        _ActiveTripCard(
                          trip: active,
                          cancelling: _cancelling,
                          unreadChat: widget.session.unreadChatCount,
                          onCancel: active.canCancel && !_cancelling ? () => _cancelActive(active) : null,
                          onShare: () => shareCustomerTrip(active),
                          onChat: () => openTripChatSheet(
                            context,
                            session: widget.session,
                            tripId: active.id,
                            status: active.status,
                            canChat: active.canChat,
                            onOpened: widget.session.clearChatUnread,
                          ),
                        ),
                      ] else if (_bookStep == 2) ...[
                        const SizedBox(height: 6),
                        Text(
                          'Preferred: ${vehicleLabel(_preferredType)}',
                          style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 13),
                        ),
                        const SizedBox(height: 10),
                        StopRail(
                          pickup: _pickup,
                          dropoff: _dropoff,
                          onPickupTap: () => _pickStop(true),
                          onDropoffTap: () => _pickStop(false),
                          pickupHint: _locating ? 'Getting GPS…' : 'Tap to set pickup',
                        ),
                        if (_error != null) ...[
                          const SizedBox(height: 8),
                          Text(_error!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
                        ],
                        const SizedBox(height: 14),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton(
                            onPressed: _goToChooseRide,
                            child: const Text('Continue to fares'),
                          ),
                        ),
                      ] else ...[
                        const SizedBox(height: 6),
                        Text(
                          '${_pickup?.label ?? 'Pickup'} → ${_dropoff?.label ?? 'Drop-off'}',
                          style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12),
                        ),
                        const SizedBox(height: 10),
                        _VehicleOfferList(
                          vehicles: vehicles,
                          selectedType: _vehicle,
                          selectedCategoryId: _vehicleCategoryId,
                          quotes: _quotes,
                          motoOk: motoOk,
                          trikeOk: trikeOk,
                          preferType: _preferredType,
                          quoting: _quoting,
                          onSelect: (type, categoryId) {
                            setState(() {
                              _vehicle = type;
                              _vehicleCategoryId = categoryId;
                              _passengers = vehicleIsCargo(type)
                                  ? 1
                                  : _passengers.clamp(1, vehicleMaxPassengers(type));
                              if (categoryId != null) {
                                _quote = _quotes[categoryId];
                              } else {
                                Quote? match;
                                for (final q in _quotes.values) {
                                  if (q.vehicleType == type) {
                                    match = q;
                                    break;
                                  }
                                }
                                _quote = match ?? _quote;
                              }
                            });
                            _scheduleQuote();
                          },
                        ),
                        if (_showPassengerPicker) ...[
                          const SizedBox(height: 8),
                          Row(
                            children: [
                              Text(
                                'Passengers (max $_maxPassengers)',
                                style: const TextStyle(fontWeight: FontWeight.w700),
                              ),
                              const Spacer(),
                              _PassengerStepButton(
                                icon: Icons.remove_circle_outline,
                                onPressed: _passengers <= 1
                                    ? null
                                    : () {
                                        setState(() => _passengers -= 1);
                                        _scheduleQuote();
                                      },
                              ),
                              Padding(
                                padding: const EdgeInsets.symmetric(horizontal: 8),
                                child: Text('$_passengers', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                              ),
                              _PassengerStepButton(
                                icon: Icons.add_circle_outline,
                                onPressed: _passengers >= _maxPassengers
                                    ? null
                                    : () {
                                        setState(() => _passengers += 1);
                                        _scheduleQuote();
                                      },
                              ),
                            ],
                          ),
                        ],
                        const SizedBox(height: 8),
                        Row(
                          children: ['Cash', 'GCash', 'Maya', 'Other'].map((p) {
                            final on = _payment == p;
                            return Expanded(
                              child: Padding(
                                padding: const EdgeInsets.symmetric(horizontal: 3),
                                child: Material(
                                  color: on ? brandRed : brandChip,
                                  borderRadius: BorderRadius.circular(999),
                                  child: InkWell(
                                    onTap: () {
                                      setState(() => _payment = p);
                                      _scheduleQuote();
                                    },
                                    borderRadius: BorderRadius.circular(999),
                                    child: Padding(
                                      padding: const EdgeInsets.symmetric(vertical: 10),
                                      child: Text(
                                        p.toUpperCase(),
                                        textAlign: TextAlign.center,
                                        style: TextStyle(
                                          color: on ? Colors.white : brandInk,
                                          fontWeight: FontWeight.w800,
                                          fontSize: 11,
                                        ),
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                            );
                          }).toList(),
                        ),
                        if (_payment == 'Other') ...[
                          const SizedBox(height: 8),
                          TextField(
                            decoration: const InputDecoration(labelText: 'Payment reference'),
                            onChanged: (v) => _paymentRef = v,
                          ),
                        ],
                        if (_error != null) ...[
                          const SizedBox(height: 8),
                          Text(_error!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
                        ],
                        const SizedBox(height: 14),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton(
                            onPressed: _booking || _quoting
                                ? null
                                : _quote == null
                                    ? null
                                    : _book,
                            child: _booking
                                ? const SizedBox(
                                    width: 22,
                                    height: 22,
                                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                                  )
                                : Text(
                                    _quote == null
                                        ? (_quoting ? 'Getting fare…' : 'Select a vehicle')
                                        : 'Book ${vehicleLabel(_vehicle)} · ${peso(_quote!.displayFare)}',
                                  ),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              );
            },
          ),
        ],
      ),
    );
  }
}

List<_VehicleItem> _buildVehicleItems({
  required List<VehicleOffer> vehicles,
  required Map<String, Quote> quotes,
  required bool motoOk,
  required bool trikeOk,
  String? preferType,
}) {
  final items = <_VehicleItem>[];
  if (vehicles.isNotEmpty) {
    for (final v in vehicles.where((v) => v.available)) {
      items.add(
        _VehicleItem(
          id: v.id,
          type: v.vehicleType,
          name: v.name,
          iconKey: v.iconKey,
          price: quotes[v.id]?.displayFare,
        ),
      );
    }
  } else {
    if (motoOk) {
      items.add(const _VehicleItem(id: null, type: 'Motorcycle', name: 'Motorcycle'));
    }
    if (trikeOk) {
      items.add(const _VehicleItem(id: null, type: 'Tricycle', name: 'Tricycle'));
    }
    items.add(const _VehicleItem(id: null, type: 'Sedan', name: 'Sedan'));
  }
  if (preferType != null && preferType.isNotEmpty) {
    items.sort((a, b) {
      final ap = a.type == preferType ? 0 : 1;
      final bp = b.type == preferType ? 0 : 1;
      return ap.compareTo(bp);
    });
  }
  return items;
}

class _VehicleGrid extends StatelessWidget {
  const _VehicleGrid({
    required this.vehicles,
    required this.selectedType,
    required this.selectedCategoryId,
    required this.quotes,
    required this.motoOk,
    required this.trikeOk,
    required this.onSelect,
    this.preferType,
    this.plainLarge = false,
  });

  final List<VehicleOffer> vehicles;
  final String selectedType;
  final String? selectedCategoryId;
  final Map<String, Quote> quotes;
  final bool motoOk;
  final bool trikeOk;
  final String? preferType;
  final void Function(String type, String? categoryId) onSelect;
  final bool plainLarge;

  @override
  Widget build(BuildContext context) {
    final items = _buildVehicleItems(
      vehicles: vehicles,
      quotes: quotes,
      motoOk: motoOk,
      trikeOk: trikeOk,
      preferType: preferType,
    );

    // Step 1: full-width rows — icon left, name aligned beside it.
    if (plainLarge) {
      return Column(
        children: [
          for (final item in items)
            Builder(
              builder: (context) {
                final selected = item.id != null
                    ? selectedCategoryId == item.id
                    : selectedCategoryId == null && selectedType == item.type;
                return Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Material(
                    color: selected ? brandAccentSoft : brandSurface,
                    borderRadius: BorderRadius.circular(16),
                    clipBehavior: Clip.none,
                    child: InkWell(
                      onTap: () => onSelect(item.type, item.id),
                      borderRadius: BorderRadius.circular(16),
                      child: Container(
                        width: double.infinity,
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(
                            color: selected ? brandRed : brandLine,
                            width: selected ? 2 : 1,
                          ),
                        ),
                        child: Row(
                          children: [
                            vehicleArtImage(item.type, iconKey: item.iconKey, height: 76, width: 108),
                            const SizedBox(width: 14),
                            Expanded(
                              child: Text(
                                item.name,
                                style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
                              ),
                            ),
                            Icon(
                              selected ? Icons.check_circle : Icons.chevron_right,
                              color: selected ? brandRed : brandMuted,
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                );
              },
            ),
        ],
      );
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        const gap = 10.0;
        final tileWidth = (constraints.maxWidth - gap * 2) / 3;
        return Wrap(
          spacing: gap,
          runSpacing: gap,
          children: [
            for (final item in items)
              Builder(
                builder: (context) {
                  final selected = item.id != null
                      ? selectedCategoryId == item.id
                      : selectedCategoryId == null && selectedType == item.type;
                  return SizedBox(
                    width: tileWidth,
                    height: 88,
                    child: Material(
                      color: brandSurface,
                      borderRadius: BorderRadius.circular(16),
                      child: InkWell(
                        onTap: () => onSelect(item.type, item.id),
                        borderRadius: BorderRadius.circular(16),
                        child: Container(
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(16),
                            border: Border.all(color: selected ? brandRed : brandLine, width: selected ? 2 : 1),
                            color: selected ? brandAccentSoft : brandSurface,
                          ),
                          padding: const EdgeInsets.all(8),
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              vehicleArtImage(item.type, iconKey: item.iconKey, height: 48, width: 68),
                              const SizedBox(height: 6),
                              Text(
                                item.name,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                textAlign: TextAlign.center,
                                style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 11),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  );
                },
              ),
          ],
        );
      },
    );
  }
}

class _VehicleOfferList extends StatelessWidget {
  const _VehicleOfferList({
    required this.vehicles,
    required this.selectedType,
    required this.selectedCategoryId,
    required this.quotes,
    required this.motoOk,
    required this.trikeOk,
    required this.preferType,
    required this.onSelect,
    this.quoting = false,
  });

  final List<VehicleOffer> vehicles;
  final String selectedType;
  final String? selectedCategoryId;
  final Map<String, Quote> quotes;
  final bool motoOk;
  final bool trikeOk;
  final String preferType;
  final bool quoting;
  final void Function(String type, String? categoryId) onSelect;

  @override
  Widget build(BuildContext context) {
    final items = _buildVehicleItems(
      vehicles: vehicles,
      quotes: quotes,
      motoOk: motoOk,
      trikeOk: trikeOk,
      preferType: preferType,
    );
    if (quoting && quotes.isEmpty) {
      return const Padding(
        padding: EdgeInsets.symmetric(vertical: 16),
        child: Center(child: CircularProgressIndicator()),
      );
    }
    return Column(
      children: [
        for (final item in items)
          Builder(
            builder: (context) {
              final selected = item.id != null
                  ? selectedCategoryId == item.id
                  : selectedCategoryId == null && selectedType == item.type;
              final preferred = item.type == preferType;
              return Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: Material(
                  color: selected ? brandAccentSoft : brandSurface,
                  borderRadius: BorderRadius.circular(16),
                  clipBehavior: Clip.none,
                  child: InkWell(
                    onTap: () => onSelect(item.type, item.id),
                    borderRadius: BorderRadius.circular(16),
                    child: Container(
                      width: double.infinity,
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(
                          color: selected ? brandRed : brandLine,
                          width: selected ? 2 : 1,
                        ),
                      ),
                      child: Row(
                        children: [
                          vehicleArtImage(item.type, iconKey: item.iconKey, height: 72, width: 104),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(item.name, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                                if (preferred)
                                  const Text(
                                    'Your first choice',
                                    style: TextStyle(color: brandRed, fontWeight: FontWeight.w700, fontSize: 12),
                                  ),
                              ],
                            ),
                          ),
                          Text(
                            item.price != null ? peso(item.price!) : (quoting ? '…' : '—'),
                            style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 16, color: brandRed),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              );
            },
          ),
      ],
    );
  }
}

class _VehicleItem {
  const _VehicleItem({
    required this.id,
    required this.type,
    required this.name,
    this.iconKey,
    this.price,
  });

  final String? id;
  final String type;
  final String name;
  final String? iconKey;
  final double? price;
}

class _PassengerStepButton extends StatelessWidget {
  const _PassengerStepButton({required this.icon, this.onPressed});

  final IconData icon;
  final VoidCallback? onPressed;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 36,
      height: 36,
      child: Material(
        color: brandChip,
        borderRadius: BorderRadius.circular(999),
        child: InkWell(
          onTap: onPressed,
          borderRadius: BorderRadius.circular(999),
          child: Icon(icon, size: 20, color: onPressed == null ? brandMuted : brandInk),
        ),
      ),
    );
  }
}

class _ActiveTripCard extends StatelessWidget {
  const _ActiveTripCard({
    required this.trip,
    this.onCancel,
    this.cancelling = false,
    this.onShare,
    this.onChat,
    this.unreadChat = 0,
  });

  final CustomerTrip trip;
  final VoidCallback? onCancel;
  final bool cancelling;
  final VoidCallback? onShare;
  final VoidCallback? onChat;
  final int unreadChat;

  @override
  Widget build(BuildContext context) {
    final meta = [
      if (trip.distanceKm > 0) '${trip.distanceKm.toStringAsFixed(1)} km',
      if (trip.passengerCount != null) '${trip.passengerCount} pax',
      vehicleLabel(trip.vehicleType),
      paymentLabel(trip.paymentMethod, trip.paymentMethodOther),
    ].join(' · ');
    final finding = trip.status == 'Pending';
    return BrandPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TripStatusBanner(status: trip.status),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: Text(
                  trip.reference,
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12),
                ),
              ),
              if (onShare != null)
                IconButton(
                  tooltip: 'Share ride',
                  onPressed: onShare,
                  icon: const Icon(Icons.share, size: 20),
                  visualDensity: VisualDensity.compact,
                ),
            ],
          ),
          if (onChat != null) ...[
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: ChatActionButton(
                unread: unreadChat,
                onPressed: onChat!,
              ),
            ),
          ],
          if (finding) ...[
            const SizedBox(height: 4),
            const Row(
              children: [
                SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2, color: brandRed),
                ),
                SizedBox(width: 10),
                Expanded(
                  child: Text(
                    'Still looking — keep this screen open.',
                    style: TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: 8),
          Text(trip.pickup, style: const TextStyle(fontWeight: FontWeight.w600)),
          const Text('↓', style: TextStyle(color: brandMuted)),
          Text(trip.dropoff, style: const TextStyle(fontWeight: FontWeight.w600)),
          const SizedBox(height: 6),
          Text(meta, style: const TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600)),
          Text(peso(trip.customerFare ?? trip.fare), style: const TextStyle(fontWeight: FontWeight.w800)),
          if (trip.riderName != null) ...[
            const SizedBox(height: 4),
            Text(
              [
                trip.riderName!,
                if ((trip.plateNumber ?? '').isNotEmpty) trip.plateNumber!,
                if ((trip.riderPhone ?? '').isNotEmpty) trip.riderPhone!,
              ].join(' · '),
              style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
            ),
          ],
          if (onCancel != null) ...[
            const SizedBox(height: 12),
            SizedBox(
              width: double.infinity,
              child: FilledButton(
                style: FilledButton.styleFrom(
                  backgroundColor: brandSos,
                  foregroundColor: Colors.white,
                ),
                onPressed: cancelling ? null : onCancel,
                child: cancelling
                    ? const SizedBox(
                        width: 18,
                        height: 18,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : Text(finding ? 'Cancel booking' : 'Cancel ride'),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
