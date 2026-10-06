import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

const brandRed = Color(0xFFF7941D);
const brandNavy = Color(0xFF001A54);
const brandInk = Color(0xFF16181D);
const brandMuted = Color(0xFF667085);
const brandCanvas = Color(0xFFEEF1F5);
const brandSurface = Color(0xFFFFFFFF);
const brandSoft = Color(0xFFF5F7FA);
const brandLine = Color(0xFFDCE2EA);
const brandChip = Color(0xFFE8EDF3);
const brandAccentSoft = Color(0x29F7941D);
const brandSuccess = Color(0xFF1EA36A);
const brandSos = Color(0xFFFF0000);
const brandWarnBg = Color(0xFFFFF4CC);
const brandWarnLine = Color(0xFFE0B400);
const brandDangerBg = Color(0xFFFFE5E5);

ThemeData passengerTheme() {
  const scheme = ColorScheme.light(
    primary: brandRed,
    onPrimary: Colors.white,
    secondary: brandNavy,
    onSecondary: Colors.white,
    surface: brandSurface,
    onSurface: brandInk,
    error: brandSos,
    onError: Colors.white,
    outline: brandLine,
  );

  final shape14 = RoundedRectangleBorder(borderRadius: BorderRadius.circular(14));
  final shape22 = RoundedRectangleBorder(borderRadius: BorderRadius.circular(22));

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: brandCanvas,
    fontFamily: 'Segoe UI',
    appBarTheme: const AppBarTheme(
      backgroundColor: brandSurface,
      foregroundColor: brandInk,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      titleTextStyle: TextStyle(
        color: brandInk,
        fontSize: 18,
        fontWeight: FontWeight.w800,
        fontFamily: 'Segoe UI',
      ),
      iconTheme: IconThemeData(color: brandInk),
      systemOverlayStyle: SystemUiOverlayStyle.dark,
    ),
    cardTheme: CardThemeData(
      color: brandSurface,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: shape22.copyWith(
        side: const BorderSide(color: brandLine),
      ),
      shadowColor: const Color(0x1416181D),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: brandRed,
        foregroundColor: Colors.white,
        disabledBackgroundColor: brandRed.withValues(alpha: 0.45),
        disabledForegroundColor: Colors.white,
        minimumSize: const Size(64, 48),
        elevation: 0,
        shadowColor: const Color(0x38F7941D),
        shape: shape14,
        textStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: brandInk,
        minimumSize: const Size(64, 48),
        side: const BorderSide(color: brandLine),
        backgroundColor: brandChip,
        shape: shape14,
        textStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: brandNavy,
        textStyle: const TextStyle(fontWeight: FontWeight.w700),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: brandSurface,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: brandLine),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: brandLine),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(14),
        borderSide: const BorderSide(color: brandRed, width: 1.5),
      ),
      labelStyle: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600),
      hintStyle: const TextStyle(color: brandMuted),
      helperStyle: const TextStyle(color: brandMuted, fontSize: 12),
    ),
    switchTheme: SwitchThemeData(
      thumbColor: WidgetStateProperty.resolveWith((states) {
        if (states.contains(WidgetState.selected)) return brandRed;
        return brandMuted;
      }),
      trackColor: WidgetStateProperty.resolveWith((states) {
        if (states.contains(WidgetState.selected)) return brandAccentSoft;
        return brandChip;
      }),
      trackOutlineColor: const WidgetStatePropertyAll(brandLine),
    ),
    snackBarTheme: SnackBarThemeData(
      backgroundColor: brandInk,
      contentTextStyle: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      behavior: SnackBarBehavior.floating,
    ),
    bottomSheetTheme: const BottomSheetThemeData(
      backgroundColor: brandSurface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(22)),
      ),
    ),
    dividerColor: brandLine,
    textTheme: const TextTheme(
      bodyMedium: TextStyle(color: brandInk, height: 1.35),
      titleLarge: TextStyle(color: brandInk, fontWeight: FontWeight.w800, fontSize: 22),
      titleMedium: TextStyle(color: brandInk, fontWeight: FontWeight.w800, fontSize: 16),
      labelLarge: TextStyle(color: brandMuted, fontWeight: FontWeight.w700, fontSize: 12, letterSpacing: 0.4),
    ),
  );
}

String peso(double value) =>
    '₱${value.toStringAsFixed(value.truncateToDouble() == value ? 0 : 2)}';

/// Bottom nav bar (~72) + wrapper padding (6) so CTAs clear `extendBody` shell.
const shellNavOccupiedHeight = 78.0;

double shellContentBottomInset(BuildContext context) =>
    MediaQuery.paddingOf(context).bottom + shellNavOccupiedHeight;

/// Full-bleed dialog matching passenger popup width requirement.
Future<T?> showPassengerDialog<T>({
  required BuildContext context,
  required WidgetBuilder builder,
  bool barrierDismissible = true,
}) {
  final width = MediaQuery.sizeOf(context).width;
  return showDialog<T>(
    context: context,
    barrierDismissible: barrierDismissible,
    builder: (ctx) => Dialog(
      insetPadding: EdgeInsets.zero,
      backgroundColor: brandSurface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(22)),
      child: SizedBox(
        width: width,
        child: builder(ctx),
      ),
    ),
  );
}

Future<bool?> showPassengerConfirm(
  BuildContext context, {
  required String title,
  required String message,
  String cancelLabel = 'Cancel',
  String confirmLabel = 'OK',
  Color? confirmColor,
}) {
  return showPassengerDialog<bool>(
    context: context,
    builder: (ctx) => Padding(
      padding: const EdgeInsets.fromLTRB(20, 20, 20, 16),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(title, style: Theme.of(ctx).textTheme.titleMedium),
          const SizedBox(height: 10),
          Text(message, style: const TextStyle(color: brandMuted, fontWeight: FontWeight.w600)),
          const SizedBox(height: 18),
          Row(
            children: [
              Expanded(
                child: OutlinedButton(
                  onPressed: () => Navigator.pop(ctx, false),
                  child: Text(cancelLabel),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: FilledButton(
                  style: confirmColor == null
                      ? null
                      : FilledButton.styleFrom(backgroundColor: confirmColor),
                  onPressed: () => Navigator.pop(ctx, true),
                  child: Text(confirmLabel),
                ),
              ),
            ],
          ),
        ],
      ),
    ),
  );
}

class BrandPanel extends StatelessWidget {
  const BrandPanel({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.color = brandSurface,
    this.borderColor = brandLine,
    this.borderWidth = 1,
  });

  final Widget child;
  final EdgeInsetsGeometry padding;
  final Color color;
  final Color borderColor;
  final double borderWidth;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: padding,
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: borderColor, width: borderWidth),
        boxShadow: const [
          BoxShadow(color: Color(0x1416181D), blurRadius: 18, offset: Offset(0, 8)),
        ],
      ),
      child: child,
    );
  }
}
