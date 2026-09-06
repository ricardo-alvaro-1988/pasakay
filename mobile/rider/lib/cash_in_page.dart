import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'api.dart';
import 'models.dart';
import 'session.dart';
import 'theme.dart';

class CashInPage extends StatefulWidget {
  const CashInPage({super.key, required this.session});

  final RiderSession session;

  @override
  State<CashInPage> createState() => _CashInPageState();
}

class _CashInPageState extends State<CashInPage> {
  CashInDestinations? _destinations;
  String? _loadError;
  bool _loading = true;
  bool _submitting = false;

  /// Cash | GCash | Maya | bank:{id}
  String? _selection;
  final _amount = TextEditingController();
  final _reference = TextEditingController();

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _amount.dispose();
    _reference.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _loadError = null;
    });
    try {
      final destinations = await widget.session.api.cashInDestinations();
      setState(() {
        _destinations = destinations;
        _loading = false;
      });
    } catch (ex) {
      setState(() {
        _loadError = ex is ApiException ? ex.message : 'Could not load cash-in details.';
        _loading = false;
      });
    }
  }

  String get _paymentMethod {
    final sel = _selection;
    if (sel == null || sel == 'Cash') return 'Cash';
    if (sel == 'GCash') return 'GCash';
    if (sel == 'Maya') return 'Maya';
    return 'Other';
  }

  CashInBankDestination? get _selectedBank {
    final sel = _selection;
    if (sel == null || !sel.startsWith('bank:')) return null;
    final id = sel.substring(5);
    for (final bank in _destinations?.banks ?? const <CashInBankDestination>[]) {
      if (bank.id == id) return bank;
    }
    return null;
  }

  bool get _needsReference => _paymentMethod != 'Cash';

  Future<void> _submit() async {
    final amountText = _amount.text.trim();
    final amount = double.tryParse(amountText);
    if (_selection == null) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Choose how you will pay.')));
      return;
    }
    if (amount == null || amount <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Enter a valid amount.')));
      return;
    }
    final reference = _reference.text.trim();
    if (_needsReference && reference.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Enter the payment reference number.')));
      return;
    }
    if (_selection!.startsWith('bank:') && _selectedBank == null) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Choose a bank account.')));
      return;
    }

    setState(() => _submitting = true);
    try {
      String? note = reference.isEmpty ? null : reference;
      final bank = _selectedBank;
      if (bank != null) {
        note = note == null || note.isEmpty
            ? '${bank.bankName} · ${bank.accountNumber}'
            : '${bank.bankName} · $note';
      }
      await widget.session.api.walletRequest('cash-in', amount, _paymentMethod, note);
      if (!mounted) return;
      Navigator.pop(context, true);
    } catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$ex')));
      }
    } finally {
      if (mounted) setState(() => _submitting = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Cash in')),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: brandRed))
          : ListView(
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 28),
              children: [
                if (_loadError != null)
                  BrandPanel(
                    color: brandDangerBg,
                    borderColor: brandRed,
                    child: Text(_loadError!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
                  )
                else ...[
                  const Text('1. Select payment', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _MethodChip(
                        label: 'Cash',
                        selected: _selection == 'Cash',
                        onTap: () => setState(() => _selection = 'Cash'),
                      ),
                      _MethodChip(
                        label: 'GCash',
                        selected: _selection == 'GCash',
                        onTap: () => setState(() => _selection = 'GCash'),
                      ),
                      _MethodChip(
                        label: 'Maya',
                        selected: _selection == 'Maya',
                        onTap: () => setState(() => _selection = 'Maya'),
                      ),
                      ...?_destinations?.banks.map(
                        (bank) => _MethodChip(
                          label: bank.bankName,
                          selected: _selection == 'bank:${bank.id}',
                          onTap: () => setState(() => _selection = 'bank:${bank.id}'),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 20),
                  const Text('2. Pay to this account', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                  const SizedBox(height: 10),
                  _DetailsPanel(
                    selection: _selection,
                    destinations: _destinations,
                    selectedBank: _selectedBank,
                    mediaUrl: widget.session.api.mediaUrl,
                  ),
                  const SizedBox(height: 20),
                  const Text('3. Amount', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
                  const SizedBox(height: 10),
                  TextField(
                    controller: _amount,
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    decoration: const InputDecoration(
                      labelText: 'Amount (₱)',
                      hintText: '0.00',
                    ),
                  ),
                  const SizedBox(height: 20),
                  Text(
                    _needsReference ? '4. Reference number' : '4. Reference (optional)',
                    style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 16),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: _reference,
                    textCapitalization: TextCapitalization.characters,
                    decoration: InputDecoration(
                      labelText: _needsReference ? 'Reference no.' : 'Reference / note',
                      hintText: _needsReference ? 'From your GCash / Maya / bank receipt' : 'Optional note',
                    ),
                  ),
                  const SizedBox(height: 28),
                  FilledButton(
                    onPressed: _submitting ? null : _submit,
                    style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(50)),
                    child: Text(_submitting ? 'Submitting…' : 'Submit cash-in request'),
                  ),
                ],
              ],
            ),
    );
  }
}

class _MethodChip extends StatelessWidget {
  const _MethodChip({required this.label, required this.selected, required this.onTap});

  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return ChoiceChip(
      label: Text(label, style: TextStyle(fontWeight: FontWeight.w700, color: selected ? Colors.white : brandInk)),
      selected: selected,
      onSelected: (_) => onTap(),
      selectedColor: brandRed,
      backgroundColor: brandSurface,
      side: BorderSide(color: selected ? brandRed : brandLine),
      showCheckmark: false,
    );
  }
}

class _DetailsPanel extends StatelessWidget {
  const _DetailsPanel({
    required this.selection,
    required this.destinations,
    required this.selectedBank,
    required this.mediaUrl,
  });

  final String? selection;
  final CashInDestinations? destinations;
  final CashInBankDestination? selectedBank;
  final String? Function(String?) mediaUrl;

  @override
  Widget build(BuildContext context) {
    if (selection == null) {
      return const BrandPanel(
        child: Text(
          'Choose Cash, GCash, Maya, or a bank above to see where to send payment.',
          style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
        ),
      );
    }

    if (selection == 'Cash') {
      return const BrandPanel(
        child: Text(
          'Pay your operator in cash, then submit the amount below for approval.',
          style: TextStyle(fontWeight: FontWeight.w600),
        ),
      );
    }

    if (selection == 'GCash') {
      final dest = destinations;
      if (dest == null || !dest.hasGCash) {
        return const BrandPanel(
          child: Text(
            'Your operator has not set GCash details yet.',
            style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
          ),
        );
      }
      return _PayTargetCard(
        title: 'GCash',
        lines: [
          if (dest.gCashNumber.trim().isNotEmpty) ('Mobile number', dest.gCashNumber),
        ],
        qrUrl: mediaUrl(dest.gCashQrUrl),
      );
    }

    if (selection == 'Maya') {
      final dest = destinations;
      if (dest == null || !dest.hasMaya) {
        return const BrandPanel(
          child: Text(
            'Your operator has not set Maya details yet.',
            style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
          ),
        );
      }
      return _PayTargetCard(
        title: 'Maya',
        lines: [
          if (dest.mayaNumber.trim().isNotEmpty) ('Mobile number', dest.mayaNumber),
        ],
        qrUrl: mediaUrl(dest.mayaQrUrl),
      );
    }

    final bank = selectedBank;
    if (bank == null) {
      return const BrandPanel(
        child: Text(
          'Your operator has not set bank details yet.',
          style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
        ),
      );
    }
    return _PayTargetCard(
      title: bank.bankName,
      lines: [
        if (bank.accountName.trim().isNotEmpty) ('Account name', bank.accountName),
        if (bank.accountNumber.trim().isNotEmpty) ('Account number', bank.accountNumber),
      ],
      qrUrl: mediaUrl(bank.qrUrl),
    );
  }
}

class _PayTargetCard extends StatelessWidget {
  const _PayTargetCard({
    required this.title,
    required this.lines,
    this.qrUrl,
  });

  final String title;
  final List<(String, String)> lines;
  final String? qrUrl;

  @override
  Widget build(BuildContext context) {
    return BrandPanel(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 18)),
          if (qrUrl != null && qrUrl!.isNotEmpty) ...[
            const SizedBox(height: 16),
            ClipRRect(
              borderRadius: BorderRadius.circular(16),
              child: Image.network(
                qrUrl!,
                height: 220,
                width: 220,
                fit: BoxFit.cover,
                errorBuilder: (_, _, _) => const SizedBox(
                  height: 120,
                  child: Center(child: Text('QR unavailable', style: TextStyle(color: brandMuted))),
                ),
              ),
            ),
          ],
          const SizedBox(height: 16),
          for (final line in lines) ...[
            Text(line.$1, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600, fontSize: 12)),
            const SizedBox(height: 4),
            Row(
              children: [
                Expanded(
                  child: Text(line.$2, style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 18)),
                ),
                IconButton(
                  tooltip: 'Copy',
                  onPressed: () async {
                    await Clipboard.setData(ClipboardData(text: line.$2));
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Copied')));
                    }
                  },
                  icon: const Icon(Icons.copy_rounded),
                ),
              ],
            ),
            const SizedBox(height: 8),
          ],
        ],
      ),
    );
  }
}
