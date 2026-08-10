import { Save } from "lucide-react";
import { useEffect, useState } from "react";
import {
  useGetSettingsQuery,
  useUpdateSettingsMutation,
} from "@/features/wordStudy/wordStudyApi";
import { getWordStudyErrorMessage } from "@/features/wordStudy/wordStudyErrors";

/** Manages the account-backed daily word-study count independently of profile fields. */
export default function WordStudySettingsPanel() {
  const settingsQuery = useGetSettingsQuery();
  const [updateSettings, updateState] = useUpdateSettingsMutation();
  const [value, setValue] = useState("20");
  const [dirty, setDirty] = useState(false);
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (settingsQuery.data && !dirty) {
      setValue(String(settingsQuery.data.dailyWordStudyCount));
    }
  }, [dirty, settingsQuery.data]);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const count = Number(value);
    if (!Number.isInteger(count) || count < 1 || count > 100) {
      setFieldError("每日背诵数量必须在 1 到 100 之间。");
      setMessage(null);
      return;
    }

    setFieldError(null);
    setMessage(null);
    try {
      const updated = await updateSettings(count).unwrap();
      setValue(String(updated.dailyWordStudyCount));
      setDirty(false);
      setMessage("每日背诵数量已保存。新设置将在下一次创建每日会话时生效。");
    } catch (error) {
      const apiError = error as { fieldErrors?: Record<string, string> };
      setFieldError(
        apiError.fieldErrors?.DailyWordStudyCount ??
          apiError.fieldErrors?.dailyWordStudyCount ??
          null,
      );
      setMessage(
        getWordStudyErrorMessage(error, "设置保存失败，请检查后重试。"),
      );
    }
  };

  if (settingsQuery.isLoading) {
    return (
      <section
        aria-label="每日单词背诵设置加载中"
        className="space-y-4"
        role="status"
      >
        <div className="skeleton h-7 w-44" />
        <div className="skeleton h-32 w-full" />
      </section>
    );
  }

  if (settingsQuery.isError && !settingsQuery.data) {
    return (
      <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm">
        <h2 className="text-xl font-semibold">每日单词背诵</h2>
        <div className="alert alert-error mt-5 text-sm" role="alert">
          {settingsQuery.error.message}
        </div>
        <button
          className="btn btn-outline btn-sm mt-4"
          type="button"
          onClick={() => void settingsQuery.refetch()}
        >
          重新加载
        </button>
      </section>
    );
  }

  return (
    <section className="rounded-lg border border-base-300 bg-base-100 p-6 shadow-sm sm:p-8">
      <h2 className="text-xl font-semibold">每日单词背诵</h2>
      <p className="mt-2 text-sm leading-6 text-base-content/65">
        系统按 UTC 日期创建每日会话，当天已开始的会话不会因修改数量而变化。
      </p>
      {message ? (
        <div className="alert alert-info mt-5 text-sm" role="status">
          {message}
        </div>
      ) : null}
      <form
        className="mt-6 flex flex-col gap-5"
        noValidate
        onSubmit={handleSubmit}
      >
        <label
          className="form-control max-w-xs"
          htmlFor="daily-word-study-count"
        >
          <span className="label pb-1">
            <span className="label-text font-medium">每天背诵数量</span>
            <span className="label-text-alt">1 - 100</span>
          </span>
          <input
            id="daily-word-study-count"
            aria-invalid={Boolean(fieldError)}
            className="input input-bordered w-full"
            inputMode="numeric"
            max={100}
            min={1}
            type="number"
            value={value}
            onChange={(event) => {
              setValue(event.target.value);
              setDirty(true);
              setFieldError(null);
              setMessage(null);
            }}
          />
          {fieldError ? (
            <span className="label pt-1 text-error">{fieldError}</span>
          ) : null}
        </label>
        <div>
          <button
            className="btn btn-primary"
            disabled={!dirty || updateState.isLoading}
            type="submit"
          >
            {updateState.isLoading ? (
              <span className="loading loading-spinner loading-sm" />
            ) : (
              <Save aria-hidden="true" className="size-4" />
            )}
            {updateState.isLoading ? "保存中" : "保存背诵设置"}
          </button>
        </div>
      </form>
    </section>
  );
}
