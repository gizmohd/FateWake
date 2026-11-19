# CharacterCustomizationOptions

Controls global availability of player character appearance customization.

Configuration section:

```json
"Features": {
  "CharacterCustomization": {
    "Enabled": false
  }
}
```

The default is **false**. When disabled, the Web application must not render customization navigation, buttons, forms, upgrade actions that invoke customization, or customization-generation requests.

This is a global product capability switch, not an account purchase entitlement. A future monetization/entitlement service will additionally determine whether a particular account has purchased/unlocked character customization. Both checks are required once paid customization ships:

`Feature enabled AND account entitled -> customization available`.

The server/API must enforce entitlement independently of UI visibility; hiding controls is not an authorization boundary.
