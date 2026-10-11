import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:rider/main.dart';

void main() {
  testWidgets('App shows a loading frame', (WidgetTester tester) async {
    await tester.pumpWidget(const RiderApp());
    expect(find.byType(CircularProgressIndicator), findsOneWidget);
  });
}
