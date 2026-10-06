# Character Customization

Fatewake supports durable character appearance customization as an optional product capability. The visual-state architecture remains:

`Base Identity → Appearance Configuration → Equipment/Loadout → Condition/Injuries → Scene/Pose/Camera → Rendered Asset`

## Availability

Character customization is intentionally disabled for the initial release. Configure:

```json
{
  "Features": {
    "CharacterCustomization": {
      "Enabled": false
    }
  }
}
```

When disabled, users receive the canonical/default character appearance and no customization option is presented.

Setting `Enabled` to `true` enables the product capability for development/testing. It must not eventually be treated as proof that a user purchased the feature.

## Future paid upgrade

Character customization is planned as an optional account/user upgrade. The future access rule is:

`global feature enabled && account entitlement active`

The entitlement must be enforced by server-side customization commands/endpoints as well as the Web UI. Account purchase state must not be encoded into the appearance fingerprint: fingerprints describe visual state, while entitlement determines whether a user may request a change.

Generated/approved appearance combinations remain durable and reusable under the existing exact-fingerprint rules.
