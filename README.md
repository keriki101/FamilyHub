# FamilyHub

FamilyHub is a Blazor Server application for a shared family calendar dashboard with SQLite persistence, recurring events, and a responsive Swedish-language interface.

## Requirements

- .NET 10 SDK

Check that the SDK is installed:

```sh
dotnet --version
```

The reported version should be `10.0` or later.

## Run the application

Authentication is required. Store only a salted PBKDF2-SHA256 password hash in configuration; never store the plain password in project files. For local development, create a hash and save it directly to .NET User Secrets:

```sh
dotnet user-secrets set FAMILYHUB_PASSWORD_HASH "$(python3 scripts/hash-familyhub-password.py)" \
  --project FamilyHub.Web/FamilyHub.Web.csproj
```

The helper prompts twice without echoing the password and generates a fresh random salt using 600,000 PBKDF2-SHA256 iterations. It outputs a value in the form `pbkdf2-sha256$iterations$base64-salt$base64-hash`; only the encoded hash is saved. Use a unique passphrase of at least 16 characters. User Secrets stays outside the source tree and calendar database. To replace the password later, run the same command with the new passphrase.

For local development on the same device, run the HTTPS profile:

```sh
dotnet restore FamilyHub.Web/FamilyHub.Web.csproj
dotnet run --project FamilyHub.Web/FamilyHub.Web.csproj --launch-profile https
```

Open [https://localhost:7132](https://localhost:7132) and sign in with the configured password. Stop the application with `Ctrl+C`. If your browser does not trust the local development certificate, configure it with `dotnet dev-certs https --trust`.

### Access from the home network

For access from other household devices, use an HTTPS certificate whose names include the FamilyHub hostname and the host device's LAN IP. One option on macOS is `mkcert`; reserve a stable LAN IP first and configure local DNS so `familyhub.home.arpa` resolves to it. Each client device must trust the local CA certificate created by `mkcert`.

```sh
brew install mkcert
mkcert -install
mkdir -p "$HOME/.familyhub"
mkcert -cert-file "$HOME/.familyhub/familyhub-lan.crt" \
  -key-file "$HOME/.familyhub/familyhub-lan.key" \
  familyhub.home.arpa 192.168.1.50
openssl pkcs12 -export \
  -out "$HOME/.familyhub/familyhub-lan.pfx" \
  -inkey "$HOME/.familyhub/familyhub-lan.key" \
  -in "$HOME/.familyhub/familyhub-lan.crt"
chmod 600 "$HOME/.familyhub/familyhub-lan.pfx" "$HOME/.familyhub/familyhub-lan.key"
```

Replace `192.168.1.50` with the host's reserved LAN IP. `openssl` prompts for a PFX password. Install/trust the `rootCA.pem` from the directory reported by `mkcert -CAROOT` on each family device; never copy or share `rootCA-key.pem`.

Use the same reserved LAN IP and local DNS name above. Bind the app to that specific LAN IP (rather than all network interfaces) and configure its certificate:

```sh
export ASPNETCORE_URLS="https://192.168.1.50:7132"
export ASPNETCORE_Kestrel__Certificates__Default__Path="$HOME/.familyhub/familyhub-lan.pfx"
printf 'PFX password: '
read -s ASPNETCORE_Kestrel__Certificates__Default__Password
export ASPNETCORE_Kestrel__Certificates__Default__Password
printf '\n'
dotnet run --no-launch-profile --project FamilyHub.Web/FamilyHub.Web.csproj
```

Open `https://familyhub.home.arpa:7132` from trusted devices. Allow the app through the host firewall only on the private home network, and do not configure router port forwarding. The app requires HTTPS for its authentication cookie; plain HTTP will not retain a sign-in session. Certificate installation and trust steps vary by device and certificate authority.

For home-LAN hosting, configure `FAMILYHUB_PASSWORD_HASH` as a protected environment/deployment setting on the host. Generate its value with the helper above and set that hash instead of the plain password.

The cookie is HttpOnly, SameSite=Strict, Secure-only, and expires after 12 hours without sliding renewal. Login is limited to five attempts per client IP address per minute. Sign out using the button in the calendar header.
The open Blazor session checks its signed expiry claim every minute and returns to sign-in after the 12-hour session ends.

## Database

FamilyHub uses a local SQLite database at `FamilyHub.Web/familyhub.db`. The application applies EF Core migrations at startup and seeds sample family members and events when the database is empty. Newly added people and events are saved to this file and remain after the app restarts.

The calendar opens on the current Monday-to-Sunday week. Use the chevrons to navigate weeks, select a family member to filter events, or create, edit, duplicate, and delete events. Each event stores its selected date and start/end times. When adding an event, enable **Upprepa varje vecka**, choose one or more weekdays, and set an end date (up to two years from the start). Recurring occurrences can be edited or deleted individually, or managed as a whole series.

Preschool pickup participants are selected with **Välj personer** in the dashboard header. Mark an event as **Arbetsevent** when creating or editing it; event titles and family member names do not affect pickup calculations. The app compares the selected participants' latest work-event end times for the current day. Existing events titled “Jobb” are marked as work events by the database migration.

Open calendars check SQLite for changes every 30 seconds so edits from another household device appear automatically. Refresh pauses while an edit or event-action dialog is open.

Deleting one recurring event hides just that occurrence while retaining its deletion marker. Editing one occurrence saves it as an override. Editing the whole series updates generated occurrences but preserves those deletions and overrides for dates that remain in the series; an exception is removed if its date is no longer included in the revised schedule.

Hover over a person card to reveal the edit and delete actions. On touchscreens, those actions stay visible. Deleting a person also permanently deletes that person's events and asks for confirmation first.

To change the database location, update the `ConnectionStrings:FamilyHub` setting in `FamilyHub.Web/appsettings.json`. Relative paths are resolved from the web project directory. Do not delete the database if you want to keep its data.

### Back up and restore

Select **Säkerhetskopiera** in the dashboard header and choose **Ladda ner säkerhetskopia** to download a consistent SQLite copy. Keep the downloaded `.db` file somewhere safe outside the project folder. It contains family calendar data, so protect it like personal information.

To restore, select a FamilyHub database backup in the same dialog, confirm that you understand it replaces all current calendar data, and select **Återställ databas**. Restore accepts valid backups from the current FamilyHub version up to 50 MB. The uploaded file is checked for database integrity, expected tables and schema, migrations, and valid person/event references before any changes are made. The app creates a temporary safety copy to roll back if applying the restore fails. Ask other household members to close the calendar and do not edit data during restore. Refreshing other open dashboards will show the restored data.

For database model changes, install the EF Core command-line tool matching the project version, then create a migration from the repository root:

```sh
dotnet tool install --global dotnet-ef --version 10.0.13
dotnet ef migrations add DescribeYourChange --project FamilyHub.Web/FamilyHub.Web.csproj
```

Migrations are applied automatically the next time the application starts.

## Run tests

Run the automated unit tests from the repository root:

```sh
dotnet test FamilyHub.Web.Tests/FamilyHub.Web.Tests.csproj
```

The xUnit test project uses Moq to control the calendar clock and covers preschool pickup decisions, recurring-event occurrence generation and exceptions, persistence failures, salted password-hash validation, and authentication-session expiration.

## GitHub

This project can be tracked locally with Git and pushed to a GitHub repository after creating an empty repository on GitHub. Do not include passwords, password hashes, local HTTPS certificates/private keys, or the SQLite database in commits. The repository ignore rules exclude these local files and common local build artifacts.

After creating the GitHub repository, connect it and publish the initial branch using the URL GitHub provides:

```sh
git remote add origin <github-repository-url>
git push -u origin main
```
