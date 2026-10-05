import 'package:flutter/material.dart';

import 'models.dart';
import 'theme.dart';

class StopRail extends StatelessWidget {
  const StopRail({
    super.key,
    required this.pickup,
    required this.dropoff,
    required this.onPickupTap,
    required this.onDropoffTap,
    this.pickupHint = 'Tap to set pickup',
    this.dropoffHint = 'Tap to set drop-off',
  });

  final Stop? pickup;
  final Stop? dropoff;
  final VoidCallback onPickupTap;
  final VoidCallback onDropoffTap;
  final String pickupHint;
  final String dropoffHint;

  @override
  Widget build(BuildContext context) {
    return BrandPanel(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Column(
        children: [
          _StopRow(
            isPickup: true,
            label: 'Pickup',
            value: pickup?.label ?? pickupHint,
            onTap: onPickupTap,
            isSet: pickup != null,
          ),
          Padding(
            padding: const EdgeInsets.only(left: 17),
            child: Align(
              alignment: Alignment.centerLeft,
              child: Container(width: 2, height: 14, color: brandLine),
            ),
          ),
          _StopRow(
            isPickup: false,
            label: 'Drop-off',
            value: dropoff?.label ?? dropoffHint,
            onTap: onDropoffTap,
            isSet: dropoff != null,
          ),
        ],
      ),
    );
  }
}

class BeatingPin extends StatefulWidget {
  const BeatingPin({super.key, required this.color});

  final Color color;

  @override
  State<BeatingPin> createState() => _BeatingPinState();
}

class _BeatingPinState extends State<BeatingPin> with SingleTickerProviderStateMixin {
  late final AnimationController _controller;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(vsync: this, duration: const Duration(milliseconds: 1400))..repeat();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 28,
      height: 28,
      child: AnimatedBuilder(
        animation: _controller,
        builder: (context, child) {
          final t = _controller.value;
          final scale = 0.92 + (0.08 * (1 - ((t * 2 - 1).abs())));
          return Stack(
            alignment: Alignment.center,
            children: [
              _ring(t, 0),
              _ring((t + 0.28) % 1.0, 0.45),
              Transform.scale(
                scale: scale,
                child: Container(
                  width: 12,
                  height: 12,
                  decoration: BoxDecoration(
                    color: widget.color,
                    shape: BoxShape.circle,
                    border: Border.all(color: Colors.white, width: 2),
                    boxShadow: [
                      BoxShadow(color: widget.color.withValues(alpha: 0.35), blurRadius: 6),
                    ],
                  ),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Widget _ring(double t, double delay) {
    final phase = ((t + delay) % 1.0);
    final size = 10 + phase * 18;
    final opacity = (1 - phase).clamp(0.0, 1.0) * 0.45;
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: Border.all(color: widget.color.withValues(alpha: opacity), width: 2),
      ),
    );
  }
}

class _StopRow extends StatelessWidget {
  const _StopRow({
    required this.isPickup,
    required this.label,
    required this.value,
    required this.onTap,
    required this.isSet,
  });

  final bool isPickup;
  final String label;
  final String value;
  final VoidCallback onTap;
  final bool isSet;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 4),
        child: Row(
          children: [
            BeatingPin(color: isPickup ? brandSuccess : brandRed),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    label,
                    style: TextStyle(
                      color: isPickup ? const Color(0xFF1570EF) : brandRed,
                      fontWeight: FontWeight.w700,
                      fontSize: 12,
                    ),
                  ),
                  Text(
                    value,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      color: isSet ? brandInk : brandMuted,
                    ),
                  ),
                ],
              ),
            ),
            const Icon(Icons.chevron_right, color: brandMuted),
          ],
        ),
      ),
    );
  }
}
