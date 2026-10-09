import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:image_picker/image_picker.dart';

import 'api.dart';
import 'session.dart';
import 'theme.dart';

enum _AccountPage { menu, profile, mobile, delete, terms, privacy }

class AccountScreen extends StatefulWidget {
  const AccountScreen({super.key, required this.session});

  final CustomerSession session;

  @override
  State<AccountScreen> createState() => _AccountScreenState();
}

class _AccountScreenState extends State<AccountScreen> {
  _AccountPage _page = _AccountPage.menu;
  bool _photoBusy = false;

  @override
  void initState() {
    super.initState();
    widget.session.addListener(_refresh);
  }

  @override
  void dispose() {
    widget.session.removeListener(_refresh);
    super.dispose();
  }

  void _refresh() {
    if (mounted) setState(() {});
  }

  Future<void> _pickPhoto() async {
    if (_photoBusy) return;
    final picked = await ImagePicker().pickImage(
      source: ImageSource.gallery,
      maxWidth: 1200,
      maxHeight: 1200,
      imageQuality: 85,
    );
    if (picked == null || !mounted) return;
    setState(() => _photoBusy = true);
    try {
      final desk = await widget.session.api.uploadProfilePhoto(picked.path);
      widget.session.updateDesk(desk);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Profile photo updated.')),
        );
      }
    } on ApiException catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
      }
    } catch (_) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Could not upload photo.')),
        );
      }
    } finally {
      if (mounted) setState(() => _photoBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final desk = widget.session.desk;
    final photoUrl = widget.session.api.mediaUrl(desk?.photoUrl);
    final initial = (desk?.fullName ?? 'C').trim().isEmpty
        ? 'C'
        : desk!.fullName.trim()[0].toUpperCase();
    return switch (_page) {
      _AccountPage.profile => _ProfileForm(
          session: widget.session,
          onBack: () => setState(() => _page = _AccountPage.menu),
        ),
      _AccountPage.mobile => _MobileForm(
          session: widget.session,
          onBack: () => setState(() => _page = _AccountPage.menu),
        ),
      _AccountPage.delete => _DeleteForm(
          session: widget.session,
          onBack: () => setState(() => _page = _AccountPage.menu),
        ),
      _AccountPage.terms => _LegalPage(
          title: 'Terms and conditions',
          body: _terms,
          onBack: () => setState(() => _page = _AccountPage.menu),
        ),
      _AccountPage.privacy => _LegalPage(
          title: 'Privacy policy',
          body: _privacy,
          onBack: () => setState(() => _page = _AccountPage.menu),
        ),
      _AccountPage.menu => Scaffold(
          appBar: AppBar(title: const Text('Account')),
          body: ListView(
            padding: EdgeInsets.fromLTRB(16, 16, 16, 16 + shellContentBottomInset(context)),
            children: [
              BrandPanel(
                child: Row(
                  children: [
                    GestureDetector(
                      onTap: _photoBusy ? null : _pickPhoto,
                      child: Stack(
                        clipBehavior: Clip.none,
                        children: [
                          CircleAvatar(
                            radius: 28,
                            backgroundColor: brandAccentSoft,
                            backgroundImage: photoUrl == null ? null : NetworkImage(photoUrl),
                            child: photoUrl != null
                                ? null
                                : _photoBusy
                                    ? const SizedBox(
                                        width: 22,
                                        height: 22,
                                        child: CircularProgressIndicator(strokeWidth: 2),
                                      )
                                    : Text(
                                        initial,
                                        style: const TextStyle(
                                          color: brandRed,
                                          fontWeight: FontWeight.w800,
                                          fontSize: 22,
                                        ),
                                      ),
                          ),
                          Positioned(
                            right: -2,
                            bottom: -2,
                            child: Container(
                              width: 22,
                              height: 22,
                              decoration: BoxDecoration(
                                color: brandRed,
                                shape: BoxShape.circle,
                                border: Border.all(color: Colors.white, width: 2),
                              ),
                              child: const Icon(Icons.camera_alt, size: 12, color: Colors.white),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(desk?.fullName ?? 'Customer', style: Theme.of(context).textTheme.titleMedium),
                          Text(desk?.phoneNumber ?? '', style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600)),
                          Text(
                            (desk?.email ?? '').isEmpty ? 'No email yet' : desk!.email!,
                            style: const TextStyle(color: brandMuted),
                          ),
                          const SizedBox(height: 4),
                          const Text(
                            'Tap photo to change',
                            style: TextStyle(color: brandMuted, fontSize: 12, fontWeight: FontWeight.w600),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              Text('Account management', style: Theme.of(context).textTheme.labelLarge),
              const SizedBox(height: 8),
              BrandPanel(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    _MenuRow(label: 'Profile', onTap: () => setState(() => _page = _AccountPage.profile)),
                    const Divider(height: 1),
                    _MenuRow(label: 'Change mobile', onTap: () => setState(() => _page = _AccountPage.mobile)),
                    const Divider(height: 1),
                    _MenuRow(
                      label: 'Account deletion',
                      danger: true,
                      onTap: () => setState(() => _page = _AccountPage.delete),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              Text('Legal', style: Theme.of(context).textTheme.labelLarge),
              const SizedBox(height: 8),
              BrandPanel(
                padding: EdgeInsets.zero,
                child: Column(
                  children: [
                    _MenuRow(label: 'Terms and conditions', onTap: () => setState(() => _page = _AccountPage.terms)),
                    const Divider(height: 1),
                    _MenuRow(label: 'Privacy policy', onTap: () => setState(() => _page = _AccountPage.privacy)),
                  ],
                ),
              ),
              const SizedBox(height: 20),
              OutlinedButton(
                onPressed: () => widget.session.logout(),
                child: const Text('Log out'),
              ),
            ],
          ),
        ),
    };
  }
}

class _MenuRow extends StatelessWidget {
  const _MenuRow({required this.label, required this.onTap, this.danger = false});

  final String label;
  final VoidCallback onTap;
  final bool danger;

  @override
  Widget build(BuildContext context) {
    return ListTile(
      title: Text(
        label,
        style: TextStyle(
          fontWeight: FontWeight.w700,
          color: danger ? brandSos : brandInk,
        ),
      ),
      trailing: const Icon(Icons.chevron_right, color: brandMuted),
      onTap: onTap,
    );
  }
}

class _ProfileForm extends StatefulWidget {
  const _ProfileForm({required this.session, required this.onBack});

  final CustomerSession session;
  final VoidCallback onBack;

  @override
  State<_ProfileForm> createState() => _ProfileFormState();
}

class _ProfileFormState extends State<_ProfileForm> {
  late final TextEditingController _first;
  late final TextEditingController _last;
  late final TextEditingController _email;
  late String _gender;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    final desk = widget.session.desk;
    _first = TextEditingController(text: desk?.firstName ?? '');
    _last = TextEditingController(text: desk?.lastName ?? '');
    _email = TextEditingController(text: desk?.email ?? '');
    _gender = desk?.gender ?? 'Other';
  }

  @override
  void dispose() {
    _first.dispose();
    _last.dispose();
    _email.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final desk = await widget.session.api.updateProfile(
        firstName: _first.text.trim(),
        lastName: _last.text.trim(),
        gender: _gender,
        email: _email.text.trim(),
      );
      widget.session.updateDesk(desk);
      widget.onBack();
    } on ApiException catch (ex) {
      setState(() => _error = ex.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Profile'),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: widget.onBack),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          BrandPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(controller: _first, decoration: const InputDecoration(labelText: 'First name')),
                const SizedBox(height: 8),
                TextField(controller: _last, decoration: const InputDecoration(labelText: 'Last name')),
                const SizedBox(height: 8),
                TextField(controller: _email, decoration: const InputDecoration(labelText: 'Email')),
                const SizedBox(height: 8),
                DropdownButtonFormField<String>(
                  initialValue: _gender,
                  decoration: const InputDecoration(labelText: 'Gender'),
                  items: const [
                    DropdownMenuItem(value: 'Male', child: Text('Male')),
                    DropdownMenuItem(value: 'Female', child: Text('Female')),
                    DropdownMenuItem(value: 'Other', child: Text('Other')),
                  ],
                  onChanged: _busy ? null : (v) => setState(() => _gender = v ?? 'Other'),
                ),
                if (_error != null) ...[
                  const SizedBox(height: 8),
                  Text(_error!, style: const TextStyle(color: brandSos)),
                ],
                const SizedBox(height: 12),
                FilledButton(onPressed: _busy ? null : _save, child: const Text('Save profile')),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _MobileForm extends StatefulWidget {
  const _MobileForm({required this.session, required this.onBack});

  final CustomerSession session;
  final VoidCallback onBack;

  @override
  State<_MobileForm> createState() => _MobileFormState();
}

class _MobileFormState extends State<_MobileForm> {
  final _phone = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _phone.dispose();
    super.dispose();
  }

  String _normalizePhone(String raw) {
    var digits = raw.replaceAll(RegExp(r'\D'), '');
    if (digits.startsWith('63') && digits.length >= 12) {
      digits = '0${digits.substring(2)}';
    }
    if (digits.startsWith('9') && digits.length == 10) {
      digits = '0$digits';
    }
    return digits;
  }

  Future<void> _save() async {
    final normalized = _normalizePhone(_phone.text);
    if (!RegExp(r'^09\d{9}$').hasMatch(normalized)) {
      setState(() => _error = 'Enter a valid PH mobile number (09XXXXXXXXX).');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final desk = await widget.session.api.updateMobile(normalized);
      widget.session.updateDesk(desk);
      widget.onBack();
    } on ApiException catch (ex) {
      setState(() => _error = ex.message);
    } catch (_) {
      setState(() => _error = 'Could not change mobile.');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Change mobile'),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: widget.onBack),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          BrandPanel(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  'Current number: ${widget.session.desk?.phoneNumber ?? ''}',
                  style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _phone,
                  decoration: const InputDecoration(labelText: 'New mobile', hintText: '09XX XXX XXXX'),
                  keyboardType: TextInputType.phone,
                  inputFormatters: [FilteringTextInputFormatter.allow(RegExp(r'[\d+\s-]'))],
                ),
                if (_error != null) ...[
                  const SizedBox(height: 8),
                  Text(_error!, style: const TextStyle(color: brandSos)),
                ],
                const SizedBox(height: 12),
                FilledButton(onPressed: _busy ? null : _save, child: const Text('Save number')),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _DeleteForm extends StatefulWidget {
  const _DeleteForm({required this.session, required this.onBack});

  final CustomerSession session;
  final VoidCallback onBack;

  @override
  State<_DeleteForm> createState() => _DeleteFormState();
}

class _DeleteFormState extends State<_DeleteForm> {
  final _reason = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _reason.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final reason = _reason.text.trim();
    if (reason.length < 4) {
      setState(() => _error = 'Please enter a reason.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final desk = await widget.session.api.deleteAccount(reason);
      widget.session.updateDesk(desk);
    } on ApiException catch (ex) {
      setState(() => _error = ex.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final desk = widget.session.desk;
    final pending = desk?.deleteStatus == 'Pending';
    return Scaffold(
      appBar: AppBar(
        title: const Text('Account deletion'),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: widget.onBack),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          BrandPanel(
            color: brandDangerBg,
            borderColor: brandSos.withValues(alpha: 0.3),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (pending)
                  const Text(
                    'Your request is pending Super Admin review. You can still use the app until it is approved.',
                    style: TextStyle(fontWeight: FontWeight.w600),
                  )
                else ...[
                  const Text(
                    'This asks Super Admin to close the account. It is not instant.',
                    style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 12),
                  TextField(controller: _reason, decoration: const InputDecoration(labelText: 'Reason'), maxLines: 2),
                  if (_error != null) ...[
                    const SizedBox(height: 8),
                    Text(_error!, style: const TextStyle(color: brandSos)),
                  ],
                  const SizedBox(height: 12),
                  FilledButton(
                    style: FilledButton.styleFrom(backgroundColor: brandSos),
                    onPressed: _busy ? null : _submit,
                    child: const Text('Request deletion'),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _LegalPage extends StatelessWidget {
  const _LegalPage({required this.title, required this.body, required this.onBack});

  final String title;
  final String body;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(title),
        leading: IconButton(icon: const Icon(Icons.arrow_back), onPressed: onBack),
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          BrandPanel(
            child: Text(body, style: const TextStyle(height: 1.45, fontWeight: FontWeight.w500)),
          ),
        ],
      ),
    );
  }
}

const _terms = '''ParaSakay is a ride-hailing platform that connects customers with motorcycle and tricycle riders operated by independent Operators.

By creating an account you confirm that the name, mobile number, and email you provide are yours, and that you will keep your Google account and PIN confidential.

Fares are quoted before you confirm a booking. Payment is collected according to the method you select (CASH, GCASH, MAYA, or OTHERS). The assigned rider must accept that method.

You may cancel a booking before the trip is ongoing. SOS alerts your Operator and Super Admin with your location during an active trip.

Scheduled bookings must be set at least 10 minutes in the future. Operators may assign or broadcast those jobs to riders in their service area.

ParaSakay may suspend accounts that abuse SOS, skip payment, or provide false identity details.''';

const _privacy = '''We collect your name, gender, mobile number, email, booking locations, and trip history to operate the service.

Location is used to set pickup, find nearby riders, and send SOS. We do not sell your personal data.

Operators in your trip see the pickup, drop-off, and contact details needed to complete the ride. Super Admin can review account deletion requests and safety alerts.

You may request account deletion from Account Management. Super Admin reviews the request before the account is closed.

PINs are stored as irreversible hashes. Customers sign in with Google.''';
