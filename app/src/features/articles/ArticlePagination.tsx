import { ChevronLeft, ChevronRight } from "lucide-react";
import { Fragment } from "react";
import { cn } from "@/lib/utils";

interface ArticlePaginationProps {
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

function getVisiblePages(page: number, totalPages: number) {
  if (totalPages <= 7) {
    return Array.from({ length: totalPages }, (_, index) => index + 1);
  }
  return [...new Set([1, page - 1, page, page + 1, totalPages])]
    .filter((value) => value >= 1 && value <= totalPages)
    .sort((left, right) => left - right);
}

/** Renders compact, keyboard-accessible pagination at narrow widths. */
export default function ArticlePagination({
  page,
  totalPages,
  onPageChange,
}: ArticlePaginationProps) {
  if (totalPages <= 1) {
    return null;
  }
  const visiblePages = getVisiblePages(page, totalPages);

  return (
    <nav aria-label="文章分页" className="flex justify-center">
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
        {visiblePages.map((visiblePage, index) => {
          const previousPage = visiblePages[index - 1];
          return (
            <Fragment key={visiblePage}>
              {previousPage && visiblePage - previousPage > 1 ? (
                <span
                  aria-hidden="true"
                  className="btn btn-square btn-sm join-item pointer-events-none sm:btn-md"
                >
                  ...
                </span>
              ) : null}
              <button
                aria-current={visiblePage === page ? "page" : undefined}
                aria-label={`第 ${visiblePage} 页`}
                className={cn(
                  "btn btn-square btn-sm join-item sm:btn-md",
                  visiblePage === page && "btn-primary",
                )}
                type="button"
                onClick={() => onPageChange(visiblePage)}
              >
                {visiblePage}
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
