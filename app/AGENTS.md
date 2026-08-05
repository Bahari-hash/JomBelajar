# TinyLang Consumer App Guide

- 本目录使用 React 19、TypeScript strict、Vite 8、Tailwind CSS 4 和 DaisyUI 5。
- 页面放在 `src/pages/`，路由集中在 `src/router/`，全局布局放在 `src/layouts/`，共享组件与 hook 分别放在 `src/components/` 和 `src/hooks/`。
- HTTP 请求必须通过 `src/services/` 中的 Axios client；禁止使用 `fetch`、硬编码生产地址或在组件中拼接 API base URL。
- 只使用 Lucide 图标和 DaisyUI/Tailwind 主题 token，不引入第二套图标或组件库，不散落硬编码主题色。
- 共享组件、hook、API 封装和公共工具应有简洁的模块注释或 JSDoc；局部渲染不添加机械注释。
- 页面必须处理键盘、焦点、窄屏与 reduced-motion；不得使用 `dangerouslySetInnerHTML` 或伪造业务数据。
- 行为变更使用 Vitest、Testing Library 和 jsdom 覆盖用户可观察结果；交付前运行 `pnpm lint`、`pnpm test` 和 `pnpm build`。
