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

shadcn/ui 配置位于 `components.json`，使用 JavaScript、Radix + Nova、Lucide 和 `@/` alias。新增组件时使用冻结的 CLI 主线并审查生成依赖，例如：

```powershell
pnpm --dir admin dlx shadcn@4.16.0 add <component>
```

## 主题

界面支持 `light`、`dark` 和 `system`，默认跟随系统。用户显式选择保存在 `tinylang.admin.theme`，仅包含非敏感的界面偏好。`index.html` 会在 React 首次绘制前应用保存或系统主题，避免刷新时出现错误主题首帧。

语义颜色和圆角统一定义在 `src/styles/global.css`。页面和组件应消费主题 token，不散落具体色值或新增平行主题体系。

## API 配置

当前基础设施不调用后端 API，也未建立 RTK Query base API。首个真实认证或管理接口任务应根据 `server/` 的 endpoint、DTO、授权策略和 Problem Details 契约建立单一 API 边界。

只有可公开的浏览器配置可以使用 `VITE_` 环境变量；所有 `VITE_` 值都会进入客户端 bundle，不能存放密钥、JWT 或其他敏感信息。
