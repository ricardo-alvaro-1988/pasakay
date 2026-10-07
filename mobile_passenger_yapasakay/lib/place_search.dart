import 'dart:async';

import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';

import 'api.dart';
import 'geocode.dart';
import 'models.dart';
import 'theme.dart';

Future<Stop?> showPlaceSearchDialog(
  BuildContext context, {
  required CustomerApi api,
  required String mapsKey,
  required String title,
  double? biasLat,
  double? biasLng,
  bool allowCurrentLocation = false,
}) async {
  final width = MediaQuery.sizeOf(context).width;
  return showModalBottomSheet<Stop>(
    context: context,
    isScrollControlled: true,
    backgroundColor: brandSurface,
    constraints: BoxConstraints(maxWidth: width, minWidth: width),
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
    ),
    builder: (ctx) => _PlaceSearchSheet(
      api: api,
      mapsKey: mapsKey,
      title: title,
      biasLat: biasLat,
      biasLng: biasLng,
      allowCurrentLocation: allowCurrentLocation,
    ),
  );
}

class _PlaceSearchSheet extends StatefulWidget {
  const _PlaceSearchSheet({
    required this.api,
    required this.mapsKey,
    required this.title,
    this.biasLat,
    this.biasLng,
    this.allowCurrentLocation = false,
  });

  final CustomerApi api;
  final String mapsKey;
  final String title;
  final double? biasLat;
  final double? biasLng;
  final bool allowCurrentLocation;

  @override
  State<_PlaceSearchSheet> createState() => _PlaceSearchSheetState();
}

class _PlaceSearchSheetState extends State<_PlaceSearchSheet> {
  final _controller = TextEditingController();
  Timer? _debounce;
  bool _busy = false;
  String? _error;
  List<PlacePrediction> _predictions = const [];
  List<Stop> _geoHits = const [];

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  Future<String> _key() async {
    var key = widget.mapsKey;
    if (key.isEmpty) {
      final maps = await widget.api.mapsConfig();
      key = asText(maps['googleMapsBrowserKey']);
    }
    return key;
  }

  void _onQueryChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 180), () {
      unawaited(_search(value));
    });
  }

  Future<void> _search(String raw) async {
    final text = raw.trim();
    if (text.length < 2) {
      setState(() {
        _predictions = const [];
        _geoHits = const [];
        _error = null;
        _busy = false;
      });
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final key = await _key();
      final results = await Future.wait([
        autocompletePlaces(text, key, biasLat: widget.biasLat, biasLng: widget.biasLng),
        geocodeAddress(text, key, biasLat: widget.biasLat, biasLng: widget.biasLng),
      ]);
      if (!mounted) return;
      final preds = results[0] as List<PlacePrediction>;
      final geo = results[1] as Stop?;
      setState(() {
        _predictions = preds;
        _geoHits = geo == null ? const [] : [geo];
        _busy = false;
        if (preds.isEmpty && geo == null) {
          _error = 'No matching place. Try another search.';
        }
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'Search failed.';
      });
    }
  }

  Future<void> _pickPrediction(PlacePrediction prediction) async {
    setState(() => _busy = true);
    try {
      final key = await _key();
      final stop = await placeDetails(prediction.placeId, key) ??
          await geocodeAddress(
            prediction.description,
            key,
            biasLat: widget.biasLat,
            biasLng: widget.biasLng,
          );
      if (!mounted) return;
      if (stop == null) {
        setState(() {
          _busy = false;
          _error = 'Could not open that place.';
        });
        return;
      }
      Navigator.of(context).pop(stop);
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'Could not open that place.';
      });
    }
  }

  Future<void> _useCurrentLocation() async {
    setState(() => _busy = true);
    try {
      var perm = await Geolocator.checkPermission();
      if (perm == LocationPermission.denied) {
        perm = await Geolocator.requestPermission();
      }
      if (perm == LocationPermission.denied || perm == LocationPermission.deniedForever) {
        setState(() {
          _busy = false;
          _error = 'Location permission is required.';
        });
        return;
      }
      final pos = await Geolocator.getCurrentPosition();
      final key = await _key();
      final stop = await reverseGeocode(pos.latitude, pos.longitude, key) ??
          Stop(
            label: 'Current location',
            details: 'Current location',
            lat: pos.latitude,
            lng: pos.longitude,
          );
      if (!mounted) return;
      Navigator.of(context).pop(stop);
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'Could not get current location.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final bottom = MediaQuery.viewInsetsOf(context).bottom;
    return Padding(
      padding: EdgeInsets.fromLTRB(16, 12, 16, 16 + bottom),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Center(
            child: Container(
              width: 40,
              height: 4,
              decoration: BoxDecoration(color: brandLine, borderRadius: BorderRadius.circular(99)),
            ),
          ),
          const SizedBox(height: 12),
          Text(widget.title, style: Theme.of(context).textTheme.titleMedium),
          const SizedBox(height: 10),
          TextField(
            controller: _controller,
            autofocus: true,
            decoration: InputDecoration(
              hintText: 'Search a place',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _busy
                  ? const Padding(
                      padding: EdgeInsets.all(12),
                      child: SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2)),
                    )
                  : null,
            ),
            onChanged: _onQueryChanged,
          ),
          if (widget.allowCurrentLocation) ...[
            const SizedBox(height: 8),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.my_location, color: brandRed),
              title: const Text('Use current location', style: TextStyle(fontWeight: FontWeight.w700)),
              onTap: _busy ? null : _useCurrentLocation,
            ),
          ],
          if (_error != null) ...[
            const SizedBox(height: 6),
            Text(_error!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
          ],
          Flexible(
            child: ListView(
              shrinkWrap: true,
              children: [
                ..._predictions.map(
                  (p) => ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.place_outlined, color: brandMuted),
                    title: Text(p.mainText, style: const TextStyle(fontWeight: FontWeight.w700)),
                    subtitle: Text(p.description, maxLines: 2, overflow: TextOverflow.ellipsis),
                    onTap: _busy ? null : () => _pickPrediction(p),
                  ),
                ),
                ..._geoHits.map(
                  (s) => ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.map_outlined, color: brandMuted),
                    title: Text(s.label, style: const TextStyle(fontWeight: FontWeight.w700)),
                    subtitle: Text(s.details, maxLines: 2, overflow: TextOverflow.ellipsis),
                    onTap: _busy ? null : () => Navigator.pop(context, s),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
