# TODO: Blood Magic Expansion Integration

Full Enemy XP is not released yet, so Blood Magic Expansion should not depend
on it or reflect into it for now.

Later, expose a small stable API/helper so Blood Magic Expansion can ask for
Full Enemy XP's adjusted kill XP basis when Full Enemy XP is installed and
enabled. Preserve Full Enemy XP disabled and dry-run behavior, and fall back to
Blood Magic Expansion's vanilla XP behavior when the API is unavailable.
