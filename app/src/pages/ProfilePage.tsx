import {
  ArrowLeft,
  CalendarDays,
  Mail,
  Save,
  Undo2,
  Upload,
} from "lucide-react";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import UserAvatar from "@/components/UserAvatar";
import {
  clearFieldError,
  getFieldError,
  toApiRequestError,
  type FieldErrors,
} from "@/features/auth/authErrors";
import { uploadAvatar } from "@/features/profile/profileApi";
import AccountSecurityPanel from "@/features/profile/AccountSecurityPanel";
import WordStudySettingsPanel from "@/features/wordStudy/WordStudySettingsPanel";
import WordStudySummaryPanel from "@/features/wordStudy/WordStudySummaryPanel";
import WordFavoritesPanel from "@/features/wordStudy/WordFavoritesPanel";
import WordReviewExclusionsPanel from "@/features/wordStudy/WordReviewExclusionsPanel";
import {
  formatProfileDate,
  getRoleLabel,
  normalizeProfileDraft,
  profileToDraft,
  type ProfileDraft,
} from "@/features/profile/profileUtils";
import { useAuth } from "@/hooks/useAuth";
import { useDocumentTitle } from "@/hooks/useDocumentTitle";

const emptyDraft: ProfileDraft = {
  nickname: "",
  avatarMediaResourceId: null,
  avatarUrl: null,
  bio: "",
};

function validate(draft: ProfileDraft): FieldErrors {
  const errors: FieldErrors = {};
  if ((draft.nickname?.length ?? 0) > 60) {
    errors.nickname = "昵称不能超过 60 个字符。";
  }
  if ((draft.bio?.length ?? 0) > 500) {
    errors.bio = "简介不能超过 500 个字符。";
  }
  return errors;
}

export default function ProfilePage() {
  useDocumentTitle("个人资料");
  const {
    profile,
    profileStatus,
    profileError,
    refreshProfile,
    updateProfile,
  } = useAuth();
  const [draft, setDraft] = useState<ProfileDraft>(() =>
    profile ? profileToDraft(profile) : emptyDraft,
  );
  const [dirty, setDirty] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);

  useEffect(() => {
    if (profile && !dirty) {
      setDraft(profileToDraft(profile));
    }
  }, [dirty, profile]);

  if (profileStatus === "loading" && !profile) {
    return (
      <div aria-label="个人资料加载中" className="space-y-5" role="status">
        <div className="skeleton h-9 w-40" />
        <div className="grid gap-6 lg:grid-cols-[20rem_1fr]">
          <div className="skeleton h-72 w-full" />
          <div className="skeleton h-96 w-full" />
        </div>
      </div>
    );
  }

  if (profileStatus === "error" && !profile) {
    return (
      <section className="mx-auto flex min-h-80 max-w-lg flex-col items-center justify-center text-center">
        <h1 className="text-2xl font-bold">资料暂时无法加载</h1>
        <p className="mt-3 text-base-content/70">{profileError}</p>
        <button
          className="btn btn-primary mt-6"
          type="button"
          onClick={() => void refreshProfile()}
        >
          重新加载
        </button>
      </section>
    );
  }

  if (!profile) {
    return null;
  }

  const displayName = profile.nickname || "未设置昵称";

  const handleChange = (field: "nickname" | "bio", value: string) => {
    setDraft({ ...draft, [field]: value });
    setFieldErrors((current) => clearFieldError(current, field));
    setDirty(true);
    setMessage(null);
  };

  const handleCancel = () => {
    setDraft(profileToDraft(profile));
    setDirty(false);
    setFieldErrors({});
    setMessage(null);
  };

  const handleAvatarChange = async (
    event: React.ChangeEvent<HTMLInputElement>,
  ) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) {
      return;
    }

    setUploadingAvatar(true);
    setUploadProgress(0);
    setFieldErrors({});
    setMessage(null);
    try {
      const resource = await uploadAvatar(file, setUploadProgress);
      setDraft((current) => ({
        ...current,
        avatarMediaResourceId: resource.id,
        avatarUrl: resource.url,
      }));
      setFieldErrors((current) =>
        clearFieldError(current, "avatarMediaResourceId"),
      );
      setDirty(true);
      setMessage("头像已上传，请保存资料以应用。");
    } catch (error) {
      const requestError = toApiRequestError(error, "头像上传失败，请重试。");
      setFieldErrors(requestError.fieldErrors);
      setMessage(requestError.message);
    } finally {
      setUploadingAvatar(false);
    }
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const localErrors = validate(draft);
    setFieldErrors(localErrors);
    setMessage(null);
    if (Object.keys(localErrors).length > 0) {
      return;
    }

    setSaving(true);
    try {
      const updated = await updateProfile(normalizeProfileDraft(draft));
      setDraft(profileToDraft(updated));
      setDirty(false);
      setMessage("个人资料已保存。");
    } catch (error) {
      const requestError = toApiRequestError(
        error,
        "资料保存失败，请检查后重试。",
      );
      setFieldErrors(requestError.fieldErrors);
      setMessage(requestError.message);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-7">
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">个人资料</h1>
          <p className="mt-2 text-base-content/70">
            管理公开昵称、头像和个人简介。
          </p>
        </div>
        <Link className="btn btn-ghost btn-sm" to="/">
          <ArrowLeft aria-hidden="true" className="size-4" />
          返回学习首页
        </Link>
      </header>

      <div className="grid items-start gap-6 lg:grid-cols-[20rem_minmax(0,1fr)]">
        <aside className="rounded-xl border border-base-300 bg-base-100/90 p-6 shadow-sm backdrop-blur">
          <UserAvatar
            className="size-20"
            name={displayName}
            url={draft.avatarUrl}
          />
          <h2 className="mt-4 wrap-break-word text-xl font-semibold">
            {displayName}
          </h2>
          <span className="badge badge-secondary mt-2">
            {getRoleLabel(profile.role)}
          </span>
          <p className="mt-4 wrap-break-word text-sm leading-6 text-base-content/70">
            {profile.bio || "尚未填写个人简介。"}
          </p>
          <dl className="mt-6 space-y-4 text-sm">
            <div>
              <dt className="flex items-center gap-2 text-base-content/60">
                <Mail aria-hidden="true" className="size-4" />
                邮箱
              </dt>
              <dd className="mt-1 break-all">{profile.email}</dd>
            </div>
            <div>
              <dt className="flex items-center gap-2 text-base-content/60">
                <CalendarDays aria-hidden="true" className="size-4" />
                加入时间
              </dt>
              <dd className="mt-1">{formatProfileDate(profile.createdAt)}</dd>
            </div>
          </dl>
        </aside>

        <section className="rounded-xl border border-base-300 bg-base-100/90 p-6 shadow-sm backdrop-blur sm:p-8">
          <h2 className="text-xl font-semibold">编辑资料</h2>
          {message ? (
            <div className="alert alert-info mt-5 text-sm" role="status">
              {message}
            </div>
          ) : null}
          <form
            className="mt-6 flex flex-col gap-6"
            noValidate
            onSubmit={handleSubmit}
          >
            <div className="form-control flex flex-col">
              <span className="label pb-1">
                <span className="label-text font-medium">头像</span>
                <span className="label-text-alt">(最大 5 MB)</span>
              </span>
              <label
                className="btn btn-outline w-fit"
                htmlFor="profile-avatar-file"
              >
                {uploadingAvatar ? (
                  <span className="loading loading-spinner loading-sm" />
                ) : (
                  <Upload aria-hidden="true" className="size-4" />
                )}
                {uploadingAvatar
                  ? `上传中 ${uploadProgress}%`
                  : draft.avatarUrl
                    ? "更换头像"
                    : "上传头像"}
              </label>

              <input
                id="profile-avatar-file"
                accept="image/png,image/jpeg,image/gif,image/webp"
                className="sr-only"
                disabled={uploadingAvatar || saving}
                type="file"
                onChange={(event) => void handleAvatarChange(event)}
              />
              {getFieldError(fieldErrors, "avatarMediaResourceId") ? (
                <span className="label pt-1 text-error">
                  {getFieldError(fieldErrors, "avatarMediaResourceId")}
                </span>
              ) : null}
            </div>

            <label
              className="form-control w-1/2 lg:w-1/3"
              htmlFor="profile-nickname"
            >
              <span className="label pb-1">
                <span className="label-text font-medium">昵称</span>
                <span className="label-text-alt">(最多 60 字符)</span>
              </span>
              <input
                id="profile-nickname"
                aria-invalid={Boolean(getFieldError(fieldErrors, "nickname"))}
                className="input input-bordered w-full"
                maxLength={60}
                value={draft.nickname ?? ""}
                onChange={(event) =>
                  handleChange("nickname", event.target.value)
                }
              />
              {getFieldError(fieldErrors, "nickname") ? (
                <span className="label pt-1 text-error">
                  {getFieldError(fieldErrors, "nickname")}
                </span>
              ) : null}
            </label>

            <label className="form-control" htmlFor="profile-bio">
              <span className="label pb-1">
                <span className="label-text font-medium">个人简介</span>
                <span className="label-text-alt">
                  ({draft.bio?.length ?? 0} / 500)
                </span>
              </span>
              <textarea
                id="profile-bio"
                aria-invalid={Boolean(getFieldError(fieldErrors, "bio"))}
                className="textarea textarea-bordered min-h-32 w-full resize-y leading-6"
                maxLength={500}
                value={draft.bio ?? ""}
                onChange={(event) => handleChange("bio", event.target.value)}
              />
              {getFieldError(fieldErrors, "bio") ? (
                <span className="label pt-1 text-error">
                  {getFieldError(fieldErrors, "bio")}
                </span>
              ) : null}
            </label>
            <div className="flex flex-wrap justify-end gap-3 pt-2">
              <button
                className="btn btn-ghost"
                disabled={!dirty || saving || uploadingAvatar}
                type="button"
                onClick={handleCancel}
              >
                <Undo2 aria-hidden="true" className="size-4" />
                取消修改
              </button>
              <button
                className="btn btn-primary"
                disabled={!dirty || saving || uploadingAvatar}
                type="submit"
              >
                {saving ? (
                  <span className="loading loading-spinner loading-sm" />
                ) : (
                  <Save aria-hidden="true" className="size-4" />
                )}
                {saving ? "保存中" : "保存资料"}
              </button>
            </div>
          </form>
        </section>
      </div>
      <WordStudySummaryPanel />
      <WordStudySettingsPanel />
      <WordFavoritesPanel />
      <WordReviewExclusionsPanel />
      <AccountSecurityPanel currentEmail={profile.email} />
    </div>
  );
}
