import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import WordStudySummary from "./WordStudySummary";
import type { WordStudySessionState } from "./wordStudyTypes";
const session: WordStudySessionState = { id:"summary", sessionType:"Review", phase:"Summary", status:"Active",
 actualCount:3, completedCount:0, memorizationPassedCount:3, spellingPassedCount:0, excludedCount:0, skippedCount:0,
 startedAt:"2026-10-03T00:00:00Z", completedAt:null, currentItem:null,
 summary:{wordCount:3,ratingCount:5,againCount:1,hardCount:1,goodCount:2,easyCount:1} };
describe("WordStudySummary",()=>{
 it("shows counts and explicitly chooses spelling",async()=>{
  const user=userEvent.setup();const choose=vi.fn().mockResolvedValue(undefined);
  render(<WordStudySummary session={session} onChoose={choose}/>);
  expect(screen.getByText("完成 3 个单词的记忆练习")).toBeVisible();
  expect(screen.getByText(/共评分 5 次/)).toBeVisible();expect(choose).not.toHaveBeenCalled();
  await user.click(screen.getByRole("button",{name:"确认，进入拼写"}));expect(choose).toHaveBeenCalledWith(false);
 });
 it("skips on request, displays failures and allows retry",async()=>{
  const user=userEvent.setup();const choose=vi.fn().mockRejectedValueOnce(new Error()).mockResolvedValue(undefined);
  render(<WordStudySummary session={session} onChoose={choose}/>);
  await user.click(screen.getByRole("button",{name:"跳过拼写，完成本组"}));expect(await screen.findByRole("alert")).toBeVisible();
  await user.click(screen.getByRole("button",{name:"跳过拼写，完成本组"}));expect(choose).toHaveBeenLastCalledWith(true);
  expect(screen.queryByRole("alert")).not.toBeInTheDocument();
 });
});
