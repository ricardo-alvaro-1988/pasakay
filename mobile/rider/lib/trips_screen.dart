import 'package:flutter/material.dart';

import 'api.dart';
import 'models.dart';
import 'session.dart';
import 'theme.dart';

class TripsScreen extends StatefulWidget {
  const TripsScreen({super.key, required this.session});

  final RiderSession session;

  @override
  State<TripsScreen> createState() => _TripsScreenState();
}

class _TripsScreenState extends State<TripsScreen> {
  List<RiderTripListItem> _trips = const [];
  String? _error;
  bool _loading = true;
  String _filter = 'All';

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final trips = await widget.session.api.trips();
      if (mounted) {
        setState(() => _trips = trips);
      }
    } on ApiException catch (ex) {
      if (mounted) {
        setState(() => _error = ex.message);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Could not load bookings.');
      }
    } finally {
      if (mounted) {
        setState(() => _loading = false);
      }
    }
  }

  List<RiderTripListItem> get _visible {
    if (_filter == 'All') {
      return _trips;
    }
    return _trips.where((trip) => trip.status == _filter).toList();
  }

  @override
  Widget build(BuildContext context) {
    final rows = _visible;
    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: 'Back',
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text('Trips'),
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: brandRed))
          : RefreshIndicator(
              color: brandRed,
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                children: [
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      for (final item in const ['All', 'Waiting', 'Ongoing', 'Completed', 'Cancelled'])
                        ChoiceChip(
                          label: Text(item),
                          selected: _filter == item,
                          onSelected: (_) => setState(() => _filter = item),
                        ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Text(_error!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
                    ),
                  if (rows.isEmpty)
                    const BrandPanel(
                      child: Text(
                        'No bookings yet.',
                        style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                      ),
                    )
                  else
                    ...rows.map(
                      (trip) => Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: InkWell(
                          borderRadius: BorderRadius.circular(22),
                          onTap: () => Navigator.push<void>(
                            context,
                            MaterialPageRoute(
                              builder: (_) => BookingDetailPage(session: widget.session, tripId: trip.id),
                            ),
                          ),
                          child: BrandPanel(
                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    Expanded(
                                      child: Text(
                                        'BOOKING : ${trip.reference}',
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 15),
                                      ),
                                    ),
                                    Text(_dateChip(trip.requestedAt), style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
                                  ],
                                ),
                                const SizedBox(height: 6),
                                Row(
                                  children: [
                                    Expanded(
                                      child: Text(
                                        '${trip.status} · ${peso(trip.fare)} · ${trip.customerName}',
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                        style: TextStyle(
                                          fontWeight: FontWeight.w800,
                                          color: _statusColor(trip.status),
                                        ),
                                      ),
                                    ),
                                    Text(_timeChip(trip.requestedAt), style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
                                  ],
                                ),
                                const SizedBox(height: 6),
                                Text(trip.pickup, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w600)),
                                Text(trip.dropoff, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600)),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
    );
  }
}

class BookingDetailPage extends StatefulWidget {
  const BookingDetailPage({super.key, required this.session, required this.tripId});

  final RiderSession session;
  final String tripId;

  @override
  State<BookingDetailPage> createState() => _BookingDetailPageState();
}

class _BookingDetailPageState extends State<BookingDetailPage> {
  RiderTripDetail? _detail;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    try {
      final detail = await widget.session.api.tripDetail(widget.tripId);
      if (mounted) {
        setState(() => _detail = detail);
      }
    } on ApiException catch (ex) {
      if (mounted) {
        setState(() => _error = ex.message);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Could not load booking details.');
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final detail = _detail;
    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: 'Back',
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text('Booking details'),
      ),
      body: detail == null
          ? Center(
              child: _error != null
                  ? Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(_error!, textAlign: TextAlign.center, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
                          const SizedBox(height: 16),
                          OutlinedButton(
                            onPressed: () => Navigator.pop(context),
                            child: const Text('Back'),
                          ),
                        ],
                      ),
                    )
                  : const CircularProgressIndicator(color: brandRed),
            )
          : ListView(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
              children: [
                BrandPanel(
                  padding: const EdgeInsets.all(16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          Expanded(
                            child: Text(
                              'BOOKING : ${detail.reference}',
                              style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 17),
                            ),
                          ),
                          Text(_dateChip(detail.requestedAt), style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
                        ],
                      ),
                      const SizedBox(height: 6),
                      Text(
                        tripStatusLabel(detail.status),
                        style: TextStyle(fontWeight: FontWeight.w800, color: _statusColor(detail.status)),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                BrandPanel(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _row('Customer', detail.customerName),
                      _row('Phone', detail.customerPhone),
                      _row('Persons', passengerLabel(detail.passengerCount)),
                      _row('Vehicle', [detail.vehicleType, detail.vehicleModel, detail.plateNumber].where((x) => (x ?? '').toString().isNotEmpty).join(' · ')),
                      _row('Payment', paymentLabel(detail.paymentMethod)),
                      _row('Fare', peso(detail.fare)),
                      _row('Distance', '${detail.distanceKm.toStringAsFixed(1)} km'),
                      _row('Requested', _dateTime(detail.requestedAt)),
                      if (detail.completedAt != null) _row('Completed', _dateTime(detail.completedAt)),
                      if (detail.cancelledAt != null) _row('Cancelled', _dateTime(detail.cancelledAt)),
                      if ((detail.cancelReason ?? '').trim().isNotEmpty) _row('Cancel reason', detail.cancelReason!),
                      if (detail.rating != null) _row('Rating', '${detail.rating}/5'),
                      if ((detail.ratingComment ?? '').trim().isNotEmpty) _row('Review', detail.ratingComment!),
                      if ((detail.notes ?? '').trim().isNotEmpty) _row('Notes', detail.notes!),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                BrandPanel(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Pickup', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
                      const SizedBox(height: 4),
                      Text(detail.pickup, style: const TextStyle(fontWeight: FontWeight.w700)),
                      const SizedBox(height: 14),
                      const Text('Drop-off', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
                      const SizedBox(height: 4),
                      Text(detail.dropoff, style: const TextStyle(fontWeight: FontWeight.w700)),
                    ],
                  ),
                ),
                const SizedBox(height: 12),
                BrandPanel(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text('Chat', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                      const SizedBox(height: 10),
                      if (detail.chat.isEmpty)
                        const Text('No chat messages on this booking.', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600))
                      else
                        ...detail.chat.map((msg) {
                          final mine = msg.fromRider;
                          final photo = widget.session.api.mediaUrl(msg.photoUrl);
                          return Align(
                            alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
                            child: Container(
                              margin: const EdgeInsets.only(bottom: 8),
                              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                              constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.72),
                              decoration: BoxDecoration(
                                color: mine ? const Color(0xFFFFE5E7) : brandSurface,
                                borderRadius: BorderRadius.circular(14),
                                border: Border.all(color: mine ? const Color(0xFFFFC9CD) : brandLine),
                              ),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  if (photo != null) ...[
                                    ClipRRect(
                                      borderRadius: BorderRadius.circular(12),
                                      child: ConstrainedBox(
                                        constraints: const BoxConstraints(maxHeight: 220),
                                        child: Image.network(photo, fit: BoxFit.cover),
                                      ),
                                    ),
                                    if (msg.body.trim().isNotEmpty) const SizedBox(height: 8),
                                  ],
                                  if (msg.body.trim().isNotEmpty) Text(msg.body, style: const TextStyle(fontWeight: FontWeight.w600)),
                                  if (msg.sentAt != null) ...[
                                    const SizedBox(height: 4),
                                    Text(_dateTime(msg.sentAt), style: const TextStyle(fontSize: 11, color: brandMuted)),
                                  ],
                                ],
                              ),
                            ),
                          );
                        }),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                OutlinedButton.icon(
                  onPressed: () => Navigator.pop(context),
                  icon: const Icon(Icons.arrow_back),
                  label: const Text('Back to trips'),
                ),
              ],
            ),
    );
  }
}

Widget _row(String label, Object? value) {
  final text = (value ?? '').toString().trim();
  if (text.isEmpty) {
    return const SizedBox.shrink();
  }
  return Padding(
    padding: const EdgeInsets.only(bottom: 10),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
        const SizedBox(height: 2),
        Text(text, style: const TextStyle(fontWeight: FontWeight.w700)),
      ],
    ),
  );
}

Color _statusColor(String status) {
  switch (tripStatusLabel(status)) {
    case 'Completed':
      return brandSuccess;
    case 'Cancelled':
      return brandRed;
    case 'Ongoing':
    case 'Waiting':
      return brandRed;
    default:
      return brandMuted;
  }
}

String _dateChip(DateTime? value) {
  if (value == null) return '';
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  return '${months[value.month - 1]} ${value.day}, ${value.year.toString().substring(2)}';
}

String _timeChip(DateTime? value) {
  if (value == null) return '';
  final hour = value.hour == 0 ? 12 : value.hour > 12 ? value.hour - 12 : value.hour;
  final suffix = value.hour >= 12 ? 'PM' : 'AM';
  return '$hour:${value.minute.toString().padLeft(2, '0')} $suffix';
}

String _dateTime(DateTime? value) {
  if (value == null) return '';
  return '${_dateChip(value)} · ${_timeChip(value)}';
}
