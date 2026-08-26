# Remove Word Batch Import Design

## Goal

Remove the JSON-based administrator word batch import feature from the admin application and server so that the current implementation does not constrain the planned word-module redesign.

## Scope

The removal covers the complete vertical feature surface:

- Remove the batch-import entry point from the administrator word list.
- Remove the `/words/batch` administrator route and page.
- Remove the administrator RTK Query mutations and response normalizers used only by batch validation and import.
- Remove `/api/admin/words/batch/validate` and `/api/admin/words/batch` from the server route table.
- Remove batch-only word DTOs, service interface members, service implementation, helpers, error codes, and tests.
- Update route and OpenAPI tests to assert the retired surface is absent.

The retired API paths will have no compatibility handlers and will naturally return `404 Not Found`.

## Out Of Scope

- Do not change single-word creation, editing, publication, audio selection, or audio upload.
- Do not change the consumer word-study module.
- Do not change multipart media upload or other functionality that uses the term "batch".
- Do not add a replacement import format or workflow.
- Do not modify the database schema or existing word data.

## Architecture

The administrator application will retain only the single-word management workflow. The word list will link to single-word creation, and the router will no longer recognize `/words/batch`; the existing wildcard route will render the administrator not-found page.

The server will expose only single-word administrator commands and user word queries. Removing the two route registrations will make both retired paths return the application's normal route-level 404. Removing the batch DTO and service graph ensures no unreachable import implementation remains for the future refactor to work around.

No database migration is required because batch import writes the same `Word`, `WordSense`, `ExampleSentence`, and `WordPronunciation` aggregates as single-word creation and owns no schema.

## Error Handling

No replacement error response is introduced. Calls to either retired endpoint will not match a route and will receive the standard ASP.NET Core 404 response. Batch-specific validation errors and error codes will be removed because no remaining request can produce them.

## Verification

Administrator tests will verify that the word list has no batch-import action, the route is absent, and word API tests no longer include batch mutations. Server tests will verify that OpenAPI has neither retired path nor batch schema. Existing single-word management and word-study tests must continue to pass.

Final verification will run administrator lint, tests, and build, followed by server unit tests and build.
