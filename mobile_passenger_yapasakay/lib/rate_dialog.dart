import 'package:flutter/material.dart';

import 'theme.dart';

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
