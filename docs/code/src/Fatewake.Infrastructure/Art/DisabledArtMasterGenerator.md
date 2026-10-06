# DisabledArtMasterGenerator

Used when `ArtGeneration:Provider` is `None`. The generation handler remains registered so approved artwork can pass through the complete reuse pipeline without any provider call. Attempts to generate new artwork fail explicitly, are logged by the durable worker, and follow its normal retry/failure policy.
