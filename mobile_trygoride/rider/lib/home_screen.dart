import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api.dart';
import 'cash_in_page.dart';
import 'chat_screen.dart';
import 'earnings_screen.dart';
import 'models.dart';
import 'offers_screen.dart';
import 'profile_screen.dart';
import 'qr_screen.dart';
import 'session.dart';
import 'sos.dart';
import 'theme.dart';
import 'trip_screen.dart';
import 'trips_screen.dart';
import 'wallet_screen.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key, required this.session});

  final RiderSession session;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  bool _waitingOnHail = false;
  bool _sosBusy = false;
  bool _acceptBusy = false;
  bool _hadActiveTrip = false;
  String _liveKey = '';
  RiderEarningsSummary? _summary;
  List<RiderTripListItem> _recent = const [];

  @override
  void initState() {
    super.initState();
    _hadActiveTrip = widget.session.desk?.activeTrip != null;
    _liveKey = _deskLiveKey(widget.session.desk);
    widget.session.addListener(_onDesk);
    unawaited(_loadExtras());
  }

  @override
  void dispose() {
    widget.session.removeListener(_onDesk);
    super.dispose();
  }

  String _deskLiveKey(RiderDesk? desk) {
    if (desk == null) return '';
    return [
      desk.isOnline,
      desk.walletBalance,
      desk.walletLow,
      desk.canReceiveBookings,
      desk.activeTrip?.tripId,
      desk.activeTrip?.status,
      desk.pendingHail?.customerId,
      desk.offers.length,
      desk.offers.isEmpty ? '' : desk.offers.first.offerId,
      widget.session.chatUnread,
      widget.session.error,
    ].join('|');
  }

  void _onDesk() {
    if (!mounted) return;
    final desk = widget.session.desk;
    if (desk?.pendingHail != null) {
      _waitingOnHail = true;
    }
    final trip = desk?.activeTrip;
    if (_waitingOnHail && trip != null) {
      _waitingOnHail = false;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        Navigator.push(
          context,
          MaterialPageRoute(builder: (_) => TripScreen(session: widget.session)),
        );
      });
    }
    if (trip == null && desk?.pendingHail == null) {
      _waitingOnHail = false;
    }

    final nextKey = _deskLiveKey(desk);
    final hasActive = desk?.activeTrip != null;
    if (nextKey != _liveKey) {
      _liveKey = nextKey;
      setState(() {});
    }
    if (_hadActiveTrip && !hasActive) {
      unawaited(_loadExtras(force: true));
    }
    _hadActiveTrip = hasActive;
  }

  Future<void> _loadExtras({bool force = false}) async {
    await widget.session.loadHomeExtras(force: force);
    if (!mounted) return;
    setState(() {
      _summary = widget.session.earningsSummary;
      _recent = widget.session.recentTrips.take(3).toList();
    });
  }

  Future<void> _refresh() async {
    await widget.session.refresh();
    await _loadExtras(force: true);
  }

  Future<void> _call(String phone) async {
    final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
    if (cleaned.isEmpty) return;
    await launchUrl(Uri(scheme: 'tel', path: cleaned));
  }

  Future<void> _sosFromHome() async {
    final trip = widget.session.desk?.activeTrip;
    if (trip == null || !trip.canSos) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('SOS is available during an active ride.')),
      );
      return;
    }
    if (_sosBusy) return;

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
    if (confirmed != true || !mounted) return;

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
      if (mounted) setState(() => _sosBusy = false);
    }
  }

  Future<void> _acceptOffer(JobOffer offer) async {
    if (_acceptBusy) return;
    setState(() => _acceptBusy = true);
    try {
      await widget.session.accept(offer.offerId);
      if (!mounted) return;
      Navigator.push(
        context,
        MaterialPageRoute(builder: (_) => TripScreen(session: widget.session)),
      );
    } catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$ex')));
      }
    } finally {
      if (mounted) setState(() => _acceptBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final desk = widget.session.desk;
    if (desk == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator(color: brandRed)));
    }

    final sosReady = desk.activeTrip != null && desk.activeTrip!.canSos;
    final unread = widget.session.chatUnread;
    final topOffer = desk.offers.isEmpty ? null : desk.offers.first;

    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        backgroundColor: brandRed,
        foregroundColor: Colors.white,
        systemOverlayStyle: SystemUiOverlayStyle.light,
        titleSpacing: 12,
        title: Row(
          children: [
            ClipOval(
              child: Image.asset('assets/logo-circle.png', width: 34, height: 34, fit: BoxFit.cover),
            ),
            const SizedBox(width: 10),
            const Text(
              'TryGoRide',
              style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 18, fontStyle: FontStyle.italic),
            ),
          ],
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
        onRefresh: _refresh,
        child: CustomScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          slivers: [
            SliverPadding(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 108),
              sliver: SliverList(
                delegate: SliverChildListDelegate([
                  RepaintBoundary(
                    child: _RiderHeaderCard(
                      desk: desk,
                      photoUrl: widget.session.api.mediaUrl(desk.photoUrl),
                      rating: _summary?.averageRating,
                      onTap: () => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => ProfileScreen(session: widget.session)),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  RepaintBoundary(
                    child: _TodayEarningsCard(
                      summary: _summary,
                      onViewEarnings: () => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => EarningsScreen(session: widget.session)),
                      ),
                    ),
                  ),
                  const SizedBox(height: 10),
                  RepaintBoundary(child: _PeriodRow(summary: _summary)),
                  const SizedBox(height: 12),
                  RepaintBoundary(
                    child: _OnlineWalletRow(
                      desk: desk,
                      onOnlineChanged: widget.session.setOnline,
                      onCashIn: () => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => CashInPage(session: widget.session)),
                      ),
                      onWallet: () => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => WalletScreen(session: widget.session)),
                      ),
                    ),
                  ),
                  if (widget.session.error != null) ...[
                    const SizedBox(height: 10),
                    Text(widget.session.error!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
                  ],
                  const SizedBox(height: 12),
                  RepaintBoundary(
                    child: _BookingSection(
                      desk: desk,
                      unread: unread,
                      topOffer: topOffer,
                      acceptBusy: _acceptBusy,
                      canViewChat: widget.session.canViewChat,
                      onOpenTrip: () => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => TripScreen(session: widget.session)),
                      ),
                      onOpenChat: (tripId) => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => ChatScreen(session: widget.session, tripId: tripId)),
                      ),
                      onCancelTrip: (tripId) async {
                        final ok = await showDialog<bool>(
                          context: context,
                          builder: (context) => AlertDialog(
                            title: const Text('Cancel booking?'),
                            content: const Text(
                              'This cancels the trip for the customer and lowers your credibility score.',
                            ),
                            actions: [
                              TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Keep trip')),
                              FilledButton(
                                style: FilledButton.styleFrom(backgroundColor: brandSos),
                                onPressed: () => Navigator.pop(context, true),
                                child: const Text('Cancel booking'),
                              ),
                            ],
                          ),
                        );
                        if (ok == true) {
                          try {
                            await widget.session.cancelTrip(tripId);
                          } catch (ex) {
                            if (context.mounted) {
                              ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$ex')));
                            }
                          }
                        }
                      },
                      onCall: _call,
                      onCancelHail: () async {
                        try {
                          await widget.session.cancelHail();
                        } catch (ex) {
                          if (context.mounted) {
                            ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$ex')));
                          }
                        }
                      },
                      onOpenJobs: () => Navigator.push(
                        context,
                        MaterialPageRoute(
                          settings: const RouteSettings(name: 'offers'),
                          builder: (_) => OffersScreen(session: widget.session),
                        ),
                      ),
                      onAccept: topOffer == null ? null : () => _acceptOffer(topOffer),
                    ),
                  ),
                  const SizedBox(height: 14),
                  RepaintBoundary(
                    child: _RecentTripsBlock(
                      trips: _recent,
                      onViewAll: () => Navigator.push(
                        context,
                        MaterialPageRoute(builder: (_) => TripsScreen(session: widget.session)),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  const RepaintBoundary(child: _PromoBanner()),
                ]),
              ),
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
                child: _NavItem(icon: Icons.home_rounded, label: 'Home', selected: true, onTap: _noop),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.work_outline_rounded,
                  label: 'Job',
                  badge: desk.offers.length,
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute(
                      settings: const RouteSettings(name: 'offers'),
                      builder: (_) => OffersScreen(session: widget.session),
                    ),
                  ),
                ),
              ),
              Padding(
                padding: const EdgeInsets.only(bottom: 2),
                child: Material(
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
                          Icon(Icons.sos, color: Colors.white.withValues(alpha: sosReady || _sosBusy ? 1 : 0.85), size: 22),
                          const Text(
                            'SOS',
                            style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 9, letterSpacing: 0.8),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.bar_chart_rounded,
                  label: 'Earnings',
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => EarningsScreen(session: widget.session)),
                  ),
                ),
              ),
              Expanded(
                child: _NavItem(
                  icon: Icons.route_outlined,
                  label: 'Trips',
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

void _noop() {}

class _RiderHeaderCard extends StatelessWidget {
  const _RiderHeaderCard({
    required this.desk,
    required this.onTap,
    this.photoUrl,
    this.rating,
  });

  final RiderDesk desk;
  final String? photoUrl;
  final double? rating;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: brandSurface,
      borderRadius: BorderRadius.circular(16),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(16),
        child: Container(
          padding: const EdgeInsets.fromLTRB(14, 14, 12, 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: brandLine),
          ),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      desk.fullName.toUpperCase(),
                      style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 20, letterSpacing: 0.2, color: brandInk),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      desk.vehicleLine,
                      style: const TextStyle(color: Color(0xFF3B82F6), fontWeight: FontWeight.w600, fontSize: 13),
                    ),
                  ],
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: const Color(0xFFECFDF5),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.star_rounded, size: 16, color: brandSuccess),
                    const SizedBox(width: 4),
                    Text(
                      (rating ?? (desk.credibilityScore / 20).clamp(0, 5)).toStringAsFixed(1),
                      style: const TextStyle(fontWeight: FontWeight.w800, color: brandSuccess),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 4),
              const Icon(Icons.chevron_right, color: brandMuted),
            ],
          ),
        ),
      ),
    );
  }
}

class _TodayEarningsCard extends StatelessWidget {
  const _TodayEarningsCard({required this.summary, required this.onViewEarnings});

  final RiderEarningsSummary? summary;
  final VoidCallback onViewEarnings;

  @override
  Widget build(BuildContext context) {
    final today = summary?.todayEarnings ?? 0;
    final trips = summary?.todayTrips ?? 0;
    final avg = summary?.avgPerTrip ?? 0;
    final goal = summary?.dailyGoal ?? 1000;
    final progress = (summary?.goalProgress ?? 0).clamp(0.0, 1.0);
    final toGoal = summary?.tripsToGoalEstimate;

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: brandRed,
        borderRadius: BorderRadius.circular(16),
        boxShadow: const [BoxShadow(color: Color(0x33E30613), blurRadius: 12, offset: Offset(0, 6))],
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            flex: 11,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  "TODAY'S EARNINGS",
                  style: TextStyle(color: Colors.white70, fontWeight: FontWeight.w700, fontSize: 10, letterSpacing: 0.8),
                ),
                const SizedBox(height: 4),
                Text(
                  peso(today),
                  style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w900, fontSize: 28, height: 1.05),
                ),
                const SizedBox(height: 8),
                _WhiteStatLine(icon: Icons.directions_car_outlined, text: '$trips Trips Completed'),
                const SizedBox(height: 4),
                _WhiteStatLine(icon: Icons.bar_chart_rounded, text: '${peso(avg)} Avg. per Trip'),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            flex: 10,
            child: Container(
              padding: const EdgeInsets.fromLTRB(10, 8, 10, 8),
              decoration: BoxDecoration(
                color: Colors.white.withValues(alpha: 0.14),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      Icon(Icons.flag_outlined, size: 13, color: Colors.white),
                      SizedBox(width: 5),
                      Text('DAILY GOAL', style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 10)),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${peso(today)} / ${peso(goal)}',
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 12),
                  ),
                  const SizedBox(height: 6),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(999),
                    child: LinearProgressIndicator(
                      value: progress,
                      minHeight: 6,
                      backgroundColor: Colors.white24,
                      color: brandSuccess,
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    toGoal == null
                        ? (today >= goal ? 'Daily goal reached!' : 'Keep taking trips!')
                        : '$toGoal more trip${toGoal == 1 ? '' : 's'} to reach your goal!',
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 10, height: 1.2),
                  ),
                  const SizedBox(height: 8),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: Colors.white,
                        foregroundColor: brandRed,
                        minimumSize: const Size.fromHeight(32),
                        padding: const EdgeInsets.symmetric(horizontal: 8),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(999)),
                      ),
                      onPressed: onViewEarnings,
                      child: const Text('View Earnings >', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 11)),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _WhiteStatLine extends StatelessWidget {
  const _WhiteStatLine({required this.icon, required this.text});
  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Icon(icon, size: 14, color: Colors.white70),
        const SizedBox(width: 5),
        Expanded(
          child: Text(text, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 11)),
        ),
      ],
    );
  }
}

class _PeriodRow extends StatelessWidget {
  const _PeriodRow({required this.summary});
  final RiderEarningsSummary? summary;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: _MiniStat(
            label: 'Today',
            value: peso(summary?.todayEarnings ?? 0),
            hint: '${summary?.todayTrips ?? 0} trips',
            icon: Icons.calendar_today_outlined,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _MiniStat(
            label: 'This Week',
            value: peso(summary?.weekEarnings ?? 0),
            hint: '${summary?.weekTrips ?? 0} trips',
            icon: Icons.bar_chart_rounded,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: _MiniStat(
            label: 'This Month',
            value: peso(summary?.monthEarnings ?? 0),
            hint: '${summary?.monthTrips ?? 0} trips',
            icon: Icons.calendar_month_outlined,
          ),
        ),
      ],
    );
  }
}

class _MiniStat extends StatelessWidget {
  const _MiniStat({required this.label, required this.value, required this.hint, required this.icon});
  final String label;
  final String value;
  final String hint;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(10, 10, 10, 10),
      decoration: BoxDecoration(
        color: brandSurface,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: brandLine),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 14, color: const Color(0xFF2563EB)),
              const SizedBox(width: 6),
              Expanded(
                child: Text(
                  label,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 11),
                ),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 14)),
          const SizedBox(height: 2),
          Text(hint, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 11)),
        ],
      ),
    );
  }
}

class _OnlineWalletRow extends StatelessWidget {
  const _OnlineWalletRow({
    required this.desk,
    required this.onOnlineChanged,
    required this.onCashIn,
    this.onWallet,
  });

  final RiderDesk desk;
  final Future<void> Function(bool) onOnlineChanged;
  final VoidCallback onCashIn;
  final VoidCallback? onWallet;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Container(
            padding: const EdgeInsets.fromLTRB(12, 10, 8, 10),
            decoration: BoxDecoration(
              color: desk.isOnline ? const Color(0xFFECFDF5) : brandSurface,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: desk.isOnline ? const Color(0xFFA7F3D0) : brandLine),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: desk.isOnline ? brandSuccess : brandMuted,
                        shape: BoxShape.circle,
                      ),
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        desk.isOnline ? 'Online' : 'Offline',
                        style: TextStyle(
                          fontWeight: FontWeight.w900,
                          color: desk.isOnline ? brandSuccess : brandInk,
                        ),
                      ),
                    ),
                    Switch.adaptive(
                      value: desk.isOnline,
                      activeThumbColor: brandSuccess,
                      onChanged: (v) => onOnlineChanged(v),
                    ),
                  ],
                ),
                Text(
                  desk.isOnline
                      ? (desk.canReceiveBookings
                          ? 'You are online and ready to receive bookings.'
                          : 'Online, but wallet is below ${peso(desk.minWalletToReceive)}')
                      : 'Go online to receive broadcast jobs.',
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 11, height: 1.3),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: brandSurface,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: desk.walletLow ? brandRed : brandLine),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                InkWell(
                  onTap: onWallet,
                  child: Row(
                    children: [
                      const Icon(Icons.account_balance_wallet_outlined, size: 16, color: brandRed),
                      const SizedBox(width: 6),
                      const Expanded(
                        child: Text('Wallet Balance', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 11)),
                      ),
                      const Icon(Icons.chevron_right, size: 18, color: brandMuted),
                    ],
                  ),
                ),
                const SizedBox(height: 4),
                Text(peso(desk.walletBalance), style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 20, color: brandRed)),
                const SizedBox(height: 6),
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        'Min ${desk.minWalletToReceive.round()} to rcv booking',
                        style: TextStyle(
                          color: desk.walletLow ? brandRed : brandMuted,
                          fontWeight: FontWeight.w700,
                          fontSize: 11,
                        ),
                      ),
                    ),
                    FilledButton(
                      style: FilledButton.styleFrom(
                        backgroundColor: const Color(0xFFF97316),
                        minimumSize: const Size(0, 32),
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                      ),
                      onPressed: onCashIn,
                      child: const Text('Cash In', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 12)),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _BookingSection extends StatelessWidget {
  const _BookingSection({
    required this.desk,
    required this.unread,
    required this.topOffer,
    required this.acceptBusy,
    required this.canViewChat,
    required this.onOpenTrip,
    required this.onOpenChat,
    required this.onCancelTrip,
    required this.onCall,
    required this.onCancelHail,
    required this.onOpenJobs,
    this.onAccept,
  });

  final RiderDesk desk;
  final int unread;
  final JobOffer? topOffer;
  final bool acceptBusy;
  final bool canViewChat;
  final VoidCallback onOpenTrip;
  final void Function(String tripId) onOpenChat;
  final Future<void> Function(String tripId) onCancelTrip;
  final Future<void> Function(String phone) onCall;
  final Future<void> Function() onCancelHail;
  final VoidCallback onOpenJobs;
  final VoidCallback? onAccept;

  @override
  Widget build(BuildContext context) {
    final activeTrip = desk.activeTrip;
    if (activeTrip != null) {
      return _MockBookingCard(
        title: 'ACTIVE BOOKING',
        reference: activeTrip.reference,
        pickup: activeTrip.pickup,
        dropoff: activeTrip.dropoff,
        passengers: passengerLabel(activeTrip.passengerCount),
        fareLine: '${peso(activeTrip.fare)} · ${paymentLabel(activeTrip.paymentMethod)}',
        distanceKm: activeTrip.distanceKm,
        tinted: unread > 0,
        detailsLabel: 'Booking details',
        primaryLabel: unread > 0 ? 'Chat ($unread)' : 'Open trip',
        onDetails: onOpenTrip,
        onPrimary: canViewChat ? () => onOpenChat(activeTrip.tripId) : onOpenTrip,
        footer: (activeTrip.canCancel || activeTrip.status.toLowerCase() == 'waiting')
            ? OutlinedButton(
                style: OutlinedButton.styleFrom(
                  foregroundColor: brandSos,
                  side: const BorderSide(color: brandSos),
                  minimumSize: const Size.fromHeight(44),
                ),
                onPressed: () => onCancelTrip(activeTrip.tripId),
                child: const Text('Cancel booking'),
              )
            : null,
      );
    }

    if (desk.pendingHail != null) {
      final hail = desk.pendingHail!;
      return Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: brandWarnBg,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: brandWarnLine, width: 2),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('Customer locked in', style: TextStyle(fontWeight: FontWeight.w800)),
            const SizedBox(height: 6),
            Text('Waiting for ${hail.customerName} to set pickup and confirm.', style: const TextStyle(fontWeight: FontWeight.w600)),
            Text(hail.customerPhone, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600)),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: FilledButton.icon(
                    onPressed: hail.customerPhone.trim().isEmpty ? null : () => onCall(hail.customerPhone),
                    icon: const Icon(Icons.call),
                    label: const Text('Call'),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(child: OutlinedButton(onPressed: onCancelHail, child: const Text('Cancel hail'))),
              ],
            ),
          ],
        ),
      );
    }

    if (topOffer != null) {
      final offer = topOffer!;
      final mins = offer.distanceKm <= 0 ? null : (offer.distanceKm * 2.4).round().clamp(3, 90);
      return _MockBookingCard(
        title: 'NEW BOOKING',
        reference: offer.reference,
        pickup: offer.pickup,
        dropoff: offer.dropoff,
        passengers: passengerLabel(offer.passengerCount),
        fareLine: '${peso(offer.fare)} · ${paymentLabel(offer.paymentMethod)}',
        distanceKm: offer.distanceKm,
        etaMinutes: mins,
        tinted: true,
        detailsLabel: 'Booking details',
        primaryLabel: acceptBusy ? 'Accepting…' : 'Accept Booking',
        onDetails: onOpenJobs,
        onPrimary: acceptBusy ? null : onAccept,
      );
    }

    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: brandSurface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: brandLine),
      ),
      child: Column(
        children: [
          const Text(
            'No incoming jobs yet. Stay online near pickup areas to receive broadcasts.',
            textAlign: TextAlign.center,
            style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
          ),
          const SizedBox(height: 12),
          FilledButton(onPressed: onOpenJobs, child: const Text('Open Jobs')),
        ],
      ),
    );
  }
}

class _MockBookingCard extends StatelessWidget {
  const _MockBookingCard({
    required this.title,
    required this.reference,
    required this.pickup,
    required this.dropoff,
    required this.passengers,
    required this.fareLine,
    required this.distanceKm,
    required this.detailsLabel,
    required this.primaryLabel,
    required this.onDetails,
    this.onPrimary,
    this.etaMinutes,
    this.tinted = false,
    this.footer,
  });

  final String title;
  final String reference;
  final String pickup;
  final String dropoff;
  final String passengers;
  final String fareLine;
  final double distanceKm;
  final int? etaMinutes;
  final bool tinted;
  final String detailsLabel;
  final String primaryLabel;
  final VoidCallback onDetails;
  final VoidCallback? onPrimary;
  final Widget? footer;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: tinted ? const Color(0xFFEFF6FF) : brandSurface,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: tinted ? const Color(0xFFBFDBFE) : brandLine),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.local_taxi_outlined, color: Color(0xFF2563EB), size: 20),
              const SizedBox(width: 8),
              Text(title, style: const TextStyle(fontWeight: FontWeight.w900, color: Color(0xFF1D4ED8))),
              const Spacer(),
              Flexible(
                child: Text(
                  'Booking ID: $reference',
                  textAlign: TextAlign.right,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 11),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _PinLine(text: pickup),
                    const SizedBox(height: 8),
                    _PinLine(text: dropoff, muted: true),
                    const SizedBox(height: 10),
                    Row(
                      children: [
                        const Icon(Icons.person_outline, size: 16, color: brandMuted),
                        const SizedBox(width: 4),
                        Text(passengers, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Text(fareLine, style: const TextStyle(fontWeight: FontWeight.w900, color: brandRed, fontSize: 15)),
                  ],
                ),
              ),
              const SizedBox(width: 8),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  const Icon(Icons.speed, size: 16, color: brandMuted),
                  Text('${distanceKm.toStringAsFixed(1)} km', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12)),
                  const Text('Est. Distance', style: TextStyle(color: brandMuted, fontSize: 10, fontWeight: FontWeight.w600)),
                  if (etaMinutes != null) ...[
                    const SizedBox(height: 10),
                    const Icon(Icons.schedule, size: 16, color: brandMuted),
                    Text('~ $etaMinutes mins', style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 12)),
                    const Text('Est. Time', style: TextStyle(color: brandMuted, fontSize: 10, fontWeight: FontWeight.w600)),
                  ],
                ],
              ),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: const Color(0xFF1E3A5F),
                    backgroundColor: const Color(0xFFE8EEF5),
                    side: BorderSide.none,
                    minimumSize: const Size.fromHeight(46),
                  ),
                  onPressed: onDetails,
                  child: Text(detailsLabel),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: FilledButton(
                  style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(46)),
                  onPressed: onPrimary,
                  child: Text(primaryLabel),
                ),
              ),
            ],
          ),
          if (footer != null) ...[const SizedBox(height: 8), footer!],
        ],
      ),
    );
  }
}

class _PinLine extends StatelessWidget {
  const _PinLine({required this.text, this.muted = false});
  final String text;
  final bool muted;

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.location_on, size: 16, color: muted ? brandMuted : brandRed),
        const SizedBox(width: 6),
        Expanded(
          child: Text(
            text,
            style: TextStyle(
              fontWeight: FontWeight.w700,
              fontSize: 13,
              color: muted ? brandMuted : brandInk,
              height: 1.25,
            ),
          ),
        ),
      ],
    );
  }
}

class _RecentTripsBlock extends StatelessWidget {
  const _RecentTripsBlock({required this.trips, required this.onViewAll});

  final List<RiderTripListItem> trips;
  final VoidCallback onViewAll;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(
              child: Text('RECENT TRIPS', style: TextStyle(fontWeight: FontWeight.w900, fontSize: 14, letterSpacing: 0.4)),
            ),
            TextButton(
              onPressed: onViewAll,
              style: TextButton.styleFrom(foregroundColor: const Color(0xFF2563EB)),
              child: const Text('View All >', style: TextStyle(fontWeight: FontWeight.w800)),
            ),
          ],
        ),
        if (trips.isEmpty)
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: brandSurface,
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: brandLine),
            ),
            child: const Text('No completed trips yet.', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600)),
          )
        else
          ...trips.map(
            (trip) => Padding(
              padding: const EdgeInsets.only(bottom: 10),
              child: Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: brandSurface,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(color: brandLine),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      trip.requestedAt == null ? trip.reference : _fmtWhen(trip.requestedAt!),
                      style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12),
                    ),
                    const SizedBox(height: 4),
                    Text('${trip.pickup} → ${trip.dropoff}', style: const TextStyle(fontWeight: FontWeight.w800)),
                    const SizedBox(height: 4),
                    Text(passengerLabel(trip.passengerCount), style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12)),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(child: _MoneyBit(label: 'Fare', value: peso(trip.fare))),
                        Expanded(
                          child: _MoneyBit(
                            label: 'Your Earnings',
                            value: peso(trip.driverAmount ?? trip.fare),
                            color: brandSuccess,
                          ),
                        ),
                        Expanded(
                          child: _MoneyBit(
                            label: 'Platform Fee',
                            value: peso(trip.platformFee ?? 0),
                            color: const Color(0xFF2563EB),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }

  String _fmtWhen(DateTime value) {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    final h = value.hour % 12 == 0 ? 12 : value.hour % 12;
    final ampm = value.hour >= 12 ? 'PM' : 'AM';
    final m = value.minute.toString().padLeft(2, '0');
    return '${months[value.month - 1]} ${value.day}, $h:$m $ampm';
  }
}

class _MoneyBit extends StatelessWidget {
  const _MoneyBit({required this.label, required this.value, this.color = brandInk});
  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 11)),
        const SizedBox(height: 2),
        Text(value, style: TextStyle(color: color, fontWeight: FontWeight.w800, fontSize: 13)),
      ],
    );
  }
}

class _PromoBanner extends StatelessWidget {
  const _PromoBanner();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(14, 14, 14, 14),
      decoration: BoxDecoration(
        color: const Color(0xFFFFE4E6),
        borderRadius: BorderRadius.circular(14),
      ),
      child: const Row(
        children: [
          Icon(Icons.savings_outlined, color: brandRed, size: 28),
          SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('More Trips, More Earnings!', style: TextStyle(fontWeight: FontWeight.w900, fontSize: 15, color: brandRed)),
                SizedBox(height: 4),
                Text(
                  'Stay online and be in high-demand areas.',
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12),
                ),
              ],
            ),
          ),
          Icon(Icons.two_wheeler, color: brandRed, size: 40),
        ],
      ),
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
                    child: Container(
                      constraints: const BoxConstraints(minWidth: 16),
                      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 1),
                      decoration: const BoxDecoration(color: brandRed, borderRadius: BorderRadius.all(Radius.circular(999))),
                      alignment: Alignment.center,
                      child: Text(
                        badge > 9 ? '9+' : '$badge',
                        style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 9),
                      ),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 4),
            Text(label, style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 11)),
          ],
        ),
      ),
    );
  }
}
