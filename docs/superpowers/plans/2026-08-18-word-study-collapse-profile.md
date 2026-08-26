# 单词背诵与个人主页内容收纳实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task with review checkpoints.

**Goal:** 让单词背诵卡片默认隐藏释义和例句，并将个人主页的单词学习、账户安全、收藏本和停止复习内容按需收纳。

**Architecture:** 使用原生 `<details>/<summary>` 实现背诵卡片和主页顶层模块折叠。收藏本与停止复习保留现有业务面板，通过 `modal` 属性决定是否渲染 DaisyUI 弹窗，主页只负责开关和布局，不接管列表业务状态。

**Tech Stack:** React、TypeScript、Tailwind/DaisyUI、Lucide React、Vitest、Testing Library。

---

### Task 1: 锁定背诵卡片折叠行为

**Files:**
- Modify: `app/src/features/wordStudy/WordMemorizationCard.tsx`
- Create: `app/src/features/wordStudy/WordMemorizationCard.test.tsx`

- [ ] **Step 1: 编写失败测试**

渲染包含两个词义、一个例句和一个无例句词义的卡片，验证释义和例句默认不可见；点击对应 `<summary>` 后验证释义、用法说明、句子、翻译和音频按钮可见；无例句词义不渲染例句入口。

```tsx
expect(screen.queryByText("definition")).not.toBeVisible();
expect(screen.getByText("例句（1）")).toBeInTheDocument();
expect(screen.queryByText("A sentence")).not.toBeVisible();
```

- [ ] **Step 2: 运行测试确认失败**

```bash
npm --prefix app test -- --run src/features/wordStudy/WordMemorizationCard.test.tsx
```

预期：测试文件不存在或默认可见断言失败。

- [ ] **Step 3: 实现折叠结构**

每个 sense 外层使用默认关闭的 `<details>`，`<summary>` 显示词性和“释义”；释义与用法说明放入 body。仅当 `sense.examples.length > 0` 时渲染第二层 `<details>`，摘要显示 `例句（${sense.examples.length}）`，内部保留现有句子、翻译和 `AudioPlaybackButton`。不添加 `open` 属性，不修改音频按钮参数或学习提交逻辑。

- [ ] **Step 4: 运行测试确认通过**

重复 Step 2 命令，预期全部通过。

### Task 2: 为收藏本和停止复习面板增加弹窗承载能力

**Files:**
- Modify: `app/src/features/wordStudy/WordFavoritesPanel.tsx`
- Modify: `app/src/features/wordStudy/WordReviewExclusionsPanel.tsx`
- Modify: `app/src/features/wordStudy/WordFavoritesPanel.test.tsx`
- Modify: `app/src/features/wordStudy/WordReviewExclusionsPanel.test.tsx`

- [ ] **Step 1: 扩展参数并编写失败测试**

两个面板增加可选 `modal?: boolean` 与 `onClose?: () => void`，默认行为不变。新增测试渲染 `modal` 模式，验证 dialog 名称和关闭按钮调用 `onClose`；停止复习面板的恢复确认 dialog 仍能正常工作。

```tsx
render(<WordFavoritesPanel modal onClose={onClose} />);
expect(screen.getByRole("dialog", { name: "收藏本" })).toBeInTheDocument();
await user.click(screen.getByRole("button", { name: "关闭收藏本" }));
expect(onClose).toHaveBeenCalled();
```

- [ ] **Step 2: 运行测试确认失败**

```bash
npm --prefix app test -- --run src/features/wordStudy/WordFavoritesPanel.test.tsx src/features/wordStudy/WordReviewExclusionsPanel.test.tsx
```

- [ ] **Step 3: 实现弹窗模式**

`modal` 为真时，面板外层使用 `modal modal-open`、`role="dialog"`、`aria-modal="true"` 和唯一标题 ID，内容盒使用 `w-11/12 max-w-3xl`。增加带 Lucide `X` 图标的关闭按钮和可点击 backdrop，均调用 `onClose`。保留现有列表、分页、错误处理和恢复/取消收藏逻辑；关闭回调为空时安全忽略。

- [ ] **Step 4: 运行面板测试确认通过**

重复 Step 2 命令，预期现有和新增测试全部通过。

### Task 3: 重组个人主页折叠和入口

**Files:**
- Modify: `app/src/pages/ProfilePage.tsx`
- Modify: `app/src/pages/ProfilePage.test.tsx`

- [ ] **Step 1: 编写失败测试**

验证主页初始状态中“单词学习”和“账户安全”两个 `<details>` 没有 `open` 属性；点击 summary 后能看到对应内容；点击“打开收藏本”和“打开停止复习”后分别出现对应 dialog，关闭后 dialog 被卸载。

```tsx
expect(screen.getByRole("group", { name: "单词学习" })).not.toHaveAttribute("open");
expect(screen.getByRole("group", { name: "账户安全" })).not.toHaveAttribute("open");
expect(screen.queryByRole("dialog", { name: "收藏本" })).not.toBeInTheDocument();
```

- [ ] **Step 2: 运行 ProfilePage 测试确认失败**

```bash
npm --prefix app test -- --run src/pages/ProfilePage.test.tsx
```

- [ ] **Step 3: 实现主页布局**

增加 `favoritesOpen` 和 `exclusionsOpen` 状态，将现有单词统计、学习设置和两个入口放入默认关闭的单词学习 `<details>`；将 `AccountSecurityPanel` 放入默认关闭的账户安全 `<details>`。入口按钮使用 `BookHeart` 与 `ListChecks` 图标和清晰的中文名称。仅在对应状态为真时挂载带 `modal` 的列表面板，关闭时卸载并停止其查询。

- [ ] **Step 4: 运行 ProfilePage 测试确认通过**

重复 Step 2 命令，预期所有 ProfilePage 测试通过。

### Task 4: 统一样式与可访问性

**Files:**
- Modify: `app/src/features/wordStudy/WordMemorizationCard.tsx`
- Modify: `app/src/pages/ProfilePage.tsx`
- Modify: `app/src/features/wordStudy/WordFavoritesPanel.tsx`
- Modify: `app/src/features/wordStudy/WordReviewExclusionsPanel.tsx`

- [ ] **Step 1: 检查窄屏布局**

确保 summary、词性、例句数量和列表文本可换行；弹窗使用 `w-11/12 max-w-3xl`，操作按钮在窄屏允许换行。

- [ ] **Step 2: 检查可访问性**

确认折叠入口均为 `<summary>`；dialog 有唯一标题 ID、`role="dialog"`、`aria-modal="true"`；图标 `aria-hidden="true"`；关闭按钮有中文 `aria-label`。

- [ ] **Step 3: 运行静态检查**

```bash
npm --prefix app run lint
npm --prefix app run build
```

预期 lint 无错误且生产构建成功。

### Task 5: 完整回归验证

**Files:**
- Test: `app/src/features/wordStudy/WordMemorizationCard.test.tsx`
- Test: `app/src/features/wordStudy/WordFavoritesPanel.test.tsx`
- Test: `app/src/features/wordStudy/WordReviewExclusionsPanel.test.tsx`
- Test: `app/src/pages/ProfilePage.test.tsx`

- [ ] **Step 1: 运行相关测试**

```bash
npm --prefix app test -- --run src/features/wordStudy/WordMemorizationCard.test.tsx src/features/wordStudy/WordFavoritesPanel.test.tsx src/features/wordStudy/WordReviewExclusionsPanel.test.tsx src/pages/ProfilePage.test.tsx
```

- [ ] **Step 2: 运行完整前端测试**

```bash
npm --prefix app test -- --run
```

预期所有测试通过，没有未处理的 act、console error 或 TypeScript 错误。

- [ ] **Step 3: 检查最终差异**

```bash
git diff --check
git status --short
```

确认只包含本次 `/app` 修改；不提交 `docs/` 和 `.superpowers/`。

