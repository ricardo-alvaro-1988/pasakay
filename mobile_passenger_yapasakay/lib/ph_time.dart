import 'package:intl/intl.dart';

const _manilaOffset = Duration(hours: 8);

DateTime utcFromManilaWallClock(DateTime wall) {
  return DateTime.utc(wall.year, wall.month, wall.day, wall.hour, wall.minute, wall.second)
      .subtract(_manilaOffset);
}

String scheduledAtUtcIsoFromManila(DateTime wall) {
  return utcFromManilaWallClock(wall).toIso8601String();
}

DateTime manilaWallClockFromUtcString(String iso) {
  final utc = DateTime.parse(iso).toUtc();
  return utc.add(_manilaOffset);
}

String phWhen(String? value) {
  if (value == null || value.trim().isEmpty) return '—';
  final manila = manilaWallClockFromUtcString(value);
  return DateFormat('MMM d, yyyy · h:mm a').format(manila);
}

DateTime defaultRentalWhenManila() {
  return DateTime.now().toUtc().add(_manilaOffset).add(const Duration(hours: 1));
}

DateTime minRentalWhenManila() {
  return DateTime.now().toUtc().add(_manilaOffset).add(const Duration(minutes: 10));
}
