import { useEffect, useState } from "react";
import { UserRound } from "lucide-react";
import { isSafeAvatarUrl } from "@/features/profile/profileUtils";
import { cn } from "@/lib/utils";

interface UserAvatarProps {
  url: string | null | undefined;
  name: string;
  className?: string;
}

/** Renders validated remote avatars with a deterministic non-sensitive fallback. */
export default function UserAvatar({ url, name, className }: UserAvatarProps) {
  const [failed, setFailed] = useState(false);
  const safeUrl = isSafeAvatarUrl(url) ? url : null;

  useEffect(() => {
    setFailed(false);
  }, [url]);

  return (
    <span
      className={cn(
        "grid size-10 shrink-0 place-items-center overflow-hidden rounded-full bg-base-200 text-base-content",
        className,
      )}
    >
      {safeUrl && !failed ? (
        <img
          alt={`${name}的头像`}
          className="size-full object-cover"
          referrerPolicy="no-referrer"
          src={safeUrl}
          onError={() => setFailed(true)}
        />
      ) : (
        <UserRound aria-label="默认头像" className="size-1/2" role="img" />
      )}
    </span>
  );
}
