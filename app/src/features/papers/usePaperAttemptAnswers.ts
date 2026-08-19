import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type {
  LocalPaperAnswer,
  PaperAnswerValue,
  PaperAttemptQuestion,
  SavePaperAnswerRequest,
} from "@/features/papers/paperTypes";

interface UsePaperAttemptAnswersOptions {
  attemptId: string;
  questions: PaperAttemptQuestion[];
  saveAnswer: (
    questionId: string,
    answer: SavePaperAnswerRequest,
  ) => Promise<void>;
  clearAnswer: (questionId: string) => Promise<void>;
  debounceMs?: number;
}

export interface FlushResult {
  ok: boolean;
  failedQuestionIds: string[];
}

function savedValue(question: PaperAttemptQuestion): PaperAnswerValue {
  if (!question.savedAnswer) return null;
  if (question.type === "SingleChoice")
    return question.savedAnswer.selectedOptionId;
  if (question.type === "TrueFalse") return question.savedAnswer.booleanAnswer;
  if (question.type === "Dictation")
    return (
      question.savedAnswer.textAnswers ??
      (question.dictationBlanks ?? []).map(() => "")
    );
  return question.savedAnswer.textAnswer;
}

function requestFor(
  question: PaperAttemptQuestion,
  value: PaperAnswerValue,
): SavePaperAnswerRequest | null {
  if (
    value === null ||
    (question.type === "FillBlank" && String(value).trim().length === 0) ||
    (question.type === "Dictation" &&
      Array.isArray(value) &&
      value.every((item) => item.trim().length === 0))
  )
    return null;
  if (question.type === "SingleChoice")
    return { selectedOptionId: String(value) };
  if (question.type === "TrueFalse") return { booleanAnswer: Boolean(value) };
  if (question.type === "Dictation")
    return { textAnswers: Array.isArray(value) ? value : [] };
  return { textAnswer: String(value) };
}

export function usePaperAttemptAnswers({
  attemptId: _attemptId,
  questions,
  saveAnswer,
  clearAnswer,
  debounceMs = 400,
}: UsePaperAttemptAnswersOptions) {
  const orderedQuestions = useMemo(
    () =>
      [...questions].sort(
        (left, right) =>
          left.sortOrder - right.sortOrder || left.id.localeCompare(right.id),
      ),
    [questions],
  );
  const questionMap = useMemo(
    () => new Map(orderedQuestions.map((question) => [question.id, question])),
    [orderedQuestions],
  );
  const initial = useMemo(
    () =>
      Object.fromEntries(
        orderedQuestions.map((question) => {
          const value = savedValue(question);
          return [
            question.id,
            {
              value,
              savedValue: value,
              status: "idle",
              revision: 0,
              errorMessage: null,
            } satisfies LocalPaperAnswer,
          ];
        }),
      ),
    [orderedQuestions],
  );
  const [answers, setAnswers] =
    useState<Record<string, LocalPaperAnswer>>(initial);
  const [isFlushing, setIsFlushing] = useState(false);
  const answersRef = useRef(answers);
  const timersRef = useRef(new Map<string, ReturnType<typeof setTimeout>>());
  const inFlightRef = useRef(
    new Map<string, { revision: number; promise: Promise<boolean> }>(),
  );
  const mountedRef = useRef(true);

  const replaceAnswer = useCallback(
    (
      questionId: string,
      update: (answer: LocalPaperAnswer) => LocalPaperAnswer,
    ) => {
      const current = answersRef.current[questionId];
      if (!current) return;
      const next = update(current);
      answersRef.current = { ...answersRef.current, [questionId]: next };
      if (mountedRef.current) setAnswers(answersRef.current);
    },
    [],
  );

  const persistRevision = useCallback(
    (questionId: string, revision: number): Promise<boolean> => {
      const current = answersRef.current[questionId];
      const question = questionMap.get(questionId);
      if (!current || !question || current.revision !== revision)
        return Promise.resolve(false);
      const existing = inFlightRef.current.get(questionId);
      if (existing?.revision === revision) return existing.promise;
      const value = current.value;
      replaceAnswer(questionId, (answer) => ({
        ...answer,
        status: "saving",
        errorMessage: null,
      }));
      const request = requestFor(question, value);
      const promise = (
        request ? saveAnswer(questionId, request) : clearAnswer(questionId)
      )
        .then(() => {
          if (answersRef.current[questionId]?.revision === revision) {
            replaceAnswer(questionId, (answer) => ({
              ...answer,
              savedValue: value,
              status: "saved",
              errorMessage: null,
            }));
          }
          return true;
        })
        .catch((error: unknown) => {
          if (answersRef.current[questionId]?.revision === revision) {
            replaceAnswer(questionId, (answer) => ({
              ...answer,
              status: "error",
              errorMessage:
                error instanceof Error ? error.message : "答案保存失败。",
            }));
          }
          return false;
        })
        .finally(() => {
          if (inFlightRef.current.get(questionId)?.promise === promise)
            inFlightRef.current.delete(questionId);
        });
      inFlightRef.current.set(questionId, { revision, promise });
      return promise;
    },
    [clearAnswer, questionMap, replaceAnswer, saveAnswer],
  );

  const flushQuestion = useCallback(
    (questionId: string) => {
      const timer = timersRef.current.get(questionId);
      if (timer) clearTimeout(timer);
      timersRef.current.delete(questionId);
      const current = answersRef.current[questionId];
      if (!current) return Promise.resolve(false);
      if (current.value === current.savedValue && current.status !== "error")
        return Promise.resolve(true);
      return persistRevision(questionId, current.revision);
    },
    [persistRevision],
  );

  const changeAnswer = useCallback(
    (questionId: string, value: PaperAnswerValue) => {
      const question = questionMap.get(questionId);
      if (!question) return;
      const nextRevision = (answersRef.current[questionId]?.revision ?? 0) + 1;
      replaceAnswer(questionId, (answer) => ({
        ...answer,
        value,
        revision: nextRevision,
        status: "idle",
        errorMessage: null,
      }));
      const existingTimer = timersRef.current.get(questionId);
      if (existingTimer) clearTimeout(existingTimer);
      timersRef.current.delete(questionId);
      if (question.type === "FillBlank" || question.type === "Dictation") {
        timersRef.current.set(
          questionId,
          setTimeout(() => {
            timersRef.current.delete(questionId);
            void persistRevision(questionId, nextRevision);
          }, debounceMs),
        );
      } else {
        void persistRevision(questionId, nextRevision);
      }
    },
    [debounceMs, persistRevision, questionMap, replaceAnswer],
  );

  const retryQuestion = useCallback(
    (questionId: string) => {
      const current = answersRef.current[questionId];
      return current
        ? persistRevision(questionId, current.revision)
        : Promise.resolve(false);
    },
    [persistRevision],
  );

  const flushAll = useCallback(async (): Promise<FlushResult> => {
    setIsFlushing(true);
    const results = await Promise.all(
      orderedQuestions.map(async (question) => ({
        id: question.id,
        ok: await flushQuestion(question.id),
      })),
    );
    if (mountedRef.current) setIsFlushing(false);
    const failedQuestionIds = results
      .filter((result) => !result.ok)
      .map((result) => result.id);
    return { ok: failedQuestionIds.length === 0, failedQuestionIds };
  }, [flushQuestion, orderedQuestions]);

  useEffect(() => {
    const mounted = mountedRef;
    const timers = timersRef;
    mounted.current = true;
    return () => {
      mounted.current = false;
      timers.current.forEach((timer) => clearTimeout(timer));
      timers.current.clear();
    };
  }, []);

  return {
    answers,
    isFlushing,
    changeAnswer,
    flushQuestion,
    retryQuestion,
    flushAll,
  };
}

export type PaperAttemptAnswersController = ReturnType<
  typeof usePaperAttemptAnswers
>;
