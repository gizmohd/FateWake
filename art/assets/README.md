# Fatewake Asset Registry

Generated/approved art is addressed by stable logical keys. Application composition data must reference keys and versions, never assume a generated filename is identity.

Suggested layout:
- backgrounds/
- characters/{character-key}/poses/
- characters/{character-key}/expressions/
- props/
- foregrounds/
- atmosphere/
- lighting/
- effects/
- hero/

Transparent reusable assets should include enough clean edge/margin for compositing. Prompt files for overlays must explicitly request transparent/isolatable subjects and must preserve the Style Bible and character/location continuity references.

Do not commit provider secrets or generation credentials here.
