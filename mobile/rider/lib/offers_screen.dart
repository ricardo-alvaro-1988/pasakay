import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import 'models.dart';
import 'session.dart';
import 'theme.dart';
import 'trip_screen.dart';

class OffersScreen extends StatefulWidget {
  const OffersScreen({super.key, required this.session});

  final RiderSession session;

  @override
  State<OffersScreen> createState() => _OffersScreenState();
}

class _OffersScreenState extends State<OffersScreen> {
  String? _busyOfferId;

  @override
  void initState() {
    super.initState();
    widget.session.addListener(_onDesk);
  }

  @override
  void dispose() {
    widget.session.removeListener(_onDesk);
    super.dispose();
  }

  void _onDesk() {
    if (mounted) setState(() {});
  }

  Future<void> _call(String phone) async {
    final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
    if (cleaned.isEmpty) return;
    await launchUrl(Uri(scheme: 'tel', path: cleaned));
  }

  String _fmtWhen(DateTime value) {
    final local = value.toLocal();
    final mm = local.month.toString().padLeft(2, '0');
    final dd = local.day.toString().padLeft(2, '0');
    final hh = local.hour.toString().padLeft(2, '0');
    final min = local.minute.toString().padLeft(2, '0');
    return '$mm/$dd $hh:$min';
  }

  Future<void> _accept(JobOffer offer) async {
    if (_busyOfferId != null) return;
    setState(() => _busyOfferId = offer.offerId);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await widget.session.accept(offer.offerId);
      if (!mounted) return;
      await Navigator.pushReplacement(
        context,
        MaterialPageRoute(builder: (_) => TripScreen(session: widget.session)),
      );
    } catch (ex) {
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text('$ex')));
        setState(() => _busyOfferId = null);
      }
    }
  }

  Future<void> _decline(JobOffer offer) async {
    if (_busyOfferId != null) return;
    setState(() => _busyOfferId = offer.offerId);
    final messenger = ScaffoldMessenger.of(context);
    try {
      await widget.session.decline(offer.offerId);
    } catch (ex) {
      if (mounted) {
        messenger.showSnackBar(SnackBar(content: Text('$ex')));
      }
    } finally {
      if (mounted) setState(() => _busyOfferId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final desk = widget.session.desk;
    final offers = desk?.offers ?? const <JobOffer>[];
    final online = desk?.isOnline == true;
    final busy = desk?.activeTrip != null;

    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        title: Text(offers.isEmpty ? 'Jobs' : 'Jobs (${offers.length})'),
      ),
      body: RefreshIndicator(
        color: brandRed,
        onRefresh: widget.session.refresh,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
          children: [
            if (desk == null)
              const BrandPanel(
                child: Text(
                  'Loading desk…',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              )
            else if (!online)
              const BrandPanel(
                child: Text(
                  'Go online on Home to receive job offers.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              )
            else if (busy)
              const BrandPanel(
                child: Text(
                  'Finish your current trip before taking another job.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              )
            else if (offers.isEmpty)
              const BrandPanel(
                child: Text(
                  'No incoming jobs yet. Stay online near pickup areas to receive broadcasts.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              )
            else
              ...offers.map((offer) {
                final locked = _busyOfferId != null;
                final working = _busyOfferId == offer.offerId;
                return Padding(
                  padding: const EdgeInsets.only(bottom: 14),
                  child: _OfferCard(
                    offer: offer,
                    busy: working,
                    locked: locked && !working,
                    whenLabel: offer.scheduledAt == null ? null : 'Scheduled · ${_fmtWhen(offer.scheduledAt!)}',
                    onCall: offer.customerPhone.trim().isEmpty ? null : () => _call(offer.customerPhone),
                    onAccept: () => _accept(offer),
                    onDecline: () => _decline(offer),
                  ),
                );
              }),
          ],
        ),
      ),
    );
  }
}

class _OfferCard extends StatelessWidget {
  const _OfferCard({
    required this.offer,
    required this.busy,
    required this.locked,
    required this.onAccept,
    required this.onDecline,
    this.onCall,
    this.whenLabel,
  });

  final JobOffer offer;
  final bool busy;
  final bool locked;
  final VoidCallback onAccept;
  final VoidCallback onDecline;
  final VoidCallback? onCall;
  final String? whenLabel;

  @override
  Widget build(BuildContext context) {
    final assigned = offer.highlighted || offer.isPreferred;
    return BrandPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: brandAccentSoft,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: const Icon(Icons.directions_bike, color: brandRed, size: 22),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(offer.reference, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
                    Text(
                      offer.customerName,
                      style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 13),
                    ),
                  ],
                ),
              ),
              if (assigned)
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  decoration: BoxDecoration(
                    color: brandWarnBg,
                    borderRadius: BorderRadius.circular(999),
                    border: Border.all(color: brandWarnLine, width: 1.5),
                  ),
                  child: const Text(
                    'ASSIGNED',
                    style: TextStyle(
                      fontWeight: FontWeight.w800,
                      fontSize: 10,
                      color: Color(0xFF7A5B00),
                      letterSpacing: 0.3,
                    ),
                  ),
                ),
              if (onCall != null) ...[
                const SizedBox(width: 6),
                IconButton.filled(
                  style: IconButton.styleFrom(
                    backgroundColor: brandChip,
                    foregroundColor: brandInk,
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: locked || busy ? null : onCall,
                  icon: const Icon(Icons.call, size: 18),
                  tooltip: 'Call customer',
                ),
              ],
            ],
          ),
          if (offer.isPromoSponsored) ...[
            const SizedBox(height: 10),
            Text(
              offer.discountPercent != null
                  ? 'Save${offer.discountPercent} · ${offer.discountPercent}% off'
                  : 'Promo ride',
              style: const TextStyle(
                color: Color(0xFF047857),
                fontWeight: FontWeight.w900,
                fontSize: 13,
              ),
            ),
          ],
          const SizedBox(height: 12),
          _StopLine(label: 'PICKUP', value: offer.pickup, icon: Icons.trip_origin),
          const SizedBox(height: 8),
          _StopLine(label: 'DROP-OFF', value: offer.dropoff, icon: Icons.location_on),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: _Stat(label: 'FARE', value: peso(offer.fare), emphasize: true)),
              Container(width: 1, height: 32, color: brandLine),
              Expanded(
                child: _Stat(label: 'TRIP', value: '${offer.distanceKm.toStringAsFixed(1)} km'),
              ),
              Container(width: 1, height: 32, color: brandLine),
              Expanded(child: _Stat(label: 'PAY', value: paymentLabel(offer.paymentMethod))),
            ],
          ),
          if (offer.customerBoostAmount > 0) ...[
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                decoration: BoxDecoration(
                  color: const Color(0xFFFFF7ED),
                  borderRadius: BorderRadius.circular(999),
                  border: Border.all(color: const Color(0xFFFDBA74)),
                ),
                child: Text(
                  '+₱${offer.customerBoostAmount.round()} boost',
                  style: const TextStyle(
                    color: Color(0xFFC2410C),
                    fontWeight: FontWeight.w900,
                    fontSize: 12,
                  ),
                ),
              ),
            ),
          ],
          const SizedBox(height: 8),
          Text(
            'Persons: ${passengerLabel(offer.passengerCount)}'
            '${offer.riderDistanceKm == null ? '' : ' · ${offer.riderDistanceKm!.toStringAsFixed(1)} km away'}',
            style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12),
          ),
          if (whenLabel != null) ...[
            const SizedBox(height: 4),
            Text(
              whenLabel!,
              style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12),
            ),
          ],
          if (offer.isPromoSponsored) ...[
            const SizedBox(height: 10),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              decoration: BoxDecoration(
                color: const Color(0xFFECFDF5),
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: const Color(0xFFA7F3D0)),
              ),
              child: Text(
                offer.discountPercent != null
                    ? 'Collect ${peso(offer.collectFromCustomer)} from customer · ${peso(offer.collectFromOperator)} from operator'
                    : 'Collect ${peso(offer.collectFromCustomer)} from customer · ${peso(offer.collectFromOperator)} from operator',
                style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 12),
              ),
            ),
          ],
          const SizedBox(height: 14),
          FilledButton(
            onPressed: locked || busy ? null : onAccept,
            child: Text(busy ? 'Accepting…' : 'Accept job'),
          ),
          const SizedBox(height: 8),
          OutlinedButton(
            onPressed: locked || busy ? null : onDecline,
            child: const Text('Decline'),
          ),
        ],
      ),
    );
  }
}

class _StopLine extends StatelessWidget {
  const _StopLine({required this.label, required this.value, required this.icon});

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 16, color: brandRed),
        const SizedBox(width: 8),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  color: brandMuted,
                  fontWeight: FontWeight.w700,
                  fontSize: 10,
                  letterSpacing: 0.4,
                ),
              ),
              Text(value, style: const TextStyle(fontWeight: FontWeight.w700, height: 1.25, fontSize: 13)),
            ],
          ),
        ),
      ],
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({required this.label, required this.value, this.emphasize = false});

  final String label;
  final String value;
  final bool emphasize;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          label,
          style: const TextStyle(
            color: brandMuted,
            fontWeight: FontWeight.w700,
            fontSize: 10,
            letterSpacing: 0.3,
          ),
        ),
        const SizedBox(height: 2),
        Text(
          value,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: emphasize ? 15 : 12,
            color: emphasize ? brandRed : brandInk,
          ),
        ),
      ],
    );
  }
}
