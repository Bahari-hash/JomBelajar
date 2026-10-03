import { render, screen } from "@testing-library/react";
import { MemoryRouter, Routes, Route } from "react-router-dom";
import { it, expect } from "vitest";
import Page from "./WordReviewPage";
it("redirects the old entry to unified word study", async () => {
  render(<MemoryRouter initialEntries={["/old"]}><Routes>
    <Route path="/old" element={<Page />} />
    <Route path="/words/study" element={<h1>单词学习</h1>} />
  </Routes></MemoryRouter>);
  expect(await screen.findByRole("heading", {name: "单词学习"})).toBeVisible();
});
