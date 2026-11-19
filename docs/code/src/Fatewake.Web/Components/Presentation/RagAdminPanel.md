# RagAdminPanel

Administrator-only upload staging and corpus status UI. Each API request uses the signed-in user's access token. Upload and refresh errors are independent and offer an optional native modal with safe explanation, HTTP status, operation, error code, request reference, and UTC timestamp. Native dialog behavior supplies focus trapping, Escape dismissal, and focus restoration; its close form never submits an HTTP request.

401/403 responses stop periodic polling and disable uploads until sign-in/page reload, preventing stale circuits from repeatedly submitting unauthenticated work. Uploads remain bounded to 100 MiB. No response bodies, document contents, or credentials are logged. Failed uploads are not retried automatically because staging may already have succeeded before a connection failed.
