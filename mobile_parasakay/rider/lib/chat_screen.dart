import 'dart:async';

import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';

import 'api.dart';
import 'chat_hub.dart';
import 'session.dart';
import 'theme.dart';

class ChatScreen extends StatefulWidget {
  const ChatScreen({super.key, required this.session, required this.tripId});

  final RiderSession session;
  final String tripId;

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> {
  final _controller = TextEditingController();
  final _scroll = ScrollController();
  final _picker = ImagePicker();
  bool _busy = false;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    widget.session.chatOpen = true;
    widget.session.markChatRead();
    widget.session.addListener(_onSession);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _jumpBottom();
      _reload();
    });
  }

  @override
  void dispose() {
    widget.session.chatOpen = false;
    widget.session.removeListener(_onSession);
    _controller.dispose();
    _scroll.dispose();
    super.dispose();
  }

  void _onSession() {
    if (!mounted) {
      return;
    }
    final live = widget.session.desk?.activeTrip;
    if (live == null || live.tripId != widget.tripId || !live.canChat) {
      Navigator.pop(context);
      return;
    }
    setState(() {});
    _jumpBottom();
  }

  void _jumpBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!_scroll.hasClients) {
        return;
      }
      _scroll.jumpTo(_scroll.position.maxScrollExtent);
    });
  }

  Future<void> _reload() async {
    setState(() {
      _loading = widget.session.chatMessages.isEmpty;
      _error = null;
    });
    try {
      await widget.session
          .refreshChat(widget.tripId, silent: false)
          .timeout(const Duration(seconds: 8));
    } on TimeoutException {
      if (mounted) {
        setState(() => _error = 'Chat is taking too long to load. Check the API and try again.');
      }
    } on ApiException catch (ex) {
      if (mounted) {
        setState(() => _error = ex.message);
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Could not load chat.');
      }
    } finally {
      if (mounted) {
        setState(() => _loading = false);
        _jumpBottom();
      }
    }
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty || _busy || !widget.session.canSendChat) {
      return;
    }
    setState(() => _busy = true);
    try {
      await widget.session.sendChat(widget.tripId, text);
      _controller.clear();
      _error = null;
      _jumpBottom();
    } on ApiException catch (ex) {
      _error = ex.message;
    } catch (_) {
      _error = 'Could not send message.';
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  Future<void> _sendPhoto(ImageSource source) async {
    if (_busy || !widget.session.canSendChat) {
      return;
    }
    final picked = await _picker.pickImage(source: source, imageQuality: 75, maxWidth: 1600);
    if (picked == null) {
      return;
    }
    setState(() => _busy = true);
    try {
      await widget.session.sendChatPhoto(
        widget.tripId,
        picked.path,
        body: _controller.text.trim().isEmpty ? null : _controller.text.trim(),
      );
      _controller.clear();
      _error = null;
      _jumpBottom();
    } on ApiException catch (ex) {
      _error = ex.message;
    } catch (_) {
      _error = 'Could not send photo.';
    } finally {
      if (mounted) {
        setState(() => _busy = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final messages = widget.session.chatMessages;
    final canSend = widget.session.canSendChat;
    final link = widget.session.chatLink;
    final linkError = widget.session.chatLinkError;
    return Scaffold(
      backgroundColor: brandCanvas,
      appBar: AppBar(title: const Text('Chat with customer')),
      body: Column(
        children: [
          if (link != ChatLinkState.live)
            Material(
              color: link == ChatLinkState.connecting ? brandWarnBg : brandDangerBg,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
                child: Row(
                  children: [
                    Icon(
                      link == ChatLinkState.connecting ? Icons.sync : Icons.wifi_off,
                      size: 18,
                      color: link == ChatLinkState.connecting ? const Color(0xFF7A5B00) : brandRed,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        linkError ??
                            (link == ChatLinkState.connecting
                                ? 'Connecting to live chat…'
                                : 'Live chat is offline. Messages still arrive on a short delay.'),
                        style: TextStyle(
                          color: link == ChatLinkState.connecting ? const Color(0xFF7A5B00) : brandRed,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                    TextButton(
                      onPressed: _reload,
                      child: const Text('Retry'),
                    ),
                  ],
                ),
              ),
            ),
          if (_error != null)
            Material(
              color: brandDangerBg,
              child: Padding(
                padding: const EdgeInsets.all(10),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(_error!, style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700)),
                    ),
                    TextButton(onPressed: _reload, child: const Text('Retry')),
                  ],
                ),
              ),
            ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator(color: brandRed))
                : messages.isEmpty
                    ? const Center(
                        child: Padding(
                          padding: EdgeInsets.all(24),
                          child: Text(
                            'No messages yet.\nSay you are on the way or send a photo.',
                            textAlign: TextAlign.center,
                            style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                          ),
                        ),
                      )
                    : ListView.builder(
                        controller: _scroll,
                        padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                        itemCount: messages.length,
                        itemBuilder: (context, index) {
                          final msg = messages[index];
                          final mine = msg.fromRider;
                          final photo = widget.session.api.mediaUrl(msg.photoUrl);
                          return Align(
                            alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
                            child: Container(
                              margin: const EdgeInsets.only(bottom: 8),
                              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                              constraints: BoxConstraints(maxWidth: MediaQuery.sizeOf(context).width * 0.78),
                              decoration: BoxDecoration(
                                color: mine ? const Color(0xFFFFE5E7) : brandSurface,
                                borderRadius: BorderRadius.circular(14),
                                border: Border.all(color: mine ? const Color(0xFFFFC9CD) : brandLine),
                              ),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  if (photo != null) ...[
                                    ClipRRect(
                                      borderRadius: BorderRadius.circular(12),
                                      child: ConstrainedBox(
                                        constraints: BoxConstraints(
                                          maxWidth: MediaQuery.sizeOf(context).width * 0.62,
                                          maxHeight: 220,
                                        ),
                                        child: Image.network(
                                          photo,
                                          fit: BoxFit.cover,
                                          filterQuality: FilterQuality.medium,
                                          loadingBuilder: (context, child, progress) {
                                            if (progress == null) {
                                              return child;
                                            }
                                            return const SizedBox(
                                              width: 180,
                                              height: 140,
                                              child: Center(
                                                child: CircularProgressIndicator(color: brandRed),
                                              ),
                                            );
                                          },
                                          errorBuilder: (context, error, stackTrace) => const Padding(
                                            padding: EdgeInsets.all(12),
                                            child: Icon(Icons.broken_image_outlined, color: brandMuted),
                                          ),
                                        ),
                                      ),
                                    ),
                                    if (msg.body.trim().isNotEmpty) const SizedBox(height: 8),
                                  ],
                                  if (msg.body.trim().isNotEmpty)
                                    Text(msg.body, style: const TextStyle(fontWeight: FontWeight.w600, color: brandInk)),
                                  if (msg.sentAt != null) ...[
                                    const SizedBox(height: 4),
                                    Text(
                                      '${msg.sentAt!.hour.toString().padLeft(2, '0')}:${msg.sentAt!.minute.toString().padLeft(2, '0')}',
                                      style: const TextStyle(fontSize: 11, color: brandMuted),
                                    ),
                                  ],
                                ],
                              ),
                            ),
                          );
                        },
                      ),
          ),
          SafeArea(
            top: false,
            child: Material(
              color: brandSurface,
              elevation: 8,
              shadowColor: const Color(0x1416181D),
              child: Padding(
                padding: const EdgeInsets.fromLTRB(4, 8, 8, 8),
                child: canSend
                    ? Row(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          IconButton(
                            tooltip: 'Camera',
                            onPressed: _busy ? null : () => _sendPhoto(ImageSource.camera),
                            icon: const Icon(Icons.photo_camera_outlined),
                          ),
                          IconButton(
                            tooltip: 'Photo',
                            onPressed: _busy ? null : () => _sendPhoto(ImageSource.gallery),
                            icon: const Icon(Icons.photo_outlined),
                          ),
                          Expanded(
                            child: TextField(
                              controller: _controller,
                              maxLength: 400,
                              minLines: 1,
                              maxLines: 4,
                              textInputAction: TextInputAction.send,
                              decoration: const InputDecoration(
                                hintText: 'Type a message',
                                counterText: '',
                                isDense: true,
                              ),
                              onSubmitted: (_) => _send(),
                            ),
                          ),
                          const SizedBox(width: 6),
                          IconButton.filled(
                            onPressed: _busy ? null : _send,
                            style: IconButton.styleFrom(
                              backgroundColor: brandRed,
                              foregroundColor: Colors.white,
                              disabledBackgroundColor: brandRed.withValues(alpha: 0.45),
                            ),
                            icon: Icon(_busy ? Icons.hourglass_top : Icons.send),
                          ),
                        ],
                      )
                    : const Padding(
                        padding: EdgeInsets.fromLTRB(12, 8, 12, 8),
                        child: Text(
                          'Chat closed. History stays on the booking.',
                          style: TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
                        ),
                      ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}
