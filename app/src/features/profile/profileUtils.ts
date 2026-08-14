import type {
  CurrentUserProfile,
  UpdateProfileRequest,
  UserRole,
} from "@/features/auth/types";

export function isSafeAvatarUrl(value: string | null | undefined) {
  if (!value) {
    return false;
  }
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

export function getRoleLabel(role: UserRole) {
  return role === "Admin" ? "管理员" : "学习者";
}

export interface ProfileDraft {
  nickname: string;
  avatarMediaResourceId: string | null;
  avatarUrl: string | null;
  bio: string;
}

export function formatProfileDate(value: string) {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "暂无";
  }
  return new Intl.DateTimeFormat("zh-CN", {
    year: "numeric",
    month: "long",
    day: "numeric",
  }).format(date);
}

export function profileToDraft(
  profile: CurrentUserProfile,
): ProfileDraft {
  return {
    nickname: profile.nickname ?? "",
    avatarMediaResourceId: null,
    avatarUrl: profile.avatarUrl,
    bio: profile.bio ?? "",
  };
}

export function normalizeProfileDraft(
  draft: ProfileDraft,
): UpdateProfileRequest {
  const optional = (value: string | null) => value?.trim() || null;
  return {
    nickname: optional(draft.nickname),
    avatarMediaResourceId: draft.avatarMediaResourceId,
    bio: optional(draft.bio),
  };
}
