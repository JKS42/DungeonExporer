# Setup Guide — DungeonExporer

> Step-by-step setup for developing and running DungeonExporer.
> Last updated: 2026-05-13

## 1. System requirements

### Minimum (developer / playtest)

- **OS**: Windows 10 / 11 (Linux & macOS should also work for Ollama; Unity dev tested on Windows).
- **CPU**: Any modern x86-64 (Intel 8th gen / Ryzen 2000 or newer).
- **RAM**: 16 GB.
- **GPU**: Integrated graphics acceptable for the default `qwen3:4b` model in CPU mode.
- **Disk**: ~10 GB free (Unity project + Ollama + one model).

### Recommended

- **CPU**: 6+ cores.
- **RAM**: 32 GB.
- **GPU**: Dedicated GPU with ≥ 8 GB VRAM (NVIDIA RTX 30-series or better, or AMD equivalent) for fast inference.
- **Disk**: SSD with ~20 GB free if pulling multiple models.

## 2. Tooling

| Tool | Version | Purpose |
|---|---|---|
| [Unity Hub](https://unity.com/download) | latest | Manages Unity editor installs |
| Unity Editor | **6000.3.8f1** (see `ProjectSettings/ProjectVersion.txt`) | Engine |
| [Git](https://git-scm.com/) | latest | Source control |
| [Ollama](https://ollama.com/download) | latest | Runs the local LLM |
| IDE | Visual Studio 2022 / Rider / VS Code | C# editing |

## 3. Clone the repo

```powershell
git clone https://github.com/<your-org>/DungeonExporer.git
cd DungeonExporer
```

## 4. Install Ollama

### Windows

1. Download the installer from <https://ollama.com/download>.
2. Run it. Ollama installs as a background service listening on `http://localhost:11434`.
3. Verify it's running:

```powershell
curl http://localhost:11434/api/tags
```

You should get a JSON response (an empty `models` array is fine at this point).

### Linux / macOS

```bash
curl -fsSL https://ollama.com/install.sh | sh
```

Then start the service (it auto-starts on macOS; on Linux: `systemctl --user start ollama` or `ollama serve` in a terminal).

## 5. Pull the required model(s)

The default development model is `qwen3:4b`.

```powershell
ollama pull qwen3:4b
```

Optional quality-tier model:

```powershell
ollama pull llama3
```

Verify:

```powershell
ollama list
```

Quick sanity check (should print a response):

```powershell
ollama run qwen3:4b "Say hello in 5 words."
```

## 6. Open the project in Unity

1. Open **Unity Hub** → **Add project from disk** → select the cloned `DungeonExporer` folder.
2. If prompted, install Unity **6000.3.8f1** (the exact version from `ProjectSettings/ProjectVersion.txt`).
3. Open the project. The first import will take a few minutes.

## 7. Verify the Ollama integration

1. In the Project window, open `Assets/Scenes/Level1.unity`.
2. Make sure Ollama is running (`curl http://localhost:11434/api/tags`).
3. Press **Play**.
4. In the on-screen UI, type a prompt and hit the Send button. You should see a streamed response.

If nothing happens:
- Check the Unity Console for errors.
- Confirm `qwen3:4b` is pulled (`ollama list`).
- Confirm port `11434` is not blocked by a firewall.

## 8. Project structure (current)

```
DungeonExporer/
├── AGENTS.md                  # Rules for AI agents working in this repo
├── README.md
├── docs/
│   ├── high-concept.md
│   ├── ollama-plan.md
│   ├── setup.md               # ← you are here
│   └── refinements-changes.md
├── Assets/
│   ├── Scenes/
│   │   ├── MainMenu.unity
│   │   └── Level1.unity
│   ├── Scripts/               # Game code (gameplay code goes here)
│   │   └── OllamaHandler.cs   # UI-driven Ollama tester
│   ├── Ollama/
│   │   └── OllamaRequester.cs # Minimal Ollama example
│   ├── SimpleOllamaInjection/ # Preferred Ollama wrapper (Microsoft.Extensions.AI)
│   ├── Resources/Neocortex/   # Neocortex SDK settings (key currently in-repo — see Risks)
│   └── ...
└── ProjectSettings/
```

## 9. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Unity console: `Cannot connect to localhost:11434` | Ollama service not running | Start Ollama; check Windows Services or run `ollama serve`. |
| First response takes > 10 s | Model cold-load | Normal; subsequent requests are fast. We pre-warm at game launch. |
| Response contains `<think>...</think>` text | `clearThinking` not enabled on the request | Set `clearThinking = true` when calling `Ollama.SendMessage`. |
| Out-of-memory crash from Ollama | Model too big for hardware | Switch to `qwen3:4b` or a smaller quantization. |
| Compile error: `Newtonsoft.Json` not found | Package not installed | Open Package Manager and install `com.unity.nuget.newtonsoft-json`, or remove `OllamaRequester.cs` if unused. |

## 10. Where to go next

- Read [`high-concept.md`](./high-concept.md) for the game's design intent.
- Read [`ollama-plan.md`](./ollama-plan.md) for how the LLM is wired into gameplay.
- Read [`refinements-changes.md`](./refinements-changes.md) to see what's changed and why.
