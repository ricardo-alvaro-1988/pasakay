import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api.dart';
import 'models.dart';
import 'ph_time.dart';
import 'rate_dialog.dart';
import 'session.dart';
import 'theme.dart';

class BookingScreen extends StatefulWidget {
  const BookingScreen({super.key, required this.session});

  final CustomerSession session;

  @override
  State<BookingScreen> createState() => _BookingScreenState();
}

class _BookingScreenState extends State<BookingScreen> {
  bool _busy = false;
  String? _error;
  String? _openHistoryId;
  final Set<String> _favoriteIds = {};
  final Map<String, CustomerTripDetail> _details = {};
  final Set<String> _loadingDetail = {};

  @override
  void initState() {
    super.initState();
    widget.session.addListener(_onDesk);
    _loadFavorites();
  }

  @override
  void dispose() {
    widget.session.removeListener(_onDesk);
    super.dispose();
  }

  void _onDesk() {
    if (mounted) setState(() {});
  }

  Future<void> _loadFavorites() async {
    try {
      final rows = await widget.session.api.favorites();
      if (!mounted) return;
      setState(() {
        _favoriteIds
          ..clear()
          ..addAll(rows.map((r) => r.riderId));
      });
    } catch (_) {}
  }

  Future<void> _cancel(CustomerTrip trip) async {
    final ok = await showPassengerConfirm(
      context,
      title: 'Cancel trip?',
      message: 'Cancel ${trip.reference}?',
      cancelLabel: 'No',
      confirmLabel: 'Cancel trip',
      confirmColor: brandSos,
    );
    if (ok != true || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.session.cancelTrip(trip.id);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Booking cancelled.')));
      }
    } on ApiException catch (ex) {
      setState(() => _error = ex.message);
    } catch (_) {
      setState(() => _error = 'Could not cancel.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _rate(CustomerTrip trip) async {
    final result = await showRateTripDialog(context);
    if (result == null || !mounted) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final desk = await widget.session.api.rate(trip.id, result.$1, comment: result.$2);
      widget.session.updateDesk(desk);
      await widget.session.refreshDesk(silent: true);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Thanks for your rating.')),
        );
      }
    } on ApiException catch (ex) {
      setState(() => _error = ex.message);
    } catch (_) {
      setState(() => _error = 'Could not submit rating.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _toggleHistory(CustomerTrip trip) async {
    final open = _openHistoryId == trip.id;
    setState(() => _openHistoryId = open ? null : trip.id);
    if (open || _details.containsKey(trip.id) || _loadingDetail.contains(trip.id)) return;
    setState(() => _loadingDetail.add(trip.id));
    try {
      final detail = await widget.session.api.tripDetail(trip.id);
      if (!mounted) return;
      setState(() => _details[trip.id] = detail);
    } on ApiException catch (ex) {
      if (mounted) setState(() => _error = ex.message);
    } catch (_) {
      if (mounted) setState(() => _error = 'Could not load booking details.');
    } finally {
      if (mounted) setState(() => _loadingDetail.remove(trip.id));
    }
  }

  Future<void> _toggleFavorite(String riderId) async {
    try {
      if (_favoriteIds.contains(riderId)) {
        await widget.session.api.removeFavorite(riderId);
        setState(() => _favoriteIds.remove(riderId));
      } else {
        await widget.session.api.addFavorite(riderId);
        setState(() => _favoriteIds.add(riderId));
      }
    } on ApiException catch (ex) {
      if (mounted) setState(() => _error = ex.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final desk = widget.session.desk;
    if (desk == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    final history = desk.recent.where((t) => t.status == 'Completed' || t.status == 'Cancelled').toList();
    final needsRating = desk.pendingRating;

    return Scaffold(
      appBar: AppBar(title: const Text('Booking')),
      body: _busy
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: () async {
                await widget.session.refreshDesk();
                await _loadFavorites();
              },
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  if (_error != null) ...[
                    Text(_error!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
                    const SizedBox(height: 12),
                  ],
                  if (needsRating != null) ...[
                    Builder(
                      builder: (context) {
                        final rateTrip = needsRating!;
                        return BrandPanel(
                          color: brandWarnBg,
                          borderColor: brandWarnLine,
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Text('Rate your ride', style: Theme.of(context).textTheme.titleMedium),
                              const SizedBox(height: 4),
                              Text(
                                '${rateTrip.reference} · ${rateTrip.pickup} → ${rateTrip.dropoff}',
                                style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                              ),
                              const SizedBox(height: 10),
                              FilledButton(
                                onPressed: () => _rate(rateTrip),
                                child: const Text('Rate ride'),
                              ),
                            ],
                          ),
                        );
                      },
                    ),
                    const SizedBox(height: 16),
                  ],
                  Text('Active booking', style: Theme.of(context).textTheme.labelLarge),
                  const SizedBox(height: 8),
                  if (desk.activeTrip == null)
                    const Text('No active ride right now.', style: TextStyle(color: brandMuted))
                  else
                    _TripCard(
                      trip: desk.activeTrip!,
                      onCancel: desk.activeTrip!.canCancel ? () => _cancel(desk.activeTrip!) : null,
                    ),
                  const SizedBox(height: 20),
                  Text('Scheduled', style: Theme.of(context).textTheme.labelLarge),
                  const SizedBox(height: 8),
                  if (desk.scheduled.isEmpty)
                    const Text('No upcoming scheduled rides.', style: TextStyle(color: brandMuted))
                  else
                    ...desk.scheduled.map(
                      (t) => _TripCard(
                        trip: t,
                        onCancel: t.canCancel ? () => _cancel(t) : null,
                      ),
                    ),
                  const SizedBox(height: 20),
                  Text('History', style: Theme.of(context).textTheme.labelLarge),
                  const SizedBox(height: 8),
                  if (history.isEmpty)
                    const Text('Completed and cancelled trips will show here.', style: TextStyle(color: brandMuted))
                  else
                    ...history.map((t) {
                      final open = _openHistoryId == t.id;
                      final detail = _details[t.id];
                      final loading = _loadingDetail.contains(t.id);
                      final riderId = t.riderId ?? detail?.riderId;
                      return _HistoryCard(
                        trip: t,
                        open: open,
                        detail: detail,
                        loading: loading,
                        isFavorite: riderId != null && _favoriteIds.contains(riderId),
                        onToggle: () => _toggleHistory(t),
                        onRate: t.canRate == true ? () => _rate(t) : null,
                        onFavorite: riderId != null && t.status == 'Completed'
                            ? () => _toggleFavorite(riderId)
                            : null,
                      );
                    }),
                ],
              ),
            ),
    );
  }
}

class _TripCard extends StatelessWidget {
  const _TripCard({required this.trip, this.onCancel});

  final CustomerTrip trip;
  final VoidCallback? onCancel;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: BrandPanel(child: _TripBody(trip: trip, onCancel: onCancel)),
    );
  }
}

class _HistoryCard extends StatelessWidget {
  const _HistoryCard({
    required this.trip,
    required this.open,
    required this.onToggle,
    this.detail,
    this.loading = false,
    this.onRate,
    this.onFavorite,
    this.isFavorite = false,
  });

  final CustomerTrip trip;
  final bool open;
  final VoidCallback onToggle;
  final CustomerTripDetail? detail;
  final bool loading;
  final VoidCallback? onRate;
  final VoidCallback? onFavorite;
  final bool isFavorite;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: BrandPanel(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            InkWell(
              onTap: onToggle,
              child: Row(
                children: [
                  Expanded(child: _TripSummary(trip: trip)),
                  Icon(open ? Icons.expand_less : Icons.expand_more, color: brandMuted),
                ],
              ),
            ),
            if (open) ...[
              const SizedBox(height: 10),
              if (loading && detail == null)
                const Padding(
                  padding: EdgeInsets.symmetric(vertical: 8),
                  child: Center(child: CircularProgressIndicator(strokeWidth: 2)),
                )
              else if (detail != null)
                _DetailBody(detail: detail!)
              else
                _TripBody(trip: trip),
              if (trip.rating != null) ...[
                const SizedBox(height: 8),
                Text(
                  'Your rating: ${trip.rating!.toStringAsFixed(0)}★'
                  '${(trip.ratingComment ?? '').isEmpty ? '' : ' · ${trip.ratingComment}'}',
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
              ],
              if (onRate != null || onFavorite != null) ...[
                const SizedBox(height: 10),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    if (onRate != null)
                      FilledButton(onPressed: onRate, child: const Text('Rate ride')),
                    if (onFavorite != null)
                      OutlinedButton(
                        onPressed: onFavorite,
                        child: Text(isFavorite ? '★ Favorited' : '☆ Add to Favs'),
                      ),
                  ],
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

class _TripSummary extends StatelessWidget {
  const _TripSummary({required this.trip});

  final CustomerTrip trip;

  @override
  Widget build(BuildContext context) {
    final when = trip.scheduledAtUtc ?? trip.requestedAtUtc;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(tripHeadline(trip.status), style: Theme.of(context).textTheme.titleMedium),
            ),
            Text(peso(trip.customerFare ?? trip.fare), style: const TextStyle(fontWeight: FontWeight.w800)),
          ],
        ),
        Text(trip.reference, style: const TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600)),
        Text(phWhen(when), style: const TextStyle(color: brandMuted, fontSize: 12)),
      ],
    );
  }
}

class _TripBody extends StatelessWidget {
  const _TripBody({required this.trip, this.onCancel});

  final CustomerTrip trip;
  final VoidCallback? onCancel;

  @override
  Widget build(BuildContext context) {
    final when = trip.scheduledAtUtc ?? trip.requestedAtUtc;
    final meta = [
      if (trip.distanceKm > 0) '${trip.distanceKm.toStringAsFixed(1)} km',
      if (trip.passengerCount != null) '${trip.passengerCount} passenger${trip.passengerCount == 1 ? '' : 's'}',
      vehicleLabel(trip.vehicleType),
      paymentLabel(trip.paymentMethod, trip.paymentMethodOther),
    ].join(' · ');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _TripSummary(trip: trip),
        const SizedBox(height: 8),
        Text(trip.pickup, style: const TextStyle(fontWeight: FontWeight.w600)),
        const Text('↓', style: TextStyle(color: brandMuted)),
        Text(trip.dropoff, style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 6),
        Text(meta, style: const TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600)),
        if ((trip.operatorName).isNotEmpty)
          Text('Operator · ${trip.operatorName}', style: const TextStyle(color: brandMuted, fontSize: 12)),
        if ((trip.promoCode ?? '').isNotEmpty || (trip.fareDiscountLabel ?? '').isNotEmpty)
          Text(
            [
              if ((trip.promoCode ?? '').isNotEmpty) 'Promo ${trip.promoCode}',
              if ((trip.fareDiscountLabel ?? '').isNotEmpty) trip.fareDiscountLabel!,
            ].join(' · '),
            style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700, fontSize: 12),
          ),
        if ((trip.riderName ?? '').isNotEmpty) ...[
          const SizedBox(height: 6),
          Text(
            [
              trip.riderName!,
              if ((trip.plateNumber ?? '').isNotEmpty) trip.plateNumber!,
              if ((trip.riderPhone ?? '').isNotEmpty) trip.riderPhone!,
            ].join(' · '),
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
          if ((trip.riderPhone ?? '').isNotEmpty)
            TextButton.icon(
              onPressed: () => launchUrl(Uri(scheme: 'tel', path: trip.riderPhone)),
              icon: const Icon(Icons.call, size: 18),
              label: Text(trip.riderPhone!),
            ),
        ],
        Text(phWhen(when), style: const TextStyle(color: brandMuted, fontSize: 12)),
        if (onCancel != null) ...[
          const SizedBox(height: 10),
          OutlinedButton(
            style: OutlinedButton.styleFrom(foregroundColor: brandSos),
            onPressed: onCancel,
            child: const Text('Cancel'),
          ),
        ] else if (trip.status == 'Ongoing') ...[
          const SizedBox(height: 8),
          const Text(
            'Trip is ongoing. Your rider will finish the ride when you arrive.',
            style: TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600),
          ),
        ],
      ],
    );
  }
}

class _DetailBody extends StatelessWidget {
  const _DetailBody({required this.detail});

  final CustomerTripDetail detail;

  @override
  Widget build(BuildContext context) {
    final meta = [
      if (detail.distanceKm > 0) '${detail.distanceKm.toStringAsFixed(1)} km',
      if (detail.passengerCount != null) '${detail.passengerCount} pax',
      vehicleLabel(detail.vehicleType),
      paymentLabel(detail.paymentMethod, detail.paymentMethodOther),
    ].join(' · ');
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(detail.pickup, style: const TextStyle(fontWeight: FontWeight.w600)),
        const Text('↓', style: TextStyle(color: brandMuted)),
        Text(detail.dropoff, style: const TextStyle(fontWeight: FontWeight.w600)),
        const SizedBox(height: 8),
        Text(meta, style: const TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600)),
        Text(
          '${peso(detail.displayFare)}${detail.fareDiscountLabel == null ? '' : ' · ${detail.fareDiscountLabel}'}',
          style: const TextStyle(fontWeight: FontWeight.w800),
        ),
        if (detail.operatorName.isNotEmpty)
          Text('Operator · ${detail.operatorName}', style: const TextStyle(color: brandMuted, fontSize: 12)),
        if (detail.riderName.isNotEmpty) ...[
          const SizedBox(height: 6),
          Text(
            [
              detail.riderName,
              if (detail.plateNumber.isNotEmpty) detail.plateNumber,
              if (detail.vehicleModel != null && detail.vehicleModel!.isNotEmpty) detail.vehicleModel!,
            ].join(' · '),
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
          if (detail.riderPhone.isNotEmpty)
            TextButton.icon(
              onPressed: () => launchUrl(Uri(scheme: 'tel', path: detail.riderPhone)),
              icon: const Icon(Icons.call, size: 18),
              label: Text(detail.riderPhone),
            ),
        ],
        if ((detail.notes ?? '').trim().isNotEmpty) ...[
          const SizedBox(height: 6),
          Text('Notes: ${detail.notes}', style: const TextStyle(fontWeight: FontWeight.w600)),
        ],
        if ((detail.cancelReason ?? '').trim().isNotEmpty) ...[
          const SizedBox(height: 6),
          Text(detail.cancelReason!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
        ],
        if (detail.chat.isNotEmpty) ...[
          const SizedBox(height: 10),
          Text('Chat', style: Theme.of(context).textTheme.labelLarge),
          const SizedBox(height: 6),
          ...detail.chat.take(12).map(
                (m) => Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Text(
                    '${m.fromRider ? 'Rider' : 'You'}: ${m.body}',
                    style: const TextStyle(fontSize: 12, color: brandMuted),
                  ),
                ),
              ),
        ],
      ],
    );
  }
}
