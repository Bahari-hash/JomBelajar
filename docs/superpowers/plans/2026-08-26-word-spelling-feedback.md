# Word Spelling Feedback Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reveal every spelling result and canonical answer, then wait for an explicit user action before rendering the server's next session state.

**Architecture:** Keep the existing server-side completion and incorrect-answer requeue rules. Expand the command response with a nullable spelling outcome, let `WordSpellingCard` stage the returned outcome and session locally, and apply that session through an explicit advance callback. Remove review exclusion from the spelling branch only.

**Tech Stack:** .NET 10, Entity Framework Core, xUnit, React 19, TypeScript, RTK Query, Vitest, Testing Library, Tailwind CSS, lucide-react.

---

## File Structure

- `server/TinyLang/Dtos/WordStudyDtos.cs`: define the spelling outcome response contract.
- `server/TinyLang/Services/WordStudySessionEngine.cs`: return the canonical submitted word with the spelling result.
- `server/TinyLang.UnitTests/WordStudySessionEngineTests.cs`: protect answer reveal and existing queue behavior.
- `app/src/features/wordStudy/wordStudyTypes.ts`: mirror the expanded API contract.
- `app/src/features/wordStudy/WordSpellingCard.tsx`: own the answer/reveal state and explicit advance control.
- `app/src/features/wordStudy/WordSpellingCard.test.tsx`: verify submit, reveal, locking, and advance behavior.
- `app/src/features/wordStudy/WordStudyWorkspace.tsx`: forward the full response, apply the advance callback, and remove spelling exclusion.
- `app/src/features/wordStudy/WordStudyWorkspace.test.tsx`: verify deferred advancement and exclusion placement.
- `app/src/pages/WordLearningPage.tsx`: return spelling responses without immediately changing session.
- `app/src/pages/WordReviewPage.tsx`: return spelling responses without immediately changing session.

### Task 1: Expand The Server Spelling Contract

**Files:**
- Modify: `server/TinyLang/Dtos/WordStudyDtos.cs`
- Modify: `server/TinyLang/Services/WordStudySessionEngine.cs`
- Test: `server/TinyLang.UnitTests/WordStudySessionEngineTests.cs`

- [ ] **Step 1: Write failing server assertions**

Extend `SpellingShouldRequeueIncorrectAndCompleteCorrectLearning` so the incorrect and correct responses assert:

```csharp
incorrect.SpellingOutcome.Should().Be(new WordSpellingOutcomeResponse(
    WordSpellingResult.Incorrect,
    "école"));
correct.SpellingOutcome.Should().Be(new WordSpellingOutcomeResponse(
    WordSpellingResult.Correct,
    "école"));
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```bash
dotnet test TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~SpellingShouldRequeueIncorrectAndCompleteCorrectLearning --no-restore
```

Expected: compilation fails because `SpellingOutcome` and `WordSpellingOutcomeResponse` do not exist.

- [ ] **Step 3: Implement the response contract**

Add:

```csharp
public sealed record WordSpellingOutcomeResponse(
    WordSpellingResult Result,
    string CorrectAnswer);

public sealed record WordStudyCommandResponse(
    WordSpellingOutcomeResponse? SpellingOutcome,
    WordStudySessionStateResponse Session);
```

In `SubmitSpellingAsync`, capture `current.Word!.Headword` as the canonical answer and return:

```csharp
new WordSpellingOutcomeResponse(
    correct ? WordSpellingResult.Correct : WordSpellingResult.Incorrect,
    correctAnswer)
```

Keep every non-spelling response outcome `null`.

- [ ] **Step 4: Run focused and engine tests and verify GREEN**

Run:

```bash
dotnet test TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter FullyQualifiedName~WordStudySessionEngineTests --no-restore
```

Expected: all engine tests pass and incorrect answers still requeue.

### Task 2: Implement The Spelling Reveal Card

**Files:**
- Modify: `app/src/features/wordStudy/wordStudyTypes.ts`
- Modify: `app/src/features/wordStudy/WordSpellingCard.tsx`
- Test: `app/src/features/wordStudy/WordSpellingCard.test.tsx`

- [ ] **Step 1: Write failing component tests**

Define response fixtures with:

```ts
spellingOutcome: { result: "Incorrect", correctAnswer: "école" },
session: nextSession,
```

Test that Enter and button submission reveal `拼写错误` or `拼写正确`, always render `正确答案：école`, retain the typed answer, disable the input, and do not call `onAdvance` until `下一词` or `查看结果` is clicked.

- [ ] **Step 2: Run the card tests and verify RED**

Run:

```bash
pnpm test -- src/features/wordStudy/WordSpellingCard.test.tsx
```

Expected: tests fail because the card accepts only a result enum and has no explicit advance state.

- [ ] **Step 3: Expand TypeScript contracts**

Replace `spellingResult` with:

```ts
export interface WordSpellingOutcome {
  result: WordSpellingResult;
  correctAnswer: string;
}

export interface WordStudyCommandResponse {
  spellingOutcome: WordSpellingOutcome | null;
  session: WordStudySessionState;
}
```

- [ ] **Step 4: Implement reveal and advance states**

Change card props to:

```ts
onSubmit: (answer: string) => Promise<WordStudyCommandResponse | null>;
onAdvance: (session: WordStudySessionState) => void;
```

Store the successful response locally. Before a response, Enter and `提交拼写` submit normally. After a response, disable the input, show a `CheckCircle2` or `XCircle` result with the canonical answer, replace submit with `下一词` or `查看结果`, and call `onAdvance(response.session)` only from that button. Clear answer and response when `item.itemId` changes.

- [ ] **Step 5: Run card tests and verify GREEN**

Run:

```bash
pnpm test -- src/features/wordStudy/WordSpellingCard.test.tsx
```

Expected: all card tests pass.

### Task 3: Integrate Deferred Advancement And Exclusion Placement

**Files:**
- Modify: `app/src/features/wordStudy/WordStudyWorkspace.tsx`
- Modify: `app/src/features/wordStudy/WordStudyWorkspace.test.tsx`
- Modify: `app/src/pages/WordLearningPage.tsx`
- Modify: `app/src/pages/WordReviewPage.tsx`

- [ ] **Step 1: Write failing workspace tests**

Update `onSpelling` fixtures to return a full command response. Verify the original spelling item remains rendered after submit, then the response session appears only after clicking `下一词`. Add a review spelling assertion:

```ts
expect(
  screen.queryByRole("button", { name: "不再复习此词" }),
).not.toBeInTheDocument();
```

Retain the existing memorization-phase exclusion confirmation test.

- [ ] **Step 2: Run workspace tests and verify RED**

Run:

```bash
pnpm test -- src/features/wordStudy/WordStudyWorkspace.test.tsx
```

Expected: deferred advance and exclusion placement assertions fail.

- [ ] **Step 3: Wire the full response and advance callback**

Change `WordStudyWorkspace` props so `onSpelling` returns `WordStudyCommandResponse | null` and add:

```ts
onSpellingAdvance: (session: WordStudySessionState) => void;
```

Pass that callback to `WordSpellingCard`. Delete the spelling-phase exclusion button while retaining the memorization-phase button and dialog.

In both pages, make `onSpelling` return the unwrapped response without calling `setSession`; pass `onSpellingAdvance={setSession}`. Keep failure reload behavior unchanged.

- [ ] **Step 4: Run workspace and page tests and verify GREEN**

Run:

```bash
pnpm test -- src/features/wordStudy/WordStudyWorkspace.test.tsx src/pages/WordLearningPage.test.tsx src/pages/WordReviewPage.test.tsx
```

Expected: all selected tests pass.

### Task 4: Full Verification

**Files:**
- Verify all modified files.

- [ ] **Step 1: Run all server unit tests**

```bash
dotnet test TinyLang.UnitTests/TinyLang.UnitTests.csproj --no-restore
```

Expected: zero failures.

- [ ] **Step 2: Run all app tests**

```bash
pnpm test
```

Expected: zero failures.

- [ ] **Step 3: Run production builds and diff checks**

```bash
pnpm build
git diff --check
```

Expected: the app build and whitespace checks pass.
