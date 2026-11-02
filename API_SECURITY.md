# BookShop API security

Public product GET endpoints remain available without credentials. Product writes, image uploads and **all booking routes** require a unique administrator key of at least 32 bytes stored only in protected server configuration.

In Azure App Service, set `Security__AdminApiKey`. Supply the `X-Admin-Api-Key` header from trusted server-side clients over HTTPS. Use Swagger's **Authorize** action for manual testing. Never commit or ship the key in frontend code. Protected routes fail closed (503 when server key is absent, 401 for missing/invalid request key).

This shared key is only an interim mitigation. A production release should use user authentication, per-customer booking authorization, role-based access control and request throttling.
