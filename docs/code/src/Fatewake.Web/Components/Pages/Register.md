# Register

The local registration form collects first name, last name, city, state/region, email, password, and confirmation. Profile fields are required and bounded to 100 characters, with standard name/address autocomplete hints and accessible labels. Browser validation is backed by API/service validation. Personal details are posted through the antiforgery-protected Web BFF, not query strings.

External-provider buttons retain their OIDC proof flow; newly created provider accounts collect their required details on `CompleteProfile` after ownership is established. Legacy account sign-in remains unchanged.
