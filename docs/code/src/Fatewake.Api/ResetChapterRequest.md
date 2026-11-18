# ResetChapterRequest

Body for `POST /api/debug/session/reset-chapter`, which is mapped only when the API runs in the Development environment. The caller must own the survivor; the event instance must be that survivor's active chapter. See `ISessionStore.ResetChapterAsync` for what is discarded.
