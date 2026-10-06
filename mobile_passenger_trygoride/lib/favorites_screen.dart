import 'dart:async';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api.dart';
import 'models.dart';
import 'session.dart';
import 'theme.dart';

typedef BookRiderCallback = void Function({required String riderId, String? vehicleType});

class FavoritesScreen extends StatefulWidget {
  const FavoritesScreen({super.key, required this.session, required this.onBookRider});

  final CustomerSession session;
  final BookRiderCallback onBookRider;

  @override
  State<FavoritesScreen> createState() => _FavoritesScreenState();
}

class _FavoritesScreenState extends State<FavoritesScreen> {
  List<FavoriteRider> _rows = const [];
  bool _loading = true;
  String? _error;
  Timer? _poll;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
    _poll = Timer.periodic(const Duration(seconds: 8), (_) {
      if (mounted) unawaited(_load(silent: true));
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }
    try {
      final rows = await widget.session.api.favorites();
      if (!mounted) return;
      setState(() {
        _rows = rows;
        _loading = false;
        _error = null;
      });
    } on ApiException catch (ex) {
      if (!mounted) return;
      if (silent) return;
      setState(() {
        _error = ex.message;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;
      if (silent) return;
      setState(() {
        _error = 'Could not load favorites.';
        _loading = false;
      });
    }
  }

  Future<void> _remove(FavoriteRider rider) async {
    final ok = await showPassengerConfirm(
      context,
      title: 'Remove favorite?',
      message: 'Remove ${rider.fullName} from favorites?',
      cancelLabel: 'Keep',
      confirmLabel: 'Remove',
      confirmColor: brandSos,
    );
    if (ok != true || !mounted) return;
    try {
      await widget.session.api.removeFavorite(rider.riderId);
      await _load();
    } on ApiException catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
      }
    }
  }

  Future<void> _call(FavoriteRider rider) async {
    final phone = (rider.phoneNumber ?? '').trim();
    if (phone.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No mobile number for this rider.')),
      );
      return;
    }
    final uri = Uri(scheme: 'tel', path: phone);
    if (!await launchUrl(uri)) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Could not dial $phone')),
        );
      }
    }
  }

  void _book(FavoriteRider rider) {
    if (!rider.isOnline) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('This rider is offline right now.')),
      );
      return;
    }
    if (rider.isBusy) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('This rider is on another trip right now.')),
      );
      return;
    }
    if (!rider.canBook) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('This rider is not available to book right now.')),
      );
      return;
    }
    widget.onBookRider(riderId: rider.riderId, vehicleType: rider.vehicleType);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text('Booking with ${rider.fullName}. Set your stops on Home.')),
    );
  }

  /// Same rules as web Favs: Busy → Online → Offline (not tied only to canBook).
  String _statusLabel(FavoriteRider rider) {
    if (rider.isBusy) return 'Busy';
    if (rider.isOnline) return 'Online';
    return 'Offline';
  }

  Color _statusColor(FavoriteRider rider) {
    if (rider.isBusy) return const Color(0xFFE0B400);
    if (rider.isOnline) return brandSuccess;
    return brandMuted;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Favorites'),
        actions: [
          IconButton(onPressed: _load, icon: const Icon(Icons.refresh)),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : _error != null
              ? Center(child: Text(_error!))
              : _rows.isEmpty
                  ? const Center(
                      child: Padding(
                        padding: EdgeInsets.all(24),
                        child: Text(
                          'No favorite riders yet.\nAdd them from Booking history after a completed trip.',
                          textAlign: TextAlign.center,
                          style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                        ),
                      ),
                    )
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: _rows.length,
                        separatorBuilder: (_, _) => const SizedBox(height: 10),
                        itemBuilder: (ctx, i) {
                          final rider = _rows[i];
                          final phone = (rider.phoneNumber ?? '').trim();
                          final status = _statusLabel(rider);
                          return BrandPanel(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Row(
                                  children: [
                                    Expanded(
                                      child: Text(
                                        rider.fullName,
                                        style: Theme.of(context).textTheme.titleMedium,
                                      ),
                                    ),
                                    Container(
                                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                      decoration: BoxDecoration(
                                        color: _statusColor(rider).withValues(alpha: 0.12),
                                        borderRadius: BorderRadius.circular(999),
                                      ),
                                      child: Text(
                                        status,
                                        style: TextStyle(
                                          color: _statusColor(rider),
                                          fontWeight: FontWeight.w800,
                                          fontSize: 11,
                                        ),
                                      ),
                                    ),
                                  ],
                                ),
                                Text(
                                  [
                                    vehicleLabel(rider.vehicleType),
                                    rider.plateNumber,
                                    if ((rider.vehicleModel ?? '').isNotEmpty) rider.vehicleModel!,
                                  ].join(' · '),
                                  style: const TextStyle(color: brandMuted),
                                ),
                                Text(rider.companyName, style: const TextStyle(fontSize: 12, color: brandMuted)),
                                if (phone.isNotEmpty) ...[
                                  const SizedBox(height: 4),
                                  Text(
                                    phone,
                                    style: const TextStyle(fontWeight: FontWeight.w700),
                                  ),
                                ],
                                const SizedBox(height: 10),
                                Wrap(
                                  spacing: 8,
                                  runSpacing: 8,
                                  children: [
                                    FilledButton(
                                      onPressed: rider.canBook ? () => _book(rider) : null,
                                      child: Text(rider.canBook ? 'Book' : 'Unavailable'),
                                    ),
                                    if (phone.isNotEmpty)
                                      OutlinedButton.icon(
                                        onPressed: () => _call(rider),
                                        icon: const Icon(Icons.call, size: 18),
                                        label: const Text('Call'),
                                      ),
                                    OutlinedButton(
                                      onPressed: () => _remove(rider),
                                      child: const Text('Remove'),
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          );
                        },
                      ),
                    ),
    );
  }
}
