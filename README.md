# Budget App

A small backend .NET app that imports a monthly Google Sheets budget into its own local database and serves it back over an API — so summaries, charts, and search can be built on top of it without touching the sheets.

Built with the help of Claude Code.

This is a hands-on project — I'm writing code myself too, with Claude Code assisting (scaffolding, boilerplate, and the trickier Google Sheets / OAuth wiring), not building the whole thing for me. Claude Code stops for review after each milestone, and I review the code before moving on.

## What it does (v1)

- Connects to Google Sheets **read-only** with your own Google account
- Imports one monthly budget spreadsheet at a time
- Stores transactions in a local SQLite database
- Serves transactions and category summaries over a REST API
- Re-running an import is safe — no duplicates

## What it deliberately does not do (v1)

- Write anything back to Google Sheets
- Connect to banks or auto-fetch transactions
- Reconcile card balances / payment logs
- Compare planned vs. actual amounts (planned amounts live in the sheet's Summary tab)

Those are v2 candidates.

## Tech stack

- ASP.NET Core Web API (latest LTS .NET)
- Entity Framework Core + SQLite
- Google.Apis.Sheets.v4 — official Google Sheets API client for .NET
- Google OAuth 2.0, scope `spreadsheets.readonly` only
- Blazor Server + MudBlazor for the dashboard (single page, `/dashboard`, same project as the API)
- Swagger / OpenAPI in development

## Getting started

### Prerequisites

- .NET SDK (latest LTS)
- A Google account that owns / can view the budget sheets
- Git

### 1. Google Cloud setup (one time)

1. Go to [console.cloud.google.com](https://console.cloud.google.com) and create a project
2. APIs & Services → Library → enable the **Google Sheets API**
3. APIs & Services → OAuth consent screen → External, add your app name and support email, and add your own Google account as a test user
4. APIs & Services → Credentials → Create credentials → OAuth client ID → **Desktop app** → download the JSON

No billing account is needed for the Sheets API.
