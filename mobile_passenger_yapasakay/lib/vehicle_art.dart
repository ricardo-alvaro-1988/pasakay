import 'package:flutter/material.dart';
import 'package:flutter_svg/flutter_svg.dart';

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

Widget vehicleArtImage(String? type, {String? iconKey, double height = 44}) {
  final key = _artKey(type, iconKey);
  switch (key) {
    case 'Motorcycle':
      return SvgPicture.asset('assets/vehicles/motorcycle.svg', height: height, fit: BoxFit.contain);
    case 'Tricycle':
      return SvgPicture.asset('assets/vehicles/tricycle.svg', height: height, fit: BoxFit.contain);
    case 'Tuktuk':
      return SvgPicture.asset('assets/vehicles/tuktuk.svg', height: height);
    case 'Mpv':
    case 'Suv':
    case 'Van':
      return SvgPicture.asset('assets/vehicles/mpv.svg', height: height);
    default:
      return SvgPicture.asset('assets/vehicles/sedan.svg', height: height);
  }
}
