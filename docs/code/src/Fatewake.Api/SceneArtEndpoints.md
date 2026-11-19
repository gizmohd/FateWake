# SceneArtEndpoints

Maps the allowlisted scene-art request route and immutable image-delivery route. Scene resolution detects incomplete approved assets and queues a durable replacement generation instead of reporting them ready. The binary endpoint resolves only approved database metadata and registered delivery derivatives (never archival masters), returns content-hash ETags, and checks account ownership before serving private assets. Public shared artwork is immutable-public; private character art uses private caching.
