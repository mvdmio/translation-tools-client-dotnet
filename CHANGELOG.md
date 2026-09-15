# Changelog

## 2026-09-15: Illegal Environment names fail locally

The client now rejects an illegal Environment name when you construct it. The exception names the allowed characters and the 64-character limit. The CLI tool applies the same check before a push, so a name like `prod:sha` fails locally and never calls the API.
