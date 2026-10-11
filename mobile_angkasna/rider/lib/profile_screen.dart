import 'package:flutter/material.dart';

import 'api.dart';
import 'models.dart';
import 'session.dart';
import 'theme.dart';
import 'wallet_screen.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key, required this.session});

  final RiderSession session;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: session,
      builder: (context, _) {
        final desk = session.desk;
        final photo = session.api.mediaUrl(desk?.photoUrl);
        final licensePhoto = session.api.mediaUrl(desk?.licensePhotoUrl);
        final payments = desk?.paymentMethods ?? const <String>[];
        return Scaffold(
          appBar: AppBar(title: const Text('Profile')),
          body: ListView(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
            children: [
              BrandPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        _Avatar(name: desk?.fullName ?? 'R', url: photo),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(desk?.fullName ?? 'Rider', style: Theme.of(context).textTheme.titleLarge),
                              const SizedBox(height: 4),
                              Text(
                                desk?.phoneNumber ?? '',
                                style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                              ),
                              const SizedBox(height: 8),
                              Wrap(
                                spacing: 8,
                                runSpacing: 8,
                                children: [
                                  _Tag(desk?.vehicleType ?? 'Vehicle'),
                                  _Tag((desk?.isActive ?? true) ? 'Active' : 'Inactive', danger: !(desk?.isActive ?? true)),
                                  if (desk?.isOnline == true) const _Tag('Online', success: true),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    _Fact(label: 'Operator', value: desk?.companyName ?? '—'),
                    _Fact(
                      label: 'Credibility',
                      value: desk == null
                          ? '—'
                          : '${desk.credibilityScore}'
                              '${desk.riderCancelCount == 0 ? '' : ' · ${desk.riderCancelCount} cancel${desk.riderCancelCount == 1 ? '' : 's'}'}',
                    ),
                    _Fact(label: 'Vehicle', value: desk?.vehicleLine ?? '—'),
                    _Fact(
                      label: 'Address',
                      value: (desk?.fullAddress ?? '').trim().isEmpty ? 'No address yet' : desk!.fullAddress!,
                    ),
                    _Fact(
                      label: 'License',
                      value: (desk?.licenseLine ?? '').isEmpty ? '—' : desk!.licenseLine,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Accepted payments', style: TextStyle(fontWeight: FontWeight.w800)),
                    const SizedBox(height: 4),
                    const Text(
                      'Turn on the methods you accept. Keep at least one.',
                      style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                    ),
                    const SizedBox(height: 10),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: ['Cash', 'GCash', 'Maya', 'Other'].map((method) {
                        final selected = payments.map(paymentCode).contains(method);
                        return FilterChip(
                          label: Text(paymentLabel(method)),
                          selected: selected,
                          onSelected: (on) => _togglePayment(context, session, payments, method, on),
                          selectedColor: brandAccentSoft,
                          checkmarkColor: brandRed,
                          labelStyle: TextStyle(
                            fontWeight: FontWeight.w800,
                            color: selected ? brandRed : brandInk,
                          ),
                          side: BorderSide(color: selected ? brandRed : brandLine),
                        );
                      }).toList(),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('Photos', style: TextStyle(fontWeight: FontWeight.w800)),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(child: _PhotoCard(label: 'Profile photo', url: photo)),
                        const SizedBox(width: 10),
                        Expanded(child: _PhotoCard(label: 'License photo', url: licensePhoto)),
                      ],
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Ask your operator to update details or photos.',
                      style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                padding: EdgeInsets.zero,
                child: ListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  leading: const Icon(Icons.account_balance_wallet_outlined, color: brandRed),
                  title: const Text('Wallet', style: TextStyle(fontWeight: FontWeight.w800)),
                  subtitle: Text(
                    peso(desk?.walletBalance ?? 0),
                    style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                  ),
                  trailing: const Icon(Icons.chevron_right, color: brandMuted),
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => WalletScreen(session: session)),
                  ),
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                padding: EdgeInsets.zero,
                child: SwitchListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                  title: const Text('Job alarm', style: TextStyle(fontWeight: FontWeight.w800)),
                  subtitle: const Text(
                    'Play a sound once when a booking arrives.',
                    style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                  ),
                  secondary: Icon(
                    session.offerAlarmEnabled ? Icons.notifications_active : Icons.notifications_off_outlined,
                    color: session.offerAlarmEnabled ? brandRed : brandMuted,
                  ),
                  value: session.offerAlarmEnabled,
                  onChanged: session.setOfferAlarmEnabled,
                ),
              ),
              const SizedBox(height: 12),
              BrandPanel(
                padding: EdgeInsets.zero,
                child: ListTile(
                  contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  leading: const Icon(Icons.lock_outline, color: brandRed),
                  title: const Text('Change password', style: TextStyle(fontWeight: FontWeight.w800)),
                  subtitle: const Text(
                    'Use a new password for this rider account.',
                    style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                  ),
                  trailing: const Icon(Icons.chevron_right, color: brandMuted),
                  onTap: () => Navigator.push(
                    context,
                    MaterialPageRoute(builder: (_) => ChangePasswordPage(session: session)),
                  ),
                ),
              ),
              const SizedBox(height: 18),
              OutlinedButton.icon(
                onPressed: () {
                  Navigator.pop(context);
                  session.logout();
                },
                icon: const Icon(Icons.logout),
                label: const Text('Sign out'),
              ),
            ],
          ),
        );
      },
    );
  }
}

Future<void> _togglePayment(
  BuildContext context,
  RiderSession session,
  List<String> current,
  String method,
  bool on,
) async {
  final selected = current.map(paymentCode).toSet();
  if (on) {
    selected.add(method);
  } else {
    selected.remove(method);
  }
  if (selected.isEmpty) {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Keep at least one payment method.')),
    );
    return;
  }
  try {
    await session.setPaymentMethods(selected.toList());
  } on ApiException catch (ex) {
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
    }
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.name, this.url});

  final String name;
  final String? url;

  @override
  Widget build(BuildContext context) {
    final letter = name.trim().isEmpty ? 'R' : name.trim()[0].toUpperCase();
    return CircleAvatar(
      radius: 36,
      backgroundColor: brandAccentSoft,
      backgroundImage: url == null ? null : NetworkImage(url!),
      child: url == null
          ? Text(letter, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w800, fontSize: 22))
          : null,
    );
  }
}

class _PhotoCard extends StatelessWidget {
  const _PhotoCard({required this.label, this.url});

  final String label;
  final String? url;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12)),
        const SizedBox(height: 8),
        ClipRRect(
          borderRadius: BorderRadius.circular(14),
          child: AspectRatio(
            aspectRatio: 1,
            child: ColoredBox(
              color: brandSoft,
              child: url == null
                  ? const Center(
                      child: Icon(Icons.photo_outlined, color: brandMuted),
                    )
                  : Image.network(
                      url!,
                      fit: BoxFit.cover,
                      errorBuilder: (_, _, _) => const Center(child: Icon(Icons.broken_image_outlined, color: brandMuted)),
                    ),
            ),
          ),
        ),
      ],
    );
  }
}

class _Fact extends StatelessWidget {
  const _Fact({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label.toUpperCase(),
            style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 11, letterSpacing: 0.4),
          ),
          const SizedBox(height: 3),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w700, height: 1.35)),
        ],
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  const _Tag(this.label, {this.danger = false, this.success = false});

  final String label;
  final bool danger;
  final bool success;

  @override
  Widget build(BuildContext context) {
    final color = danger
        ? brandRed
        : success
            ? brandSuccess
            : brandInk;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: color.withValues(alpha: 0.22)),
      ),
      child: Text(
        label,
        style: TextStyle(color: color, fontWeight: FontWeight.w800, fontSize: 12),
      ),
    );
  }
}

class ChangePasswordPage extends StatefulWidget {
  const ChangePasswordPage({super.key, required this.session});

  final RiderSession session;

  @override
  State<ChangePasswordPage> createState() => _ChangePasswordPageState();
}

class _ChangePasswordPageState extends State<ChangePasswordPage> {
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _confirm = TextEditingController();
  String? _error;
  bool _busy = false;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    final current = _current.text;
    final next = _next.text.trim();
    final confirm = _confirm.text.trim();
    if (current.isEmpty || next.isEmpty) {
      setState(() => _error = 'Enter your current and new password.');
      return;
    }
    if (next.length < 6) {
      setState(() => _error = 'New password must be at least 6 characters.');
      return;
    }
    if (next != confirm) {
      setState(() => _error = 'New passwords do not match.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.session.api.changePassword(current, next);
      if (!mounted) {
        return;
      }
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Password updated.')),
      );
      Navigator.pop(context);
    } on ApiException catch (ex) {
      if (mounted) {
        setState(() => _error = ex.message);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Could not change password.');
      }
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          tooltip: 'Back',
          onPressed: () => Navigator.pop(context),
        ),
        title: const Text('Change password'),
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
        children: [
          BrandPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Your operator can also reset this password if you forget it.',
                  style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 16),
                TextField(
                  controller: _current,
                  obscureText: true,
                  decoration: const InputDecoration(labelText: 'Current password'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _next,
                  obscureText: true,
                  decoration: const InputDecoration(labelText: 'New password'),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _confirm,
                  obscureText: true,
                  decoration: const InputDecoration(labelText: 'Confirm new password'),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 12),
                  Text(_error!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
                ],
                const SizedBox(height: 18),
                FilledButton(
                  onPressed: _busy ? null : _save,
                  child: _busy
                      ? const SizedBox(
                          height: 18,
                          width: 18,
                          child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                        )
                      : const Text('Save password'),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
