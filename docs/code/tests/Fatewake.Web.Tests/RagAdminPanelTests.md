# RagAdminPanelTests

Prerenders the real RAG panel with an administrator circuit and a deliberately rejected API token. Verifies that the token is sent and the page offers explicit sign-in recovery, an extended-error action, and a labelled native dialog rather than the former generic import failure. The tightly coupled private handler never connects to a real API.
