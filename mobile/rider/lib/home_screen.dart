import 'dart:async';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api.dart';
import 'models.dart';
import 'qr_screen.dart';
import 'session.dart';
import 'sos.dart';
import 'theme.dart';
import 'trip_screen.dart';
import 'trips_screen.dart';
import 'wallet_screen.dart';
import 'profile_screen.dart';
import 'chat_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, required this.session});

  final RiderSession session;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  bool _waitingOnHail = false;
  bool _sosBusy = false;

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
    if (!mounted) {
      return;
    }
    setState(() {});
    final desk = widget.session.desk;
    if (desk?.pendingHail != null) {
      _waitingOnHail = true;
    }
    final trip = desk?.activeTrip;
    if (_waitingOnHail && trip != null) {
      _waitingOnHail = false;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) {
          return;
        }
        Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => TripScreen(session: widget.session)),
        );
      });
    }
    if (trip == null && desk?.pendingHail == null) {
      _waitingOnHail = false;
    }
    final offer = widget.session.takeIncomingOffer();
    if (offer != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) {
          _showOffer(offer);
        }
      });
    }
  }

  Future<void> _call(String phone) async {
    final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
    if (cleaned.isEmpty) {
      return;
    }
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

  Future<void> _sosFromHome() async {
    final trip = widget.session.desk?.activeTrip;
    if (trip == null || !trip.canSos) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('SOS is available during an active ride.')),
      );
      return;
    }
    if (_sosBusy) {
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Send SOS?'),
        content: const Text('This alerts your operator and Super Admin with your location.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          FilledButton(
            style: FilledButton.styleFrom(backgroundColor: brandSos),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Send SOS'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) {
      return;
    }

    setState(() => _sosBusy = true);
    final last = await sosLastKnown();
    try {
      await widget.session.api.sos(trip.tripId, message: 'Rider SOS', lat: last.$1, lng: last.$2);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('SOS sent.')));
      }
      unawaited(refineSosLocation(widget.session.api, trip.tripId));
    } on ApiException catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
      }
    } finally {
      if (mounted) {
        setState(() => _sosBusy = false);
      }
    }
  }

  Future<void> _showOffer(JobOffer offer) async {
    final session = widget.session;
    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      backgroundColor: brandCanvas,
      showDragHandle: true,
      builder: (context) {
        final bottom = MediaQuery.paddingOf(context).bottom;
        return Padding(
          padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + bottom),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      color: brandAccentSoft,
                      borderRadius: BorderRadius.circular(14),
                    ),
                    child: const Icon(Icons.directions_bike, color: brandRed),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Incoming job', style: Theme.of(context).textTheme.titleMedium),
                        Text(
                          offer.reference,
                          style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                        ),
                      ],
                    ),
                  ),
                  if (offer.highlighted || offer.isPreferred)
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                      decoration: BoxDecoration(
                        color: brandWarnBg,
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(color: brandWarnLine, width: 1.5),
                      ),
                      child: const Text(
                        'ASSIGNED',
                        style: TextStyle(
                          fontWeight: FontWeight.w800,
                          fontSize: 11,
                          color: Color(0xFF7A5B00),
                          letterSpacing: 0.4,
                        ),
                      ),
                    ),
                ],
              ),
              if (offer.highlighted || offer.isPreferred) ...[
                const SizedBox(height: 12),
                BrandPanel(
                  color: brandWarnBg,
                  borderColor: brandWarnLine,
                  borderWidth: 2,
                  padding: const EdgeInsets.all(14),
                  child: const Text(
                    'Assigned to you — highlighted job. Accept to take it.',
                    style: TextStyle(fontWeight: FontWeight.w700),
                  ),
                ),
              ],
              const SizedBox(height: 12),
              BrandPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                offer.customerName,
                                style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 18),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                offer.customerPhone,
                                style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                              ),
                            ],
                          ),
                        ),
                        if (offer.customerPhone.trim().isNotEmpty)
                          IconButton.filled(
                            style: IconButton.styleFrom(
                              backgroundColor: brandChip,
                              foregroundColor: brandInk,
                            ),
                            onPressed: () => _call(offer.customerPhone),
                            icon: const Icon(Icons.call),
                            tooltip: 'Call customer',
                          ),
                      ],
                    ),
                    const SizedBox(height: 14),
                    const Divider(height: 1, color: brandLine),
                    const SizedBox(height: 14),
                    _OfferStop(label: 'PICKUP', value: offer.pickup, icon: Icons.trip_origin),
                    const SizedBox(height: 12),
                    _OfferStop(label: 'DROP-OFF', value: offer.dropoff, icon: Icons.location_on),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 14),
                child: Row(
                  children: [
                    Expanded(
                      child: _OfferStat(label: 'FARE', value: peso(offer.fare), emphasize: true),
                    ),
                    Container(width: 1, height: 36, color: brandLine),
                    Expanded(
                      child: _OfferStat(
                        label: 'TRIP',
                        value: '${offer.distanceKm.toStringAsFixed(1)} km',
                      ),
                    ),
                    Container(width: 1, height: 36, color: brandLine),
                    Expanded(
                      child: _OfferStat(
                        label: 'PERSONS',
                        value: '${offer.passengerCount}',
                      ),
                    ),
                    Container(width: 1, height: 36, color: brandLine),
                    Expanded(
                      child: _OfferStat(
                        label: 'PAY',
                        value: paymentLabel(offer.paymentMethod),
                      ),
                    ),
                  ],
                ),
              ),
              if (offer.riderDistanceKm != null) ...[
                const SizedBox(height: 10),
                Text(
                  '${offer.riderDistanceKm!.toStringAsFixed(1)} km from pickup',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              ],
              if (offer.scheduledAt != null) ...[
                const SizedBox(height: 8),
                Text(
                  'Scheduled · ${_fmtWhen(offer.scheduledAt!)}',
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              ],
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () async {
                  Navigator.pop(context);
                  try {
                    await session.accept(offer.offerId);
                    if (!mounted) {
                      return;
                    }
                    await Navigator.push(
                      this.context,
                      MaterialPageRoute(builder: (_) => TripScreen(session: session)),
                    );
                  } catch (ex) {
                    if (mounted) {
                      ScaffoldMessenger.of(this.context).showSnackBar(SnackBar(content: Text('$ex')));
                    }
                  }
                },
                child: const Text('Accept job'),
              ),
              const SizedBox(height: 8),
              OutlinedButton(
                onPressed: () async {
                  Navigator.pop(context);
                  await session.decline(offer.offerId);
                },
                child: const Text('Decline'),
              ),
            ],
          ),
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    final desk = widget.session.desk;
    if (desk == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator(color: brandRed)));
    }

    final sosReady = desk.activeTrip != null && desk.activeTrip!.canSos;
    final unread = widget.session.chatUnread;
    final activeTrip = desk.activeTrip;

    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        titleSpacing: 12,
        title: FittedBox(
          fit: BoxFit.scaleDown,
          alignment: Alignment.centerLeft,
          child: BrandPill(subtitle: desk.companyName),
        ),
        actions: [
          IconButton(
            tooltip: 'Profile',
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(builder: (_) => ProfileScreen(session: widget.session)),
            ),
            icon: const Icon(Icons.person_outline),
          ),
          IconButton(
            tooltip: 'Scan customer QR',
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(builder: (_) => QrScreen(session: widget.session)),
            ),
            icon: const Icon(Icons.qr_code_scanner),
          ),
        ],
      ),
      body: RefreshIndicator(
        color: brandRed,
        onRefresh: widget.session.refresh,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 108),
          children: [
            InkWell(
              onTap: () => Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => ProfileScreen(session: widget.session)),
              ),
              child: Row(
                children: [
                  CircleAvatar(
                    radius: 26,
                    backgroundColor: brandAccentSoft,
                    backgroundImage: widget.session.api.mediaUrl(desk.photoUrl) == null
                        ? null
                        : NetworkImage(widget.session.api.mediaUrl(desk.photoUrl)!),
                    child: widget.session.api.mediaUrl(desk.photoUrl) == null
                        ? Text(
                            desk.fullName.trim().isEmpty ? 'R' : desk.fullName.trim()[0].toUpperCase(),
                            style: const TextStyle(color: brandRed, fontWeight: FontWeight.w800),
                          )
                        : null,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(desk.fullName, style: Theme.of(context).textTheme.titleLarge),
                        const SizedBox(height: 4),
                        Text(
                          desk.vehicleLine,
                          style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                        ),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right, color: brandMuted),
                ],
              ),
            ),
            const SizedBox(height: 14),
            BrandPanel(
              color: desk.walletLow ? brandDangerBg : brandWarnBg,
              borderColor: desk.walletLow ? brandRed : brandWarnLine,
              borderWidth: 2,
              padding: const EdgeInsets.all(14),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    desk.walletLow
                        ? 'WALLET TOO LOW'
                        : 'MINIMUM ₱${desk.minWalletToReceive.toStringAsFixed(0)} TO RECEIVE BOOKINGS',
                    style: TextStyle(
                      fontWeight: FontWeight.w800,
                      color: desk.walletLow ? brandRed : const Color(0xFF7A5B00),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(desk.walletHighlight, style: const TextStyle(fontWeight: FontWeight.w600)),
                ],
              ),
            ),
            const SizedBox(height: 12),
            BrandPanel(
              padding: EdgeInsets.zero,
              child: SwitchListTile(
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                title: Text(
                  desk.isOnline ? 'Online' : 'Offline',
                  style: const TextStyle(fontWeight: FontWeight.w800),
                ),
                subtitle: Text(
                  desk.isOnline
                      ? (desk.canReceiveBookings
                          ? 'Waiting for nearby bookings'
                          : 'Online, but wallet is below ₱${desk.minWalletToReceive.toStringAsFixed(0)}')
                      : 'Go online to receive broadcast jobs',
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
                value: desk.isOnline,
                onChanged: widget.session.setOnline,
              ),
            ),
            if (widget.session.error != null)
              Padding(
                padding: const EdgeInsets.only(top: 10),
                child: Text(widget.session.error!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
              ),
            const SizedBox(height: 12),
            if (activeTrip != null)
              BrandPanel(
                padding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
                color: unread > 0 ? brandDangerBg : brandSurface,
                borderColor: unread > 0 ? brandRed : brandLine,
                borderWidth: unread > 0 ? 2 : 1,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'BOOKING : ${activeTrip.reference}',
                      style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 16),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      '${activeTrip.status} · ${activeTrip.customerName}',
                      style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 10),
                    Text(activeTrip.pickup, style: const TextStyle(fontWeight: FontWeight.w700)),
                    const SizedBox(height: 4),
                    Text(activeTrip.dropoff, style: const TextStyle(fontWeight: FontWeight.w700)),
                    const SizedBox(height: 8),
                    Text(
                      '${peso(activeTrip.fare)} · ${passengerLabel(activeTrip.passengerCount)} · ${paymentLabel(activeTrip.paymentMethod)}',
                      style: const TextStyle(fontWeight: FontWeight.w800, color: brandRed),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton(
                            onPressed: () => Navigator.push(
                              context,
                              MaterialPageRoute(builder: (_) => TripScreen(session: widget.session)),
                            ),
                            child: const Text('Booking details'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: FilledButton.icon(
                            style: unread > 0
                                ? FilledButton.styleFrom(backgroundColor: brandRed)
                                : null,
                            onPressed: !widget.session.canViewChat
                                ? null
                                : () => Navigator.push(
                                      context,
                                      MaterialPageRoute(
                                        builder: (_) => ChatScreen(session: widget.session, tripId: activeTrip.tripId),
                                      ),
                                    ),
                            icon: const Icon(Icons.chat_bubble_outline, size: 18),
                            label: Text(unread > 0 ? 'Chat ($unread)' : 'Chat'),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              )
            else if (desk.pendingHail != null)
              BrandPanel(
                color: brandWarnBg,
                borderColor: brandWarnLine,
                borderWidth: 2,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text('Customer locked in', style: TextStyle(fontWeight: FontWeight.w800)),
                    const SizedBox(height: 6),
                    Text(
                      'Waiting for ${desk.pendingHail!.customerName} to set pickup and confirm.',
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    Text(
                      desk.pendingHail!.customerPhone,
                      style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: desk.pendingHail!.customerPhone.trim().isEmpty
                                ? null
                                : () => _call(desk.pendingHail!.customerPhone),
                            icon: const Icon(Icons.call),
                            label: const Text('Call'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: OutlinedButton(
                            onPressed: () async {
                              final messenger = ScaffoldMessenger.of(context);
                              try {
                                await widget.session.cancelHail();
                              } catch (ex) {
                                messenger.showSnackBar(SnackBar(content: Text('$ex')));
                              }
                            },
                            child: const Text('Cancel hail'),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              )
            else if (desk.offers.isEmpty)
              const BrandPanel(
                child: Text(
                  'No incoming jobs yet. Stay online near the customer pickup to receive broadcasts.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              ),
            const SizedBox(height: 14),
            FilledButton.icon(
              onPressed: () => Navigator.push(
                context,
                MaterialPageRoute(builder: (_) => QrScreen(session: widget.session)),
              ),
              icon: const Icon(Icons.qr_code_scanner),
              label: const Text('Scan customer QR'),
            ),
          ],
        ),
      ),
      bottomNavigationBar: SafeArea(
        minimum: const EdgeInsets.fromLTRB(12, 0, 12, 10),
        child: Container(
          padding: const EdgeInsets.fromLTRB(8, 6, 8, 6),
          decoration: BoxDecoration(
            color: brandSurface,
            borderRadius: BorderRadius.circular(22),
            border: Border.all(color: brandLine),
            boxShadow: const [
              BoxShadow(color: Color(0x1416181D), blurRadius: 18, offset: Offset(0, 8)),
            ],
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Expanded(
                child: _NavItem(
                  icon: Icons.home_rounded,
                  label: 'Home',
                  selected: true,
                  onTap: () {},
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.qr_code_scanner,
                  label: 'Scan',
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => QrScreen(session: widget.session)),
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(bottom: 2),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Material(
                      color: brandSos,
                      shape: const CircleBorder(),
                      elevation: 6,
                      shadowColor: const Color(0x61E30613),
                      child: InkWell(
                        customBorder: const CircleBorder(),
                        onTap: _sosBusy ? null : _sosFromHome,
                        child: SizedBox(
                          width: 56,
                          height: 56,
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Icon(
                                Icons.sos,
                                color: Colors.white.withValues(alpha: sosReady || _sosBusy ? 1 : 0.85),
                                size: 22,
                              ),
                              const Text(
                                'SOS',
                                style: TextStyle(
                                  color: Colors.white,
                                  fontWeight: FontWeight.w800,
                                  fontSize: 9,
                                  letterSpacing: 0.8,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.account_balance_wallet_outlined,
                  label: 'Wallet',
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => WalletScreen(session: widget.session)),
                  ),
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.route_outlined,
                  label: 'Trip',
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => TripsScreen(session: widget.session)),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _OfferStop extends StatelessWidget {
  const _OfferStop({required this.label, required this.value, required this.icon});

  final String label;
  final String value;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 18, color: brandRed),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  color: brandMuted,
                  fontWeight: FontWeight.w700,
                  fontSize: 11,
                  letterSpacing: 0.5,
                ),
              ),
              const SizedBox(height: 2),
              Text(value, style: const TextStyle(fontWeight: FontWeight.w700, height: 1.3)),
            ],
          ),
        ),
      ],
    );
  }
}

class _OfferStat extends StatelessWidget {
  const _OfferStat({required this.label, required this.value, this.emphasize = false});

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
            fontSize: 11,
            letterSpacing: 0.4,
          ),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          textAlign: TextAlign.center,
          style: TextStyle(
            fontWeight: FontWeight.w800,
            fontSize: emphasize ? 16 : 13,
            color: emphasize ? brandRed : brandInk,
          ),
        ),
      ],
    );
  }
}

class _NavItem extends StatelessWidget {
  const _NavItem({
    required this.icon,
    required this.label,
    required this.onTap,
    this.selected = false,
    this.badge = 0,
  });

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool selected;
  final int badge;

  @override
  Widget build(BuildContext context) {
    final color = selected ? brandRed : brandMuted;
    return InkWell(
      borderRadius: BorderRadius.circular(14),
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                Icon(icon, size: 22, color: color),
                if (badge > 0)
                  Positioned(
                    right: -8,
                    top: -4,
                    child: _UnreadBadge(count: badge, compact: true),
                  ),
              ],
            ),
            const SizedBox(height: 4),
            Text(
              label,
              style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 11),
            ),
          ],
        ),
      ),
    );
  }
}

class _UnreadBadge extends StatelessWidget {
  const _UnreadBadge({required this.count, this.compact = false});

  final int count;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: BoxConstraints(minWidth: compact ? 16 : 22),
      padding: EdgeInsets.symmetric(horizontal: compact ? 4 : 6, vertical: compact ? 1 : 4),
      decoration: const BoxDecoration(
        color: brandRed,
        borderRadius: BorderRadius.all(Radius.circular(999)),
      ),
      alignment: Alignment.center,
      child: Text(
        count > 9 ? '9+' : '$count',
        style: TextStyle(
          color: Colors.white,
          fontWeight: FontWeight.w800,
          fontSize: compact ? 9 : 11,
        ),
      ),
    );
  }
}
