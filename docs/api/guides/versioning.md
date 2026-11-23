# Versioning and compatibility

## Scheme

- **URL major version:** every business endpoint is under `/api/v1/…`. The OpenAPI documents are named `v1`
  (`/openapi/v1.json`, `info.version: v1`).
- **Releases:** the backend as a whole follows semantic versioning (`MAJOR.MINOR.PATCH`) in the
  [changelog](../CHANGELOG.md); images are tagged with the commit SHA and `latest`.
- **Event messages:** the routing key is the contract name (`batch.planned`); a breaking change gets a new routing key
  (`batch.planned.v2`) published in parallel until all consumers moved.

## What is a breaking change

Breaking (needs `/api/v2` or a new routing key):

- removing or renaming an endpoint, field, query parameter or enum value;
- changing a field's type or meaning, making an optional request field required;
- changing a success status code or the error `type` of a case;
- tightening validation so previously valid requests fail.

Not breaking (allowed in `v1`, listed in the changelog):

- new endpoints, new optional request fields, new response fields, new enum values in responses*;
- new error cases with documented status codes;
- looser validation, performance changes.

\* Clients must ignore unknown JSON fields and handle unknown enum values gracefully (show them as text).

## Deprecation policy

1. The old element is marked `deprecated: true` in OpenAPI and in the reference, with the replacement.
2. Responses of a deprecated endpoint carry `Deprecation: true` and `Sunset: <date>` headers.
3. It is removed no earlier than one minor release and 3 months later, announced in the changelog.

(There are no deprecated elements in `v1` yet.)

## How compatibility is enforced

- `OpenApiContractTests` compare each service's generated OpenAPI document with the committed one; any change is visible
  as a diff in the pull request and must be regenerated deliberately.
- Spectral rules (`.spectral.yaml`) enforce the conventions: versioned paths, camelCase fields, problem details errors.
- Integration tests pin the behaviour of every endpoint against PostgreSQL, RabbitMQ and Redis.
