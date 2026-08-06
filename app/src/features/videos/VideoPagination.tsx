import { Fragment } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

interface VideoPaginationProps {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

function getPages(page: number, totalPages: number) {
  if (totalPages <= 7)
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  return [...new Set([1, page - 1, page, page + 1, totalPages])]
    .filter((value) => value > 0 && value <= totalPages)
    .sort((a, b) => a - b);
}

/** Renders compact video pagination with a stable current-page affordance. */
export default function VideoPagination({
  page,
  totalPages,
  onPageChange,
}: VideoPaginationProps) {
  if (totalPages <= 1) return null;
  const pages = getPages(page, totalPages);
  return (
    <nav aria-label="视频分页" className="flex justify-center">
      <div className="join max-w-full">
        <button
          aria-label="上一页"
          className="btn btn-square btn-sm join-item sm:btn-md"
          disabled={page <= 1}
          type="button"
          onClick={() => onPageChange(page - 1)}
        >
          <ChevronLeft aria-hidden="true" className="size-4" />
        </button>
        {pages.map((item, index) => {
          const previous = pages[index - 1];
          return (
            <Fragment key={item}>
              {previous && item - previous > 1 ? (
                <span
                  aria-hidden="true"
                  className="btn btn-square btn-sm join-item pointer-events-none sm:btn-md"
                >
                  ...
                </span>
              ) : null}
              <button
                aria-current={item === page ? "page" : undefined}
                aria-label={`第 ${item} 页`}
                className={cn(
                  "btn btn-square btn-sm join-item sm:btn-md",
                  item === page && "btn-primary",
                )}
                type="button"
                onClick={() => onPageChange(item)}
              >
                {item}
              </button>
            </Fragment>
          );
        })}
        <button
          aria-label="下一页"
          className="btn btn-square btn-sm join-item sm:btn-md"
          disabled={page >= totalPages}
          type="button"
          onClick={() => onPageChange(page + 1)}
        >
          <ChevronRight aria-hidden="true" className="size-4" />
        </button>
      </div>
    </nav>
  );
}
