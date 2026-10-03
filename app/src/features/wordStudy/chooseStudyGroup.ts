import type { WordLearningOverview, WordReviewOverview } from "./wordStudyTypes";

/** Resume the most recently started group; otherwise study due words before new words. */
export function chooseStudyGroup(learning: WordLearningOverview, review: WordReviewOverview): {
  mode: "learning" | "review"; sessionId?: string;
} | null {
  const l = learning.activeSession;
  const r = review.activeSession;
  if (l || r) {
    if (r && (!l || r.startedAt >= l.startedAt)) return { mode: "review", sessionId: r.id };
    return { mode: "learning", sessionId: l!.id };
  }
  if (review.dueCount > 0) return { mode: "review" };
  if (learning.hasMoreWords) return { mode: "learning" };
  return null;
}
