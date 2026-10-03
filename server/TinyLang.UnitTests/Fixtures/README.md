# FSRS reference fixtures

`fsrs-reference.json` contains 20 deterministic results from the official
https://github.com/open-spaced-repetition/py-fsrs implementation, version 6.3.2,
commit `9446cb06605c597a063aeee49f7d188d42e34dc2`.

All four ratings are evaluated for a new card, an intraday learning card, a review
card, an intraday relearning card and a late review card. Evaluation time is
2026-10-03T00:00:00Z. Retention is 0.9, fuzzing is disabled; learning steps are
1 and 10 minutes and relearning is 10 minutes. Parameters are explicitly fixed
to FSRS.Core's 21 defaults rather than implicitly relying on library defaults.

The C# test compares resulting state, learning step, stability, difficulty and
due interval. It additionally exposed and led to corrections for short-term
Hard stability, whole-day retrievability and unclamped initial difficulty in
the vendored implementation (see its README).

Fixtures contain synthetic test states and no user data.
