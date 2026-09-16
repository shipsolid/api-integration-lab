# Manual OAuth and observability checks

These are the only checks that require a browser, a real Entra tenant, or visual inspection in
Grafana. Automated tests use fakes and never require personal or tenant credentials.

## Microsoft Entra app registration

1. Create a confidential **Web** app registration.
2. Add the exact redirect URI `http://localhost:8080/auth/microsoft/callback`.
3. Add delegated Microsoft Graph permission `User.Read`.
4. Add application Microsoft Graph permission `User.Read.All` and grant tenant admin consent.
5. Create a client secret and copy its **value** immediately. The portal's secret object ID cannot
   authenticate the application.
6. Put tenant ID, client ID, and secret value in the matching `MS_*` fields of the uncommitted `.env`.
7. Restart the API after changing `.env`; configuration is read when the process starts.

## Delegated Authorization Code check

1. Open `http://localhost:8080/api/microsoft/login` in a browser.
2. Sign in and consent to `User.Read`. Entra redirects the one-time code to
   `/auth/microsoft/callback`; middleware validates state/nonce and redeems it server-side.
3. Open `http://localhost:8080/api/microsoft/me`. Expect your normalized ID, display name, mail,
   and user principal name—never an access or refresh token.
4. Open `http://localhost:8080/api/demo`. Expect separate public, GitHub, and Microsoft envelopes;
   a configured provider can fail without suppressing the other results.
5. Open `http://localhost:8080/api/microsoft/logout` when finished to clear the local encrypted
   cookie and the Entra browser session.

## Application Client Credentials check

Call `GET http://localhost:8080/api/microsoft/users?top=10` from Swagger. This route does not use the
browser identity. The workload requests the Graph `.default` scope and requires the application
`User.Read.All` permission with admin consent. A `403` normally means permission/admin consent is
missing; `401` normally means tenant/client credentials were rejected.

## Grafana verification

1. Generate traffic through `/api/public/posts`, `/api/github/profile`, and the Microsoft routes.
2. Open `http://localhost:3000` and select **Explore**.
3. In Tempo, search service name `api-integration-lab`. A trace should contain the inbound ASP.NET
   span, bounded `integration.request` span, and outbound HTTP span.
4. Copy a trace ID from a Problem Details response or trace. In Loki, query:

   ```logql
   {service_name="api-integration-lab"} |= "<trace-id>"
   ```

5. In Prometheus/Mimir, query the normalized OpenTelemetry metric names:

   ```promql
   api_client_requests_total
   ```

   ```promql
   rate(api_client_request_duration_seconds_sum[5m])
   /
   rate(api_client_request_duration_seconds_count[5m])
   ```

Expected custom metric labels are only `provider`, `operation`, `outcome`, and bounded `error_type`.
Tokens, identities, URLs, request IDs, raw timestamps, bodies, and signatures must not appear as
metric labels. Provider responses may contain identities because those are response data, not
telemetry dimensions.
