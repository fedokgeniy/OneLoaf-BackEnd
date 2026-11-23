# Mock server

Frontend work can start without the backend: [Prism](https://github.com/stoplightio/prism) serves responses generated
from the OpenAPI documents and validates requests against them.

```powershell
npx @stoplight/prism-cli@5.14.2 mock docs/api/reference/core-api.openapi.json --port 4010
```

```powershell
npx @stoplight/prism-cli@5.14.2 mock docs/api/reference/assistant.openapi.json --port 4011
```

```powershell
npx @stoplight/prism-cli@5.14.2 mock docs/api/reference/notification.openapi.json --port 4012
```

```powershell
curl.exe -s http://localhost:4010/api/v1/recipes -H "Authorization: Bearer anything"
```

- Responses follow the schemas (types, required fields), not the business rules; ids and numbers are placeholders.
- A request that breaks the contract (missing required field, wrong type) gets a `422` from Prism with the violations —
  useful to check a client against the contract.
- Ask for an error response with the `Prefer` header: `Prefer: code=404`.
- `--dynamic` (`-d`) generates random data per request instead of static examples.

For realistic data use the [sandbox](sandbox.md) instead.
