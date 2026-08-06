import { useEffect, useState } from "react";
import { ImageOff } from "lucide-react";
import { isSafeHttpUrl } from "@/features/articles/articleUtils";
import { cn } from "@/lib/utils";

interface ArticleCoverProps {
  url: string | null | undefined;
  alt: string;
  className?: string;
  eager?: boolean;
}

/** Displays validated article covers with a fixed, non-retrying fallback. */
export default function ArticleCover({
  url,
  alt,
  className,
  eager = false,
}: ArticleCoverProps) {
  const [failed, setFailed] = useState(false);
  const safeUrl = isSafeHttpUrl(url) ? url : null;

  useEffect(() => {
    setFailed(false);
  }, [url]);

  return (
    <div
      className={cn(
        "grid aspect-video w-full place-items-center overflow-hidden bg-base-200 text-base-content/45",
        className,
      )}
    >
      {safeUrl && !failed ? (
        <img
          alt={alt}
          className="size-full object-cover"
          decoding="async"
          loading={eager ? "eager" : "lazy"}
          referrerPolicy="no-referrer"
          src={safeUrl}
          onError={() => setFailed(true)}
        />
      ) : (
        <ImageOff aria-label="暂无文章封面" className="size-9" role="img" />
      )}
    </div>
  );
}
