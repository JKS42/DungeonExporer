#!/usr/bin/env python3
"""Render cap_personality.jinja2 with a JSON context file (stdout = Ollama prompt)."""
import json
import sys
from pathlib import Path

from jinja2 import Template


def main() -> int:
    if len(sys.argv) != 3:
        print("Usage: render_cap_prompt.py <template.jinja2> <context.json>", file=sys.stderr)
        return 2

    template_path = Path(sys.argv[1])
    context_path = Path(sys.argv[2])
    context = json.loads(context_path.read_text(encoding="utf-8"))
    text = Template(template_path.read_text(encoding="utf-8")).render(**context)
    sys.stdout.write(text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
