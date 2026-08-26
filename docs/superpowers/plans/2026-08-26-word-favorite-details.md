# Word Favorite Details Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a complete favorite-word detail dialog showing all senses, usage notes, examples, translations, and available word/example audio, with close and cancel-favorite actions.

**Architecture:** Reuse the existing complete `WordFavorite` payload and `AudioPlaybackButton`; no server endpoint or schema changes are needed. Keep selection and removal state in `WordFavoritesPanel`, and extract the detail rendering into a focused `WordFavoriteDetails` component so the existing list pagination and modal behavior remain stable.

**Tech Stack:** React, TypeScript, Redux Toolkit Query, Testing Library, Vitest, lucide-react.

---

### Task 1: Add the focused detail component contract

**Files:**
- Create: `app/src/features/wordStudy/WordFavoriteDetails.tsx`
- Test: `app/src/features/wordStudy/WordFavoriteDetails.test.tsx`

- [ ] **Step 1: Write the failing test**

Create a representative `WordFavorite` with two senses, usage notes, two examples, word audio, and one example audio. Render the component with `onClose` and `onRemove` spies and assert that all content is visible, the word audio button is labelled with the headword, the example audio button is labelled with the example text, and both close/remove controls are present.

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `pnpm test -- --run src/features/wordStudy/WordFavoriteDetails.test.tsx`

Expected: FAIL because `WordFavoriteDetails` does not exist.

- [ ] **Step 3: Implement the minimal detail component**

Use a semantic dialog-like section with a heading, close button, headword audio control, and ordered sense/example rendering:

```tsx
type Props = {
  word: WordFavorite;
  onClose: () => void;
  onRemove: () => void;
};
```

Render `word.senses` in order; for each sense render part of speech, definition, optional `usageNote`, and `examples`. Render `AudioPlaybackButton` only when the corresponding audio id is non-null, passing labels such as `朗读 ${word.headword}` and `朗读例句 ${example.sentence}`. Keep the remove command as a plain button and use lucide `X` for close.

- [ ] **Step 4: Run the focused test to verify it passes**

Run: `pnpm test -- --run src/features/wordStudy/WordFavoriteDetails.test.tsx`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add app/src/features/wordStudy/WordFavoriteDetails.tsx app/src/features/wordStudy/WordFavoriteDetails.test.tsx
git commit -m "feat: add favorite word detail view"
```

### Task 2: Add selection and detail navigation to the favorite panel

**Files:**
- Modify: `app/src/features/wordStudy/WordFavoritesPanel.tsx`
- Modify: `app/src/features/wordStudy/WordFavoritesPanel.test.tsx`

- [ ] **Step 1: Write failing interaction tests**

Add tests that click a row opener and verify every sense/example from the fixture appears, click the detail close control and verify the list returns, click the detail `取消收藏` control and verify the existing delete request is made and the detail closes after a successful response, and make the delete request fail to verify the detail remains open with `收藏状态更新失败，请重试。`.

- [ ] **Step 2: Run the focused tests to verify they fail**

Run: `pnpm test -- --run src/features/wordStudy/WordFavoritesPanel.test.tsx`

Expected: FAIL because rows are not interactive and no detail view exists.

- [ ] **Step 3: Implement selection state and detail mode**

Add `selectedWord` state typed as `WordFavorite | null`. Make only the word summary a button, keeping the row-level remove button separate to avoid nested interactive elements. When `selectedWord` is set, render `WordFavoriteDetails` in place of the list content while retaining the outer panel/modal shell. Pass `onClose={() => setSelectedWord(null)}`.

Update removal to accept an optional source word and, on success, clear selection when the removed word is selected. Preserve the existing query invalidation and page clamp behavior. On detail removal failure, set the existing error state without clearing selection.

- [ ] **Step 4: Run the focused tests to verify they pass**

Run: `pnpm test -- --run src/features/wordStudy/WordFavoritesPanel.test.tsx`

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add app/src/features/wordStudy/WordFavoritesPanel.tsx app/src/features/wordStudy/WordFavoritesPanel.test.tsx
git commit -m "feat: open favorite word details"
```

### Task 3: Verify accessibility, regression behavior, and production output

**Files:**
- Modify: `app/src/features/wordStudy/WordFavoriteDetails.test.tsx` if assertions need accessibility-specific coverage.
- Modify: `app/src/features/wordStudy/WordFavoritesPanel.test.tsx` if existing modal/pagination fixtures need compatibility updates.

- [ ] **Step 1: Confirm required interaction coverage**

Ensure tests cover the list modal close button, backdrop close button, pagination, failed row removal, failed detail removal, detail close, and successful detail removal.

- [ ] **Step 2: Run the complete frontend test suite**

Run: `pnpm test -- --run`

Expected: all frontend test files pass with zero failed tests.

- [ ] **Step 3: Run the production build**

Run: `pnpm build`

Expected: TypeScript compilation and Vite production build exit successfully.

- [ ] **Step 4: Check the final diff**

Run: `git diff --check && git status --short`

Expected: no whitespace errors; only the feature files and the already-existing unrelated user files are present.

- [ ] **Step 5: Commit any test-only cleanup**

```bash
git add app/src/features/wordStudy/WordFavoriteDetails.test.tsx app/src/features/wordStudy/WordFavoritesPanel.test.tsx
git commit -m "test: cover favorite word details"
```

