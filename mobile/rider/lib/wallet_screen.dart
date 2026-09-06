import 'package:flutter/material.dart';

import 'api.dart';
import 'cash_in_page.dart';
import 'models.dart';
import 'session.dart';
import 'theme.dart';

class WalletScreen extends StatefulWidget {
  const WalletScreen({super.key, required this.session});

  final RiderSession session;

  @override
  State<WalletScreen> createState() => _WalletScreenState();
}

class _WalletScreenState extends State<WalletScreen> {
  WalletSummary? _wallet;
  String? _error;
  bool _loading = true;

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
      final wallet = await widget.session.api.wallet();
      await widget.session.refresh();
      setState(() => _wallet = wallet);
    } on ApiException catch (ex) {
      setState(() => _error = ex.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  Future<void> _cashIn() async {
    final ok = await Navigator.push<bool>(
      context,
      MaterialPageRoute(builder: (_) => CashInPage(session: widget.session)),
    );
    if (ok == true) {
      await _load();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Cash-in request submitted. Waiting for operator approval.')),
        );
      }
    }
  }

  Future<void> _openBooking(WalletTx tx) async {
    final tripId = tx.tripId;
    if (tripId == null || tripId.isEmpty) {
      return;
    }
    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => WalletTripDetailPage(session: widget.session, tx: tx),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final desk = widget.session.desk;
    final balance = _wallet?.balance ?? desk?.walletBalance ?? 0;
    final low = desk?.walletLow ?? balance < 100;

    return Scaffold(
      appBar: AppBar(title: const Text('Wallet')),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: brandRed))
          : RefreshIndicator(
              color: brandRed,
              onRefresh: _load,
              child: ListView(
                padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
                children: [
                  BrandPanel(
                    color: const Color(0x14E30613),
                    borderColor: brandRed,
                    borderWidth: 2,
                    padding: const EdgeInsets.all(20),
                    child: Column(
                      children: [
                        const Text('BALANCE', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12)),
                        const SizedBox(height: 6),
                        Text(peso(balance), style: const TextStyle(fontSize: 34, fontWeight: FontWeight.w800, color: brandRed)),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  BrandPanel(
                    color: low ? brandDangerBg : brandWarnBg,
                    borderColor: low ? brandRed : brandWarnLine,
                    borderWidth: 2,
                    padding: const EdgeInsets.all(14),
                    child: Text(
                      desk?.walletHighlight ?? 'Keep at least ₱100 in your wallet to receive bookings.',
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                  ),
                  if (_error != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 10),
                      child: Text(_error!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
                    ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: _cashIn,
                    child: const Text('Cash in'),
                  ),
                  const SizedBox(height: 20),
                  const Text('History', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                  const SizedBox(height: 10),
                  if ((_wallet?.recent ?? const <WalletTx>[]).isEmpty)
                    const BrandPanel(
                      child: Text(
                        'No wallet transactions yet.',
                        style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                      ),
                    )
                  else
                    ..._wallet!.recent.map((tx) => Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: tx.isBooking
                              ? InkWell(
                                  borderRadius: BorderRadius.circular(22),
                                  onTap: () => _openBooking(tx),
                                  child: BrandPanel(
                                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                                    child: _BookingWalletTile(tx: tx),
                                  ),
                                )
                              : BrandPanel(
                                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('${tx.kind} · ${peso(tx.amount)}', style: const TextStyle(fontWeight: FontWeight.w800)),
                                      const SizedBox(height: 4),
                                      Text(
                                        [
                                          tx.status,
                                          if (tx.paymentMethod != null) paymentLabel(tx.paymentMethod),
                                          if (tx.note != null) tx.note!,
                                        ].join(' · '),
                                        style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                                      ),
                                    ],
                                  ),
                                ),
                        )),
                ],
              ),
            ),
    );
  }
}

class _BookingWalletTile extends StatelessWidget {
  const _BookingWalletTile({required this.tx});

  final WalletTx tx;

  @override
  Widget build(BuildContext context) {
    final fare = tx.tripFare ?? 0;
    final system = tx.amount;
    final rider = fare - system;
    return _BookingHistoryLines(
      reference: tx.tripReference ?? '—',
      date: _dateChip(tx.createdAt),
      time: _timeChip(tx.createdAt),
      fare: fare,
      system: system,
      rider: rider,
    );
  }
}

class _BookingHistoryLines extends StatelessWidget {
  const _BookingHistoryLines({
    required this.reference,
    required this.date,
    required this.time,
    required this.fare,
    required this.system,
    required this.rider,
    this.large = false,
  });

  final String reference;
  final String date;
  final String time;
  final double fare;
  final double system;
  final double rider;
  final bool large;

  @override
  Widget build(BuildContext context) {
    final titleSize = large ? 17.0 : 15.0;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.baseline,
          textBaseline: TextBaseline.alphabetic,
          children: [
            Expanded(
              child: Text(
                'BOOKING : $reference',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(fontWeight: FontWeight.w900, fontSize: titleSize),
              ),
            ),
            const SizedBox(width: 8),
            Text(date, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
          ],
        ),
        const SizedBox(height: 6),
        Row(
          children: [
            Expanded(
              child: FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: Text.rich(
                  TextSpan(
                    style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13, color: brandInk),
                    children: [
                      TextSpan(text: 'Total Fare : ${peso(fare)}'),
                      const TextSpan(text: '  |  '),
                      TextSpan(
                        text: 'System : (-${peso(system)})',
                        style: const TextStyle(color: brandRed),
                      ),
                      const TextSpan(text: '  |  '),
                      TextSpan(
                        text: 'Rider : ${peso(rider)}',
                        style: const TextStyle(color: brandSuccess),
                      ),
                    ],
                  ),
                  maxLines: 1,
                ),
              ),
            ),
            const SizedBox(width: 8),
            Text(time, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
          ],
        ),
      ],
    );
  }
}

class WalletTripDetailPage extends StatefulWidget {
  const WalletTripDetailPage({super.key, required this.session, required this.tx});

  final RiderSession session;
  final WalletTx tx;

  @override
  State<WalletTripDetailPage> createState() => _WalletTripDetailPageState();
}

class _WalletTripDetailPageState extends State<WalletTripDetailPage> {
  RiderTripDetail? _detail;
  String? _error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final tripId = widget.tx.tripId;
    if (tripId == null || tripId.isEmpty) {
      setState(() => _error = 'This wallet row is not linked to a booking.');
      return;
    }
    try {
      final detail = await widget.session.api.tripDetail(tripId);
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
    final fare = widget.tx.tripFare ?? detail?.fare ?? 0;
    final system = widget.tx.amount;
    final rider = fare - system;
    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: 'Back',
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text('Booking'),
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
                  child: _BookingHistoryLines(
                    reference: detail.reference,
                    date: _dateChip(widget.tx.createdAt),
                    time: _timeChip(widget.tx.createdAt),
                    fare: fare,
                    system: system,
                    rider: rider,
                    large: true,
                  ),
                ),
                const SizedBox(height: 12),
                BrandPanel(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _detailRow('Status', detail.status),
                      _detailRow('Customer', detail.customerName),
                      _detailRow('Phone', detail.customerPhone),
                      _detailRow('Persons', passengerLabel(detail.passengerCount)),
                      _detailRow('Vehicle', [detail.vehicleType, detail.vehicleModel, detail.plateNumber].where((x) => (x ?? '').toString().isNotEmpty).join(' · ')),
                      _detailRow('Payment', paymentLabel(detail.paymentMethod)),
                      _detailRow('Requested', _dateTime(detail.requestedAt)),
                      if (detail.completedAt != null) _detailRow('Completed', _dateTime(detail.completedAt)),
                      if ((detail.notes ?? '').trim().isNotEmpty) _detailRow('Notes', detail.notes!),
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
                  label: const Text('Back to wallet'),
                ),
              ],
            ),
    );
  }
}

Widget _detailRow(String label, Object? value) {
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
