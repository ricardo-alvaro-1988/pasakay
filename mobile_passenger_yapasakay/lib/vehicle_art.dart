import 'package:flutter/material.dart';

String vehicleLabel(String type) {
  switch (type) {
    case 'PickupL300':
      return 'Pickup L300';
    case 'PickupCargo':
      return 'Pickup (Cargo)';
    case 'Mpv':
      return 'MPV';
    case 'Suv':
      return 'SUV';
    default:
      return type;
  }
}

int vehicleMaxPassengers(String type, {int? offerMax}) {
  if (offerMax != null && offerMax > 0) return offerMax;
  switch (type) {
    case 'Motorcycle':
      return 1;
    case 'Tricycle':
    case 'Sedan':
      return 4;
    case 'Mpv':
    case 'Suv':
      return 6;
    case 'Van':
      return 12;
    case 'PickupL300':
      return 10;
    case 'PickupCargo':
      return 2;
    case 'Tuktuk':
      return 5;
    default:
      return offerMax ?? 4;
  }
}

bool vehicleIsCargo(String type, {bool? offerCargo}) {
  if (offerCargo != null) return offerCargo;
  return type == 'PickupCargo';
}

String _artKey(String? type, String? iconKey) {
  if (iconKey != null && iconKey.isNotEmpty) {
    final key = iconKey.toLowerCase();
    if (key.contains('tuktuk') || key.contains('tuk-tuk') || key.contains('tuk')) return 'Tuktuk';
    if (key.contains('tricycle')) return 'Tricycle';
    if (key.contains('cargo')) return 'PickupCargo';
    if (key.contains('pickup') || key.contains('l300')) return 'PickupL300';
    if (key.contains('van')) return 'Van';
    if (key.contains('suv')) return 'Suv';
    if (key.contains('mpv')) return 'Mpv';
    if (key.contains('sedan') || key.contains('car') || key.contains('generic')) return 'Sedan';
    if (key.contains('motor')) return 'Motorcycle';
  }
  return type ?? 'Sedan';
}

String _assetFor(String key) {
  switch (key) {
    case 'Motorcycle':
      return 'assets/vehicles/motorcycle.png';
    case 'Tricycle':
      return 'assets/vehicles/tricycle.png';
    case 'Tuktuk':
      return 'assets/vehicles/tuktuk.png';
    case 'Mpv':
      return 'assets/vehicles/mpv.png';
    case 'Suv':
      return 'assets/vehicles/suv.png';
    case 'Van':
      return 'assets/vehicles/van.png';
    case 'PickupL300':
      return 'assets/vehicles/pickup_l300.png';
    case 'PickupCargo':
      return 'assets/vehicles/pickup_cargo.png';
    case 'Sedan':
    default:
      return 'assets/vehicles/sedan.png';
  }
}

Widget vehicleArtImage(String? type, {String? iconKey, double height = 44, double? width}) {
  final key = _artKey(type, iconKey);
  final h = height;
  final w = width ?? height * 1.35;
  // Decode at ~2–3x display width so high-res PNGs stay crisp on phones.
  final dpr = WidgetsBinding.instance.platformDispatcher.views.isEmpty
      ? 3.0
      : WidgetsBinding.instance.platformDispatcher.views.first.devicePixelRatio;
  final cacheW = (w * dpr * 1.5).round().clamp(128, 1536);
  return SizedBox(
    height: h,
    width: w,
    child: Image.asset(
      _assetFor(key),
      height: h,
      width: w,
      fit: BoxFit.contain,
      filterQuality: FilterQuality.high,
      isAntiAlias: true,
      cacheWidth: cacheW,
      errorBuilder: (_, _, _) => SizedBox(
        height: h,
        width: w,
        child: const Icon(Icons.directions_car, color: Color(0xFF667085)),
      ),
    ),
  );
}
