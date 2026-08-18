import { Save } from "lucide-react";
import { useEffect, useState } from "react";
import { useGetSettingsQuery, useUpdateSettingsMutation } from "./wordStudyApi";

export default function WordStudySettingsPanel() {
  const query = useGetSettingsQuery();
  const [update, state] = useUpdateSettingsMutation();
  const [study, setStudy] = useState(20);
  const [review, setReview] = useState(50);
  const [message, setMessage] = useState<string | null>(null);
  const [requestError, setRequestError] = useState<string | null>(null);
  const [errors, setErrors] = useState<{ study?: string; review?: string }>({});
  useEffect(() => {
    if (query.data) {
      setStudy(query.data.dailyWordStudyCount);
      setReview(query.data.dailyWordReviewCount);
    }
  }, [query.data]);
  return (
    <section className="border-t border-base-300 py-8">
      <h2 className="text-xl font-semibold">单词学习设置</h2>
      {query.isLoading ? (
        <p className="mt-4 text-sm text-base-content/60">正在加载设置...</p>
      ) : null}
      {query.isError ? (
        <div className="mt-4 space-y-3">
          <p className="alert alert-error text-sm" role="alert">
            学习设置暂时无法加载。
          </p>
          <button
            className="btn btn-ghost btn-sm"
            type="button"
            onClick={() => void query.refetch()}
          >
            重新加载
          </button>
        </div>
      ) : null}
      <form
        className="mt-5 grid gap-5 sm:grid-cols-2"
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          const nextErrors = {
            ...(study < 1 || study > 100
              ? { study: "每组新词数量必须在 1 到 100 之间。" }
              : {}),
            ...(review < 1 || review > 200
              ? { review: "每组复习数量必须在 1 到 200 之间。" }
              : {}),
          };
          setErrors(nextErrors);
          setRequestError(null);
          setMessage(null);
          if (Object.keys(nextErrors).length > 0) return;
          void update({
            dailyWordStudyCount: study,
            dailyWordReviewCount: review,
          })
            .unwrap()
            .then(() => setMessage("设置已保存，新数量从下一组开始生效。"))
            .catch(() => setRequestError("设置保存失败，请重试。"));
        }}
      >
        <label className="form-control">
          <span className="label-text mb-2">每组新词数量</span>
          <input
            aria-invalid={Boolean(errors.study)}
            className="input input-bordered mt-4 md:mt-0 md:ml-4"
            max={100}
            min={1}
            type="number"
            value={study}
            onChange={(event) => {
              setStudy(Number(event.target.value));
              setErrors((current) => ({ ...current, study: undefined }));
            }}
          />
          {errors.study ? (
            <span className="label-text-alt text-error">{errors.study}</span>
          ) : null}
        </label>
        <label className="form-control">
          <span className="label-text mb-2">每组复习数量</span>
          <input
            aria-invalid={Boolean(errors.review)}
            className="input input-bordered mt-4 md:mt-0 md:ml-4"
            max={200}
            min={1}
            type="number"
            value={review}
            onChange={(event) => {
              setReview(Number(event.target.value));
              setErrors((current) => ({ ...current, review: undefined }));
            }}
          />
          {errors.review ? (
            <span className="label-text-alt text-error">{errors.review}</span>
          ) : null}
        </label>
        <div className="sm:col-span-2">
          <button
            className="btn btn-primary"
            disabled={state.isLoading}
            type="submit"
          >
            <Save className="size-4" />
            保存设置
          </button>
          {message ? (
            <p className="mt-3 text-sm text-success">{message}</p>
          ) : null}
          {requestError ? (
            <p className="alert alert-error mt-3 text-sm" role="alert">
              {requestError}
            </p>
          ) : null}
        </div>
      </form>
    </section>
  );
}
