import 'dart:async';

import 'package:flutter/material.dart';

import 'api.dart';
import 'models.dart';
import 'ph_time.dart';
import 'session.dart';
import 'theme.dart';

bool tripCanSendChat({required String status, bool? canChat}) {
  if (status == 'Completed' || status == 'Cancelled') return false;
  if (status == 'Waiting' || status == 'Ongoing' || status == 'Pending' || status == 'ScheduledAccepted') return true;
  if (canChat == true) return true;
  return false;
}

bool tripCanViewChat({required String status, bool? canViewChat, int unread = 0}) {
  if (unread > 0) return true;
  if (status == 'Pending' || status == 'Waiting' || status == 'Ongoing' || status == 'Scheduled' || status == 'ScheduledAccepted') return true;
  if (status == 'Completed' || status == 'Cancelled') return true;
  if (canViewChat == true) return true;
  return false;
}

Future<void> openTripChatSheet(
  BuildContext context, {
  required CustomerSession session,
  required String tripId,
  required String status,
  bool? canChat,
  VoidCallback? onOpened,
}) {
  onOpened?.call();
  session.markChatOpen(tripId);
  return Navigator.of(context)
      .push<void>(
        MaterialPageRoute(
          fullscreenDialog: true,
          builder: (_) => TripChatPage(
            session: session,
            tripId: tripId,
            initialStatus: status,
            initialCanChat: canChat,
          ),
        ),
      )
      .whenComplete(session.markChatClosed);
}

class TripChatPage extends StatefulWidget {
  const TripChatPage({
    super.key,
    required this.session,
    required this.tripId,
    required this.initialStatus,
    this.initialCanChat,
  });

  final CustomerSession session;
  final String tripId;
  final String initialStatus;
  final bool? initialCanChat;

  @override
  State<TripChatPage> createState() => _TripChatPageState();
}

class _TripChatPageState extends State<TripChatPage> {
  final _text = TextEditingController();
  final _scroll = ScrollController();
  final _focus = FocusNode();
  final List<ChatMessage> _messages = [];
  final Set<String> _seen = {};
  bool _loading = true;
  bool _sending = false;
  String? _error;
  Timer? _poll;

  bool get _showComposer {
    final live = widget.session.desk?.activeTrip;
    final status = (live != null && live.id == widget.tripId) ? live.status : widget.initialStatus;
    return status != 'Completed' && status != 'Cancelled';
  }

  @override
  void initState() {
    super.initState();
    widget.session.addListener(_onSession);
    for (final msg in widget.session.takeLiveChat(widget.tripId)) {
      _remember(msg);
    }
    unawaited(_load());
    _poll = Timer.periodic(const Duration(seconds: 2), (_) {
      if (!mounted || _sending) return;
      unawaited(_load(silent: true));
    });
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted && _showComposer) _focus.requestFocus();
    });
  }

  @override
  void dispose() {
    _poll?.cancel();
    widget.session.removeListener(_onSession);
    _text.dispose();
    _scroll.dispose();
    _focus.dispose();
    super.dispose();
  }

  void _onSession() {
    if (!mounted) return;
    var added = false;
    for (final msg in widget.session.takeLiveChat(widget.tripId)) {
      if (_remember(msg)) added = true;
    }
    setState(() {});
    if (added) _jumpBottom();
  }

  bool _remember(ChatMessage msg) {
    final id = msg.id.trim();
    if (id.isEmpty || _seen.contains(id)) return false;
    _seen.add(id);
    _messages.add(msg);
    return true;
  }

  Future<void> _load({bool silent = false}) async {
    if (!silent) {
      setState(() {
        _loading = _messages.isEmpty;
        _error = null;
      });
    }
    try {
      final rows = await widget.session.api.chat(widget.tripId);
      if (!mounted) return;
      var added = false;
      for (final row in rows) {
        if (_remember(row)) added = true;
      }
      _messages.sort((a, b) => a.sentAtUtc.compareTo(b.sentAtUtc));
      setState(() {
        _loading = false;
        if (!silent) _error = null;
      });
      if (added || !silent) _jumpBottom();
    } on ApiException catch (ex) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        if (!silent) _error = ex.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        if (!silent) _error = 'Could not load chat.';
      });
    }
  }

  void _jumpBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scroll.hasClients) return;
      _scroll.jumpTo(_scroll.position.maxScrollExtent);
    });
  }

  Future<void> _send() async {
    final body = _text.text.trim();
    if (body.isEmpty || _sending || !_showComposer) return;
    setState(() => _sending = true);
    try {
      final sent = await widget.session.api.sendChat(widget.tripId, body);
      if (!mounted) return;
      setState(() {
        _remember(sent);
        _text.clear();
        _sending = false;
        _error = null;
      });
      _jumpBottom();
      _focus.requestFocus();
    } on ApiException catch (ex) {
      if (!mounted) return;
      setState(() => _sending = false);
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
    } catch (_) {
      if (!mounted) return;
      setState(() => _sending = false);
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Could not send.')));
    }
  }

  @override
  Widget build(BuildContext context) {
    // CRITICAL: theme FilledButton uses Size.fromHeight(48) = infinite min width.
    // Never put a bare FilledButton in a Row next to Expanded TextField — it crushes the field
    // to a 1px line (exactly what users screenshot as “no textbox”).
    const sendStyle = ButtonStyle(
      minimumSize: WidgetStatePropertyAll(Size(88, 48)),
      maximumSize: WidgetStatePropertyAll(Size(120, 48)),
      padding: WidgetStatePropertyAll(EdgeInsets.symmetric(horizontal: 16)),
      tapTargetSize: MaterialTapTargetSize.shrinkWrap,
      visualDensity: VisualDensity.standard,
    );

    return Scaffold(
      backgroundColor: brandCanvas,
      resizeToAvoidBottomInset: true,
      appBar: AppBar(
        backgroundColor: brandSurface,
        foregroundColor: brandInk,
        elevation: 0,
        title: const Text('Chat with rider', style: TextStyle(fontWeight: FontWeight.w800)),
      ),
      body: Column(
        children: [
          if (_error != null)
            Material(
              color: const Color(0xFFFFE8EA),
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                child: Row(
                  children: [
                    const Icon(Icons.error_outline, color: brandSos, size: 18),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(_error!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
                    ),
                  ],
                ),
              ),
            ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : ListView.builder(
                    controller: _scroll,
                    padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
                    keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
                    itemCount: _messages.isEmpty ? 1 : _messages.length,
                    itemBuilder: (context, index) {
                      if (_messages.isEmpty) {
                        return const Padding(
                          padding: EdgeInsets.all(16),
                          child: Text(
                            'No messages yet. Say hello or share a landmark.',
                            style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                          ),
                        );
                      }
                      final msg = _messages[index];
                      final mine = !msg.fromRider;
                      return Align(
                        alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
                        child: Container(
                          margin: const EdgeInsets.only(bottom: 8),
                          constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.78),
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                          decoration: BoxDecoration(
                            color: mine ? brandRed : brandChip,
                            borderRadius: BorderRadius.circular(16),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              if (msg.body.isNotEmpty)
                                Text(
                                  msg.body,
                                  style: TextStyle(
                                    color: mine ? Colors.white : brandInk,
                                    fontWeight: FontWeight.w600,
                                  ),
                                ),
                              const SizedBox(height: 4),
                              Text(
                                phWhen(msg.sentAtUtc),
                                style: TextStyle(
                                  color: mine ? Colors.white70 : brandMuted,
                                  fontSize: 11,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
          ),
          Material(
            color: brandSurface,
            elevation: 12,
            child: SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 12, 12, 12),
                child: _showComposer
                    ? Row(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Expanded(
                            child: TextField(
                              controller: _text,
                              focusNode: _focus,
                              enabled: !_sending,
                              maxLength: 400,
                              minLines: 1,
                              maxLines: 4,
                              textInputAction: TextInputAction.send,
                              onSubmitted: (_) => _send(),
                              style: const TextStyle(
                                color: brandInk,
                                fontWeight: FontWeight.w600,
                                fontSize: 16,
                              ),
                              decoration: InputDecoration(
                                hintText: 'Type a message',
                                hintStyle: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                                counterText: '',
                                filled: true,
                                fillColor: brandCanvas,
                                contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                                border: OutlineInputBorder(
                                  borderRadius: BorderRadius.circular(14),
                                  borderSide: const BorderSide(color: brandInk, width: 1.5),
                                ),
                                enabledBorder: OutlineInputBorder(
                                  borderRadius: BorderRadius.circular(14),
                                  borderSide: const BorderSide(color: brandInk, width: 1.5),
                                ),
                                focusedBorder: OutlineInputBorder(
                                  borderRadius: BorderRadius.circular(14),
                                  borderSide: const BorderSide(color: brandRed, width: 2),
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(width: 10),
                          FilledButton(
                            style: sendStyle,
                            onPressed: _sending ? null : _send,
                            child: _sending
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                                  )
                                : const Text('Send', style: TextStyle(fontWeight: FontWeight.w800)),
                          ),
                        ],
                      )
                    : const Text(
                        'Chat closed. History stays on this booking.',
                        style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
