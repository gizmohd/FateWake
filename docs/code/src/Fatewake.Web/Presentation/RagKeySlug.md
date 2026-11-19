# RagKeySlug

Pure helper that turns a RAG document title into the default source key and timeline key shown in the admin import form.

- Lowercases, replaces whitespace runs with a single `-`, and removes characters other than `a-z`, `0-9` and `-`.
- Truncates to the field's maximum length (200 source key, 100 timeline key) without a trailing `-`.
- Has no side effects. The panel applies the result only while the administrator has not typed in that key field.
