# TinyLang 管理后台

TinyLang 管理后台使用 React 19、JavaScript、Vite 8、React Router 7、Redux Toolkit、Tailwind CSS 4 和 shadcn/ui 构建。

## 本地命令

从仓库根目录执行：

```powershell
pnpm --dir admin install --frozen-lockfile
pnpm --dir admin dev
pnpm --dir admin lint
pnpm --dir admin test
pnpm --dir admin build
pnpm --dir admin preview
```

`test` 使用 Vitest 非 watch 模式运行；本地持续测试使用 `pnpm --dir admin test:watch`。

## 项目边界

- `src/main.jsx`：全局样式、StrictMode 和根组件挂载。
- `src/App.jsx`：Redux、主题、Tooltip 和 Router Provider 装配。
- `src/router/`：集中路由树和共享导航配置。
- `src/layouts/`：响应式管理员应用壳层。
- `src/pages/`：路由页面、404 和路由错误恢复界面。
- `src/components/ui/`：项目持有的 shadcn/ui 基础组件源码。
- `src/components/`、`src/hooks/`、`src/lib/`：共享组件、hook 和公共工具。
- `src/store/`：Redux store；只有真实跨页面状态出现时才增加 reducer。
- `src/services/`：统一 HTTP、认证会话、RTK Query 和后端契约映射。
- `src/features/`：按业务领域组织可复用的登录与用户管理能力。

shadcn/ui 配置位于 `components.json`，使用 JavaScript、Radix + Nova、Lucide 和 `@/` alias。新增组件时使用冻结的 CLI 主线并审查生成依赖，例如：

```powershell
pnpm --dir admin dlx shadcn@4.16.0 add <component>
```

## 主题

界面支持 `light`、`dark` 和 `system`，默认跟随系统。用户显式选择保存在 `tinylang.admin.theme`，仅包含非敏感的界面偏好。`index.html` 会在 React 首次绘制前应用保存或系统主题，避免刷新时出现错误主题首帧。

语义颜色和圆角统一定义在 `src/styles/global.css`。页面和组件应消费主题 token，不散落具体色值或新增平行主题体系。

## API 配置

浏览器请求默认使用同源 `/api`。开发服务器会把 `/api` 安全代理到 `https://localhost:7000`，本机需要信任 .NET development certificate。生产环境应由同源服务或网关转发 `/api`；若部署时确需其他公开 base URL，可通过 `VITE_API_BASE_URL` 覆盖。

只有可公开的浏览器配置可以使用 `VITE_` 环境变量；所有 `VITE_` 值都会进入客户端 bundle，不能存放密钥、JWT 或其他敏感信息。

所有浏览器 HTTP 请求通过单一 Axios client 执行。用户查询和管理操作使用单一 RTK Query cache；登录、刷新和退出复用同一 HTTP/Error 边界，但不会进入 Redux action 或 cache，避免 password 和 token 暴露在 Redux DevTools。

后端 JSON 枚举统一使用与 C# 成员同名的 PascalCase 字符串，例如 `Admin`、`Draft` 和 `CourseVideo`；数字枚举响应视为契约错误。Problem Details 中的稳定 `errorCode` 会由共享 Axios 错误边界保留，业务恢复应联合 HTTP status 与 `errorCode` 判断，不得匹配本地化 `detail`。

## 管理员会话

- 管理后台只接受后端 login/refresh 响应中的 Admin 角色，不解析 JWT 来推导权限。
- access token 仅保存在模块内存；轮换后的 refresh token 保存在当前标签页的 `sessionStorage`，关闭标签页后不形成长期登录。
- 页面刷新时先轮换 refresh token，再显示受保护内容；并发 401 共享同一次刷新且每个原请求最多重试一次。
- 退出请求无论成功或失败都会清理 token、认证状态和用户 API cache。
- `/users` 使用真实管理员分页、详情、封禁、解封、角色更新和会话撤销 endpoint，不包含 mock 用户或静态统计。

## 文章管理

内容管理使用以下受保护路由：

- `/articles`：文章列表、筛选和状态操作。
- `/articles/new`：创建文章草稿。
- `/articles/:articleId/edit`：编辑草稿及请求服务端规范预览。
- `/articles/:articleId/preview`：预览已保存文章。
- `/article-categories`：维护文章分类及其文章关联。

Markdown 源文始终由后端保存和渲染。`mdast-util-from-markdown` 只用于识别正文图片节点并维护已确认媒体 ID 映射，不作为客户端权威渲染器。规范 HTML 只允许通过 `ArticleHtmlPreview` 渲染。

图片上传遵循 TinyLang 的 presign、对象存储 PUT、confirm 流程。TinyLang 元数据请求复用认证 Axios transport；预签名绝对 URL 使用隔离的 Axios PUT，不携带 Bearer token、cookie 或 API client 默认配置。
