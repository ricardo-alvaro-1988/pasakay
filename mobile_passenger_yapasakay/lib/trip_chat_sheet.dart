import 'dart:async';

import 'package:flutter/material.dart';

import 'api.dart';
import 'models.dart';
import 'ph_time.dart';
import 'theme.dart';

bool tripCanSendChat({required String status, bool? canChat}) {
  if (canChat == true) return true;
  return status == 'Waiting' || status == 'Ongoing';
}

bool tripCanViewChat({required String status, bool? canViewChat}) {
  if (canViewChat == true) return true;
  if (canViewChat == false) {
    // Still allow live/history statuses if the flag is wrong/stale.
    return status == 'Waiting' || status == 'Ongoing' || status == 'Completed' || status == 'Cancelled';
  }
  return status == 'Waiting' || status == 'Ongoing' || status == 'Completed' || status == 'Cancelled';
}

Future<void> openTripChatSheet(
  BuildContext context, {
  required CustomerApi api,
  required String tripId,
  required String status,
  bool? canChat,
  VoidCallback? onOpened,
}) {
  onOpened?.call();
  final canSend = tripCanSendChat(status: status, canChat: canChat);
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    // Avoid useSafeArea+full-screen fraction (clips the composer). Pad manually.
    useSafeArea: false,
    backgroundColor: brandSurface,
    shape: const RoundedRectangleBorder(
      borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
    ),
    builder: (ctx) => _TripChatSheet(api: api, tripId: tripId, canSend: canSend),
  );
}

class _TripChatSheet extends StatefulWidget {
  const _TripChatSheet({required this.api, required this.tripId, required this.canSend});

  final CustomerApi api;
  final String tripId;
  final bool canSend;

  @override
  State<_TripChatSheet> createState() => _TripChatSheetState();
}

class _TripChatSheetState extends State<_TripChatSheet> {
  final _text = TextEditingController();
  final _scroll = ScrollController();
  final _focus = FocusNode();
  final List<ChatMessage> _messages = [];
  bool _loading = true;
  bool _sending = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    unawaited(_load());
  }

  @override
  void dispose() {
    _text.dispose();
    _scroll.dispose();
    _focus.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final rows = await widget.api.chat(widget.tripId);
      if (!mounted) return;
      setState(() {
        _messages
          ..clear()
          ..addAll(rows);
        _loading = false;
      });
      _jumpBottom();
    } on ApiException catch (ex) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = ex.message;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = 'Could not load chat.';
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
    if (body.isEmpty || _sending || !widget.canSend) return;
    setState(() => _sending = true);
    try {
      final sent = await widget.api.sendChat(widget.tripId, body);
      if (!mounted) return;
      setState(() {
        if (!_messages.any((m) => m.id == sent.id)) _messages.add(sent);
        _text.clear();
        _sending = false;
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
    final mq = MediaQuery.of(context);
    final keyboard = mq.viewInsets.bottom;
    // Height fits under status bar and above keyboard so the composer stays on-screen.
    final height = (mq.size.height - mq.padding.top - keyboard - 12).clamp(280.0, mq.size.height);

    return Padding(
      padding: EdgeInsets.only(top: mq.padding.top + 8, bottom: keyboard),
      child: SizedBox(
        height: height,
        width: double.infinity,
        child: Column(
          children: [
            const SizedBox(height: 8),
            Container(
              width: 40,
              height: 4,
              decoration: BoxDecoration(color: brandLine, borderRadius: BorderRadius.circular(99)),
            ),
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 12, 8, 8),
              child: Row(
                children: [
                  const Expanded(
                    child: Text('Chat with rider', style: TextStyle(fontWeight: FontWeight.w800, fontSize: 17)),
                  ),
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close),
                  ),
                ],
              ),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.symmetric(horizontal: 16),
                child: Text(_error!, style: const TextStyle(color: brandSos, fontWeight: FontWeight.w600)),
              ),
            Expanded(
              child: _loading
                  ? const Center(child: CircularProgressIndicator())
                  : ListView.builder(
                      controller: _scroll,
                      padding: const EdgeInsets.fromLTRB(14, 4, 14, 12),
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
                            constraints: BoxConstraints(maxWidth: mq.size.width * 0.78),
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
              elevation: 8,
              shadowColor: const Color(0x3316181D),
              child: Padding(
                padding: EdgeInsets.fromLTRB(12, 10, 12, 10 + (keyboard > 0 ? 0 : mq.padding.bottom)),
                child: widget.canSend
                    ? Row(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Expanded(
                            child: TextField(
                              controller: _text,
                              focusNode: _focus,
                              maxLength: 400,
                              minLines: 1,
                              maxLines: 4,
                              textInputAction: TextInputAction.send,
                              onSubmitted: (_) => _send(),
                              decoration: const InputDecoration(
                                hintText: 'Type a message',
                                counterText: '',
                                filled: true,
                                fillColor: brandSoft,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          FilledButton(
                            onPressed: _sending ? null : _send,
                            child: _sending
                                ? const SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                                  )
                                : const Text('Send'),
                          ),
                        ],
                      )
                    : const Text(
                        'Chat closed. History stays on this booking.',
                        style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                      ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
