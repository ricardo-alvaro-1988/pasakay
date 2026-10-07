import 'package:flutter_test/flutter_test.dart';

import 'package:passenger/api.dart';
import 'package:passenger/main.dart';
import 'package:passenger/session.dart';

void main() {
  testWidgets('PassengerApp builds', (WidgetTester tester) async {
    await tester.pumpWidget(PassengerApp(session: CustomerSession(CustomerApi())));
    await tester.pump();
    expect(find.byType(PassengerApp), findsOneWidget);
  });
}
