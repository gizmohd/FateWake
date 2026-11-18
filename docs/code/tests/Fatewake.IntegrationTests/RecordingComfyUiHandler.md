# RecordingComfyUiHandler

Test HTTP handler emulating ComfyUI `/prompt`, `/history/{id}`, and a `/view` endpoint that redirects to an off-host signed storage URL. Records every request's URI and headers so tests can assert where credentials were sent.
