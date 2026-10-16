# Privacy and account deletion

Customer public routes: `/privacy` and `/delete-account`, with aliases
`/privacy-policy` and `/account-deletion`. They load the deployment's existing
public branding (name, uploaded logo, colors). Configure branding in admin.
Branding failures show a retry rather than another brand's identity.

Contact defaults to `support@<hostname>` with `www.` removed. Provision and
monitor these mailboxes. Override with `VITE_PRIVACY_CONTACT_EMAIL` at build
time for subdomain deployments or other mailboxes.

The deletion link opens `/?account=delete`, selecting the existing account
request screen after Google sign-in. Opening a link does not submit a request.

## Audit and remaining release work (2026-10-16)

- Privacy text and deletion requests already existed inside the customer web
  and YaPasakay passenger apps, but lacked dedicated public web URLs.
- AdminController.ResolveDeleteRequest only disables AppUser.IsActive on
  approval. It does not erase associated data. The pages disclose this limitation.
- Actual erasure/anonymization, retained record categories, retention periods,
  backup expiration, and a processing timeframe still need implementation or
  confirmation before claiming Google Play account deletion compliance.
- This policy covers passenger services. Rider apps require a separate review
  of data collection and a rider account deletion pathway.
- Verify each deployed HTTPS URL, brand logo, support mailbox, and the real
  sign-in/request flow in staging. Add the privacy URL in Play Console and
  mobile apps, and the deletion URL in Data safety. Match disclosures to SDKs
  and operating practices. These changes do not deploy or update Play Console.

References:
- https://support.google.com/googleplay/android-developer/answer/10144311
- https://support.google.com/googleplay/android-developer/answer/13327111
