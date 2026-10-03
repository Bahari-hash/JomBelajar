Vendored FSRS.Core (MIT) from https://github.com/TranPhucTien/FSRS.Core
Commit: a4a4a373174700d78578c93de5c186b613ed632f
FSRS-6, 21 default parameters. DI factory excluded; explicit System imports added for this repository.
Integration uses deterministic scheduling (fuzzing disabled) and default 1/10 minute learning, 10 minute relearning.
Reference parity fixtures are generated against open-spaced-repetition/py-fsrs with identical parameters.
Added null-forgiving operator to the validated learning step for nullable warnings-as-errors.

Parity corrections against py-fsrs 6.3.2: Hard ratings cannot reduce short-term stability; retrievability uses whole elapsed days; difficulty mean reversion uses unclamped initial Easy difficulty.
