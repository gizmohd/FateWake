# CompleteProfile

Authenticated `/complete-profile` page for finishing a new provider signup. Collects the same four required bounded fields as local registration using a same-origin antiforgery-protected POST. The BFF forwards the current user's access token and updates only the API-authenticated account. Safe error redirects contain no submitted details. Completion proceeds to backup-password setup when needed, otherwise gameplay.

This is a signup-flow prompt, not a new gameplay authorization policy. Existing account sessions and direct gameplay authorization retain their existing behavior.
