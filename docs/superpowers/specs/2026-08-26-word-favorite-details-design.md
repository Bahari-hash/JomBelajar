# Word Favorite Details Design

## Goal

Allow a user to open a favorite word from the profile favorite library and inspect its complete learning content: every sense, usage note, example sentence, translation, word audio, and example-sentence audio.

## Scope

The feature is limited to the user-facing favorite library. It does not change the database schema or add an API endpoint because the existing paged favorites response already contains the complete word aggregate required by the detail view.

## Interaction

- The word heading and summary area in each favorite row becomes a clearly interactive button.
- Activating it opens a word-detail view.
- When the favorite library itself is shown as a profile modal, the detail view replaces its visible content instead of stacking a second modal on top of it.
- Closing the detail view returns to the same favorite page and preserves the surrounding library state.
- The detail header shows the headword and a word-pronunciation control when word audio exists.
- The detail body shows every sense in server-provided order.
- Each sense shows its part of speech, definition, optional usage note, and every example in server-provided order.
- Each example shows the sentence, translation, and a playback control when example audio exists.
- The detail view contains a `取消收藏` command and a close command.

## Removal Behavior

- Removing the selected word uses the existing favorite mutation.
- On success, the detail view closes and the word disappears from the favorite list through the existing cache invalidation/refetch behavior.
- If removing the last item reduces the page count, the existing page-clamping behavior remains responsible for returning to a valid page.
- On failure, the detail view remains open and displays `收藏状态更新失败，请重试。`; its content remains available so the user can retry or close it.
- The row-level remove command remains available for fast list management.

## Component Boundaries

`WordFavoritesPanel` continues to own paging, selection, removal, and modal navigation state. A focused favorite-word detail component renders one `WordFavorite` and emits close/remove commands. This keeps the nested content rendering and audio controls independently testable without introducing new global state.

The existing `AudioPlaybackButton` is reused for both word and example audio. No raw audio URL is stored or rendered by the favorite components.

## Data Flow

1. `useGetFavoritesQuery` loads the current page.
2. The existing response supplies `senses`, nested `examples`, `audioResourceId`, and each example's `audioResourceId`.
3. Selecting a row stores that `WordFavorite` locally and renders the detail view without another network request.
4. Closing clears the selected word and restores the list.
5. Removing calls `useSetFavoriteMutation`; success clears selection, while failure retains selection and records an error.

## Accessibility

- The row opener is a semantic button with an accessible name containing the headword.
- The detail view has a labelled dialog/content heading and a dedicated close button.
- Existing audio controls retain their accessible play/pause labels; word and example labels identify the content being played.
- Interactive row content and the separate remove control do not use nested buttons.
- Error feedback uses an alert role.

## Error And Empty States

- Existing favorite-list loading, retrieval error, retry, empty, and pagination states remain unchanged.
- Missing word audio or example audio simply omits that playback control.
- Senses with no examples still render their definition and optional usage note.
- Removal errors are shown in the currently visible context: the detail view when removal starts there, or the list when it starts from a row.

## Testing

Frontend tests will verify:

- Clicking a favorite word opens its detail view.
- All senses, usage notes, examples, and translations are rendered.
- Word and example audio controls receive the correct resource identifiers and labels.
- Closing returns to the same favorite list.
- Successful removal closes the detail and removes/refetches the word.
- Failed removal keeps the detail open and shows an error.
- The existing page-clamping and modal close behavior do not regress.

The production build and complete frontend test suite will be run after implementation.
