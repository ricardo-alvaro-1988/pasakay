import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'theme.dart';

/// Shared digit-slot + numeric keypad PIN experience for Set PIN and login.
class PinEntryPanel extends StatefulWidget {
  const PinEntryPanel({
    super.key,
    required this.title,
    required this.subtitle,
    this.minLength = 4,
    this.maxLength = 6,
    this.autoSubmitAt,
    this.busy = false,
    this.error,
    this.actionLabel,
    this.onSubmit,
    this.onChanged,
  });

  final String title;
  final String subtitle;
  final int minLength;
  final int maxLength;
  final int? autoSubmitAt;
  final bool busy;
  final String? error;
  final String? actionLabel;
  final ValueChanged<String>? onSubmit;
  final ValueChanged<String>? onChanged;

  @override
  State<PinEntryPanel> createState() => PinEntryPanelState();
}

class PinEntryPanelState extends State<PinEntryPanel> with SingleTickerProviderStateMixin {
  String _value = '';
  late final AnimationController _shake;
  late final Animation<double> _shakeAnim;

  String get value => _value;

  @override
  void initState() {
    super.initState();
    _shake = AnimationController(vsync: this, duration: const Duration(milliseconds: 420));
    _shakeAnim = TweenSequence<double>([
      TweenSequenceItem(tween: Tween(begin: 0, end: -10), weight: 1),
      TweenSequenceItem(tween: Tween(begin: -10, end: 10), weight: 2),
      TweenSequenceItem(tween: Tween(begin: 10, end: -8), weight: 2),
      TweenSequenceItem(tween: Tween(begin: -8, end: 6), weight: 2),
      TweenSequenceItem(tween: Tween(begin: 6, end: 0), weight: 1),
    ]).animate(CurvedAnimation(parent: _shake, curve: Curves.easeOut));
  }

  @override
  void didUpdateWidget(covariant PinEntryPanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.error != null &&
        widget.error!.isNotEmpty &&
        widget.error != oldWidget.error &&
        !_shake.isAnimating) {
      _shake.forward(from: 0);
    }
  }

  @override
  void dispose() {
    _shake.dispose();
    super.dispose();
  }

  void clear({bool notify = true}) {
    setState(() => _value = '');
    if (notify) widget.onChanged?.call(_value);
  }

  void _append(String digit) {
    if (widget.busy || _value.length >= widget.maxLength) return;
    HapticFeedback.selectionClick();
    setState(() => _value += digit);
    widget.onChanged?.call(_value);
    final autoAt = widget.autoSubmitAt;
    if (autoAt != null && _value.length == autoAt) {
      widget.onSubmit?.call(_value);
    }
  }

  void _backspace() {
    if (widget.busy || _value.isEmpty) return;
    HapticFeedback.selectionClick();
    setState(() => _value = _value.substring(0, _value.length - 1));
    widget.onChanged?.call(_value);
  }

  void _submit() {
    if (widget.busy || _value.length < widget.minLength) return;
    widget.onSubmit?.call(_value);
  }

  @override
  Widget build(BuildContext context) {
    final canContinue = _value.length >= widget.minLength && !widget.busy;
    final error = widget.error?.trim();
    final hasError = error != null && error.isNotEmpty;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SizedBox(height: 8),
        Center(
          child: Container(
            width: 72,
            height: 72,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: brandAccentSoft,
              border: Border.all(color: brandRed.withValues(alpha: 0.18)),
            ),
            child: const Icon(Icons.lock_rounded, color: brandRed, size: 34),
          ),
        ),
        const SizedBox(height: 18),
        Text(
          widget.title,
          textAlign: TextAlign.center,
          style: const TextStyle(fontSize: 24, fontWeight: FontWeight.w800, color: brandInk),
        ),
        const SizedBox(height: 8),
        Text(
          widget.subtitle,
          textAlign: TextAlign.center,
          style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, height: 1.35),
        ),
        const SizedBox(height: 28),
        AnimatedBuilder(
          animation: _shakeAnim,
          builder: (context, child) => Transform.translate(
            offset: Offset(_shakeAnim.value, 0),
            child: child,
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: List.generate(widget.maxLength, (i) {
              final filled = i < _value.length;
              final active = i == _value.length && !filled;
              return AnimatedContainer(
                duration: const Duration(milliseconds: 160),
                margin: const EdgeInsets.symmetric(horizontal: 5),
                width: 42,
                height: 52,
                decoration: BoxDecoration(
                  color: filled ? brandRed.withValues(alpha: 0.08) : brandSurface,
                  borderRadius: BorderRadius.circular(14),
                  border: Border.all(
                    color: hasError
                        ? brandSos
                        : active
                            ? brandRed
                            : filled
                                ? brandRed.withValues(alpha: 0.55)
                                : brandLine,
                    width: active || filled ? 2 : 1.2,
                  ),
                  boxShadow: active
                      ? [
                          BoxShadow(
                            color: brandRed.withValues(alpha: 0.18),
                            blurRadius: 12,
                            offset: const Offset(0, 4),
                          ),
                        ]
                      : null,
                ),
                child: Center(
                  child: filled
                      ? Container(
                          width: 12,
                          height: 12,
                          decoration: const BoxDecoration(color: brandRed, shape: BoxShape.circle),
                        )
                      : null,
                ),
              );
            }),
          ),
        ),
        if (hasError) ...[
          const SizedBox(height: 12),
          Text(
            error,
            textAlign: TextAlign.center,
            style: const TextStyle(color: brandSos, fontWeight: FontWeight.w700),
          ),
        ],
        const SizedBox(height: 28),
        _PinKeypad(
          enabled: !widget.busy,
          onDigit: _append,
          onBackspace: _backspace,
        ),
        if (widget.actionLabel != null) ...[
          const SizedBox(height: 18),
          FilledButton(
            onPressed: canContinue ? _submit : null,
            child: widget.busy
                ? const SizedBox(
                    width: 22,
                    height: 22,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                  )
                : Text(widget.actionLabel!),
          ),
        ],
      ],
    );
  }
}

class _PinKeypad extends StatelessWidget {
  const _PinKeypad({
    required this.enabled,
    required this.onDigit,
    required this.onBackspace,
  });

  final bool enabled;
  final ValueChanged<String> onDigit;
  final VoidCallback onBackspace;

  @override
  Widget build(BuildContext context) {
    Widget key(String label, {VoidCallback? onTap, IconData? icon}) {
      return Expanded(
        child: Padding(
          padding: const EdgeInsets.all(6),
          child: Material(
            color: brandSurface,
            borderRadius: BorderRadius.circular(22),
            child: InkWell(
              onTap: enabled ? onTap : null,
              borderRadius: BorderRadius.circular(22),
              child: Container(
                height: 58,
                alignment: Alignment.center,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(22),
                  border: Border.all(color: brandLine),
                  boxShadow: const [
                    BoxShadow(color: Color(0x0A16181D), blurRadius: 8, offset: Offset(0, 3)),
                  ],
                ),
                child: icon != null
                    ? Icon(icon, color: brandInk)
                    : Text(
                        label,
                        style: TextStyle(
                          fontSize: 22,
                          fontWeight: FontWeight.w800,
                          color: enabled ? brandInk : brandMuted,
                        ),
                      ),
              ),
            ),
          ),
        ),
      );
    }

    return Column(
      children: [
        for (final row in const [
          ['1', '2', '3'],
          ['4', '5', '6'],
          ['7', '8', '9'],
        ])
          Row(
            children: [
              for (final d in row) key(d, onTap: () => onDigit(d)),
            ],
          ),
        Row(
          children: [
            const Expanded(child: SizedBox(height: 58)),
            key('0', onTap: () => onDigit('0')),
            key('', icon: Icons.backspace_outlined, onTap: onBackspace),
          ],
        ),
      ],
    );
  }
}

/// Full-screen scaffold wrapping [PinEntryPanel].
class PinEntryPage extends StatelessWidget {
  const PinEntryPage({
    super.key,
    required this.title,
    required this.subtitle,
    this.onBack,
    this.minLength = 4,
    this.maxLength = 6,
    this.autoSubmitAt,
    this.busy = false,
    this.error,
    this.actionLabel = 'Continue',
    this.onSubmit,
  });

  final String title;
  final String subtitle;
  final VoidCallback? onBack;
  final int minLength;
  final int maxLength;
  final int? autoSubmitAt;
  final bool busy;
  final String? error;
  final String? actionLabel;
  final ValueChanged<String>? onSubmit;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        backgroundColor: brandCanvas,
        leading: onBack == null
            ? null
            : IconButton(icon: const Icon(Icons.arrow_back), onPressed: busy ? null : onBack),
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.fromLTRB(20, 0, 20, 24),
          children: [
            PinEntryPanel(
              title: title,
              subtitle: subtitle,
              minLength: minLength,
              maxLength: maxLength,
              autoSubmitAt: autoSubmitAt,
              busy: busy,
              error: error,
              actionLabel: actionLabel,
              onSubmit: onSubmit,
            ),
          ],
        ),
      ),
    );
  }
}
