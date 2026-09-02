import 'dart:async';

import 'package:flutter/material.dart';

import 'home_screen.dart';
import 'login_screen.dart';
import 'session.dart';
import 'api.dart';
import 'theme.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  ErrorWidget.builder = (details) {
    return Material(
      color: brandCanvas,
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Text(
            details.exceptionAsString(),
            style: const TextStyle(color: brandRed, fontWeight: FontWeight.w700, height: 1.35),
          ),
        ),
      ),
    );
  };
  runApp(const RiderApp());
}

class RiderApp extends StatefulWidget {
  const RiderApp({super.key});

  @override
  State<RiderApp> createState() => _RiderAppState();
}

class _RiderAppState extends State<RiderApp> with WidgetsBindingObserver {
  final RiderSession session = RiderSession(RiderApi());
  bool _ready = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    session.addListener(_refresh);
    unawaited(session.restore().whenComplete(() {
      if (mounted) {
        setState(() => _ready = true);
      }
    }));
  }

  void _refresh() {
    if (!mounted) {
      return;
    }
    setState(() => _ready = true);
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed && session.loggedIn) {
      session.refresh();
    }
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    session.removeListener(_refresh);
    session.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final homeReady = session.loggedIn && session.desk != null;
    final opening = !_ready || (session.loggedIn && session.desk == null && session.error == null);

    return MaterialApp(
      title: 'Pricebadz',
      debugShowCheckedModeBanner: false,
      theme: riderTheme(),
      home: homeReady
          ? HomeScreen(session: session)
          : opening
              ? const _OpeningScreen()
              : LoginScreen(session: session),
    );
  }
}

class _OpeningScreen extends StatelessWidget {
  const _OpeningScreen();

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: brandRed,
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(color: Colors.white),
            SizedBox(height: 16),
            Text(
              'Opening Pricebadz…',
              style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 16),
            ),
          ],
        ),
      ),
    );
  }
}
