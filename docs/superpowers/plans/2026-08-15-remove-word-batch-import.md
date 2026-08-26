# Remove Word Batch Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the JSON-based administrator word batch import feature from the admin application and ASP.NET Core API, leaving retired endpoints to return 404 and preserving single-word/audio workflows.

**Architecture:** Delete the feature vertically from its UI entry point through RTK Query contracts, HTTP endpoints, DTOs, service methods, batch-only validation helpers, error codes, and tests. No database migration is required because the feature has no schema of its own.

**Tech Stack:** React 19, React Router 7, Redux Toolkit Query, Vitest, ASP.NET Core Minimal API, EF Core, xUnit, FluentAssertions, Moq.

---

### Task 1: Add red admin regression assertions for the retired surface

**Files:**
- Modify: `admin/src/services/wordsApi.test.js:144-237`
- Modify: `admin/src/pages/Words.test.jsx` (existing word-list behavior tests)
- Modify: `admin/src/router/router.test.jsx` (existing route behavior tests)

- [ ] **Step 1: Write failing tests**

In `wordsApi.test.js`, add a contract assertion next to the existing aggregate test:

```js
it("does not expose batch import mutations", () => {
  expect(wordsApi.endpoints.validateWordBatch).toBeUndefined();
  expect(wordsApi.endpoints.importWordBatch).toBeUndefined();
});
```

In the word-list page test, assert the old entry point is absent:

```js
expect(screen.queryByRole("link", { name: /批量录入/ })).not.toBeInTheDocument();
```

In the router test, render `/words/batch` and assert the administrator not-found view is shown instead of the importer.

- [ ] **Step 2: Run the focused admin tests to verify RED**

Run:

```bash
pnpm --dir admin test -- src/services/wordsApi.test.js src/pages/Words.test.jsx src/router/router.test.jsx
```

Expected: failures because the two RTK Query endpoints, the word-list link, and the `/words/batch` route currently exist.

### Task 2: Remove the admin batch feature surface

**Files:**
- Modify: `admin/src/pages/Words.jsx:1-9,56-75`
- Modify: `admin/src/router/index.jsx:95-99`
- Modify: `admin/src/services/wordsApi.js:1-13,174-191,269-288`
- Modify: `admin/src/services/wordContracts.js:378-499`
- Delete: `admin/src/pages/WordBatchImport.jsx`
- Delete: `admin/src/pages/WordBatchImport.test.jsx`
- Modify: `admin/src/services/wordsApi.test.js:7-17,144-237`

- [ ] **Step 1: Remove the list entry and route**

Remove `FileJson` from `Words.jsx` imports and remove the `Link` button whose target is `/words/batch`. Remove the lazy route importing `WordBatchImport.jsx`; leave the existing wildcard route to handle `/words/batch`.

- [ ] **Step 2: Remove API mutations and exports**

Remove `normalizeBatchImport` and `normalizeBatchValidation` from the `wordContracts.js` import list. Delete the `validateWordBatch` and `importWordBatch` endpoint definitions and remove their generated hooks from the export destructuring. Keep all single-word and audio endpoints unchanged.

- [ ] **Step 3: Remove batch-only response normalizers**

Delete `fieldError`, `normalizeBatchValidation`, `normalizeWordInputPreview`, `normalizePreviewSense`, `normalizePreviewExample`, `normalizePreviewPronunciation`, and `normalizeBatchImport` from `wordContracts.js`. Do not remove shared `array`, `number`, `uuid`, enum, or audio normalizers still used by remaining contracts.

- [ ] **Step 4: Delete obsolete page and tests**

Delete the importer page and its page test. Remove the old batch setup, mock responses, endpoint dispatches, and expected URLs from `wordsApi.test.js`; retain assertions for create, update, lifecycle, delete, list filters, and audio behavior.

- [ ] **Step 5: Run focused admin verification**

Run:

```bash
pnpm --dir admin test -- src/services/wordsApi.test.js src/pages/Words.test.jsx src/router/router.test.jsx
pnpm --dir admin lint
```

Expected: all focused tests pass, and lint reports no unused icon, hook, normalizer, or route imports.

### Task 3: Add red server contract assertions for retired endpoints

**Files:**
- Modify: `server/TinyLang.UnitTests/OpenApiContractTests.cs:35-42,88-92`
- Modify: `server/TinyLang.UnitTests/WordEndpointTests.cs` near `BatchEndpointsShouldUseAuthenticatedAdmin`

- [ ] **Step 1: Replace OpenAPI presence assertions with absence assertions**

Change the two path checks to:

```csharp
paths.TryGetProperty("/api/admin/words/batch/validate", out _).Should().BeFalse();
paths.TryGetProperty("/api/admin/words/batch", out _).Should().BeFalse();
```

Replace the `BatchWordRequest` and `BatchWordImportResponse` schema checks with assertions that the schemas are absent from `components.schemas`.

- [ ] **Step 2: Add a failing 404 route test**

Add a test that maps `MapWordsApi`, posts an empty JSON object to both retired paths, and asserts both responses have `HttpStatusCode.NotFound`. Do not register or mock batch service methods in this test.

- [ ] **Step 3: Run the focused server tests to verify RED**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~OpenApiContractTests|FullyQualifiedName~WordEndpointTests"
```

Expected: failures because OpenAPI still exposes both paths/schemas and the endpoints currently return 200.

### Task 4: Remove server routes, contracts, and service graph

**Files:**
- Modify: `server/TinyLang/Endpoints/WordEndpoints.cs:35-36,160-186`
- Modify: `server/TinyLang/Services/IWordService.cs:66-77`
- Modify: `server/TinyLang/Services/WordService.cs:264-310,474-713,1378-1581`
- Modify: `server/TinyLang/Dtos/WordDtos.cs:171-258,324-329`
- Modify: `server/TinyLang/Exceptions/ErrorCodes.cs:537-546`
- Modify: `server/TinyLang.UnitTests/WordServiceTests.cs:474-625`
- Modify: `server/TinyLang.UnitTests/WordEndpointTests.cs` to remove the old batch endpoint test

- [ ] **Step 1: Remove endpoint registrations and handlers**

Delete both `/words/batch` route registrations and the `ValidateBatchAsync` and `ImportBatchAsync` handler methods. Keep the single-word administrator routes and their validation metadata unchanged.

- [ ] **Step 2: Remove batch DTOs and limits**

Delete all `BatchWord*` and `BatchExampleSentenceInput` records from `WordDtos.cs`. Remove only the six `MaxBatch*` constants from `WordConstraints`; retain all single-word limits.

- [ ] **Step 3: Remove service interface and implementation members**

Delete `ValidateBatchAsync` and `ImportBatchAsync` from `IWordService`. In `WordService`, delete the public batch methods, `BuildBatchValidationAsync`, `ValidateBatchAudioAsync`, and all helpers/types used only by batch validation: `GetAudioErrorCode`, `ToCreateRequest`, `NormalizeCreateRequest`, `CountBatchCharacters`, `CountRequestCharacters`, `TextLength`, `ToBatchFieldPath`, `AddBatchError`, `ToBatchErrorResponses`, `CreateBatchError`, `ToBatchErrorDictionary`, `BatchNormalizedRow`, `BatchValidationResult`, and `BatchError`.

Keep `NormalizeIdentity`, `WordIdentity`, `ValidateRequestedAudioAsync`, and `ValidateStoredAudioAsync`, which are used by single-word create/update or publish paths. After deletion, use `rg -n 'BatchWord|WordBatch|BatchValidation|CountBatch|ToBatch|AddBatchError' server/TinyLang` and remove any remaining batch-only reference.

- [ ] **Step 4: Remove batch-only error codes and obsolete tests**

Delete `WordBatchRowCountInvalid`, `WordBatchChildCountLimit`, `WordBatchTextLengthLimit`, and `WordBatchValidationFailed` from `ErrorCodes.cs`. Delete the four service tests that only exercise batch validation/import/audio error reporting and remove the old endpoint test that mocks batch service members. Retain tests for single-word audio availability, publication, concurrency, and study-history behavior.

- [ ] **Step 5: Run server build and focused tests**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~OpenApiContractTests|FullyQualifiedName~WordEndpointTests|FullyQualifiedName~WordServiceTests"
dotnet build server/TinyLang/TinyLang.csproj
```

Expected: focused tests pass, the API builds, and no `BatchWord`/`WordBatch` references remain in production server code.

### Task 5: Verify the complete removal boundary

**Files:**
- Verify: `admin/src`, `server/TinyLang`, `server/TinyLang.UnitTests`, `docs/superpowers/specs/2026-08-15-remove-word-batch-import-design.md`
- Modify: none unless verification finds a remaining feature reference

- [ ] **Step 1: Search for stale feature references**

Run:

```bash
rg -n 'WordBatchImport|validateWordBatch|importWordBatch|BatchWord|WordBatch|/words/batch' admin/src server/TinyLang server/TinyLang.UnitTests
```

Expected: no matches. Matches for unrelated multipart-upload batching are allowed only outside these word-specific names and paths.

- [ ] **Step 2: Run complete frontend verification**

Run:

```bash
pnpm --dir admin lint
pnpm --dir admin test
pnpm --dir admin build
```

Expected: all commands exit with code 0.

- [ ] **Step 3: Run complete server verification**

Run:

```bash
dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj
dotnet build server/TinyLang/TinyLang.csproj
```

Expected: all unit tests pass and the server build exits with code 0.

- [ ] **Step 4: Review the final diff**

Run:

```bash
git diff --check
git diff --stat
git status --short
```

Confirm the diff contains only the batch-import removal, the approved design/plan documents, and the previously existing `admin/README.md` documentation change.
