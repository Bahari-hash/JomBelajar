import { Save } from "lucide-react";
import { useEffect, useState } from "react";
import { useGetSettingsQuery, useUpdateSettingsMutation } from "./wordStudyApi";

export default function WordStudySettingsPanel() {
  const query = useGetSettingsQuery();
  const [update, state] = useUpdateSettingsMutation();
  const [study, setStudy] = useState(20);
  const [review, setReview] = useState(50);
  const [message, setMessage] = useState<string | null>(null);
  useEffect(() => {
    if (query.data) {
      setStudy(query.data.dailyWordStudyCount);
      setReview(query.data.dailyWordReviewCount ?? 50);
    }
  }, [query.data]);
  return (
    <section className="border-t border-base-300 py-8">
      <h2 className="text-xl font-semibold">单词学习设置</h2>
      <form
        className="mt-5 grid gap-5 sm:grid-cols-2"
        onSubmit={(event) => {
          event.preventDefault();
          if (study < 1 || study > 100 || review < 1 || review > 200) return;
          void update({
            dailyWordStudyCount: study,
            dailyWordReviewCount: review,
          })
            .unwrap()
            .then(() => setMessage("设置已保存，新数量从下一组开始生效。"));
        }}
      >
        <label className="form-control">
          <span className="label-text mb-2">每组新词数量</span>
          <input
            className="input input-bordered"
            max={100}
            min={1}
            type="number"
            value={study}
            onChange={(event) => setStudy(Number(event.target.value))}
          />
        </label>
        <label className="form-control">
          <span className="label-text mb-2">每组复习数量</span>
          <input
            className="input input-bordered"
            max={200}
            min={1}
            type="number"
            value={review}
            onChange={(event) => setReview(Number(event.target.value))}
          />
        </label>
        <div className="sm:col-span-2">
          <button
            className="btn btn-primary"
            disabled={
              state.isLoading ||
              study < 1 ||
              study > 100 ||
              review < 1 ||
              review > 200
            }
          >
            <Save className="size-4" />
            保存设置
          </button>
          {message ? (
            <p className="mt-3 text-sm text-success">{message}</p>
          ) : null}
        </div>
      </form>
    </section>
  );
}
