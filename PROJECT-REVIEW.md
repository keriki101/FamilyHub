# FamilyHub — Project Review

## Overall assessment

FamilyHub has grown into a useful family-calendar prototype with a responsive touchscreen interface, SQLite persistence, event and person management, filtering, and recurring schedules. The first automated-test slice is now in place. The next step should be improving reliability as a live, shared family calendar before adding more features.

## Highest-priority improvements

## Additional improvements

- **Expand automated tests.** The xUnit/Moq test project covers pickup calculations, recurring-event generation and exceptions, week navigation, and database refresh/persistence. Add coverage for save-failure behavior.
- **Improve save-failure feedback.** Database failures should be shown clearly while preserving form input and allowing a retry.
- **Back up and restore SQLite data.** The database is one local file, so a backup plan protects against device failure and accidental deletion.

## Recently implemented

- The clock, date, and today highlight now refresh at minute boundaries while the page remains open. This also refreshes the upcoming-events list and pickup calculation, including after midnight. The calendar advances to the new week at a week boundary only when the user was viewing the current week; manually selected weeks are preserved. The refresh task is cancelled and awaited when the page is disposed.
- Added a unit test confirming pickup assignment changes when the current date advances.
- Added shared-password cookie authentication for the calendar and Blazor circuit, antiforgery-protected sign-in/sign-out, per-IP login throttling, and 12-hour session expiration that is revalidated by open circuits. The password and LAN HTTPS certificate are configured outside the source tree. The README documents local and home-network setup.
- Added 30-second database refresh for open dashboards, paused while edit/action dialogs are open. Refresh errors are logged and shown in the header; local writes are guarded against stale refresh results. Added test coverage for Monday-based week calculations/navigation and reading cross-context SQLite changes.
- Whole-series edits now preserve individually edited occurrences and deleted-occurrence markers. SQLite migration backfills the stable recurrence date for existing instances, while calendar queries hide deleted instances. Tests cover series merges, exception persistence, deletion visibility, and migration backfill.
- Preschool pickup participants can now be chosen by person ID in dashboard settings, and events have an explicit work classification that also applies to recurring series and overrides. Existing “Jobb” events are migrated to work events; tests cover arbitrary names, title-independent classification, persistence, and migration.

## Suggested order

1. Add clear save-failure feedback while preserving form input and allowing a retry.
2. Add a SQLite backup/restore workflow.

Authentication is designed for trusted home-network use, not direct public-internet exposure. Keep router port forwarding disabled and limit access with the host firewall. Revisit deployment and backup instructions as those workflows are implemented.
