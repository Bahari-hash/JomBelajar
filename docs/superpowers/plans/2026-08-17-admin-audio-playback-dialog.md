# 管理端音频试听弹窗与轮询移除 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 停止音频管理页面每 5 秒发送列表请求，并将页面内联试听区改为主题适配的原生音频播放器弹窗。

**Architecture:** 保持修改集中在 `AudioLibrary`，不新增播放器组件或依赖。列表查询移除 RTK Query polling；试听状态改为“目标音频 + loading/data/error”，播放请求由可取消的 RTK mutation request 管理，弹窗内直接渲染 `<audio controls autoPlay>`。

**Tech Stack:** React 19、React Router 7、Redux Toolkit Query、Radix Dialog、Tailwind CSS 4、Vitest、Testing Library。

**提交约定：** 实现提交只包含 `admin/` 代码和测试，禁止提交 `docs/` 与 `word.md`。使用显式 `git add`，禁止 `git add .` 或 `git add -A`。

---

## 文件结构与职责

- `admin/src/pages/AudioLibrary.jsx`：移除列表轮询；管理试听弹窗、可取消播放地址请求、错误重试和原生播放器主题样式。
- `admin/src/pages/AudioLibrary.test.jsx`：覆盖无轮询、弹窗加载/成功/失败/关闭行为，并继续承担音频库页面集成测试。

不创建独立播放器组件，不修改 `audioApi.js`、后端或全局主题文件。

---

## Task 1：移除音频列表心跳轮询

**Files:**

- Modify: `admin/src/pages/AudioLibrary.jsx`
- Modify: `admin/src/pages/AudioLibrary.test.jsx`

- [ ] **Step 1：编写页面静置不重复请求的失败测试。**

在 `AudioLibrary.test.jsx` 增加测试。使用 fake timers 渲染页面，等待首次列表请求完成，推进超过原轮询周期后断言请求次数不变：

```jsx
it("does not poll the audio list while the page is idle", async () => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  try {
    const requestMock = installAudioServer();
    renderAppAt("/audio");

    await screen.findByRole("heading", { level: 1, name: "音频资源" });
    const listRequestCount = () =>
      requestMock.mock.calls.filter(([config]) =>
        config.url.startsWith("/admin/audio?"),
      ).length;
    const initialCount = listRequestCount();

    await vi.advanceTimersByTimeAsync(6000);

    expect(listRequestCount()).toBe(initialCount);
  } finally {
    vi.useRealTimers();
  }
});
```

- [ ] **Step 2：运行新测试确认 RED。**

Run:

```bash
pnpm --dir admin exec vitest run src/pages/AudioLibrary.test.jsx
```

Expected: FAIL，推进 6000 ms 后 `/admin/audio?...` 请求次数增加，证明现有 `pollingInterval: 5000` 被测试捕获。

- [ ] **Step 3：删除 RTK Query polling 配置。**

将：

```jsx
const { data, error, isLoading, isFetching, refetch } =
  useGetAdminAudioResourcesQuery(filters, { pollingInterval: 5000 });
```

改为：

```jsx
const { data, error, isLoading, isFetching, refetch } =
  useGetAdminAudioResourcesQuery(filters);
```

不要删除现有手动刷新按钮，也不要修改上传、重处理、删除后的 `refetch()`。

- [ ] **Step 4：运行页面测试确认 GREEN。**

Run:

```bash
pnpm --dir admin exec vitest run src/pages/AudioLibrary.test.jsx
```

Expected: PASS，页面首次加载和显式刷新行为保持不变，静置不再重复请求。

- [ ] **Step 5：提交 Task 1。**

```bash
git add admin/src/pages/AudioLibrary.jsx admin/src/pages/AudioLibrary.test.jsx
git commit -m "fix(admin): stop polling audio library"
```

---

## Task 2：将试听改为原生播放器弹窗

**Files:**

- Modify: `admin/src/pages/AudioLibrary.jsx`
- Modify: `admin/src/pages/AudioLibrary.test.jsx`

- [ ] **Step 1：将现有试听测试改为弹窗 RED 测试。**

替换当前“页面内渲染播放器”测试，明确以下成功行为：

```jsx
it("opens a themed native player dialog and requests playback", async () => {
  const requestMock = installAudioServer();
  const user = userEvent.setup();
  renderAppAt("/audio");

  await user.click(
    await screen.findByRole("button", { name: "试听 lesson.mp3" }),
  );

  expect(
    screen.getByRole("heading", { name: "试听音频" }),
  ).toBeVisible();
  const player = await screen.findByLabelText("正在试听 lesson.mp3");
  expect(player).toHaveAttribute("controls");
  expect(player).toHaveAttribute("autoplay");
  expect(player).toHaveAttribute(
    "src",
    "https://media.example.test/lesson.mp3",
  );
  expect(player).toHaveStyle({ accentColor: "var(--primary)" });
  expect(player).toHaveClass(
    "[color-scheme:light]",
    "dark:[color-scheme:dark]",
  );
  expect(
    requestMock.mock.calls.some(
      ([config]) =>
        config.url === `/audio/${READY_ID}/playback` &&
        config.method === "POST",
    ),
  ).toBe(true);
});
```

点击后、请求返回前必须先出现 loading 状态。为此在测试中创建 deferred playback response：

```jsx
it("opens the playback dialog immediately while the URL is loading", async () => {
  let resolvePlayback;
  installAudioServer({
    request: (config) =>
      config.url.endsWith("/playback")
        ? new Promise((resolve) => {
            resolvePlayback = resolve;
          })
        : null,
  });
  const user = userEvent.setup();
  renderAppAt("/audio");

  await user.click(
    await screen.findByRole("button", { name: "试听 lesson.mp3" }),
  );

  expect(screen.getByLabelText("正在加载 lesson.mp3")).toBeVisible();
  expect(screen.queryByRole("audio")).not.toBeInTheDocument();

  resolvePlayback(
    axiosResponse({
      url: "https://media.example.test/lesson.mp3",
      expiresAt: null,
      durationSeconds: 42.5,
    }),
  );
});
```

不要依赖 `queryByRole("audio")` 作为最终播放器查询；成功测试继续使用明确的 `aria-label`。

- [ ] **Step 2：增加失败、重试和关闭竞态 RED 测试。**

失败后错误必须出现在弹窗内，第二次点击“重试”重新请求：

```jsx
it("shows playback errors in the dialog and retries", async () => {
  const requestMock = installAudioServer();
  requestMock
    .mockRejectedValueOnce(
      axiosHttpError({ detail: "Playback unavailable." }, 503),
    )
    .mockResolvedValueOnce(
      axiosResponse({
        url: "https://media.example.test/lesson.mp3",
        expiresAt: null,
        durationSeconds: 42.5,
      }),
    );
  const user = userEvent.setup();
  renderAppAt("/audio");

  await user.click(
    await screen.findByRole("button", { name: "试听 lesson.mp3" }),
  );
  expect(await screen.findByText("Playback unavailable.")).toBeVisible();

  await user.click(screen.getByRole("button", { name: "重试" }));

  expect(await screen.findByLabelText("正在试听 lesson.mp3")).toBeVisible();
});
```

实现测试时不要直接对全局 mock 使用无条件 `mockRejectedValueOnce`，否则可能拦截列表或 capability 请求；应在 `installAudioServer({ request })` 中只统计并控制 `/playback` 请求。

再增加关闭竞态测试：播放请求保持 pending，关闭弹窗后再释放响应，断言弹窗和播放器都不会重新出现。

```jsx
expect(screen.queryByRole("heading", { name: "试听音频" })).toBeNull();
expect(screen.queryByLabelText("正在试听 lesson.mp3")).toBeNull();
```

- [ ] **Step 3：运行试听测试确认 RED。**

Run:

```bash
pnpm --dir admin exec vitest run src/pages/AudioLibrary.test.jsx
```

Expected: FAIL，当前实现仍在页面内渲染播放器，没有弹窗 loading、错误重试、自动播放和主题属性。

- [ ] **Step 4：建立可取消的试听弹窗状态。**

将 React import 扩展为：

```jsx
import { useEffect, useMemo, useRef, useState } from "react";
```

用以下状态替换 `activePlayback`：

```jsx
const playbackRequestRef = useRef(null);
const [playbackDialog, setPlaybackDialog] = useState(null);
```

状态形状固定为：

```js
{
  audio: { id, name },
  loading: true,
  playback: null,
  error: null,
}
```

实现统一请求函数：

```jsx
const requestPlayback = async (audio) => {
  playbackRequestRef.current?.abort?.();
  setPlaybackDialog({
    audio: { id: audio.id, name: audio.name },
    loading: true,
    playback: null,
    error: null,
  });
  setPendingAction({ action: "play", audioResourceId: audio.id });

  const request = getPlayback(audio.id);
  playbackRequestRef.current = request;
  try {
    const playback = await request.unwrap();
    if (playbackRequestRef.current !== request) return;
    setPlaybackDialog({
      audio: { id: audio.id, name: audio.name },
      loading: false,
      playback,
      error: null,
    });
  } catch (requestError) {
    if (playbackRequestRef.current !== request) return;
    setPlaybackDialog({
      audio: { id: audio.id, name: audio.name },
      loading: false,
      playback: null,
      error: requestError,
    });
  } finally {
    if (playbackRequestRef.current === request) {
      playbackRequestRef.current = null;
      setPendingAction(null);
    }
  }
};
```

`playAudio` 直接调用 `requestPlayback(audio)`，不再把播放错误写入页面级 `notice`。

关闭函数必须先使当前请求失效，再 abort，防止 catch 更新已关闭弹窗：

```jsx
const closePlayback = () => {
  const request = playbackRequestRef.current;
  playbackRequestRef.current = null;
  request?.abort?.();
  setPendingAction((current) =>
    current?.action === "play" ? null : current,
  );
  setPlaybackDialog(null);
};
```

- [ ] **Step 5：用 Dialog 替换页面内联播放器。**

删除 `activePlayback` 对应的 `<section>`，在其他页面级 Dialog 附近加入：

```jsx
<Dialog
  open={Boolean(playbackDialog)}
  onOpenChange={(open) => !open && closePlayback()}
>
  <DialogContent className="sm:max-w-lg">
    <DialogHeader>
      <DialogTitle>试听音频</DialogTitle>
      <DialogDescription className="truncate" title={playbackDialog?.audio.name}>
        {playbackDialog?.audio.name}
      </DialogDescription>
    </DialogHeader>

    {playbackDialog?.loading ? (
      <div
        className="space-y-3 py-2"
        role="status"
        aria-label={`正在加载 ${playbackDialog.audio.name}`}
      >
        <Skeleton className="h-12 w-full" />
      </div>
    ) : playbackDialog?.error ? (
      <Alert variant="destructive">
        <AlertDescription className="flex flex-wrap items-center justify-between gap-3">
          <span>
            {getErrorMessage(playbackDialog.error, "音频暂不可用。")}
          </span>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => requestPlayback(playbackDialog.audio)}
          >
            <RotateCcw aria-hidden="true" />
            重试
          </Button>
        </AlertDescription>
      </Alert>
    ) : playbackDialog?.playback ? (
      <div className="rounded-lg border bg-muted/40 p-3">
        <audio
          className="block h-12 w-full max-w-full [color-scheme:light] dark:[color-scheme:dark]"
          style={{ accentColor: "var(--primary)" }}
          controls
          autoPlay
          src={playbackDialog.playback.url}
          aria-label={`正在试听 ${playbackDialog.audio.name}`}
        />
      </div>
    ) : null}
  </DialogContent>
</Dialog>
```

不要使用 WebKit 私有音频伪元素，不要显示签名 URL、过期时间或“临时授权”等技术文案。

- [ ] **Step 6：运行 focused tests、lint 和 build。**

Run:

```bash
pnpm --dir admin exec vitest run src/pages/AudioLibrary.test.jsx
pnpm --dir admin lint
pnpm --dir admin build
```

Expected: PASS；原生播放器位于弹窗中，错误可重试，关闭后迟到响应不会恢复播放器。

- [ ] **Step 7：提交 Task 2。**

```bash
git add admin/src/pages/AudioLibrary.jsx admin/src/pages/AudioLibrary.test.jsx
git commit -m "feat(admin): show audio playback dialog"
```

---

## Task 3：完整验证与提交边界检查

**Files:**

- Verify: `admin/src/pages/AudioLibrary.jsx`
- Verify: `admin/src/pages/AudioLibrary.test.jsx`
- Modify: 仅限验证发现的本功能缺陷

- [ ] **Step 1：扫描残留轮询和旧内联播放器。**

Run:

```bash
rg -n "pollingInterval|activePlayback|播放地址由服务端临时授权" admin/src/pages/AudioLibrary.jsx admin/src/pages/AudioLibrary.test.jsx
```

Expected: 无匹配。`refetch()`、`playbackDialog` 和 `requestPlayback` 应继续存在。

- [ ] **Step 2：运行完整管理端验证。**

Run:

```bash
pnpm --dir admin lint
pnpm --dir admin test
pnpm --dir admin build
```

Expected: 全部 exit code 0，现有上传、详情、重命名、重处理和删除行为无回归。

- [ ] **Step 3：检查工作区和提交边界。**

Run:

```bash
git diff --check
git status --short
git diff --cached --name-only
git log --format= --name-only HEAD~2..HEAD | rg '^(docs/|word\.md$)' || true
```

Expected: staging area 为空；`docs/` 和 `word.md` 可以保持用户原有未跟踪状态，但不出现在实现提交中。

- [ ] **Step 4：仅在验证修复产生新 diff 时提交。**

若 Task 3 发现并修复功能缺陷，只显式暂存 `admin/src/pages/AudioLibrary.jsx` 和 `admin/src/pages/AudioLibrary.test.jsx`，提交：

```bash
git add admin/src/pages/AudioLibrary.jsx admin/src/pages/AudioLibrary.test.jsx
git commit -m "test(admin): complete audio playback verification"
```

若没有新 diff，不创建空提交。

---

## 最终验收

1. 音频管理页面空闲超过 5 秒不会自动重新请求列表。
2. 手动刷新和业务操作后的主动刷新保持可用。
3. 点击试听立即打开居中弹窗，并在播放地址返回前显示 loading。
4. 播放成功后使用带 `controls` 和 `autoPlay` 的浏览器原生播放器。
5. 播放器外观使用项目主题 token，并通过 `accent-color`、`color-scheme` 适配亮暗主题。
6. 播放地址错误在弹窗内展示并可重试。
7. 关闭弹窗停止并移除播放器，迟到请求不会恢复弹窗。
8. 不新增前端依赖、播放器组件、后端改动或全局 CSS 私有覆盖。
9. 实现提交不包含 `docs/` 或 `word.md`。
