import 'dart:async';

import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

import 'api.dart';
import 'chat_screen.dart';
import 'models.dart';
import 'session.dart';
import 'sos.dart';
import 'theme.dart';

class TripScreen extends StatelessWidget {
  const TripScreen({super.key, required this.session});

  final RiderSession session;

  Future<void> _call(String phone) async {
    final cleaned = phone.replaceAll(RegExp(r'[^\d+]'), '');
    if (cleaned.isEmpty) {
      return;
    }
    await launchUrl(Uri(scheme: 'tel', path: cleaned));
  }

  Future<void> _navigate(RiderTrip trip) async {
    final dest = trip.status == 'Waiting' || trip.canStart
        ? (lat: trip.pickupLat, lng: trip.pickupLng, label: trip.pickup)
        : (lat: trip.dropoffLat, lng: trip.dropoffLng, label: trip.dropoff);
    Uri uri;
    if (dest.lat != null && dest.lng != null) {
      uri = Uri.parse(
        'https://www.google.com/maps/dir/?api=1&destination=${dest.lat},${dest.lng}&travelmode=driving',
      );
    } else {
      uri = Uri.parse(
        'https://www.google.com/maps/dir/?api=1&destination=${Uri.encodeComponent(dest.label)}&travelmode=driving',
      );
    }
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }

  Future<void> _sos(BuildContext context, RiderTrip trip) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Send SOS?'),
        content: const Text('This alerts your operator and Super Admin.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Send SOS')),
        ],
      ),
    );
    if (confirmed != true) {
      return;
    }
    final last = await sosLastKnown();
    try {
      await session.api.sos(trip.tripId, message: 'Rider SOS', lat: last.$1, lng: last.$2);
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('SOS sent.')));
      }
      unawaited(refineSosLocation(session.api, trip.tripId));
    } on ApiException catch (ex) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: session,
      builder: (context, _) {
        final live = session.desk?.activeTrip;
        final trip = live ?? session.lastTrip;
        if (trip == null) {
          return Scaffold(
            appBar: AppBar(title: const Text('Trip')),
            body: const Center(child: Text('Trip finished.', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600))),
          );
        }

        final ended = live == null;
        final canChat = !ended && trip.canChat;
        final risk = _riskBadgeFor(trip);
        final statusLabel = ended && !_endedStatus(trip.status) ? 'Ended' : trip.status;

        return Scaffold(
          appBar: AppBar(title: Text(trip.reference)),
          body: ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
            children: [
              Align(
                alignment: Alignment.centerLeft,
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: brandAccentSoft,
                    borderRadius: BorderRadius.circular(999),
                  ),
                  child: Text(
                    statusLabel.toUpperCase(),
                    style: const TextStyle(color: brandRed, fontWeight: FontWeight.w800, fontSize: 12),
                  ),
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(trip.customerName, style: Theme.of(context).textTheme.titleLarge),
                    const SizedBox(height: 4),
                    Text(trip.customerPhone, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600)),
                    const SizedBox(height: 8),
                    Text(
                      'Persons: ${passengerLabel(trip.passengerCount)}',
                      style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 16, color: brandRed),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: trip.customerPhone.trim().isEmpty ? null : () => _call(trip.customerPhone),
                            icon: const Icon(Icons.call),
                            label: const Text('Call'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: OutlinedButton.icon(
                            onPressed: !canChat
                                ? null
                                : () => Navigator.push(
                                      context,
                                      MaterialPageRoute(
                                        builder: (_) => ChatScreen(session: session, tripId: trip.tripId),
                                      ),
                                    ),
                            icon: const Icon(Icons.chat_bubble_outline),
                            label: Text(session.chatUnread > 0 ? 'Chat (${session.chatUnread})' : 'Chat'),
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                color: risk.background,
                borderColor: risk.border,
                borderWidth: 2,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(999),
                            border: Border.all(color: risk.border),
                          ),
                          child: Text(
                            risk.label.toUpperCase(),
                            style: TextStyle(color: risk.text, fontWeight: FontWeight.w900, fontSize: 12),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            risk.title,
                            style: const TextStyle(fontWeight: FontWeight.w800),
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Text(
                      risk.message,
                      style: const TextStyle(fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        _HistoryPill(label: 'Completed', value: '${trip.completedBookingCount}', color: brandSuccess),
                        _HistoryPill(label: 'Cancelled', value: '${trip.cancelledBookingCount}', color: brandRed),
                        if (trip.lastCompletedAt != null)
                          _HistoryPill(label: 'Last ride', value: _historyDate(trip.lastCompletedAt!), color: brandMuted),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('PICKUP', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12)),
                    const SizedBox(height: 4),
                    Text(trip.pickup, style: const TextStyle(fontWeight: FontWeight.w700)),
                    const SizedBox(height: 14),
                    const Text('DROP-OFF', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12)),
                    const SizedBox(height: 4),
                    Text(trip.dropoff, style: const TextStyle(fontWeight: FontWeight.w700)),
                    const SizedBox(height: 14),
                    const Text('PERSONS', style: TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12)),
                    const SizedBox(height: 4),
                    Text(
                      passengerLabel(trip.passengerCount),
                      style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 18),
                    ),
                    const SizedBox(height: 14),
                    Text(
                      '${peso(trip.fare)}  ·  ${trip.distanceKm.toStringAsFixed(1)} km  ·  ${paymentLabel(trip.paymentMethod)}',
                      style: const TextStyle(fontWeight: FontWeight.w800),
                    ),
                  ],
                ),
              ),
              if (ended) ...[
                const SizedBox(height: 14),
                const Text(
                  'Trip ended. Chat is closed. History stays on the booking.',
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
              ] else ...[
              const SizedBox(height: 14),
              FilledButton.icon(
                onPressed: () => _navigate(trip),
                icon: const Icon(Icons.navigation),
                label: Text(trip.canStart ? 'Navigate to pickup' : 'Navigate to drop-off'),
              ),
              if (trip.canStart) ...[
                const SizedBox(height: 8),
                FilledButton(
                  onPressed: () => session.startTrip(trip.tripId),
                  child: const Text('Start trip'),
                ),
              ],
              if (trip.canComplete) ...[
                const SizedBox(height: 8),
                FilledButton(
                  onPressed: () async {
                    final ok = await showDialog<bool>(
                      context: context,
                      builder: (context) => AlertDialog(
                        title: const Text('Complete trip?'),
                        content: const Text('This deducts System from your wallet.'),
                        actions: [
                          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
                          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Complete')),
                        ],
                      ),
                    );
                    if (ok == true) {
                      await session.completeTrip(trip.tripId);
                      if (context.mounted) {
                        Navigator.pop(context);
                      }
                    }
                  },
                  child: const Text('Complete'),
                ),
              ],
              if (trip.canCancel || trip.status.toLowerCase() == 'waiting') ...[
                const SizedBox(height: 8),
                OutlinedButton(
                  onPressed: () async {
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
                        await session.cancelTrip(trip.tripId);
                        if (context.mounted) {
                          Navigator.pop(context);
                        }
                      } catch (ex) {
                        if (context.mounted) {
                          ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$ex')));
                        }
                      }
                    }
                  },
                  child: const Text('Cancel booking'),
                ),
              ],
              if (trip.canSos) ...[
                const SizedBox(height: 16),
                FilledButton.icon(
                  style: FilledButton.styleFrom(
                    backgroundColor: brandSos,
                    foregroundColor: Colors.white,
                    minimumSize: const Size.fromHeight(48),
                  ),
                  onPressed: () => _sos(context, trip),
                  icon: const Icon(Icons.sos),
                  label: const Text('SOS'),
                ),
              ],
              ],
            ],
          ),
        );
      },
    );
  }
}

class _HistoryPill extends StatelessWidget {
  const _HistoryPill({required this.label, required this.value, required this.color});

  final String label;
  final String value;
  final Color color;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: color.withValues(alpha: 0.22)),
      ),
      child: RichText(
        text: TextSpan(
          style: const TextStyle(fontFamily: 'inherit'),
          children: [
            TextSpan(text: '$label: ', style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700)),
            TextSpan(text: value, style: TextStyle(color: color, fontWeight: FontWeight.w900)),
          ],
        ),
      ),
    );
  }
}

class _RiskBadge {
  const _RiskBadge({
    required this.label,
    required this.title,
    required this.message,
    required this.background,
    required this.border,
    required this.text,
  });

  final String label;
  final String title;
  final String message;
  final Color background;
  final Color border;
  final Color text;
}

_RiskBadge _riskBadgeFor(RiderTrip trip) {
  if (trip.previousBookingCount == 0) {
    return const _RiskBadge(
      label: 'New customer',
      title: 'Customer verification needed',
      message: 'This customer has no previous bookings yet. Verify carefully before proceeding.',
      background: brandWarnBg,
      border: brandWarnLine,
      text: brandWarnLine,
    );
  }

  if (trip.cancelledBookingCount >= 3 && trip.cancelledBookingCount >= trip.completedBookingCount) {
    return const _RiskBadge(
      label: 'High risk',
      title: 'Has many cancellations',
      message: 'This customer has a heavy cancellation history. Confirm the trip details before moving.',
      background: brandDangerBg,
      border: brandRed,
      text: brandRed,
    );
  }

  if (trip.completedBookingCount >= 5 && trip.cancelledBookingCount <= 1) {
    return const _RiskBadge(
      label: 'Trusted',
      title: 'Trusted repeat customer',
      message: 'This customer has strong completed-booking history and very few cancellations.',
      background: Color(0xFFEAF7ED),
      border: brandSuccess,
      text: brandSuccess,
    );
  }

  return const _RiskBadge(
    label: 'Review',
    title: 'Customer booking history',
    message: 'Check the booking counts below before proceeding with the ride.',
    background: Color(0xFFF5F7FA),
    border: brandLine,
    text: brandMuted,
  );
}

String _historyDate(DateTime value) {
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  return '${months[value.month - 1]} ${value.day}, ${value.year}';
}

bool _endedStatus(String status) {
  final value = status.toLowerCase();
  return value == 'completed' || value == 'cancelled' || value == '1' || value == '2';
}
