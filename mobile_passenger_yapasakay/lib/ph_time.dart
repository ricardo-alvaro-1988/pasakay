import 'package:intl/intl.dart';

const _manilaOffset = Duration(hours: 8);

DateTime utcFromManilaWallClock(DateTime wall) {
  return DateTime.utc(wall.year, wall.month, wall.day, wall.hour, wall.minute, wall.second)
      .subtract(_manilaOffset);
}

String scheduledAtUtcIsoFromManila(DateTime wall) {
  return utcFromManilaWallClock(wall).toIso8601String();
}

/// Parse API *Utc stamps as UTC even when the trailing Z is missing (same as web `phWhen`).
DateTime parseApiUtc(String iso) {
  final raw = iso.trim();
  if (raw.isEmpty) return DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
  final hasZone = RegExp(r'[zZ]$|[+-]\d{2}:?\d{2}$').hasMatch(raw);
  if (hasZone) return DateTime.parse(raw).toUtc();
  final normalized = raw.contains('T') ? '${raw}Z' : '${raw}T00:00:00Z';
  return DateTime.parse(normalized).toUtc();
}

DateTime manilaWallClockFromUtcString(String iso) {
  return parseApiUtc(iso).add(_manilaOffset);
}

/// Philippine time, e.g. `Sep 5, 2026 : 10:00 PM`.
String phWhen(String? value) {
  if (value == null || value.trim().isEmpty) return '—';
  final manila = manilaWallClockFromUtcString(value);
  return DateFormat('MMM d, yyyy : h:mm a').format(manila);
}

DateTime defaultRentalWhenManila() {
  return DateTime.now().toUtc().add(_manilaOffset).add(const Duration(hours: 1));
}

DateTime minRentalWhenManila() {
  return DateTime.now().toUtc().add(_manilaOffset).add(const Duration(minutes: 10));
}
