import 'package:flutter/material.dart';
import 'package:geolocator/geolocator.dart';

import 'account_screen.dart';
import 'api.dart';
import 'booking_screen.dart';
import 'favorites_screen.dart';
import 'home_screen.dart';
import 'rental_screen.dart';
import 'session.dart';
import 'theme.dart';

class ShellScreen extends StatefulWidget {
  const ShellScreen({super.key, required this.session});

  final CustomerSession session;

  @override
  State<ShellScreen> createState() => _ShellScreenState();
}

class _ShellScreenState extends State<ShellScreen> {
  int _index = 0;
  bool _sosBusy = false;

  void goHomeWithHail({required String riderId, String? vehicleType}) {
    widget.session.setHailIntent(riderId: riderId, vehicleType: vehicleType);
    setState(() => _index = 0);
  }

  Future<void> _sos() async {
    final trip = widget.session.desk?.activeTrip;
    if (trip == null || !trip.canSos) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('SOS is available during an active ride.')),
      );
      return;
    }
    if (_sosBusy) return;
    final ok = await showPassengerConfirm(
      context,
      title: 'Send SOS?',
      message: 'Alert operators and share your location for this trip.',
      cancelLabel: 'Cancel',
      confirmLabel: 'Send SOS',
      confirmColor: brandSos,
    );
    if (ok != true || !mounted) return;
    setState(() => _sosBusy = true);
    try {
      double? lat;
      double? lng;
      try {
        final pos = await Geolocator.getCurrentPosition();
        lat = pos.latitude;
        lng = pos.longitude;
      } catch (_) {}
      await widget.session.api.sos(trip.id, lat: lat, lng: lng);
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('SOS sent.')));
      }
    } on ApiException catch (ex) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(ex.message)));
      }
    } finally {
      if (mounted) setState(() => _sosBusy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final rental = widget.session.rentalEnabled;
    final pages = <Widget>[
      HomeScreen(session: widget.session),
      BookingScreen(session: widget.session),
      if (rental) RentalScreen(session: widget.session, onGoBooking: () => setState(() => _index = 1)),
      FavoritesScreen(session: widget.session, onBookRider: goHomeWithHail),
      AccountScreen(session: widget.session),
    ];
    final safeIndex = _index.clamp(0, pages.length - 1);

    final tabs = <_NavTab>[
      const _NavTab(icon: Icons.home_outlined, activeIcon: Icons.home, label: 'Home'),
      const _NavTab(icon: Icons.bookmark_border, activeIcon: Icons.bookmark, label: 'Booking'),
      if (rental) const _NavTab(icon: Icons.directions_car_outlined, activeIcon: Icons.directions_car, label: 'Rental'),
      const _NavTab(icon: Icons.star_border, activeIcon: Icons.star, label: 'Favs'),
      const _NavTab(icon: Icons.person_outline, activeIcon: Icons.person, label: 'Account'),
    ];

    // Insert SOS slot after Booking (index 1).
    final left = tabs.take(2).toList();
    final right = tabs.skip(2).toList();

    return Scaffold(
      extendBody: true,
      body: IndexedStack(index: safeIndex, children: pages),
      bottomNavigationBar: Material(
        color: Colors.transparent,
        child: SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(0, 0, 0, 6),
            child: ClipRRect(
              borderRadius: BorderRadius.circular(22),
              clipBehavior: Clip.none,
              child: Container(
                margin: const EdgeInsets.symmetric(horizontal: 0),
                height: 72,
                width: double.infinity,
                decoration: const BoxDecoration(
                  color: brandSurface,
                  border: Border(
                    top: BorderSide(color: brandLine),
                  ),
                  boxShadow: [
                    BoxShadow(color: Color(0x1416181D), blurRadius: 16, offset: Offset(0, -4)),
                  ],
                ),
                child: Row(
                  children: [
                    ...List.generate(left.length, (i) {
                      return Expanded(
                        child: _NavButton(
                          tab: left[i],
                          selected: safeIndex == i,
                          onTap: () => setState(() => _index = i),
                        ),
                      );
                    }),
                    SizedBox(
                      width: 72,
                      child: Center(
                        child: GestureDetector(
                          onTap: _sosBusy ? null : _sos,
                          child: Transform.translate(
                            offset: const Offset(0, -10),
                            child: Container(
                              width: 58,
                              height: 58,
                              decoration: BoxDecoration(
                                color: brandSos,
                                shape: BoxShape.circle,
                                boxShadow: [
                                  BoxShadow(
                                    color: brandSos.withValues(alpha: 0.35),
                                    blurRadius: 12,
                                    offset: const Offset(0, 4),
                                  ),
                                ],
                              ),
                              child: Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  if (_sosBusy)
                                    const SizedBox(
                                      width: 18,
                                      height: 18,
                                      child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                                    )
                                  else
                                    const Icon(Icons.warning_amber_rounded, color: Colors.white, size: 22),
                                  const Text(
                                    'SOS',
                                    style: TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 10),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                    ...List.generate(right.length, (i) {
                      final pageIndex = i + 2;
                      return Expanded(
                        child: _NavButton(
                          tab: right[i],
                          selected: safeIndex == pageIndex,
                          onTap: () => setState(() => _index = pageIndex),
                        ),
                      );
                    }),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _NavTab {
  const _NavTab({required this.icon, required this.activeIcon, required this.label});
  final IconData icon;
  final IconData activeIcon;
  final String label;
}

class _NavButton extends StatelessWidget {
  const _NavButton({required this.tab, required this.selected, required this.onTap});

  final _NavTab tab;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected ? brandRed : brandMuted;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(16),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(selected ? tab.activeIcon : tab.icon, color: color, size: 22),
          const SizedBox(height: 2),
          Text(
            tab.label,
            style: TextStyle(color: color, fontWeight: FontWeight.w700, fontSize: 11),
          ),
        ],
      ),
    );
  }
}
