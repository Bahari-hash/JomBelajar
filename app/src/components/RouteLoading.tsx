/** Stable route fallback that avoids shifting the application shell. */
export default function RouteLoading() {
  return (
    <div aria-label="页面加载中" className="space-y-5 py-8" role="status">
      <div className="skeleton h-8 w-44" />
      <div className="skeleton h-4 w-full max-w-xl" />
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="skeleton h-32 w-full" />
        <div className="skeleton h-32 w-full" />
      </div>
      <span className="sr-only">页面加载中</span>
    </div>
  );
}
