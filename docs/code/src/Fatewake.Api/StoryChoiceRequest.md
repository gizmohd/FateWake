# StoryChoiceRequest

Body for `POST /api/story/{survivorId}/scene/{sequence}/choose`. The server validates `ActionType` against the choices the persisted scene offered (`SceneChoiceResolver`); unknown actions return 400.
