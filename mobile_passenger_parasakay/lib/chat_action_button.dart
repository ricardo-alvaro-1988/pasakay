import 'package:flutter/material.dart';

import 'theme.dart';

/// Chat control that heartbeat-pulses when there are unread rider messages.
class ChatActionButton extends StatefulWidget {
  const ChatActionButton({
    super.key,
    required this.onPressed,
    this.unread = 0,
    this.compact = false,
    this.label,
  });

  final VoidCallback onPressed;
  final int unread;
  final bool compact;
  final String? label;

  @override
  State<ChatActionButton> createState() => _ChatActionButtonState();
}

class _ChatActionButtonState extends State<ChatActionButton> with SingleTickerProviderStateMixin {
  late final AnimationController _beat;
  late final Animation<double> _scale;
  late final Animation<double> _glow;

  bool get _hasUnread => widget.unread > 0;

  @override
  void initState() {
    super.initState();
    _beat = AnimationController(vsync: this, duration: const Duration(milliseconds: 1050));
    _scale = TweenSequence<double>([
      TweenSequenceItem(tween: Tween(begin: 1.0, end: 1.08), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 1.08, end: 1.0), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 1.0, end: 1.1), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 1.1, end: 1.0), weight: 20),
      TweenSequenceItem(tween: ConstantTween(1.0), weight: 26),
    ]).animate(CurvedAnimation(parent: _beat, curve: Curves.easeInOut));
    _glow = TweenSequence<double>([
      TweenSequenceItem(tween: Tween(begin: 0.0, end: 0.65), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 0.65, end: 0.2), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 0.2, end: 0.8), weight: 18),
      TweenSequenceItem(tween: Tween(begin: 0.8, end: 0.0), weight: 20),
      TweenSequenceItem(tween: ConstantTween(0.0), weight: 26),
    ]).animate(CurvedAnimation(parent: _beat, curve: Curves.easeInOut));
    _syncBeat();
  }

  @override
  void didUpdateWidget(covariant ChatActionButton oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.unread != widget.unread) _syncBeat();
  }

  void _syncBeat() {
    if (_hasUnread) {
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
    final label = widget.label ?? (_hasUnread ? 'New message' : 'Chat');
    final showLabel = !widget.compact || _hasUnread;

    return AnimatedBuilder(
      animation: _beat,
      builder: (context, child) {
        final g = _hasUnread ? _glow.value : 0.0;
        return Transform.scale(
          scale: _hasUnread ? _scale.value : 1,
          child: DecoratedBox(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(999),
              boxShadow: _hasUnread && g > 0.05
                  ? [
                      BoxShadow(
                        color: brandRed.withValues(alpha: 0.28 + g * 0.35),
                        blurRadius: 8 + g * 14,
                        spreadRadius: g * 2,
                      ),
                    ]
                  : null,
            ),
            child: child,
          ),
        );
      },
      child: Material(
        color: _hasUnread ? brandRed : brandChip,
        borderRadius: BorderRadius.circular(999),
        child: InkWell(
          onTap: widget.onPressed,
          borderRadius: BorderRadius.circular(999),
          child: Padding(
            padding: EdgeInsets.symmetric(
              horizontal: showLabel ? 12 : 10,
              vertical: widget.compact ? 8 : 10,
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Stack(
                  clipBehavior: Clip.none,
                  children: [
                    Icon(
                      Icons.chat_bubble_rounded,
                      size: 20,
                      color: _hasUnread ? Colors.white : brandInk,
                    ),
                    if (_hasUnread)
                      Positioned(
                        top: -6,
                        right: -8,
                        child: Container(
                          constraints: const BoxConstraints(minWidth: 18, minHeight: 18),
                          padding: const EdgeInsets.symmetric(horizontal: 4),
                          alignment: Alignment.center,
                          decoration: BoxDecoration(
                            color: Colors.white,
                            borderRadius: BorderRadius.circular(999),
                            border: Border.all(color: brandRed, width: 1.5),
                          ),
                          child: Text(
                            widget.unread > 9 ? '9+' : '${widget.unread}',
                            style: const TextStyle(
                              color: brandRed,
                              fontSize: 10,
                              fontWeight: FontWeight.w900,
                              height: 1,
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
                if (showLabel) ...[
                  const SizedBox(width: 8),
                  Text(
                    label,
                    style: TextStyle(
                      color: _hasUnread ? Colors.white : brandInk,
                      fontWeight: FontWeight.w800,
                      fontSize: 13,
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
