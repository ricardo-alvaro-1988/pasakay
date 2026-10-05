import 'dart:async';

import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';

import 'api.dart';
import 'directions.dart';
import 'geocode.dart';
import 'models.dart';
import 'place_search.dart';
import 'session.dart';
import 'show_qr.dart';
import 'stop_rail.dart';
import 'theme.dart';
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

  @override
  void initState() {
    super.initState();
    widget.session.addListener(_onSessionChanged);
    unawaited(_initPickupGps());
    _applyHailIntent();
    unawaited(_syncRoute());
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
      if (hasActive) _booking = false;
    });
    unawaited(_syncRoute());
  }

  void _applyHailIntent() {
    final riderId = widget.session.hailBookRiderId;
    final vehicle = widget.session.hailBookVehicleType;
    if (vehicle != null && vehicle.isNotEmpty) {
      setState(() => _vehicle = vehicle);
    }
    if (riderId != null) {
      final hailed = widget.session.desk?.hailedRider;
      if (hailed != null) {
        setState(() => _vehicle = hailed.vehicleType);
      }
    }
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
        final selected = _selectedOffer;
        setState(() {
          _quotes
            ..clear()
            ..addAll(entries);
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
                        const Text('Ya! Pasakay', style: TextStyle(fontWeight: FontWeight.w800)),
                      ],
                    ),
                  ),
                ),
                const Spacer(),
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
                ? MediaQuery.sizeOf(context).height * 0.48
                : 110 + shellContentBottomInset(context),
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
            initialChildSize: 0.48,
            minChildSize: 0.22,
            maxChildSize: 0.9,
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
                    padding: EdgeInsets.fromLTRB(14, 10, 14, 16 + shellContentBottomInset(context)),
                    children: [
                      Center(
                        child: Container(
                          width: 40,
                          height: 4,
                          decoration: BoxDecoration(color: brandLine, borderRadius: BorderRadius.circular(99)),
                        ),
                      ),
                      const SizedBox(height: 10),
                      InkWell(
                        onTap: () => setState(() => _sheetOpen = !_sheetOpen),
                        child: Row(
                          children: [
                            Text(
                              active != null ? 'Your ride' : 'Where to?',
                              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18),
                            ),
                            const Spacer(),
                            const Icon(Icons.expand_more, color: brandMuted),
                          ],
                        ),
                      ),
                      if (active != null) ...[
                        const SizedBox(height: 10),
                        _ActiveTripCard(
                          trip: active,
                          cancelling: _cancelling,
                          onCancel: active.canCancel && !_cancelling ? () => _cancelActive(active) : null,
                        ),
                      ] else ...[
                        if (hailed != null || hailId != null) ...[
                          const SizedBox(height: 10),
                          BrandPanel(
                            child: Text(
                              hailed != null
                                  ? 'Booking with ${hailed.fullName}'
                                  : 'Booking your favorite rider',
                              style: const TextStyle(fontWeight: FontWeight.w700),
                            ),
                          ),
                        ],
                        const SizedBox(height: 10),
                        StopRail(
                          pickup: _pickup,
                          dropoff: _dropoff,
                          onPickupTap: () => _pickStop(true),
                          onDropoffTap: () => _pickStop(false),
                          pickupHint: _locating ? 'Getting GPS…' : 'Tap to set pickup',
                        ),
                        const SizedBox(height: 12),
                        _VehicleGrid(
                          vehicles: vehicles,
                          selectedType: _vehicle,
                          selectedCategoryId: _vehicleCategoryId,
                          quotes: _quotes,
                          motoOk: motoOk,
                          trikeOk: trikeOk,
                          onSelect: (type, categoryId) {
                            setState(() {
                              _vehicle = type;
                              _vehicleCategoryId = categoryId;
                              _passengers = vehicleIsCargo(type)
                                  ? 1
                                  : _passengers.clamp(1, vehicleMaxPassengers(type));
                              _quote = categoryId == null ? _quote : _quotes[categoryId];
                            });
                            _scheduleQuote();
                          },
                        ),
                        if (_showPassengerPicker) ...[
                          const SizedBox(height: 12),
                          Row(
                            children: [
                              Text(
                                'Passengers (max $_maxPassengers)',
                                style: const TextStyle(fontWeight: FontWeight.w700),
                              ),
                              const Spacer(),
                              IconButton(
                                onPressed: _passengers <= 1
                                    ? null
                                    : () {
                                        setState(() => _passengers -= 1);
                                        _scheduleQuote();
                                      },
                                icon: const Icon(Icons.remove_circle_outline),
                              ),
                              Text('$_passengers', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                              IconButton(
                                onPressed: _passengers >= _maxPassengers
                                    ? null
                                    : () {
                                        setState(() => _passengers += 1);
                                        _scheduleQuote();
                                      },
                                icon: const Icon(Icons.add_circle_outline),
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
                        if (_quoting)
                          const Padding(
                            padding: EdgeInsets.all(12),
                            child: Center(child: CircularProgressIndicator()),
                          )
                        else if (_quote != null) ...[
                          const SizedBox(height: 10),
                          BrandPanel(
                            child: Row(
                              children: [
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(peso(_quote!.displayFare), style: Theme.of(context).textTheme.titleMedium),
                                      Text(
                                        '${_quote!.distanceKm.toStringAsFixed(1)} km · ~${_quote!.etaMinutes} min',
                                        style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                                      ),
                                    ],
                                  ),
                                ),
                                Text(_quote!.operatorName, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 12)),
                              ],
                            ),
                          ),
                        ],
                        if (_error != null) ...[
                          const SizedBox(height: 8),
                          Text(_error!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
                        ],
                        const SizedBox(height: 14),
                        FilledButton(
                          onPressed: _booking || _quoting
                              ? null
                              : (_pickup == null || _dropoff == null)
                                  ? () => setState(() => _error = 'Choose pickup and drop-off.')
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
                                  _pickup == null || _dropoff == null
                                      ? 'Choose pickup and drop-off'
                                      : _quote == null
                                          ? 'Getting fare…'
                                          : 'Book ride · ${peso(_quote!.displayFare)}',
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

class _VehicleGrid extends StatelessWidget {
  const _VehicleGrid({
    required this.vehicles,
    required this.selectedType,
    required this.selectedCategoryId,
    required this.quotes,
    required this.motoOk,
    required this.trikeOk,
    required this.onSelect,
  });

  final List<VehicleOffer> vehicles;
  final String selectedType;
  final String? selectedCategoryId;
  final Map<String, Quote> quotes;
  final bool motoOk;
  final bool trikeOk;
  final void Function(String type, String? categoryId) onSelect;

  @override
  Widget build(BuildContext context) {
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

    return GridView.builder(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      itemCount: items.length,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 3,
        mainAxisSpacing: 8,
        crossAxisSpacing: 8,
        childAspectRatio: 0.95,
      ),
      itemBuilder: (context, index) {
        final item = items[index];
        final selected = item.id != null
            ? selectedCategoryId == item.id
            : selectedCategoryId == null && selectedType == item.type;
        return Material(
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
                  Expanded(child: vehicleArtImage(item.type, iconKey: item.iconKey, height: 40)),
                  Text(
                    item.name,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12),
                  ),
                  if (item.price != null)
                    Text(
                      peso(item.price!),
                      style: const TextStyle(color: brandRed, fontWeight: FontWeight.w800, fontSize: 12),
                    ),
                ],
              ),
            ),
          ),
        );
      },
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

class _ActiveTripCard extends StatelessWidget {
  const _ActiveTripCard({required this.trip, this.onCancel, this.cancelling = false});

  final CustomerTrip trip;
  final VoidCallback? onCancel;
  final bool cancelling;

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
          Row(
            children: [
              Expanded(
                child: Text(tripHeadline(trip.status), style: Theme.of(context).textTheme.titleMedium),
              ),
              Text(trip.reference, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12)),
            ],
          ),
          if (finding) ...[
            const SizedBox(height: 8),
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
                    'Finding a rider… Broadcasting to nearby phones.',
                    style: TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: 6),
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
          ] else if (trip.status == 'Ongoing') ...[
            const SizedBox(height: 8),
            const Text(
              'Trip is ongoing. Your rider will finish the ride when you arrive.',
              style: TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600),
            ),
          ],
        ],
      ),
    );
  }
}
