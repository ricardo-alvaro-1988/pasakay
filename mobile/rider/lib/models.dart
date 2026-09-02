String asText(dynamic value, [String fallback = '']) {
  if (value == null) {
    return fallback;
  }
  return value.toString();
}

String? asTextOrNull(dynamic value) {
  if (value == null) {
    return null;
  }
  return value.toString();
}

String vehicleLabel(dynamic value) {
  switch (asText(value)) {
    case '1':
      return 'Motorcycle';
    case '2':
      return 'Tricycle';
    default:
      return asText(value);
  }
}

String paymentCode(dynamic value) {
  switch (asText(value, 'Cash')) {
    case '1':
      return 'Cash';
    case '2':
      return 'GCash';
    case '3':
      return 'Maya';
    case '4':
      return 'Other';
    default:
      return asText(value, 'Cash');
  }
}

bool asFlag(dynamic value, [bool fallback = false]) {
  if (value is bool) {
    return value;
  }
  if (value is num) {
    return value != 0;
  }
  if (value is String) {
    return value.toLowerCase() == 'true' || value == '1';
  }
  return fallback;
}

int asInt(dynamic value, [int fallback = 0]) {
  if (value is int) {
    return value;
  }
  if (value is num) {
    return value.toInt();
  }
  return int.tryParse(value?.toString() ?? '') ?? fallback;
}

String paymentLabel(String? method) {
  switch ((method ?? '').toLowerCase()) {
    case 'gcash':
    case '2':
      return 'GCASH';
    case 'maya':
    case '3':
      return 'MAYA';
    case 'other':
    case '4':
      return 'OTHERS';
    default:
      return 'CASH';
  }
}

String passengerLabel(int? count) {
  final n = (count ?? 1).clamp(1, 999);
  return n == 1 ? '1 person' : '$n persons';
}

DateTime? parseUtc(dynamic value) {
  if (value is! String || value.isEmpty) {
    return null;
  }
  final parsed = DateTime.tryParse(value);
  return parsed?.toLocal();
}

Map<String, dynamic>? asJsonMap(dynamic value) {
  if (value is Map<String, dynamic>) {
    return value;
  }
  if (value is Map) {
    return Map<String, dynamic>.from(value);
  }
  return null;
}

class RiderDesk {
  RiderDesk({
    required this.riderId,
    required this.fullName,
    required this.phoneNumber,
    required this.plateNumber,
    required this.vehicleType,
    required this.companyName,
    required this.isOnline,
    required this.walletBalance,
    required this.minWalletToReceive,
    required this.canReceiveBookings,
    required this.walletLow,
    required this.walletHighlight,
    required this.paymentMethods,
    required this.offers,
    this.activeTrip,
    this.pendingHail,
    this.photoUrl,
    this.vehicleModel,
    this.licenseType,
    this.licenseNumber,
    this.licensePhotoUrl,
    this.fullAddress,
    this.isActive = true,
  });

  final String riderId;
  final String fullName;
  final String phoneNumber;
  final String plateNumber;
  final String vehicleType;
  final String companyName;
  final bool isOnline;
  final double walletBalance;
  final double minWalletToReceive;
  final bool canReceiveBookings;
  final bool walletLow;
  final String walletHighlight;
  final List<String> paymentMethods;
  final RiderTrip? activeTrip;
  final List<JobOffer> offers;
  final PendingHail? pendingHail;
  final String? photoUrl;
  final String? vehicleModel;
  final String? licenseType;
  final String? licenseNumber;
  final String? licensePhotoUrl;
  final String? fullAddress;
  final bool isActive;

  String get vehicleLine {
    final model = (vehicleModel ?? '').trim();
    if (model.isEmpty) {
      return '$vehicleType · $plateNumber';
    }
    return '$vehicleType · $model · $plateNumber';
  }

  String get licenseLine {
    final parts = [licenseType, licenseNumber].where((x) => (x ?? '').trim().isNotEmpty).toList();
    return parts.join(' · ');
  }

  factory RiderDesk.fromJson(Map<String, dynamic> json) => RiderDesk(
        riderId: asText(json['riderId']),
        fullName: asText(json['fullName']),
        phoneNumber: asText(json['phoneNumber']),
        plateNumber: asText(json['plateNumber']),
        vehicleType: vehicleLabel(json['vehicleType']),
        companyName: asText(json['companyName']),
        isOnline: asFlag(json['isOnline']),
        walletBalance: (json['walletBalance'] as num?)?.toDouble() ?? 0,
        minWalletToReceive: (json['minWalletToReceive'] as num?)?.toDouble() ?? 100,
        canReceiveBookings: asFlag(json['canReceiveBookings']),
        walletLow: asFlag(json['walletLow']),
        walletHighlight: asText(json['walletHighlight']),
        paymentMethods: (json['paymentMethods'] as List? ?? []).map(paymentCode).toList(),
        activeTrip: asJsonMap(json['activeTrip']) == null ? null : RiderTrip.fromJson(asJsonMap(json['activeTrip'])!),
        offers: (json['offers'] as List? ?? [])
            .map(asJsonMap)
            .whereType<Map<String, dynamic>>()
            .map(JobOffer.fromJson)
            .toList(),
        pendingHail: asJsonMap(json['pendingHail']) == null ? null : PendingHail.fromJson(asJsonMap(json['pendingHail'])!),
        photoUrl: asTextOrNull(json['photoUrl']),
        vehicleModel: asTextOrNull(json['vehicleModel']),
        licenseType: asTextOrNull(json['licenseType']),
        licenseNumber: asTextOrNull(json['licenseNumber']),
        licensePhotoUrl: asTextOrNull(json['licensePhotoUrl']),
        fullAddress: asTextOrNull(json['fullAddress']),
        isActive: asFlag(json['isActive'], true),
      );
}

class PendingHail {
  PendingHail({
    required this.customerId,
    required this.customerName,
    required this.customerPhone,
    this.scannedAt,
  });

  final String customerId;
  final String customerName;
  final String customerPhone;
  final DateTime? scannedAt;

  factory PendingHail.fromJson(Map<String, dynamic> json) => PendingHail(
        customerId: asText(json['customerId']),
        customerName: asText(json['customerName']),
        customerPhone: asText(json['customerPhone']),
        scannedAt: parseUtc(json['scannedAtUtc']),
      );
}

class JobOffer {
  JobOffer({
    required this.offerId,
    required this.tripId,
    required this.reference,
    required this.customerName,
    required this.customerPhone,
    required this.pickup,
    required this.dropoff,
    required this.fare,
    required this.distanceKm,
    this.passengerCount = 1,
    required this.paymentMethod,
    required this.isPreferred,
    required this.highlighted,
    this.pickupLat,
    this.pickupLng,
    this.riderDistanceKm,
    this.paymentMethodOther,
    this.scheduledAt,
  });

  final String offerId;
  final String tripId;
  final String reference;
  final String customerName;
  final String customerPhone;
  final String pickup;
  final String dropoff;
  final double? pickupLat;
  final double? pickupLng;
  final double fare;
  final double distanceKm;
  final int passengerCount;
  final double? riderDistanceKm;
  final String paymentMethod;
  final String? paymentMethodOther;
  final DateTime? scheduledAt;
  final bool isPreferred;
  final bool highlighted;

  factory JobOffer.fromJson(Map<String, dynamic> json) => JobOffer(
        offerId: asText(json['offerId']),
        tripId: asText(json['tripId']),
        reference: asText(json['reference']),
        customerName: asText(json['customerName']),
        customerPhone: asText(json['customerPhone']),
        pickup: asText(json['pickup']),
        dropoff: asText(json['dropoff']),
        pickupLat: (json['pickupLat'] as num?)?.toDouble(),
        pickupLng: (json['pickupLng'] as num?)?.toDouble(),
        fare: (json['fare'] as num?)?.toDouble() ?? 0,
        distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
        passengerCount: asInt(json['passengerCount'], 1).clamp(1, 999),
        riderDistanceKm: (json['riderDistanceKm'] as num?)?.toDouble(),
        paymentMethod: paymentCode(json['paymentMethod']),
        paymentMethodOther: asTextOrNull(json['paymentMethodOther']),
        scheduledAt: parseUtc(json['scheduledAtUtc']),
        isPreferred: asFlag(json['isPreferred']),
        highlighted: asFlag(json['highlighted']),
      );
}

class RiderTrip {
  RiderTrip({
    required this.tripId,
    required this.reference,
    required this.status,
    required this.customerName,
    required this.customerPhone,
    required this.previousBookingCount,
    required this.completedBookingCount,
    required this.cancelledBookingCount,
    required this.pickup,
    required this.dropoff,
    required this.fare,
    required this.distanceKm,
    this.passengerCount = 1,
    required this.paymentMethod,
    required this.canStart,
    required this.canComplete,
    required this.canSos,
    this.pickupLat,
    this.pickupLng,
    this.dropoffLat,
    this.dropoffLng,
    this.lastCompletedAt,
    this.paymentMethodOther,
    this.canViewChat = true,
    this.canSendChat = false,
  });

  final String tripId;
  final String reference;
  final String status;
  final String customerName;
  final String customerPhone;
  final int previousBookingCount;
  final int completedBookingCount;
  final int cancelledBookingCount;
  final String pickup;
  final String dropoff;
  final double? pickupLat;
  final double? pickupLng;
  final double? dropoffLat;
  final double? dropoffLng;
  final DateTime? lastCompletedAt;
  final double fare;
  final double distanceKm;
  final int passengerCount;
  final String paymentMethod;
  final String? paymentMethodOther;
  final bool canStart;
  final bool canComplete;
  final bool canSos;
  final bool canViewChat;
  final bool canSendChat;

  bool get isNewCustomer => previousBookingCount == 0;

  bool get canChat => canSendChat;

  factory RiderTrip.fromJson(Map<String, dynamic> json) => RiderTrip(
        tripId: asText(json['tripId']),
        reference: asText(json['reference']),
        status: asText(json['status']),
        customerName: asText(json['customerName']),
        customerPhone: asText(json['customerPhone']),
        previousBookingCount: asInt(json['previousBookingCount']),
        completedBookingCount: asInt(json['completedBookingCount']),
        cancelledBookingCount: asInt(json['cancelledBookingCount']),
        pickup: asText(json['pickup']),
        dropoff: asText(json['dropoff']),
        pickupLat: (json['pickupLat'] as num?)?.toDouble(),
        pickupLng: (json['pickupLng'] as num?)?.toDouble(),
        dropoffLat: (json['dropoffLat'] as num?)?.toDouble(),
        dropoffLng: (json['dropoffLng'] as num?)?.toDouble(),
        lastCompletedAt: parseUtc(json['lastCompletedAtUtc']),
        fare: (json['fare'] as num?)?.toDouble() ?? 0,
        distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
        passengerCount: asInt(json['passengerCount'], 1).clamp(1, 999),
        paymentMethod: paymentCode(json['paymentMethod']),
        paymentMethodOther: asTextOrNull(json['paymentMethodOther']),
        canStart: asFlag(json['canStart']),
        canComplete: asFlag(json['canComplete']),
        canSos: asFlag(json['canSos']),
        canViewChat: json['canViewChat'] is bool
            ? json['canViewChat'] as bool
            : _chatStatus(asText(json['status'])),
        canSendChat: json['canChat'] is bool
            ? json['canChat'] as bool
            : _chatStatus(asText(json['status'])),
      );
}

bool _chatStatus(String status) {
  final value = status.toLowerCase();
  return value == 'waiting' || value == 'ongoing' || value == '5' || value == '3';
}

class RiderTripListItem {
  RiderTripListItem({
    required this.id,
    required this.reference,
    required this.status,
    required this.customerName,
    required this.pickup,
    required this.dropoff,
    required this.fare,
    required this.distanceKm,
    this.passengerCount = 1,
    required this.vehicleType,
    required this.paymentMethod,
    required this.requestedAt,
    this.paymentMethodOther,
  });

  final String id;
  final String reference;
  final String status;
  final String customerName;
  final String pickup;
  final String dropoff;
  final double fare;
  final double distanceKm;
  final int passengerCount;
  final String vehicleType;
  final String paymentMethod;
  final String? paymentMethodOther;
  final DateTime? requestedAt;

  factory RiderTripListItem.fromJson(Map<String, dynamic> json) => RiderTripListItem(
        id: asText(json['id']),
        reference: asText(json['reference']),
        status: tripStatusLabel(json['status']),
        customerName: asText(json['customerName']),
        pickup: asText(json['pickup']),
        dropoff: asText(json['dropoff']),
        fare: (json['fare'] as num?)?.toDouble() ?? 0,
        distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
        passengerCount: asInt(json['passengerCount'], 1).clamp(1, 999),
        vehicleType: vehicleLabel(json['vehicleType']),
        paymentMethod: paymentCode(json['paymentMethod']),
        paymentMethodOther: asTextOrNull(json['paymentMethodOther']),
        requestedAt: parseUtc(json['requestedAtUtc']),
      );
}

String tripStatusLabel(dynamic value) {
  switch (asText(value).toLowerCase()) {
    case '1':
    case 'completed':
      return 'Completed';
    case '2':
    case 'cancelled':
      return 'Cancelled';
    case '3':
    case 'ongoing':
      return 'Ongoing';
    case '4':
    case 'pending':
      return 'Pending';
    case '5':
    case 'waiting':
      return 'Waiting';
    default:
      return asText(value, 'Unknown');
  }
}

class WalletSummary {
  WalletSummary({
    required this.balance,
    required this.pendingCount,
    required this.recent,
  });

  final double balance;
  final int pendingCount;
  final List<WalletTx> recent;

  factory WalletSummary.fromJson(Map<String, dynamic> json) => WalletSummary(
        balance: (json['balance'] as num?)?.toDouble() ?? 0,
        pendingCount: asInt(json['pendingCount']),
        recent: (json['recent'] as List? ?? [])
            .whereType<Map<String, dynamic>>()
            .map(WalletTx.fromJson)
            .toList(),
      );
}

class WalletTx {
  WalletTx({
    required this.id,
    required this.kind,
    required this.status,
    required this.amount,
    this.balanceAfter,
    this.tripId,
    this.tripReference,
    this.tripFare,
    this.paymentMethod,
    this.note,
    this.rejectionReason,
    this.createdAt,
    this.resolvedAt,
  });

  final String id;
  final String kind;
  final String status;
  final String? paymentMethod;
  final double amount;
  final double? balanceAfter;
  final String? tripId;
  final String? tripReference;
  final double? tripFare;
  final String? note;
  final String? rejectionReason;
  final DateTime? createdAt;
  final DateTime? resolvedAt;

  bool get isBooking => tripId != null && tripId!.isNotEmpty && tripFare != null;

  factory WalletTx.fromJson(Map<String, dynamic> json) => WalletTx(
        id: asText(json['id']),
        kind: asText(json['kind']),
        status: asText(json['status']),
        paymentMethod: asTextOrNull(json['paymentMethod']),
        amount: (json['amount'] as num?)?.toDouble() ?? 0,
        balanceAfter: (json['balanceAfter'] as num?)?.toDouble(),
        tripId: asTextOrNull(json['tripId']),
        tripReference: asTextOrNull(json['tripReference']),
        tripFare: (json['tripFare'] as num?)?.toDouble(),
        note: asTextOrNull(json['note']),
        createdAt: parseUtc(json['createdAtUtc']),
        rejectionReason: asTextOrNull(json['rejectionReason']),
        resolvedAt: parseUtc(json['resolvedAtUtc']),
      );
}

class RideStop {
  RideStop({
    required this.label,
    required this.barangay,
    required this.municipality,
    required this.province,
    required this.fullAddress,
  });

  final String label;
  final String barangay;
  final String municipality;
  final String province;
  final String fullAddress;

  factory RideStop.fromJson(Map<String, dynamic> json) => RideStop(
        label: asText(json['label']),
        barangay: asText(json['barangay']),
        municipality: asText(json['municipality']),
        province: asText(json['province']),
        fullAddress: asText(json['fullAddress']),
      );
}

class RiderTripDetail {
  RiderTripDetail({
    required this.id,
    required this.reference,
    required this.status,
    required this.customerName,
    required this.customerPhone,
    required this.pickupStop,
    required this.dropoffStop,
    required this.pickup,
    required this.dropoff,
    required this.fare,
    required this.distanceKm,
    this.passengerCount = 1,
    required this.vehicleType,
    required this.requestedAt,
    required this.paymentMethod,
    required this.operatorName,
    required this.operatorPhone,
    required this.riderName,
    required this.riderPhone,
    required this.plateNumber,
    required this.chat,
    this.notes,
    this.durationMinutes,
    this.scheduledAt,
    this.completedAt,
    this.cancelledAt,
    this.cancelReason,
    this.rating,
    this.ratingComment,
    this.ratedAt,
    this.paymentMethodOther,
    this.vehicleModel,
    this.riderPhotoUrl,
  });

  final String id;
  final String reference;
  final String status;
  final String customerName;
  final String customerPhone;
  final RideStop pickupStop;
  final RideStop dropoffStop;
  final String pickup;
  final String dropoff;
  final String? notes;
  final double fare;
  final double distanceKm;
  final int passengerCount;
  final int? durationMinutes;
  final String vehicleType;
  final DateTime requestedAt;
  final DateTime? scheduledAt;
  final DateTime? completedAt;
  final DateTime? cancelledAt;
  final String? cancelReason;
  final int? rating;
  final String? ratingComment;
  final DateTime? ratedAt;
  final String paymentMethod;
  final String? paymentMethodOther;
  final String operatorName;
  final String operatorPhone;
  final String riderName;
  final String riderPhone;
  final String plateNumber;
  final String? vehicleModel;
  final String? riderPhotoUrl;
  final List<ChatMessage> chat;

  bool get canViewChat {
    final value = status.toLowerCase();
    return value == 'waiting' ||
        value == 'ongoing' ||
        value == 'completed' ||
        value == 'cancelled' ||
        value == '5' ||
        value == '3' ||
        value == '1' ||
        value == '2';
  }

  bool get canSendChat {
    final value = status.toLowerCase();
    return value == 'waiting' || value == 'ongoing' || value == '5' || value == '3';
  }

  factory RiderTripDetail.fromJson(Map<String, dynamic> json) => RiderTripDetail(
        id: asText(json['id']),
        reference: asText(json['reference']),
        status: asText(json['status']),
        customerName: asText(json['customerName']),
        customerPhone: asText(json['customerPhone']),
        pickupStop: RideStop.fromJson(asJsonMap(json['pickupStop']) ?? const {}),
        dropoffStop: RideStop.fromJson(asJsonMap(json['dropoffStop']) ?? const {}),
        pickup: asText(json['pickup']),
        dropoff: asText(json['dropoff']),
        notes: asTextOrNull(json['notes']),
        fare: (json['fare'] as num?)?.toDouble() ?? 0,
        distanceKm: (json['distanceKm'] as num?)?.toDouble() ?? 0,
        passengerCount: asInt(json['passengerCount'], 1).clamp(1, 999),
        durationMinutes: json['durationMinutes'] == null ? null : asInt(json['durationMinutes']),
        vehicleType: vehicleLabel(json['vehicleType']),
        requestedAt: parseUtc(json['requestedAtUtc']) ?? DateTime.now(),
        scheduledAt: parseUtc(json['scheduledAtUtc']),
        completedAt: parseUtc(json['completedAtUtc']),
        cancelledAt: parseUtc(json['cancelledAtUtc']),
        cancelReason: asTextOrNull(json['cancelReason']),
        rating: json['rating'] == null ? null : asInt(json['rating']),
        ratingComment: asTextOrNull(json['ratingComment']),
        ratedAt: parseUtc(json['ratedAtUtc']),
        paymentMethod: paymentCode(json['paymentMethod']),
        paymentMethodOther: asTextOrNull(json['paymentMethodOther']),
        operatorName: asText(json['operatorName']),
        operatorPhone: asText(json['operatorPhone']),
        riderName: asText(json['riderName']),
        riderPhone: asText(json['riderPhone']),
        plateNumber: asText(json['plateNumber']),
        vehicleModel: asTextOrNull(json['vehicleModel']),
        riderPhotoUrl: asTextOrNull(json['riderPhotoUrl']),
        chat: (json['chat'] as List? ?? [])
            .map(asJsonMap)
            .whereType<Map<String, dynamic>>()
            .map(ChatMessage.fromJson)
            .toList(),
      );
}

class ChatMessage {
  ChatMessage({
    required this.id,
    required this.sender,
    required this.body,
    this.sentAt,
    this.photoUrl,
  });

  final String id;
  final String sender;
  final String body;
  final DateTime? sentAt;
  final String? photoUrl;

  bool get fromRider {
    final value = sender.toLowerCase();
    return value == 'rider' || value == '2';
  }

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
        id: asText(json['id']),
        sender: asText(json['sender']),
        body: asText(json['body']),
        sentAt: parseUtc(json['sentAtUtc']),
        photoUrl: asTextOrNull(json['photoUrl']),
      );
}
