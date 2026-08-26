# Word Spelling Feedback Design

## Goal

Make spelling checks in both new-word learning and review explicit and user-controlled. Submitting an answer must reveal whether it is correct and show the canonical spelling. The interface must remain on that result until the user explicitly advances.

## Existing Behavior

The server evaluates a spelling submission and immediately updates the session queue. A correct item is completed; an incorrect item is moved to the queue tail. The response contains the already-advanced session state, and the pages immediately render it. As a result, feedback in the spelling card is reset as the item changes and is effectively invisible.

The queue rule is intentional and remains unchanged: an incorrectly spelled word returns later in the same session.

## API Contract

The spelling command response will carry a spelling outcome containing:

- `result`: `Correct` or `Incorrect`.
- `correctAnswer`: the canonical `Word.Headword` used by server-side spelling validation.

The response will continue to include the updated session state. Non-spelling commands have no spelling outcome.

The server remains the sole source of truth for both answer validation and the revealed answer. The spelling prompt itself will not expose the answer before submission.

## Client State Flow

Learning and review pages will not immediately apply the session state returned by a successful spelling command. Instead, the workspace will retain the returned command response as a pending spelling outcome while continuing to display the submitted item.

During this reveal state:

- The input and submit button are disabled.
- The submitted answer remains visible.
- A clear correct or incorrect status is shown.
- `正确答案：<canonical answer>` is always shown.
- A single primary action advances the UI.

The action is labeled `下一词` while the returned session is still active and has a current item. It is labeled `查看结果` when the returned session is completed. Activating it applies the pending session state and clears the local reveal state.

Pressing Enter submits only while the form is awaiting an answer. It does not advance from the reveal state; advancing requires the explicit button.

If the spelling request fails, no outcome is staged. The existing command error is displayed and the page reloads the authoritative session as it does today.

## Review Exclusion Placement

The `不再复习此词` action remains available in the review memorization preview. It is removed entirely from the review spelling phase. The confirmation dialog and exclusion behavior otherwise remain unchanged.

## Server State Rules

No database schema or intermediate server state is added.

- Correct submission: complete the item and update learning or review progress as today.
- Incorrect submission: mark the failure and move the item to the spelling queue tail as today.
- Return the canonical answer for the submitted item together with the updated session.

Because the server commits before the reveal interaction, refreshing after submission shows the authoritative advanced state. This is acceptable and prevents duplicate submissions or additional persisted UI state.

## Testing

Server tests will verify that correct and incorrect spelling responses include the canonical answer while retaining the existing completion and requeue behavior.

Client component tests will verify:

- Enter and button submission both invoke validation.
- Correct and incorrect results show distinct status indicators and the canonical answer.
- The current item does not change before the advance button is clicked.
- The advance button applies the returned session, including the completed-session result screen.
- The input cannot be resubmitted while the result is displayed.
- Review memorization retains the exclusion action while review spelling does not render it.

Existing learning and review page tests will be updated for the expanded response contract and deferred session application.
