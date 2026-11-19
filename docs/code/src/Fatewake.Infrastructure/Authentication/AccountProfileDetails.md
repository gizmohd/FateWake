# AccountProfileDetails

Shared private-registration input contract: first name, last name, city, and free-text state/region. All four fields are required, trimmed, limited to 100 UTF-16 characters, and reject embedded control characters. Unicode names, spaces, apostrophes, and hyphens are supported. Validation errors never echo submitted values. These are self-reported account details, not verified legal identity, geolocation, or survivor state.
