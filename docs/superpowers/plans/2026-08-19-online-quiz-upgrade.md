# Online Quiz Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将在线测试模块升级为分类试卷、支持多空听写题、整批 JSON 录入和用户错题本/单题重做，并同步更新管理端与用户端。

**Architecture:** 在现有 Paper/PaperAttempt 聚合上渐进扩展，删除旧标签模型并新增试卷分类多对多关系；听写题使用独立有序空集合和可用音频资源引用；错题本以用户与题目唯一记录保存状态，提交试卷与错题重做分别在服务端事务中完成判分和状态更新。数据库采用破坏式迁移，旧在线测试数据全部清空。

**Tech Stack:** ASP.NET Core Minimal API、EF Core/Npgsql、FluentValidation、React 19、TypeScript、Vite、RTK Query、Vitest、Testing Library、DaisyUI/Tailwind。

---

### Task 1: 重建在线测试领域模型和契约

**Files:**
- Create: `server/TinyLang/Entities/PaperCategory.cs`
- Create: `server/TinyLang/Entities/PaperCategoryAssignment.cs`
- Create: `server/TinyLang/Entities/PaperDictationBlank.cs`
- Create: `server/TinyLang/Entities/PaperWrongQuestion.cs`
- Create: `server/TinyLang/Entities/Enums/PaperWrongQuestionStatus.cs`
- Modify: `server/TinyLang/Entities/Paper.cs`
- Modify: `server/TinyLang/Entities/PaperQuestion.cs`
- Modify: `server/TinyLang/Entities/PaperAttemptAnswer.cs`
- Modify: `server/TinyLang/Entities/Enums/PaperQuestionType.cs`
- Modify: `server/TinyLang/Database/ApplicationDbContext.cs`
- Modify: `server/TinyLang/Dtos/OnlineQuizDtos.cs`
- Modify: `server/TinyLang/Dtos/OnlineQuizValidators.cs`
- Test: `server/TinyLang.UnitTests/OnlineQuizModelTests.cs`

- [ ] **Step 1: Write model tests** for `Dictation`, one-answer-per-blank, `TextAnswers` array shape, category optionality, and `UserId + QuestionId` wrong-question uniqueness.
- [ ] **Step 2: Run the focused tests** with `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~OnlineQuizModelTests"`; verify the new enum and properties are absent or fail before implementation.
- [ ] **Step 3: Add entities and DTOs.** `PaperQuestion` gets nullable `AudioResourceId`, `AudioResource`, and `DictationBlanks`; `PaperAttemptAnswer` gets nullable `string[] TextAnswers` for dictation while retaining `TextAnswer` for ordinary fill-blank; `Paper` replaces `Tags` with `CategoryAssignments`; DTOs use `categoryIds`, `categoryNames`, `audioResourceId`, `audioFileName`, `blanks`, and `answers`.
- [ ] **Step 4: Add validators.** Enforce mutually exclusive answer fields, `Dictation` array length and blank limits, category name limits, and no audio/blanks on non-dictation questions.
- [ ] **Step 5: Run the focused tests** again and commit the model/contracts checkpoint with `git add server/TinyLang && git commit -m "feat(server): define upgraded quiz model"`.

### Task 2: Configure EF Core and perform the destructive migration

**Files:**
- Create: `server/TinyLang/Database/Configurations/PaperCategoryConfiguration.cs`
- Create: `server/TinyLang/Database/Configurations/PaperCategoryAssignmentConfiguration.cs`
- Create: `server/TinyLang/Database/Configurations/PaperDictationBlankConfiguration.cs`
- Create: `server/TinyLang/Database/Configurations/PaperWrongQuestionConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/PaperConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/PaperQuestionConfiguration.cs`
- Modify: `server/TinyLang/Database/Configurations/PaperAttemptAnswerConfiguration.cs`
- Create: `server/TinyLang/Database/Migrations/<timestamp>_RebuildOnlineQuizForUpgrade.cs`
- Create/Modify: generated migration designer and `ApplicationDbContextModelSnapshot.cs`
- Test: `server/TinyLang.UnitTests/Database/OnlineQuizSchemaTests.cs`

- [ ] **Step 1: Write schema tests** for category uniqueness, paper/category uniqueness, ordered dictation blanks, audio foreign-key restriction, wrong-question uniqueness, and answer-shape checks.
- [ ] **Step 2: Configure constraints and indexes.** Use unique indexes for category name/slug, `(PaperId, CategoryId)`, `(QuestionId, SortOrder)`, `(AttemptId, QuestionId)`, and `(UserId, QuestionId)`; use `text[]` for `TextAnswers`; restrict deletion of referenced audio, questions, and papers.
- [ ] **Step 3: Generate or author the destructive migration.** Drop old paper/question/attempt tables and tag indexes, then create the new tables and foreign keys in dependency order. Do not copy old rows.
- [ ] **Step 4: Apply the migration** with `dotnet ef database update --project server/TinyLang/TinyLang.csproj --startup-project server/TinyLang/TinyLang.csproj` and run schema tests against the development database.
- [ ] **Step 5: Commit** with `git add server/TinyLang && git commit -m "feat(server): rebuild quiz database schema"`.

### Task 3: Add paper-category management and remove tag behavior

**Files:**
- Create: `server/TinyLang/Services/IPaperCategoryService.cs`
- Create: `server/TinyLang/Services/PaperCategoryService.cs`
- Modify: `server/TinyLang/Services/DependencyInjection.cs`
- Modify: `server/TinyLang/Endpoints/OnlineQuizEndpoints.cs`
- Modify: `server/TinyLang/Services/PaperService.cs`
- Modify: `server/TinyLang/Services/IPaperService.cs`
- Modify: `server/TinyLang/Constants/ErrorCodes.cs`
- Create: `server/TinyLang/Dtos/PaperCategoryDtos.cs`
- Create: `server/TinyLang.UnitTests/PaperCategoryServiceTests.cs`
- Modify: `server/TinyLang.UnitTests/PaperServiceTests.cs`

- [ ] **Step 1: Add service tests** for create/update, inactive-category rejection, optional categories, public active-only listing, and delete-in-use conflict.
- [ ] **Step 2: Implement category CRUD** at `/api/admin/paper-categories` and active public listing at `/api/paper-categories`, preserving the existing authorization policies.
- [ ] **Step 3: Replace paper tag queries and projections** with category IDs and summaries; delete tag DTOs, validators, service methods, and error paths.
- [ ] **Step 4: Run** `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~PaperCategoryServiceTests|FullyQualifiedName~PaperServiceTests"` and commit.

### Task 4: Implement dictation editing, validation, and batch import on the server

**Files:**
- Create: `server/TinyLang/Dtos/PaperBatchDtos.cs`
- Create: `server/TinyLang/Services/IPaperBatchService.cs`
- Create: `server/TinyLang/Services/PaperBatchService.cs`
- Modify: `server/TinyLang/Services/PaperService.cs`
- Modify: `server/TinyLang/Endpoints/OnlineQuizEndpoints.cs`
- Modify: `server/TinyLang/Dtos/OnlineQuizValidators.cs`
- Create: `server/TinyLang.UnitTests/PaperBatchServiceTests.cs`
- Modify: `server/TinyLang.UnitTests/OnlineQuizEndpointTests.cs`

- [ ] **Step 1: Write failing tests** for the JSON shape, category-name lookup, ready-audio lookup, all-or-nothing validation, draft-only import, and field paths such as `papers[0].questions[1].blanks[0].answer`.
- [ ] **Step 2: Implement paper upsert mapping** for category IDs and ordered dictation blanks; reject non-ready or missing audio before save/publish.
- [ ] **Step 3: Implement batch validation/import** using the WordBatchService pattern: bounded structural scan, normalized category/audio lookup, full error collection, one transaction, and no partial import.
- [ ] **Step 4: Extend response projections** so user responses include playable audio metadata and blank count but never dictation answers before submission; result responses include all answers and explanations.
- [ ] **Step 5: Run** `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~PaperBatchServiceTests|FullyQualifiedName~OnlineQuizEndpointTests"` and commit.

### Task 5: Extend attempt grading and implement the wrong-question service

**Files:**
- Create: `server/TinyLang/Services/IWrongQuestionService.cs`
- Create: `server/TinyLang/Services/WrongQuestionService.cs`
- Modify: `server/TinyLang/Services/DependencyInjection.cs`
- Modify: `server/TinyLang/Services/PaperAttemptService.cs`
- Modify: `server/TinyLang/Endpoints/OnlineQuizEndpoints.cs`
- Modify: `server/TinyLang/Dtos/OnlineQuizDtos.cs`
- Create: `server/TinyLang.UnitTests/WrongQuestionServiceTests.cs`
- Modify: `server/TinyLang.UnitTests/PaperAttemptServiceTests.cs`

- [ ] **Step 1: Write failing grading tests** for all-correct, partially-correct, empty, case-insensitive, and internally-space-sensitive dictation answers.
- [ ] **Step 2: Extend save/submit grading** with `TextAnswers`; require exact blank count, award all-or-zero points, and treat unanswered questions as wrong.
- [ ] **Step 3: In the submit transaction, upsert wrong questions** for every incorrect or unanswered answer; preserve one row per user/question and update counts/timestamps.
- [ ] **Step 4: Implement wrong-question list/detail endpoints** with `Pending|Mastered` filtering, pagination, ownership checks, and answer-hidden projections.
- [ ] **Step 5: Implement single-question redo** with immediate grading, status transitions, redo count, concurrency handling, and answer-bearing result response.
- [ ] **Step 6: Run** `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj --filter "FullyQualifiedName~WrongQuestionServiceTests|FullyQualifiedName~PaperAttemptServiceTests"` and commit.

### Task 6: Upgrade the admin category, editor, and batch-import UI

**Files:**
- Create: `admin/src/pages/PaperCategories.jsx`
- Create: `admin/src/features/paperCategories/PaperCategoryFormDialog.jsx`
- Create: `admin/src/features/paperCategories/PaperCategoryDeleteDialog.jsx`
- Create: `admin/src/features/paperCategories/PaperCategoryTable.jsx`
- Create: `admin/src/services/paperCategoriesApi.js`
- Modify: `admin/src/router/index.jsx`
- Modify: `admin/src/router/navigation.js`
- Modify: `admin/src/services/papersApi.js`
- Modify: `admin/src/services/paperContracts.js`
- Modify: `admin/src/pages/Papers.jsx`
- Modify: `admin/src/pages/PaperEditor.jsx`
- Modify: `admin/src/constants/paperStatus.js`
- Create: `admin/src/pages/PaperBatchImport.jsx`
- Create: `admin/src/features/papers/paperBatchFile.js`
- Test: `admin/src/pages/PaperCategories.test.jsx`, `admin/src/pages/PaperBatchImport.test.jsx`, `admin/src/pages/PaperEditor.test.jsx`, `admin/src/services/paperCategoriesApi.test.js`

- [ ] **Step 1: Add failing tests** for category CRUD states, category filtering, dictation editor fields, audio picker restrictions, and atomic batch-import messaging.
- [ ] **Step 2: Implement category API/page** using the existing article/video category patterns.
- [ ] **Step 3: Replace tag contracts and UI** with category IDs and multi-select category control; remove tag navigation, filters, and components from the paper flow.
- [ ] **Step 4: Add the dictation question editor** with the existing audio-resource picker, ordered blank controls, and preview without answers.
- [ ] **Step 5: Add batch JSON file reading, example download, validation preview, error table, and draft-only import confirmation.**
- [ ] **Step 6: Run** `pnpm --dir admin test -- --run`, `pnpm --dir admin run lint`, and `pnpm --dir admin exec tsc -b`; commit the admin checkpoint.

### Task 7: Upgrade the consumer app and profile wrong-question modal

**Files:**
- Modify: `app/src/features/papers/paperTypes.ts`
- Modify: `app/src/features/papers/paperApi.ts`
- Modify: `app/src/features/papers/paperUtils.ts`
- Modify: `app/src/features/papers/PaperFilters.tsx`
- Modify: `app/src/pages/PapersPage.tsx`
- Modify: `app/src/pages/PaperDetailPage.tsx`
- Modify: `app/src/features/papers/PaperQuestion.tsx`
- Modify: `app/src/features/papers/PaperAttemptResult.tsx`
- Modify: `app/src/features/papers/usePaperAttemptAnswers.ts`
- Create: `app/src/features/papers/wrongQuestionTypes.ts`
- Create: `app/src/features/papers/wrongQuestionApi.ts`
- Create: `app/src/features/papers/WrongQuestionPanel.tsx`
- Create: `app/src/features/papers/WrongQuestionRedoDialog.tsx`
- Modify: `app/src/pages/ProfilePage.tsx`
- Test: corresponding `*.test.tsx` and `*.test.ts` files for filters, dictation input, result rendering, API normalization, profile collapse, and modal behavior

- [ ] **Step 1: Add failing tests** for category filtering, dictation `answers` arrays, saved-answer restoration, audio rendering, and answer-hidden wrong-question projections.
- [ ] **Step 2: Extend paper types/API normalization** for categories, `Dictation`, audio metadata, blank counts, `answers`, and wrong-question endpoints.
- [ ] **Step 3: Implement the dictation question UI** with native audio playback, labeled inputs, autosave integration, exact array ordering, keyboard support, and narrow-screen layout.
- [ ] **Step 4: Update result rendering** to show each dictation answer, correct answer, explanation, score, and audio from server-persisted results.
- [ ] **Step 5: Add the default-collapsed “在线测试” profile section** with counts and a modal wrong-question panel; keep large lists out of the profile page DOM until opened.
- [ ] **Step 6: Add single-question redo dialog** with immediate result, retry state, status refresh, ESC/backdrop close, focus handling, and reduced-motion-safe transitions.
- [ ] **Step 7: Run** `pnpm --dir app test -- --run`, `pnpm --dir app run lint`, `pnpm --dir app exec tsc -b`, and `pnpm --dir app run build`; commit the app checkpoint.

### Task 8: Cross-module regression verification and documentation

**Files:**
- Modify only if needed: `server/TinyLang.UnitTests/*`, `admin/src/**/*.test.*`, `app/src/**/*.test.*`
- Do not commit: `docs/`

- [ ] **Step 1: Run server tests** with `dotnet test server/TinyLang.UnitTests/TinyLang.UnitTests.csproj`.
- [ ] **Step 2: Run admin tests/lint/typecheck/build** with `pnpm --dir admin test -- --run`, `pnpm --dir admin run lint`, `pnpm --dir admin exec tsc -b`, and `pnpm --dir admin run build`.
- [ ] **Step 3: Run app tests/lint/typecheck/build** with `pnpm --dir app test -- --run`, `pnpm --dir app run lint`, `pnpm --dir app exec tsc -b`, and `pnpm --dir app run build`.
- [ ] **Step 4: Run repository hygiene checks** with `git diff --check` and confirm `git status --short` contains no generated build artifacts and no staged `docs/` files.
- [ ] **Step 5: Manually verify** category CRUD, batch all-or-nothing import, dictation with two blanks, unanswered-question capture, pending/mastered transitions, and profile modal behavior.

## Acceptance Criteria

- Existing tags no longer appear in API contracts or either frontend.
- Administrators can CRUD flat paper categories; papers can have zero or multiple active categories.
- A dictation question references one ready audio resource, has one or more ordered blanks, and awards points only when every blank is correct.
- Batch JSON validates every paper, category, audio reference, question, and blank before one transactional draft import.
- Submitted attempts automatically record both wrong and unanswered questions exactly once per user/question.
- Wrong questions support pending/mastered filtering and single-question redo with the specified state transitions.
- The profile page keeps the online-test area collapsed by default and renders the wrong-question list only inside the opened modal.
- Server, admin, and app tests, lint, type checks, builds, and `git diff --check` pass.
