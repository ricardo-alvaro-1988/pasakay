import 'dart:async';

import 'package:flutter/material.dart';

import 'api.dart';
import 'models.dart';
import 'ph_time.dart';
import 'place_search.dart';
import 'session.dart';
import 'stop_rail.dart';
import 'theme.dart';
import 'vehicle_art.dart';

class RentalScreen extends StatefulWidget {
  const RentalScreen({super.key, required this.session, this.onGoBooking});

  final CustomerSession session;
  final VoidCallback? onGoBooking;

  @override
  State<RentalScreen> createState() => _RentalScreenState();
}

class _RentalScreenState extends State<RentalScreen> {
  Stop? _pickup;
  Stop? _dropoff;
  String _vehicle = 'Motorcycle';
  String? _vehicleCategoryId;
  String _payment = 'Cash';
  String _paymentRef = '';
  Quote? _quote;
  bool _quoting = false;
  bool _booking = false;
  ServiceCheckResult? _serviceCheck;
  String? _error;
  String? _note;
  late DateTime _whenManila;
  Timer? _quoteDebounce;

  @override
  void initState() {
    super.initState();
    _whenManila = defaultRentalWhenManila();
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

  Future<void> _pickWhen() async {
    final manilaNow = DateTime.now().toUtc().add(const Duration(hours: 8));
    final date = await showDatePicker(
      context: context,
      initialDate: DateTime(_whenManila.year, _whenManila.month, _whenManila.day),
      firstDate: DateTime(manilaNow.year, manilaNow.month, manilaNow.day),
      lastDate: manilaNow.add(const Duration(days: 30)),
    );
    if (date == null || !mounted) return;
    final time = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(hour: _whenManila.hour, minute: _whenManila.minute),
    );
    if (time == null || !mounted) return;
    setState(() {
      _whenManila = DateTime(date.year, date.month, date.day, time.hour, time.minute);
      _error = null;
    });
  }

  Future<void> _pickStop(bool pickup) async {
    final key = await _mapsKey();
    final stop = await showPlaceSearchDialog(
      context,
      api: widget.session.api,
      mapsKey: key,
      title: pickup ? 'Pickup' : 'Drop-off',
      allowCurrentLocation: pickup,
      biasLat: _pickup?.lat ?? widget.session.desk?.mapLat,
      biasLng: _pickup?.lng ?? widget.session.desk?.mapLng,
    );
    if (stop == null || !mounted) return;
    setState(() {
      if (pickup) {
        _pickup = stop;
      } else {
        _dropoff = stop;
      }
    });
    _scheduleQuote();
    unawaited(_runServiceCheck());
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
      if (mounted) setState(() => _serviceCheck = result);
    } catch (_) {}
  }

  BookBody? _bookBody({String? scheduledAtUtc}) {
    final pickup = _pickup;
    final dropoff = _dropoff;
    if (pickup == null || dropoff == null) return null;
    return BookBody(
      vehicleType: _vehicle,
      vehicleCategoryId: _vehicleCategoryId,
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
      scheduledAtUtc: scheduledAtUtc,
      passengerCount: 1,
    );
  }

  void _scheduleQuote() {
    _quoteDebounce?.cancel();
    _quoteDebounce = Timer(const Duration(milliseconds: 350), () {
      unawaited(_fetchQuote());
    });
  }

  Future<void> _fetchQuote() async {
    final body = _bookBody();
    if (body == null) {
      setState(() {
        _quote = null;
        _quoting = false;
      });
      return;
    }
    setState(() {
      _quoting = true;
      _error = null;
    });
    try {
      final quote = await widget.session.api.quote(body);
      if (!mounted) return;
      setState(() {
        _quote = quote;
        _quoting = false;
      });
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
        _error = 'Could not quote fare. Check your stops and try again.';
      });
    }
  }

  Future<void> _book() async {
    if (_pickup == null || _dropoff == null) {
      setState(() => _error = 'Please set your pickup and drop-off first.');
      return;
    }
    final scheduledAtUtc = scheduledAtUtcIsoFromManila(_whenManila);
    final scheduledMs = utcFromManilaWallClock(_whenManila).millisecondsSinceEpoch;
    if (scheduledMs < DateTime.now().toUtc().millisecondsSinceEpoch + 10 * 60 * 1000) {
      setState(() => _error = 'Please choose a time at least 10 minutes from now.');
      return;
    }
    if (_quote == null) {
      setState(() => _error = 'Please wait for the fare before booking.');
      return;
    }
    final body = _bookBody(scheduledAtUtc: scheduledAtUtc);
    if (body == null) {
      setState(() => _error = 'Set pickup, drop-off, and wait for fare.');
      return;
    }
    if (_payment == 'Other' && _paymentRef.trim().isEmpty) {
      setState(() => _error = 'Enter payment reference for Other.');
      return;
    }
    setState(() {
      _booking = true;
      _error = null;
      _note = null;
    });
    try {
      final desk = await widget.session.api.book(body);
      widget.session.updateDesk(desk);
      setState(() {
        _note = 'You\'re booked. We\'ll alert riders about an hour before pickup.';
        _whenManila = defaultRentalWhenManila();
      });
      _scheduleQuote();
      widget.onGoBooking?.call();
    } on ApiException catch (ex) {
      setState(() {
        _error = ex.message;
        _quoting = false;
      });
    } catch (_) {
      setState(() {
        _error = 'Could not book this ride.';
        _quoting = false;
      });
    } finally {
      if (mounted) setState(() => _booking = false);
    }
  }

  @override
  void dispose() {
    _quoteDebounce?.cancel();
    super.dispose();
  }

  static const _fallbackVehicleTypes = <(String type, String name)>[
    ('Motorcycle', 'Motorcycle'),
    ('Tricycle', 'Tricycle'),
    ('Tuktuk', 'Tuktuk'),
    ('Sedan', 'Sedan'),
    ('Mpv', 'MPV'),
    ('Suv', 'SUV'),
    ('Van', 'Van'),
    ('PickupL300', 'Pickup L300'),
    ('PickupCargo', 'Pickup (Cargo)'),
  ];

  @override
  Widget build(BuildContext context) {
    final vehicles = _serviceCheck?.vehicles ?? const <VehicleOffer>[];

    return Scaffold(
      appBar: AppBar(title: const Text('Rental')),
      body: ListView(
        padding: EdgeInsets.fromLTRB(16, 16, 16, 16 + shellContentBottomInset(context)),
        children: [
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(22),
              gradient: const LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [brandNavy, Color(0xFF061236)],
              ),
            ),
            child: const Row(
              children: [
                Icon(Icons.directions_car, color: Colors.white, size: 28),
                SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('RIDE LATER Rental', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 18)),
                      SizedBox(height: 4),
                      Text(
                        'Pick a time, set your stops, and we\'ll hold a ride for you.',
                        style: TextStyle(color: Colors.white, fontWeight: FontWeight.w600),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          BrandPanel(
            child: ListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Pickup time (PH)', style: TextStyle(fontWeight: FontWeight.w700)),
              subtitle: Text(phWhen(scheduledAtUtcIsoFromManila(_whenManila))),
              trailing: const Icon(Icons.schedule),
              onTap: _pickWhen,
            ),
          ),
          const SizedBox(height: 12),
          StopRail(
            pickup: _pickup,
            dropoff: _dropoff,
            onPickupTap: () => _pickStop(true),
            onDropoffTap: () => _pickStop(false),
          ),
          const SizedBox(height: 12),
          Text('VEHICLE', style: Theme.of(context).textTheme.labelLarge),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              if (vehicles.isNotEmpty)
                ...vehicles.map((v) {
                  final selected =
                      _vehicleCategoryId == v.id || (_vehicleCategoryId == null && _vehicle == v.vehicleType);
                  return _RentalVehicleCard(
                    selected: selected,
                    type: v.vehicleType,
                    name: v.name,
                    iconKey: v.iconKey,
                    onTap: () {
                      setState(() {
                        _vehicle = v.vehicleType;
                        _vehicleCategoryId = v.id;
                      });
                      _scheduleQuote();
                    },
                  );
                })
              else
                ..._fallbackVehicleTypes.map((entry) {
                  final type = entry.$1;
                  final name = entry.$2;
                  return _RentalVehicleCard(
                    selected: _vehicle == type && _vehicleCategoryId == null,
                    type: type,
                    name: name,
                    onTap: () {
                      setState(() {
                        _vehicle = type;
                        _vehicleCategoryId = null;
                      });
                      _scheduleQuote();
                    },
                  );
                }),
            ],
          ),
          const SizedBox(height: 12),
          Text('Payment', style: Theme.of(context).textTheme.labelLarge),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            children: ['Cash', 'GCash', 'Maya', 'Other'].map((p) {
              return ChoiceChip(
                label: Text(p),
                selected: _payment == p,
                onSelected: (_) {
                  setState(() => _payment = p);
                  _scheduleQuote();
                },
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
          const SizedBox(height: 12),
          if (_quoting)
            const Center(child: CircularProgressIndicator())
          else if (_quote != null)
            BrandPanel(
              child: Text('${peso(_quote!.displayFare)} · ${_quote!.distanceKm.toStringAsFixed(1)} km'),
            ),
          if (_note != null) ...[
            const SizedBox(height: 8),
            Text(_note!, style: const TextStyle(color: brandSuccess, fontWeight: FontWeight.w700)),
          ],
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!, style: const TextStyle(color: brandSos)),
          ],
          const SizedBox(height: 16),
          FilledButton(
            onPressed: _booking || _quoting || _quote == null ? null : _book,
            child: _booking
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                  )
                : const Text('Book rental'),
          ),
        ],
      ),
    );
  }
}

class _RentalVehicleCard extends StatelessWidget {
  const _RentalVehicleCard({
    required this.selected,
    required this.type,
    required this.name,
    required this.onTap,
    this.iconKey,
  });

  final bool selected;
  final String type;
  final String name;
  final String? iconKey;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: selected ? brandAccentSoft : brandSurface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          width: 140,
          padding: const EdgeInsets.all(10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: selected ? brandRed : brandLine, width: selected ? 2 : 1),
          ),
          child: Column(
            children: [
              vehicleArtImage(type, iconKey: iconKey, height: 42),
              const SizedBox(height: 6),
              Text(name, style: const TextStyle(fontWeight: FontWeight.w800)),
            ],
          ),
        ),
      ),
    );
  }
}
