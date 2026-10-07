import 'dart:async';

import 'package:flutter/material.dart';

import 'api.dart';
import 'complete_mobile_screen.dart';
import 'login_screen.dart';
import 'session.dart';
import 'shell_screen.dart';
import 'theme.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(PassengerApp(session: CustomerSession(CustomerApi())));
}

class PassengerApp extends StatefulWidget {
  const PassengerApp({super.key, required this.session});

  final CustomerSession session;

  @override
  State<PassengerApp> createState() => _PassengerAppState();
}

class _PassengerAppState extends State<PassengerApp> with WidgetsBindingObserver {
  bool _restoring = true;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    widget.session.addListener(_onSession);
    unawaited(_restore());
  }

  Future<void> _restore() async {
    await widget.session.restore();
    if (mounted) {
      setState(() => _restoring = false);
    }
  }

  void _onSession() {
    if (mounted) setState(() {});
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      unawaited(widget.session.onAppResumed());
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    widget.session.removeListener(_onSession);
    widget.session.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Pasakya Man',
      theme: passengerTheme(),
      home: _restoring
          ? const Scaffold(body: Center(child: CircularProgressIndicator()))
          : _HomeGate(session: widget.session),
    );
  }
}

class _HomeGate extends StatelessWidget {
  const _HomeGate({required this.session});

  final CustomerSession session;

  @override
  Widget build(BuildContext context) {
    if (!session.loggedIn) {
      return LoginScreen(session: session);
    }
    if (session.needsMobile) {
      return CompleteMobileScreen(session: session);
    }
    if (session.desk == null) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (session.error != null) Text(session.error!),
              const SizedBox(height: 12),
              FilledButton(onPressed: () => session.restore(), child: const Text('Retry')),
            ],
          ),
        ),
      );
    }
    return ShellScreen(session: session);
  }
}
