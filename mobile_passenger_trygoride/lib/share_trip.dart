import 'package:share_plus/share_plus.dart';

import 'models.dart';
import 'theme.dart';

String formatTripShare(CustomerTrip trip, {String brandName = 'TryGoRide'}) {
  final brand = brandName.trim().isEmpty ? 'TryGoRide' : brandName.trim();
  final pax = (trip.passengerCount ?? 1).clamp(1, 99);
  final fare = trip.customerFare ?? trip.fare;
  final vehicleBits = <String>[
    if ((trip.plateNumber ?? '').isNotEmpty) trip.plateNumber!,
    if ((trip.vehicleModel ?? '').isNotEmpty) trip.vehicleModel! else trip.vehicleType,
  ];
  final lines = <String>[
    '$brand — ${trip.reference}',
    tripHeadline(trip.status),
    '',
    if ((trip.riderName ?? '').isNotEmpty) 'Rider: ${trip.riderName}',
    if ((trip.riderPhone ?? '').isNotEmpty) 'Phone: ${trip.riderPhone}',
    if (vehicleBits.isNotEmpty) 'Vehicle: ${vehicleBits.join(' · ')}',
    '',
    'Pickup: ${trip.pickup}',
    'Drop-off: ${trip.dropoff}',
    '',
    'Fare: ${peso(fare)}'
        '${trip.distanceKm > 0 ? ' · ${trip.distanceKm.toStringAsFixed(1)} km' : ''}'
        ' · $pax passenger${pax == 1 ? '' : 's'}'
        ' · ${paymentLabel(trip.paymentMethod, trip.paymentMethodOther)}',
    'Operator: ${trip.operatorName}',
  ];
  return lines.join('\n');
}

Future<void> shareCustomerTrip(CustomerTrip trip, {String brandName = 'TryGoRide'}) async {
  final text = formatTripShare(trip, brandName: brandName);
  await Share.share(text, subject: '$brandName ${trip.reference}');
}
