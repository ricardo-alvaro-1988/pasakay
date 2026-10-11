import 'package:flutter/material.dart';

import 'account_screen.dart';
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

  void goHomeWithHail({required String riderId, String? vehicleType}) {
    widget.session.setHailIntent(riderId: riderId, vehicleType: vehicleType);
    setState(() => _index = 0);
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

    return Scaffold(
      extendBody: true,
      body: IndexedStack(index: safeIndex, children: pages),
      bottomNavigationBar: Material(
        color: Colors.transparent,
        child: SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.fromLTRB(0, 0, 0, 6),
            child: Container(
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
                children: List.generate(tabs.length, (i) {
                  return Expanded(
                    child: _NavButton(
                      tab: tabs[i],
                      selected: safeIndex == i,
                      onTap: () => setState(() => _index = i),
                    ),
                  );
                }),
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
