# S2 Frontend

Run these commands from `frontend/`:

```powershell
npm.cmd ci
npm.cmd run gen:api
npm.cmd run build
npm.cmd run dev
```

`gen:api` generates `src/api/schema.d.ts` from `../openapi/ledger.yaml`.
Commit the generated file and regenerate it whenever the contract changes.
Capture the successful `gen:api` output for the Session 4 report.

Use the typed `api` export from `src/api/client.ts` for backend requests.
Contract paths already include `/api/v1`, so the client uses `/` as its base URL.
The Vite development server proxies `/api` to `http://localhost:8082`;
start the backend separately. Production hosting must configure its own API routing.

TypeScript 5.9 is used to satisfy the installed OpenAPI generator's peer dependency.
The optional Session 4 UI page has not been implemented.

## Original Template Notes

This template provides a minimal setup to get React working in Vite with HMR and some Oxlint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the Oxlint configuration

If you are developing a production application, we recommend enabling type-aware lint rules by installing `oxlint-tsgolint` and editing `.oxlintrc.json`:

```json
{
  "$schema": "./node_modules/oxlint/configuration_schema.json",
  "plugins": ["react", "typescript", "oxc"],
  "options": {
    "typeAware": true
  },
  "rules": {
    "react/rules-of-hooks": "error",
    "react/only-export-components": ["warn", { "allowConstantExport": true }]
  }
}
```

See the [Oxlint rules documentation](https://oxc.rs/docs/guide/usage/linter/rules) for the full list of rules and categories.
