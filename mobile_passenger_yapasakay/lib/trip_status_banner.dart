import 'package:flutter/material.dart';

import 'models.dart';
import 'theme.dart';

class _StatusLook {
  const _StatusLook({
    required this.bg,
    required this.fg,
    required this.border,
    required this.icon,
    this.subtitle,
    this.beat = false,
  });

  final Color bg;
  final Color fg;
  final Color border;
  final IconData icon;
  final String? subtitle;
  final bool beat;
}

_StatusLook _lookFor(String status) {
  switch (status) {
    case 'Pending':
      return const _StatusLook(
        bg: brandWarnBg,
        fg: Color(0xFF8A6A00),
        border: brandWarnLine,
        icon: Icons.radar,
        subtitle: 'Broadcasting to nearby riders…',
        beat: true,
      );
    case 'Waiting':
      return const _StatusLook(
        bg: brandRed,
        fg: Colors.white,
        border: brandRed,
        icon: Icons.directions_bike,
        subtitle: 'Your rider accepted — heading to pickup.',
        beat: true,
      );
    case 'Ongoing':
      return const _StatusLook(
        bg: Color(0xFF0B3D2E),
        fg: Colors.white,
        border: Color(0xFF0B3D2E),
        icon: Icons.navigation,
        subtitle: 'Trip in progress — sit tight.',
        beat: true,
      );
    case 'Completed':
      return const _StatusLook(
        bg: Color(0xFFE6F6EE),
        fg: brandSuccess,
        border: brandSuccess,
        icon: Icons.check_circle,
      );
    case 'Cancelled':
      return const _StatusLook(
        bg: brandDangerBg,
        fg: brandSos,
        border: brandSos,
        icon: Icons.cancel,
      );
    default:
      return const _StatusLook(
        bg: brandChip,
        fg: brandInk,
        border: brandLine,
        icon: Icons.info_outline,
      );
  }
}

/// High-contrast status strip for active trips (Finding / On the way / Ongoing…).
class TripStatusBanner extends StatefulWidget {
  const TripStatusBanner({super.key, required this.status, this.compact = false});

  final String status;
  final bool compact;

  @override
  State<TripStatusBanner> createState() => _TripStatusBannerState();
}

class _TripStatusBannerState extends State<TripStatusBanner> with SingleTickerProviderStateMixin {
  late final AnimationController _beat;
  late final Animation<double> _scale;
  late final Animation<double> _glow;

  @override
  void initState() {
    super.initState();
    // Soft “heartbeat”: quick pulse, brief rest, repeat.
    _beat = AnimationController(vsync: this, duration: const Duration(milliseconds: 1100));
    _scale = TweenSequence<double>([
      TweenSequenceItem(tween: Tween(begin: 1.0, end: 1.035), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 1.035, end: 1.0), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 1.0, end: 1.045), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 1.045, end: 1.0), weight: 20),
      TweenSequenceItem(tween: ConstantTween(1.0), weight: 26),
    ]).animate(CurvedAnimation(parent: _beat, curve: Curves.easeInOut));
    _glow = TweenSequence<double>([
      TweenSequenceItem(tween: Tween(begin: 0.0, end: 0.55), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 0.55, end: 0.15), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 0.15, end: 0.7), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 0.7, end: 0.0), weight: 20),
      TweenSequenceItem(tween: ConstantTween(0.0), weight: 26),
    ]).animate(CurvedAnimation(parent: _beat, curve: Curves.easeInOut));
    _syncBeat();
  }

  @override
  void didUpdateWidget(covariant TripStatusBanner oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.status != widget.status) _syncBeat();
  }

  void _syncBeat() {
    final shouldBeat = _lookFor(widget.status).beat;
    if (shouldBeat) {
      if (!_beat.isAnimating) _beat.repeat();
    } else {
      _beat
        ..stop()
        ..value = 0;
    }
  }

  @override
  void dispose() {
    _beat.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final look = _lookFor(widget.status);
    final title = tripHeadline(widget.status);
    final iconSize = widget.compact ? 34.0 : 38.0;

    Widget banner = Container(
      width: double.infinity,
      padding: EdgeInsets.symmetric(horizontal: 12, vertical: widget.compact ? 10 : 12),
      decoration: BoxDecoration(
        color: look.bg,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: look.border, width: 1.5),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          AnimatedBuilder(
            animation: _beat,
            builder: (context, child) {
              final pulse = look.beat ? _glow.value : 0.0;
              return Transform.scale(
                scale: look.beat ? (0.92 + (_scale.value - 1.0) * 2.2).clamp(0.92, 1.12) : 1.0,
                child: Container(
                  width: iconSize,
                  height: iconSize,
                  decoration: BoxDecoration(
                    color: look.fg == Colors.white
                        ? Colors.white.withValues(alpha: 0.2 + pulse * 0.25)
                        : look.fg.withValues(alpha: 0.12 + pulse * 0.2),
                    shape: BoxShape.circle,
                    boxShadow: look.beat && pulse > 0.05
                        ? [
                            BoxShadow(
                              color: (look.fg == Colors.white ? Colors.white : look.fg)
                                  .withValues(alpha: 0.35 * pulse),
                              blurRadius: 10 * pulse,
                              spreadRadius: 1.5 * pulse,
                            ),
                          ]
                        : null,
                  ),
                  child: child,
                ),
              );
            },
            child: Icon(look.icon, color: look.fg, size: widget.compact ? 18 : 20),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: TextStyle(
                    color: look.fg,
                    fontWeight: FontWeight.w900,
                    fontSize: widget.compact ? 15 : 17,
                    height: 1.15,
                    letterSpacing: -0.2,
                  ),
                ),
                if (!widget.compact && look.subtitle != null) ...[
                  const SizedBox(height: 2),
                  Text(
                    look.subtitle!,
                    style: TextStyle(
                      color: look.fg.withValues(alpha: 0.9),
                      fontWeight: FontWeight.w600,
                      fontSize: 12,
                      height: 1.25,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );

    if (!look.beat) return banner;

    return AnimatedBuilder(
      animation: _beat,
      builder: (context, child) {
        return Transform.scale(
          scale: _scale.value,
          alignment: Alignment.center,
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(16),
              boxShadow: [
                BoxShadow(
                  color: look.border.withValues(alpha: 0.22 + _glow.value * 0.28),
                  blurRadius: 10 + _glow.value * 14,
                  spreadRadius: _glow.value * 1.5,
                ),
              ],
            ),
            child: child,
          ),
        );
      },
      child: banner,
    );
  }
}
