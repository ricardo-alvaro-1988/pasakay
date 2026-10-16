import datetime as dt
import unittest
from backup_manager import data_kind, week_name, TZ


class ScheduleTests(unittest.TestCase):
    def test_initial_friday_requires_full(self):
        now = dt.datetime(2026, 10, 9, 20, tzinfo=TZ)
        self.assertEqual(week_name(now), '2026-10-04_to_2026-10-11')
        self.assertEqual(data_kind(None, now), 'full')

    def test_next_night_is_differential(self):
        previous = {'week': '2026-10-04_to_2026-10-11', 'data_day': '2026-10-09'}
        self.assertEqual(data_kind(previous, dt.datetime(2026, 10, 10, tzinfo=TZ)), 'diff')

    def test_sunday_rolls_over_to_full(self):
        previous = {'week': '2026-10-04_to_2026-10-11', 'data_day': '2026-10-10'}
        self.assertEqual(data_kind(previous, dt.datetime(2026, 10, 11, tzinfo=TZ)), 'full')

    def test_monday_is_differential(self):
        previous = {'week': '2026-10-11_to_2026-10-18', 'data_day': '2026-10-11'}
        self.assertEqual(data_kind(previous, dt.datetime(2026, 10, 12, tzinfo=TZ)), 'diff')

    def test_no_repeated_data_backup_within_day(self):
        previous = {'week': '2026-10-04_to_2026-10-11', 'data_day': '2026-10-10'}
        self.assertIsNone(data_kind(previous, dt.datetime(2026, 10, 10, 0, 15, tzinfo=TZ)))

    def test_missing_downloaded_file_does_not_reset_baseline(self):
        previous = {'week': '2026-10-04_to_2026-10-11', 'data_day': '2026-10-10', 'full_file': 'downloaded.bak'}
        self.assertIsNone(data_kind(previous, dt.datetime(2026, 10, 10, 12, tzinfo=TZ)))

    def test_external_full_reestablishes_our_baseline(self):
        previous = {'week': '2026-10-04_to_2026-10-11', 'data_day': '2026-10-10'}
        self.assertEqual(data_kind(previous, dt.datetime(2026, 10, 10, 12, tzinfo=TZ), False), 'full')

    def test_year_boundary(self):
        self.assertEqual(week_name(dt.datetime(2027, 1, 1, tzinfo=TZ)), '2026-12-27_to_2027-01-03')


if __name__ == '__main__':
    unittest.main()
