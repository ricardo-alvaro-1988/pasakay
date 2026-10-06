import 'dart:convert';

String asText(dynamic value, [String fallback = '']) {
  if (value == null) return fallback;
  return value.toString();
}

String? asTextOrNull(dynamic value) {
  if (value == null) return null;
  return value.toString();
}

Map<String, dynamic>? asJsonMap(dynamic value) {
  if (value is Map<String, dynamic>) return value;
  if (value is Map) return Map<String, dynamic>.from(value);
  return null;
}

bool asFlag(dynamic value, [bool fallback = false]) {
  if (value is bool) return value;
  if (value is num) return value != 0;
  if (value is String) {
    return value.toLowerCase() == 'true' || value == '1';
  }
  return fallback;
}

int asInt(dynamic value, [int fallback = 0]) {
  if (value is int) return value;
  if (value is num) return value.toInt();
  return int.tryParse(value?.toString() ?? '') ?? fallback;
}

double asDouble(dynamic value, [double fallback = 0]) {
  if (value is double) return value;
  if (value is num) return value.toDouble();
  return double.tryParse(value?.toString() ?? '') ?? fallback;
}

double? asDoubleOrNull(dynamic value) {
  if (value == null) return null;
  if (value is num) return value.toDouble();
  return double.tryParse(value.toString());
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

String paymentLabel(String? method, [String? other]) {
  final value = (method ?? 'Cash').toLowerCase();
  final name = value == 'gcash' || value == '2'
      ? 'GCASH'
      : value == 'maya' || value == '3'
          ? 'MAYA'
          : value == 'other' || value == '4'
              ? 'OTHERS'
              : 'CASH';
  final extra = (other ?? '').trim();
  return extra.isEmpty ? name : '$name · $extra';
}

String vehicleLabel(dynamic value) {
  switch (asText(value)) {
    case '1':
    case 'Motorcycle':
      return 'Motorcycle';
    case '2':
    case 'Tricycle':
      return 'Tricycle';
    case '3':
    case 'Sedan':
      return 'Sedan';
    case '4':
    case 'Mpv':
      return 'MPV';
    case '5':
    case 'Suv':
      return 'SUV';
    case '6':
    case 'Van':
      return 'Van';
    case '7':
    case 'PickupL300':
      return 'Pickup L300';
    case '8':
    case 'PickupCargo':
      return 'Pickup (Cargo)';
    case '9':
    case 'Tuktuk':
      return 'Tuktuk';
    default:
      return asText(value);
  }
}

String tripHeadline(String status) {
  switch (status) {
    case 'Pending':
      return 'Finding a rider';
    case 'Waiting':
      return 'Rider on the way';
    case 'Ongoing':
      return 'On your trip';
    case 'Completed':
      return 'Trip completed';
    case 'Cancelled':
      return 'Cancelled';
    default:
      return status;
  }
}

String tripStatusCode(dynamic value) {
  // Matches YaPasakay.Domain.Enums.TripStatus:
  // Completed=1, Cancelled=2, Ongoing=3, Pending=4, Waiting=5
  switch (asText(value)) {
    case '1':
    case 'Completed':
      return 'Completed';
    case '2':
    case 'Cancelled':
      return 'Cancelled';
    case '3':
    case 'Ongoing':
      return 'Ongoing';
    case '4':
    case 'Pending':
      return 'Pending';
    case '5':
    case 'Waiting':
      return 'Waiting';
    default:
      return asText(value, 'Pending');
  }
}

class AuthResponse {
  AuthResponse({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAtUtc,
    required this.role,
    required this.fullName,
    required this.phoneNumber,
  });

  final String accessToken;
  final String refreshToken;
  final String expiresAtUtc;
  final String role;
  final String fullName;
  final String phoneNumber;

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    final user = asJsonMap(json['user']) ?? {};
    return AuthResponse(
      accessToken: asText(json['accessToken']),
      refreshToken: asText(json['refreshToken']),
      expiresAtUtc: asText(json['expiresAtUtc']),
      role: asText(user['role']),
      fullName: asText(user['fullName']),
      phoneNumber: asText(user['phoneNumber']),
    );
  }
}

class Branding {
  Branding({
    required this.brandName,
    required this.shortName,
    this.logoUrl,
    this.faviconUrl,
    required this.themeId,
    required this.accent,
    required this.good,
  });

  final String brandName;
  final String shortName;
  final String? logoUrl;
  final String? faviconUrl;
  final String themeId;
  final String accent;
  final String good;

  factory Branding.fromJson(Map<String, dynamic> json) => Branding(
        brandName: asText(json['brandName'], 'Ya! Pasakay'),
        shortName: asText(json['shortName'], 'Ya! Pasakay'),
        logoUrl: asTextOrNull(json['logoUrl']),
        faviconUrl: asTextOrNull(json['faviconUrl']),
        themeId: asText(json['themeId']),
        accent: asText(json['accent']),
        good: asText(json['good']),
      );
}

class CustomerServices {
  CustomerServices({required this.pabiliEnabled, required this.rentalEnabled});

  final bool pabiliEnabled;
  final bool rentalEnabled;

  factory CustomerServices.fromJson(Map<String, dynamic> json) => CustomerServices(
        pabiliEnabled: asFlag(json['pabiliEnabled']),
        rentalEnabled: asFlag(json['rentalEnabled']),
      );
}

class Stop {
  Stop({
    required this.label,
    required this.details,
    required this.lat,
    required this.lng,
    this.barangayId,
  });

  final String label;
  final String details;
  final double lat;
  final double lng;
  final String? barangayId;

  Map<String, dynamic> toJson() => {
        'label': label,
        'details': details,
        'lat': lat,
        'lng': lng,
        if (barangayId != null && barangayId!.isNotEmpty) 'barangayId': barangayId,
      };
}

class BookBody {
  BookBody({
    required this.vehicleType,
    this.vehicleCategoryId,
    this.pickupBarangayId,
    required this.pickupDetails,
    required this.pickupLat,
    required this.pickupLng,
    this.dropoffBarangayId,
    required this.dropoffDetails,
    required this.dropoffLat,
    required this.dropoffLng,
    required this.paymentMethod,
    this.paymentMethodOther,
    this.scheduledAtUtc,
    this.riderId,
    this.passengerCount,
    this.promoCode,
    this.customerBoostAmount,
    this.fareDiscount,
    this.fareDiscountNote,
  });

  final String vehicleType;
  final String? vehicleCategoryId;
  final String? pickupBarangayId;
  final String pickupDetails;
  final double pickupLat;
  final double pickupLng;
  final String? dropoffBarangayId;
  final String dropoffDetails;
  final double dropoffLat;
  final double dropoffLng;
  final String paymentMethod;
  final String? paymentMethodOther;
  final String? scheduledAtUtc;
  final String? riderId;
  final int? passengerCount;
  final String? promoCode;
  final double? customerBoostAmount;
  final String? fareDiscount;
  final String? fareDiscountNote;

  Map<String, dynamic> toJson() => {
        'vehicleType': vehicleType,
        if (vehicleCategoryId != null && vehicleCategoryId!.isNotEmpty) 'vehicleCategoryId': vehicleCategoryId,
        if (pickupBarangayId != null && pickupBarangayId!.isNotEmpty) 'pickupBarangayId': pickupBarangayId,
        'pickupDetails': pickupDetails,
        'pickupLat': pickupLat,
        'pickupLng': pickupLng,
        if (dropoffBarangayId != null && dropoffBarangayId!.isNotEmpty) 'dropoffBarangayId': dropoffBarangayId,
        'dropoffDetails': dropoffDetails,
        'dropoffLat': dropoffLat,
        'dropoffLng': dropoffLng,
        'paymentMethod': paymentMethod,
        if (paymentMethodOther != null && paymentMethodOther!.isNotEmpty) 'paymentMethodOther': paymentMethodOther,
        if (scheduledAtUtc != null && scheduledAtUtc!.isNotEmpty) 'scheduledAtUtc': scheduledAtUtc,
        if (riderId != null && riderId!.isNotEmpty) 'riderId': riderId,
        if (passengerCount != null) 'passengerCount': passengerCount,
        if (promoCode != null && promoCode!.isNotEmpty) 'promoCode': promoCode,
        if (customerBoostAmount != null) 'customerBoostAmount': customerBoostAmount,
        if (fareDiscount != null && fareDiscount!.isNotEmpty) 'fareDiscount': fareDiscount,
        if (fareDiscountNote != null && fareDiscountNote!.isNotEmpty) 'fareDiscountNote': fareDiscountNote,
      };
}

class Quote {
  Quote({
    required this.fare,
    required this.distanceKm,
    required this.etaMinutes,
    required this.operatorName,
    required this.vehicleType,
    required this.paymentMethod,
    this.riderAvailable,
    this.bookingDispatchMode,
    this.originalFare,
    this.customerFare,
    this.promoApplied,
    this.discountPercent,
    this.promoCode,
    this.hasActivePromos,
    this.customerBoostAmount,
    this.fareDiscountLabel,
    this.fareDiscountPercent,
  });

  final double fare;
  final double distanceKm;
  final int etaMinutes;
  final String operatorName;
  final String vehicleType;
  final String paymentMethod;
  final bool? riderAvailable;
  final String? bookingDispatchMode;
  final double? originalFare;
  final double? customerFare;
  final bool? promoApplied;
  final double? discountPercent;
  final String? promoCode;
  final bool? hasActivePromos;
  final double? customerBoostAmount;
  final String? fareDiscountLabel;
  final double? fareDiscountPercent;

  double get displayFare => customerFare ?? fare;

  factory Quote.fromJson(Map<String, dynamic> json) => Quote(
        fare: asDouble(json['fare']),
        distanceKm: asDouble(json['distanceKm']),
        etaMinutes: asInt(json['etaMinutes']),
        operatorName: asText(json['operatorName']),
        vehicleType: asText(json['vehicleType'], 'Motorcycle'),
        paymentMethod: paymentCode(json['paymentMethod']),
        riderAvailable: json['riderAvailable'] == null ? null : asFlag(json['riderAvailable']),
        bookingDispatchMode: asTextOrNull(json['bookingDispatchMode']),
        originalFare: asDoubleOrNull(json['originalFare']),
        customerFare: asDoubleOrNull(json['customerFare']),
        promoApplied: json['promoApplied'] == null ? null : asFlag(json['promoApplied']),
        discountPercent: asDoubleOrNull(json['discountPercent']),
        promoCode: asTextOrNull(json['promoCode']),
        hasActivePromos: json['hasActivePromos'] == null ? null : asFlag(json['hasActivePromos']),
        customerBoostAmount: asDoubleOrNull(json['customerBoostAmount']),
        fareDiscountLabel: asTextOrNull(json['fareDiscountLabel']),
        fareDiscountPercent: asDoubleOrNull(json['fareDiscountPercent']),
      );
}

class VehicleOffer {
  VehicleOffer({
    required this.id,
    required this.code,
    required this.name,
    required this.iconKey,
    required this.maxPassengers,
    required this.isCargo,
    required this.available,
    required this.vehicleType,
  });

  final String id;
  final String code;
  final String name;
  final String iconKey;
  final int maxPassengers;
  final bool isCargo;
  final bool available;
  final String vehicleType;

  factory VehicleOffer.fromJson(Map<String, dynamic> json) => VehicleOffer(
        id: asText(json['id']),
        code: asText(json['code']),
        name: asText(json['name']),
        iconKey: asText(json['iconKey']),
        maxPassengers: asInt(json['maxPassengers'], 1),
        isCargo: asFlag(json['isCargo']),
        available: asFlag(json['available'], true),
        vehicleType: asText(json['vehicleType'], 'Motorcycle'),
      );
}

class ServiceCheckResult {
  ServiceCheckResult({
    required this.municipalityHasOperator,
    this.municipalityName,
    required this.motorcycleAvailable,
    required this.tricycleAvailable,
    this.vehicles,
  });

  final bool municipalityHasOperator;
  final String? municipalityName;
  final bool motorcycleAvailable;
  final bool tricycleAvailable;
  final List<VehicleOffer>? vehicles;

  factory ServiceCheckResult.fromJson(Map<String, dynamic> json) {
    final rows = json['vehicles'];
    List<VehicleOffer>? vehicles;
    if (rows is List) {
      vehicles = rows
          .map(asJsonMap)
          .whereType<Map<String, dynamic>>()
          .map(VehicleOffer.fromJson)
          .toList();
    }
    return ServiceCheckResult(
      municipalityHasOperator: asFlag(json['municipalityHasOperator']),
      municipalityName: asTextOrNull(json['municipalityName']),
      motorcycleAvailable: asFlag(json['motorcycleAvailable']),
      tricycleAvailable: asFlag(json['tricycleAvailable']),
      vehicles: vehicles,
    );
  }
}

class HailRider {
  HailRider({
    required this.riderId,
    required this.fullName,
    required this.plateNumber,
    required this.vehicleType,
    this.vehicleModel,
    this.photoUrl,
    this.phoneNumber,
    required this.isOnline,
    required this.isBusy,
    required this.companyName,
    required this.paymentMethods,
    this.distanceKm,
  });

  final String riderId;
  final String fullName;
  final String plateNumber;
  final String vehicleType;
  final String? vehicleModel;
  final String? photoUrl;
  final String? phoneNumber;
  final bool isOnline;
  final bool isBusy;
  final String companyName;
  final List<String> paymentMethods;
  final double? distanceKm;

  factory HailRider.fromJson(Map<String, dynamic> json) {
    final methods = json['paymentMethods'];
    return HailRider(
      riderId: asText(json['riderId']),
      fullName: asText(json['fullName']),
      plateNumber: asText(json['plateNumber']),
      vehicleType: asText(json['vehicleType'], 'Motorcycle'),
      vehicleModel: asTextOrNull(json['vehicleModel']),
      photoUrl: asTextOrNull(json['photoUrl']),
      phoneNumber: asTextOrNull(json['phoneNumber']),
      isOnline: asFlag(json['isOnline']),
      isBusy: asFlag(json['isBusy']),
      companyName: asText(json['companyName']),
      paymentMethods: methods is List
          ? methods.map((m) => paymentCode(m)).toList()
          : const ['Cash'],
      distanceKm: asDoubleOrNull(json['distanceKm']),
    );
  }
}

class FavoriteRider {
  FavoriteRider({
    required this.riderId,
    required this.fullName,
    required this.plateNumber,
    required this.vehicleType,
    this.vehicleModel,
    this.photoUrl,
    this.phoneNumber,
    required this.isOnline,
    required this.isBusy,
    required this.canBook,
    required this.companyName,
    required this.paymentMethods,
  });

  final String riderId;
  final String fullName;
  final String plateNumber;
  final String vehicleType;
  final String? vehicleModel;
  final String? photoUrl;
  final String? phoneNumber;
  final bool isOnline;
  final bool isBusy;
  final bool canBook;
  final String companyName;
  final List<String> paymentMethods;

  factory FavoriteRider.fromJson(Map<String, dynamic> json) {
    final methods = json['paymentMethods'] ?? json['PaymentMethods'];
    final online = json['isOnline'] ?? json['IsOnline'];
    final busy = json['isBusy'] ?? json['IsBusy'];
    final bookable = json['canBook'] ?? json['CanBook'];
    return FavoriteRider(
      riderId: asText(json['riderId'] ?? json['RiderId']),
      fullName: asText(json['fullName'] ?? json['FullName']),
      plateNumber: asText(json['plateNumber'] ?? json['PlateNumber']),
      vehicleType: asText(json['vehicleType'] ?? json['VehicleType'], 'Motorcycle'),
      vehicleModel: asTextOrNull(json['vehicleModel'] ?? json['VehicleModel']),
      photoUrl: asTextOrNull(json['photoUrl'] ?? json['PhotoUrl']),
      phoneNumber: asTextOrNull(json['phoneNumber'] ?? json['PhoneNumber']),
      isOnline: asFlag(online),
      isBusy: asFlag(busy),
      // Default false so a missing flag never paints a rider "Online" / bookable by mistake.
      canBook: asFlag(bookable),
      companyName: asText(json['companyName'] ?? json['CompanyName']),
      paymentMethods: methods is List
          ? methods.map((m) => paymentCode(m)).toList()
          : const ['Cash'],
    );
  }
}

class CustomerTrip {
  CustomerTrip({
    required this.id,
    required this.reference,
    required this.status,
    required this.pickup,
    required this.dropoff,
    this.pickupLat,
    this.pickupLng,
    this.dropoffLat,
    this.dropoffLng,
    required this.fare,
    required this.distanceKm,
    this.passengerCount,
    required this.vehicleType,
    required this.paymentMethod,
    this.paymentMethodOther,
    required this.operatorName,
    this.riderName,
    this.riderPhone,
    this.plateNumber,
    this.vehicleModel,
    this.riderPhotoUrl,
    this.riderLat,
    this.riderLng,
    required this.requestedAtUtc,
    this.scheduledAtUtc,
    required this.canCancel,
    required this.canSos,
    this.hailQr,
    this.rating,
    this.ratingComment,
    this.canRate,
    this.canViewChat,
    this.canChat,
    this.customerFare,
    this.promoDiscountAmount,
    this.isPromoSponsored,
    this.discountPercent,
    this.promoCode,
    this.customerBoostAmount,
    this.riderId,
    this.fareDiscountLabel,
    this.fareDiscountAmount,
  });

  final String id;
  final String reference;
  final String status;
  final String pickup;
  final String dropoff;
  final double? pickupLat;
  final double? pickupLng;
  final double? dropoffLat;
  final double? dropoffLng;
  final double fare;
  final double distanceKm;
  final int? passengerCount;
  final String vehicleType;
  final String paymentMethod;
  final String? paymentMethodOther;
  final String operatorName;
  final String? riderName;
  final String? riderPhone;
  final String? plateNumber;
  final String? vehicleModel;
  final String? riderPhotoUrl;
  final double? riderLat;
  final double? riderLng;
  final String requestedAtUtc;
  final String? scheduledAtUtc;
  final bool canCancel;
  final bool canSos;
  final bool? hailQr;
  final double? rating;
  final String? ratingComment;
  final bool? canRate;
  final bool? canViewChat;
  final bool? canChat;
  final double? customerFare;
  final double? promoDiscountAmount;
  final bool? isPromoSponsored;
  final double? discountPercent;
  final String? promoCode;
  final double? customerBoostAmount;
  final String? riderId;
  final String? fareDiscountLabel;
  final double? fareDiscountAmount;

  bool get isActive => status == 'Pending' || status == 'Waiting' || status == 'Ongoing';

  factory CustomerTrip.fromJson(Map<String, dynamic> json) => CustomerTrip(
        id: asText(json['id']),
        reference: asText(json['reference']),
        status: tripStatusCode(json['status']),
        pickup: asText(json['pickup']),
        dropoff: asText(json['dropoff']),
        pickupLat: asDoubleOrNull(json['pickupLat']),
        pickupLng: asDoubleOrNull(json['pickupLng']),
        dropoffLat: asDoubleOrNull(json['dropoffLat']),
        dropoffLng: asDoubleOrNull(json['dropoffLng']),
        fare: asDouble(json['fare']),
        distanceKm: asDouble(json['distanceKm']),
        passengerCount: json['passengerCount'] == null ? null : asInt(json['passengerCount'], 1),
        vehicleType: asText(json['vehicleType'], 'Motorcycle'),
        paymentMethod: paymentCode(json['paymentMethod']),
        paymentMethodOther: asTextOrNull(json['paymentMethodOther']),
        operatorName: asText(json['operatorName']),
        riderName: asTextOrNull(json['riderName']),
        riderPhone: asTextOrNull(json['riderPhone']),
        plateNumber: asTextOrNull(json['plateNumber']),
        vehicleModel: asTextOrNull(json['vehicleModel']),
        riderPhotoUrl: asTextOrNull(json['riderPhotoUrl']),
        riderLat: asDoubleOrNull(json['riderLat']),
        riderLng: asDoubleOrNull(json['riderLng']),
        requestedAtUtc: asText(json['requestedAtUtc']),
        scheduledAtUtc: asTextOrNull(json['scheduledAtUtc']),
        canCancel: asFlag(json['canCancel']),
        canSos: asFlag(json['canSos']),
        hailQr: json['hailQr'] == null ? null : asFlag(json['hailQr']),
        rating: asDoubleOrNull(json['rating']),
        ratingComment: asTextOrNull(json['ratingComment']),
        canRate: json['canRate'] == null ? null : asFlag(json['canRate']),
        canViewChat: json['canViewChat'] == null ? null : asFlag(json['canViewChat']),
        canChat: json['canChat'] == null ? null : asFlag(json['canChat']),
        customerFare: asDoubleOrNull(json['customerFare']),
        promoDiscountAmount: asDoubleOrNull(json['promoDiscountAmount']),
        isPromoSponsored: json['isPromoSponsored'] == null ? null : asFlag(json['isPromoSponsored']),
        discountPercent: asDoubleOrNull(json['discountPercent']),
        promoCode: asTextOrNull(json['promoCode']),
        customerBoostAmount: asDoubleOrNull(json['customerBoostAmount']),
        riderId: asTextOrNull(json['riderId']),
        fareDiscountLabel: asTextOrNull(json['fareDiscountLabel']),
        fareDiscountAmount: asDoubleOrNull(json['fareDiscountAmount']),
      );
}

class DeskPlace {
  DeskPlace({
    required this.barangayId,
    required this.label,
    required this.details,
    required this.barangay,
    required this.municipality,
    required this.lat,
    required this.lng,
  });

  final String barangayId;
  final String label;
  final String details;
  final String barangay;
  final String municipality;
  final double lat;
  final double lng;

  factory DeskPlace.fromJson(Map<String, dynamic> json) => DeskPlace(
        barangayId: asText(json['barangayId']),
        label: asText(json['label']),
        details: asText(json['details']),
        barangay: asText(json['barangay']),
        municipality: asText(json['municipality']),
        lat: asDouble(json['lat']),
        lng: asDouble(json['lng']),
      );
}

class Desk {
  Desk({
    required this.customerId,
    required this.fullName,
    required this.firstName,
    required this.lastName,
    required this.phoneNumber,
    this.email,
    this.gender,
    required this.hasPin,
    required this.deleteStatus,
    this.activeTrip,
    required this.scheduled,
    required this.recent,
    required this.places,
    this.mapLat,
    this.mapLng,
    this.hailedRider,
    this.pendingRating,
    this.needsMobile,
    this.photoUrl,
  });

  final String customerId;
  final String fullName;
  final String firstName;
  final String lastName;
  final String phoneNumber;
  final String? email;
  final String? gender;
  final bool hasPin;
  final String deleteStatus;
  final CustomerTrip? activeTrip;
  final List<CustomerTrip> scheduled;
  final List<CustomerTrip> recent;
  final List<DeskPlace> places;
  final double? mapLat;
  final double? mapLng;
  final HailRider? hailedRider;
  final CustomerTrip? pendingRating;
  final bool? needsMobile;
  final String? photoUrl;

  factory Desk.fromJson(Map<String, dynamic> json) {
    List<CustomerTrip> trips(dynamic value) {
      if (value is! List) return const [];
      return value
          .map(asJsonMap)
          .whereType<Map<String, dynamic>>()
          .map(CustomerTrip.fromJson)
          .toList();
    }

    List<DeskPlace> places(dynamic value) {
      if (value is! List) return const [];
      return value.map(asJsonMap).whereType<Map<String, dynamic>>().map(DeskPlace.fromJson).toList();
    }

    final hailed = asJsonMap(json['hailedRider']);
    final active = asJsonMap(json['activeTrip']);
    final pending = asJsonMap(json['pendingRating']);

    return Desk(
      customerId: asText(json['customerId']),
      fullName: asText(json['fullName']),
      firstName: asText(json['firstName']),
      lastName: asText(json['lastName']),
      phoneNumber: asText(json['phoneNumber']),
      email: asTextOrNull(json['email']),
      gender: asTextOrNull(json['gender']),
      hasPin: asFlag(json['hasPin']),
      deleteStatus: asText(json['deleteStatus'], 'None'),
      activeTrip: active == null ? null : CustomerTrip.fromJson(active),
      scheduled: trips(json['scheduled']),
      recent: trips(json['recent']),
      places: places(json['places']),
      mapLat: asDoubleOrNull(json['mapLat']),
      mapLng: asDoubleOrNull(json['mapLng']),
      hailedRider: hailed == null ? null : HailRider.fromJson(hailed),
      pendingRating: pending == null ? null : CustomerTrip.fromJson(pending),
      needsMobile: json['needsMobile'] == null ? null : asFlag(json['needsMobile']),
      photoUrl: asTextOrNull(json['photoUrl']),
    );
  }
}

class RideStop {
  RideStop({
    required this.details,
    required this.barangay,
    required this.municipality,
    required this.province,
    required this.fullAddress,
  });

  final String details;
  final String barangay;
  final String municipality;
  final String province;
  final String fullAddress;

  factory RideStop.fromJson(Map<String, dynamic> json) => RideStop(
        details: asText(json['details']),
        barangay: asText(json['barangay']),
        municipality: asText(json['municipality']),
        province: asText(json['province']),
        fullAddress: asText(json['fullAddress']),
      );
}

class CustomerTripDetail {
  CustomerTripDetail({
    required this.id,
    required this.reference,
    required this.status,
    required this.pickup,
    required this.dropoff,
    required this.fare,
    required this.distanceKm,
    required this.vehicleType,
    required this.paymentMethod,
    required this.operatorName,
    required this.riderName,
    required this.riderPhone,
    required this.plateNumber,
    required this.chat,
    this.notes,
    this.passengerCount,
    this.durationMinutes,
    this.requestedAtUtc,
    this.scheduledAtUtc,
    this.completedAtUtc,
    this.cancelledAtUtc,
    this.cancelReason,
    this.rating,
    this.ratingComment,
    this.paymentMethodOther,
    this.operatorPhone,
    this.vehicleModel,
    this.riderId,
    this.riderPhotoUrl,
    this.customerFare,
    this.promoCode,
    this.fareDiscountLabel,
    this.fareDiscountAmount,
    this.pickupStop,
    this.dropoffStop,
  });

  final String id;
  final String reference;
  final String status;
  final String pickup;
  final String dropoff;
  final String? notes;
  final double fare;
  final double distanceKm;
  final int? passengerCount;
  final int? durationMinutes;
  final String vehicleType;
  final String? requestedAtUtc;
  final String? scheduledAtUtc;
  final String? completedAtUtc;
  final String? cancelledAtUtc;
  final String? cancelReason;
  final double? rating;
  final String? ratingComment;
  final String paymentMethod;
  final String? paymentMethodOther;
  final String operatorName;
  final String? operatorPhone;
  final String? riderId;
  final String riderName;
  final String riderPhone;
  final String plateNumber;
  final String? vehicleModel;
  final String? riderPhotoUrl;
  final List<ChatMessage> chat;
  final double? customerFare;
  final String? promoCode;
  final String? fareDiscountLabel;
  final double? fareDiscountAmount;
  final RideStop? pickupStop;
  final RideStop? dropoffStop;

  double get displayFare => customerFare ?? fare;

  factory CustomerTripDetail.fromJson(Map<String, dynamic> json) {
    final pickupStop = asJsonMap(json['pickupStop']);
    final dropoffStop = asJsonMap(json['dropoffStop']);
    final chatRaw = json['chat'];
    return CustomerTripDetail(
      id: asText(json['id']),
      reference: asText(json['reference']),
      status: tripStatusCode(json['status']),
      pickup: asText(json['pickup']),
      dropoff: asText(json['dropoff']),
      notes: asTextOrNull(json['notes']),
      fare: asDouble(json['fare']),
      distanceKm: asDouble(json['distanceKm']),
      passengerCount: json['passengerCount'] == null ? null : asInt(json['passengerCount'], 1),
      durationMinutes: json['durationMinutes'] == null ? null : asInt(json['durationMinutes']),
      vehicleType: asText(json['vehicleType'], 'Motorcycle'),
      requestedAtUtc: asTextOrNull(json['requestedAtUtc']),
      scheduledAtUtc: asTextOrNull(json['scheduledAtUtc']),
      completedAtUtc: asTextOrNull(json['completedAtUtc']),
      cancelledAtUtc: asTextOrNull(json['cancelledAtUtc']),
      cancelReason: asTextOrNull(json['cancelReason']),
      rating: asDoubleOrNull(json['rating']),
      ratingComment: asTextOrNull(json['ratingComment']),
      paymentMethod: paymentCode(json['paymentMethod']),
      paymentMethodOther: asTextOrNull(json['paymentMethodOther']),
      operatorName: asText(json['operatorName']),
      operatorPhone: asTextOrNull(json['operatorPhone']),
      riderId: asTextOrNull(json['riderId']),
      riderName: asText(json['riderName']),
      riderPhone: asText(json['riderPhone']),
      plateNumber: asText(json['plateNumber']),
      vehicleModel: asTextOrNull(json['vehicleModel']),
      riderPhotoUrl: asTextOrNull(json['riderPhotoUrl']),
      chat: chatRaw is List
          ? chatRaw.map(asJsonMap).whereType<Map<String, dynamic>>().map(ChatMessage.fromJson).toList()
          : const [],
      customerFare: asDoubleOrNull(json['customerFare']),
      promoCode: asTextOrNull(json['promoCode']),
      fareDiscountLabel: asTextOrNull(json['fareDiscountLabel']),
      fareDiscountAmount: asDoubleOrNull(json['fareDiscountAmount']),
      pickupStop: pickupStop == null ? null : RideStop.fromJson(pickupStop),
      dropoffStop: dropoffStop == null ? null : RideStop.fromJson(dropoffStop),
    );
  }
}

dynamic _pickJson(Map<String, dynamic> json, String key) {
  if (json.containsKey(key)) return json[key];
  if (key.isEmpty) return null;
  final pascal = '${key[0].toUpperCase()}${key.substring(1)}';
  return json[pascal];
}

class ChatMessage {
  ChatMessage({
    required this.id,
    required this.sender,
    required this.body,
    required this.sentAtUtc,
    this.photoUrl,
  });

  final String id;
  final String sender;
  final String body;
  final String sentAtUtc;
  final String? photoUrl;

  bool get fromRider {
    final value = sender.toLowerCase();
    return value == 'rider' || value == '2';
  }

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
        id: asText(_pickJson(json, 'id')),
        sender: asText(_pickJson(json, 'sender')),
        body: asText(_pickJson(json, 'body')),
        sentAtUtc: asText(_pickJson(json, 'sentAtUtc')),
        photoUrl: asTextOrNull(_pickJson(json, 'photoUrl')),
      );
}

String encodeJson(Map<String, dynamic> body) => jsonEncode(body);
