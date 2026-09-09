import 'package:flutter/material.dart';

import 'cash_in_page.dart';
import 'session.dart';
import 'theme.dart';
import 'wallet_screen.dart';

class EarningsScreen extends StatefulWidget {
  const EarningsScreen({super.key, required this.session});

  final RiderSession session;

  @override
  State<EarningsScreen> createState() => _EarningsScreenState();
}

class _EarningsScreenState extends State<EarningsScreen> {
  bool _loading = true;
  String? _error;

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
      await widget.session.loadHomeExtras(force: true);
    } catch (ex) {
      _error = '$ex';
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final summary = widget.session.earningsSummary;
    final recent = widget.session.recentTrips;

    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        title: const Text('Earnings'),
        actions: [
          TextButton(
            onPressed: () => Navigator.push(
              context,
              MaterialPageRoute(builder: (_) => WalletScreen(session: widget.session)),
            ),
            child: const Text('Wallet'),
          ),
        ],
      ),
      body: RefreshIndicator(
        color: brandRed,
        onRefresh: _load,
        child: ListView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
          children: [
            if (_loading && summary == null)
              const Padding(
                padding: EdgeInsets.only(top: 40),
                child: Center(child: CircularProgressIndicator(color: brandRed)),
              )
            else if (_error != null && summary == null)
              BrandPanel(child: Text(_error!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)))
            else if (summary != null) ...[
              BrandPanel(
                color: brandRed,
                borderColor: brandRed,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      "TODAY'S EARNINGS",
                      style: TextStyle(color: Colors.white70, fontWeight: FontWeight.w700, fontSize: 12, letterSpacing: 0.6),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      peso(summary.todayEarnings),
                      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w900, fontSize: 34),
                    ),
                    const SizedBox(height: 10),
                    Text(
                      '${summary.todayTrips} trips · avg ${peso(summary.avgPerTrip)}',
                      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 14),
                    Text(
                      'Daily goal ${peso(summary.todayEarnings)} / ${peso(summary.dailyGoal)}',
                      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w700, fontSize: 13),
                    ),
                    const SizedBox(height: 8),
                    ClipRRect(
                      borderRadius: BorderRadius.circular(999),
                      child: LinearProgressIndicator(
                        value: summary.goalProgress.clamp(0, 1),
                        minHeight: 8,
                        backgroundColor: Colors.white24,
                        color: brandSuccess,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(child: _StatCard(label: 'Today', value: peso(summary.todayEarnings), hint: '${summary.todayTrips} trips')),
                  const SizedBox(width: 8),
                  Expanded(child: _StatCard(label: 'This week', value: peso(summary.weekEarnings), hint: '${summary.weekTrips} trips')),
                  const SizedBox(width: 8),
                  Expanded(child: _StatCard(label: 'This month', value: peso(summary.monthEarnings), hint: '${summary.monthTrips} trips')),
                ],
              ),
              const SizedBox(height: 16),
              FilledButton(
                onPressed: () => Navigator.push(
                  context,
                  MaterialPageRoute(builder: (_) => CashInPage(session: widget.session)),
                ),
                child: const Text('Cash In'),
              ),
              const SizedBox(height: 18),
              const Text('Recent completed', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
              const SizedBox(height: 8),
              if (recent.isEmpty)
                const BrandPanel(
                  child: Text('No completed trips yet.', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600)),
                )
              else
                ...recent.map(
                  (trip) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: BrandPanel(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            trip.requestedAt == null
                                ? trip.reference
                                : _fmt(trip.requestedAt!),
                            style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12),
                          ),
                          const SizedBox(height: 4),
                          Text('${trip.pickup} → ${trip.dropoff}', style: const TextStyle(fontWeight: FontWeight.w700)),
                          const SizedBox(height: 10),
                          Row(
                            children: [
                              Expanded(child: _MoneyCol(label: 'Fare', value: peso(trip.fare))),
                              Expanded(
                                child: _MoneyCol(
                                  label: 'Your earnings',
                                  value: peso(trip.driverAmount ?? trip.fare),
                                  color: brandSuccess,
                                ),
                              ),
                              Expanded(
                                child: _MoneyCol(
                                  label: 'Platform fee',
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
          ],
        ),
      ),
    );
  }

  String _fmt(DateTime value) {
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    final h = value.hour % 12 == 0 ? 12 : value.hour % 12;
    final ampm = value.hour >= 12 ? 'PM' : 'AM';
    final m = value.minute.toString().padLeft(2, '0');
    return '${months[value.month - 1]} ${value.day}, $h:$m $ampm';
  }
}

class _StatCard extends StatelessWidget {
  const _StatCard({required this.label, required this.value, this.hint});
  final String label;
  final String value;
  final String? hint;

  @override
  Widget build(BuildContext context) {
    return BrandPanel(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 11)),
          const SizedBox(height: 4),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 14)),
          if (hint != null) ...[
            const SizedBox(height: 2),
            Text(hint!, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 11)),
          ],
        ],
      ),
    );
  }
}

class _MoneyCol extends StatelessWidget {
  const _MoneyCol({required this.label, required this.value, this.color = brandInk});
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
