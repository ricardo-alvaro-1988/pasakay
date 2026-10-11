import 'package:flutter/material.dart';

import 'api.dart';
import 'models.dart';
import 'session.dart';
import 'theme.dart';

/// Returns true if submitted, false if Later/dismissed, null if dialog closed without choice.
Future<bool?> showCompletedRideRateDialog(
  BuildContext context, {
  required CustomerTrip trip,
  required CustomerSession session,
}) async {
  var rating = 5;
  final comment = TextEditingController();
  var busy = false;

  final result = await showPassengerDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (ctx) => StatefulBuilder(
      builder: (ctx, setState) => Padding(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              decoration: BoxDecoration(
                color: const Color(0xFFE6F6EE),
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: brandSuccess, width: 1.5),
              ),
              child: const Row(
                children: [
                  Icon(Icons.check_circle, color: brandSuccess, size: 26),
                  SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'Ride Completed',
                      style: TextStyle(
                        color: brandSuccess,
                        fontWeight: FontWeight.w900,
                        fontSize: 18,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),
            Text(
              trip.reference,
              style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12),
            ),
            const SizedBox(height: 4),
            Text(
              '${trip.pickup} → ${trip.dropoff}',
              style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
            ),
            if (trip.riderName != null && trip.riderName!.isNotEmpty) ...[
              const SizedBox(height: 6),
              Text(
                'Rider: ${trip.riderName}',
                style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
              ),
            ],
            const SizedBox(height: 16),
            Text('Rate your ride', style: Theme.of(ctx).textTheme.titleMedium),
            const SizedBox(height: 8),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(5, (i) {
                final star = i + 1;
                return IconButton(
                  onPressed: busy ? null : () => setState(() => rating = star),
                  icon: Icon(star <= rating ? Icons.star : Icons.star_border, color: Colors.amber, size: 32),
                );
              }),
            ),
            TextField(
              controller: comment,
              enabled: !busy,
              decoration: const InputDecoration(labelText: 'Comment (optional)'),
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: busy ? null : () => Navigator.pop(ctx, false),
                    child: const Text('Later'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton(
                    onPressed: busy
                        ? null
                        : () async {
                            setState(() => busy = true);
                            try {
                              final desk = await session.api.rate(
                                trip.id,
                                rating,
                                comment: comment.text.trim().isEmpty ? null : comment.text.trim(),
                              );
                              session.updateDesk(desk);
                              if (ctx.mounted) Navigator.pop(ctx, true);
                            } on ApiException catch (ex) {
                              setState(() => busy = false);
                              if (ctx.mounted) {
                                ScaffoldMessenger.of(ctx).showSnackBar(SnackBar(content: Text(ex.message)));
                              }
                            } catch (_) {
                              setState(() => busy = false);
                              if (ctx.mounted) {
                                ScaffoldMessenger.of(ctx).showSnackBar(
                                  const SnackBar(content: Text('Could not submit rating.')),
                                );
                              }
                            }
                          },
                    child: busy
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                          )
                        : const Text('Submit'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    ),
  );
  comment.dispose();
  return result;
}

/// Legacy booking-tab helper (stars only). Prefer [showCompletedRideRateDialog] for completed rides.
Future<(int rating, String comment)?> showRateTripDialog(BuildContext context) async {
  var rating = 5;
  final comment = TextEditingController();
  final result = await showPassengerDialog<(int, String)?>(
    context: context,
    builder: (ctx) => StatefulBuilder(
      builder: (ctx, setState) => Padding(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 16),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              decoration: BoxDecoration(
                color: const Color(0xFFE6F6EE),
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: brandSuccess, width: 1.5),
              ),
              child: const Row(
                children: [
                  Icon(Icons.check_circle, color: brandSuccess, size: 22),
                  SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'Ride Completed',
                      style: TextStyle(color: brandSuccess, fontWeight: FontWeight.w900, fontSize: 16),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 14),
            Text('Rate your ride', style: Theme.of(ctx).textTheme.titleMedium),
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(5, (i) {
                final star = i + 1;
                return IconButton(
                  onPressed: () => setState(() => rating = star),
                  icon: Icon(star <= rating ? Icons.star : Icons.star_border, color: Colors.amber),
                );
              }),
            ),
            TextField(
              controller: comment,
              decoration: const InputDecoration(labelText: 'Comment (optional)'),
              maxLines: 3,
            ),
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => Navigator.pop(ctx),
                    child: const Text('Cancel'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton(
                    onPressed: () => Navigator.pop(ctx, (rating, comment.text.trim())),
                    child: const Text('Submit'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    ),
  );
  comment.dispose();
  return result;
}
