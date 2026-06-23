#!/usr/bin/env python3
"""
comfyui_mcp.py — MCP server (JSON-RPC 2.0 / stdio) para o pipeline ComfyUI.

Sem pip install. Requer Python 3.10+.

Ferramentas expostas:
  comfy_status    — verifica se o ComfyUI está rodando em localhost:8188
  comfy_upscale   — upscale 2x (Real-ESRGAN) de uma pasta
  comfy_removebg  — remove fundo em lote (ISNet-anime)
  comfy_batch     — pipeline completo por tipo (inbox → upscaled)
  comfy_skinvar   — (IMG-07, experimental) variante de skin via img2img+ControlNet+IPAdapter
  comfy_list      — lista arquivos em output/inbox ou output/upscaled
  comfy_validate  — valida proporção / alpha / set completo (via validate_assets.py)

Registro (já em .claude/settings.json):
  "mcpServers": {
    "comfyui": { "command": "python", "args": ["tools/comfyui_mcp.py"] }
  }
"""
from __future__ import annotations
import json, pathlib, subprocess, sys, urllib.request

PROJECT_ROOT    = pathlib.Path(__file__).parent.parent
BATCH_SCRIPT    = pathlib.Path(__file__).parent / "comfyui_batch.py"
VALIDATE_SCRIPT = pathlib.Path(__file__).parent / "validate_assets.py"
COMFY_URL       = "http://localhost:8188"
ASSET_TYPES     = ["kaeli", "item", "mob", "background", "logo", "motion"]


# ── Helpers ────────────────────────────────────────────────────────────────────

def _run(script: pathlib.Path, *args: str) -> tuple[int, str]:
    result = subprocess.run(
        [sys.executable, "-u", str(script)] + list(args),
        capture_output=True, text=True, encoding="utf-8", errors="replace",
        cwd=str(PROJECT_ROOT),
    )
    out = result.stdout
    if result.stderr:
        out += "\n--- stderr ---\n" + result.stderr
    return result.returncode, out.strip()


def _comfy_alive() -> bool:
    try:
        with urllib.request.urlopen(f"{COMFY_URL}/system_stats", timeout=3):
            return True
    except Exception:
        return False


# ── Tool handlers ──────────────────────────────────────────────────────────────

def tool_comfy_status(args: dict) -> str:
    if _comfy_alive():
        return f"ComfyUI rodando em {COMFY_URL} ✓"
    return (
        f"ComfyUI NÃO responde em {COMFY_URL}.\n"
        "Inicie com:  python main.py  (na pasta do ComfyUI)"
    )


def tool_comfy_upscale(args: dict) -> str:
    input_dir = args.get("input_dir", "")
    if not input_dir:
        return "ERRO: input_dir é obrigatório."

    cmd = [
        "upscale",
        "--input",  input_dir,
        "--glob",   args.get("glob",  "*.png"),
        "--scale",  str(args.get("scale", 0.5)),
        "--upscale-model", args.get("model", "RealESRGAN_x4plus_anime_6B.pth"),
    ]
    if args.get("output_dir"):
        cmd += ["--output", args["output_dir"]]
    if args.get("backup"):
        cmd.append("--backup")
    if args.get("dry_run"):
        cmd.append("--dry-run")

    rc, out = _run(BATCH_SCRIPT, *cmd)
    return f"{'✓ OK' if rc == 0 else f'✗ Erro (rc={rc})'}\n\n{out}"


def tool_comfy_removebg(args: dict) -> str:
    input_dir = args.get("input_dir", "")
    if not input_dir:
        return "ERRO: input_dir é obrigatório."

    cmd = [
        "removebg",
        "--input", input_dir,
        "--glob",  args.get("glob", "*.png"),
        "--model", args.get("model", "isnet-anime"),
    ]
    if args.get("output_dir"):
        cmd += ["--output", args["output_dir"]]
    if args.get("backup"):
        cmd.append("--backup")
    if args.get("dry_run"):
        cmd.append("--dry-run")

    rc, out = _run(BATCH_SCRIPT, *cmd)
    return f"{'✓ OK' if rc == 0 else f'✗ Erro (rc={rc})'}\n\n{out}"


def tool_comfy_batch(args: dict) -> str:
    asset_type = args.get("asset_type", "")
    if asset_type not in ASSET_TYPES:
        return f"ERRO: asset_type deve ser um de: {', '.join(ASSET_TYPES)}"

    cmd = ["batch", "--type", asset_type]
    if args.get("input_dir"):
        cmd += ["--input", args["input_dir"]]
    if args.get("output_dir"):
        cmd += ["--output", args["output_dir"]]
    if args.get("glob"):
        cmd += ["--glob", args["glob"]]
    if args.get("force"):
        cmd.append("--force")
    if args.get("dry_run"):
        cmd.append("--dry-run")
    if args.get("no_upscale"):
        cmd.append("--no-upscale")
    if args.get("no_removebg"):
        cmd.append("--no-removebg")
    if args.get("backup"):
        cmd.append("--backup")

    rc, out = _run(BATCH_SCRIPT, *cmd)
    return f"{'✓ OK' if rc == 0 else f'✗ Erro (rc={rc})'}\n\n{out}"


def tool_comfy_skinvar(args: dict) -> str:
    prompt = args.get("prompt", "")
    if not prompt:
        return "ERRO: prompt é obrigatório (descreva a nova roupa/cenário)."

    cmd = ["skinvar", "--prompt", prompt]
    if args.get("input_dir"):
        cmd += ["--input", args["input_dir"]]
    if args.get("output_dir"):
        cmd += ["--output", args["output_dir"]]
    if args.get("glob"):
        cmd += ["--glob", args["glob"]]
    if args.get("name"):
        cmd += ["--name", args["name"]]
    if args.get("count"):
        cmd += ["--count", str(args["count"])]
    if args.get("seed"):
        cmd += ["--seed", str(args["seed"])]
    if args.get("checkpoint"):
        cmd += ["--checkpoint", args["checkpoint"]]
    if args.get("control_type"):
        cmd += ["--control-type", args["control_type"]]
    if args.get("control_model"):
        cmd += ["--control-model", args["control_model"]]
    if args.get("control_strength") is not None:
        cmd += ["--control-strength", str(args["control_strength"])]
    if args.get("ipadapter_weight") is not None:
        cmd += ["--ipadapter-weight", str(args["ipadapter_weight"])]
    if args.get("denoise") is not None:
        cmd += ["--denoise", str(args["denoise"])]
    if args.get("max_mp") is not None:
        cmd += ["--max-mp", str(args["max_mp"])]
    if args.get("hires") is not None:
        cmd += ["--hires", str(args["hires"])]
    if args.get("hires_denoise") is not None:
        cmd += ["--hires-denoise", str(args["hires_denoise"])]
    if args.get("no_ipadapter"):
        cmd.append("--no-ipadapter")
    if args.get("dry_run"):
        cmd.append("--dry-run")

    rc, out = _run(BATCH_SCRIPT, *cmd)
    return f"{'✓ OK' if rc == 0 else f'✗ Erro (rc={rc})'}\n\n{out}"


def tool_comfy_list(args: dict) -> str:
    folder     = args.get("folder", "output")
    asset_type = args.get("asset_type", "")
    slug       = args.get("slug", "")

    base = PROJECT_ROOT / folder
    if asset_type:
        base = base / asset_type
    if slug:
        base = base / slug

    if not base.exists():
        rel = base.relative_to(PROJECT_ROOT)
        return f"Pasta não encontrada: {rel}"

    lines = []
    for p in sorted(base.rglob("*")):
        if p.is_file() and "_originais" not in p.parts:
            lines.append(str(p.relative_to(PROJECT_ROOT)))

    if not lines:
        return f"Nenhum arquivo em {base.relative_to(PROJECT_ROOT)}"
    return "\n".join(lines)


def tool_comfy_validate(args: dict) -> str:
    asset_type = args.get("asset_type", "kaeli")
    if asset_type not in ASSET_TYPES:
        return f"ERRO: asset_type deve ser um de: {', '.join(ASSET_TYPES)}"
    directory = args.get("directory", f"output/upscaled/{asset_type}")

    if not VALIDATE_SCRIPT.exists():
        return f"ERRO: {VALIDATE_SCRIPT.name} não encontrado em tools/."

    rc, out = _run(VALIDATE_SCRIPT, "validate", "--type", asset_type, "--dir", directory)
    return f"{'✓ OK' if rc == 0 else f'✗ Erros (rc={rc})'}\n\n{out}"


# ── Tool registry ──────────────────────────────────────────────────────────────

TOOLS = [
    {
        "name": "comfy_status",
        "description": "Verifica se o ComfyUI está rodando em localhost:8188.",
        "inputSchema": {"type": "object", "properties": {}},
    },
    {
        "name": "comfy_upscale",
        "description": (
            "Faz upscale 2x (Real-ESRGAN) de todas as imagens de uma pasta. "
            "Requer ComfyUI rodando."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "input_dir":  {"type": "string",  "description": "Pasta de entrada (ex: output/inbox/kaeli/velvet)"},
                "output_dir": {"type": "string",  "description": "Pasta de saída. Padrão: output/upscaled"},
                "glob":       {"type": "string",  "description": "Filtro de arquivo (ex: idle-*.png). Padrão: *.png"},
                "scale":      {"type": "number",  "description": "Fator após 4x: 0.5=net 2x, 1.0=net 4x. Padrão: 0.5"},
                "model":      {"type": "string",  "description": "Modelo de upscale. Padrão: RealESRGAN_x4plus_anime_6B.pth"},
                "backup":     {"type": "boolean", "description": "Salvar cópia dos originais antes de processar"},
                "dry_run":    {"type": "boolean", "description": "Lista o que seria processado sem executar"},
            },
            "required": ["input_dir"],
        },
    },
    {
        "name": "comfy_removebg",
        "description": (
            "Remove o fundo de imagens em lote via ISNet-anime (comfyui-art-venture). "
            "Saída: PNGs com canal alpha. Requer ComfyUI rodando."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "input_dir":  {"type": "string",  "description": "Pasta de entrada"},
                "output_dir": {"type": "string",  "description": "Pasta de saída. Padrão: output/resultado"},
                "glob":       {"type": "string",  "description": "Filtro de arquivo. Padrão: *.png"},
                "model":      {"type": "string",  "description": "Modelo: isnet-anime (padrão), isnet_is, u2net, u2netp"},
                "backup":     {"type": "boolean", "description": "Salvar cópia dos originais antes de processar"},
                "dry_run":    {"type": "boolean", "description": "Lista o que seria processado sem executar"},
            },
            "required": ["input_dir"],
        },
    },
    {
        "name": "comfy_batch",
        "description": (
            "Pipeline completo (upscale + removebg conforme o tipo de asset) "
            "lendo de output/inbox/<tipo> e salvando em output/upscaled/<tipo>. "
            "Requer ComfyUI rodando."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "asset_type": {
                    "type": "string",
                    "description": "Tipo de asset",
                    "enum": ASSET_TYPES,
                },
                "input_dir":   {"type": "string",  "description": "Pasta de entrada. Padrão: output/inbox/<asset_type>"},
                "output_dir":  {"type": "string",  "description": "Pasta de saída. Padrão: output/upscaled/<asset_type>"},
                "glob":        {"type": "string",  "description": "Filtro de arquivo (substitui o padrão do tipo)"},
                "force":       {"type": "boolean", "description": "Reprocessa arquivos já existentes no destino"},
                "dry_run":     {"type": "boolean", "description": "Lista o que seria processado sem executar"},
                "no_upscale":  {"type": "boolean", "description": "Pula a etapa de upscale"},
                "no_removebg": {"type": "boolean", "description": "Pula a etapa de removebg"},
                "backup":      {"type": "boolean", "description": "Salvar cópia dos originais antes de processar"},
            },
            "required": ["asset_type"],
        },
    },
    {
        "name": "comfy_skinvar",
        "description": (
            "IMG-07 (EXPERIMENTAL). Gera variante(s) de skin de uma Kaeli via "
            "img2img + ControlNet (preserva pose) + IPAdapter (preserva rosto), "
            "trocando roupa/cenário segundo o prompt. Lê a imagem base (idle-1 "
            "por padrão) e salva <stem>-<name>-<i>.png em output/skins. "
            "Requer ComfyUI + nodes ComfyUI_IPAdapter_plus e comfyui_controlnet_aux. "
            "Use dry_run=true primeiro. Avalie a consistência: se ficar fraco, "
            "skins seguem no GPT."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "prompt":      {"type": "string",  "description": "Nova roupa/cenário (ex: 'elegant red winter dress, snowy castle')"},
                "input_dir":   {"type": "string",  "description": "Pasta com a base. Padrão: output/upscaled/kaeli"},
                "output_dir":  {"type": "string",  "description": "Pasta de saída. Padrão: output/skins"},
                "glob":        {"type": "string",  "description": "Imagem base. Padrão: idle-1.png"},
                "name":        {"type": "string",  "description": "Rótulo da variante no nome do arquivo. Padrão: skin"},
                "count":       {"type": "integer", "description": "Quantas variações (seeds) por base. Padrão: 2"},
                "seed":        {"type": "integer", "description": "Seed base (0 = derivada do tempo)"},
                "checkpoint":  {"type": "string",  "description": "Checkpoint base (anime recomendado)"},
                "control_type":     {"type": "string", "description": "canny (core, mantém contorno) | openpose (troca roupa) | lineart | depth", "enum": ["canny", "openpose", "lineart", "depth"]},
                "control_model":    {"type": "string", "description": "Override do modelo ControlNet (default: do control_type)"},
                "control_strength": {"type": "number", "description": "Força do ControlNet. Padrão: 0.7"},
                "ipadapter_weight": {"type": "number", "description": "Fidelidade do rosto. 0.5 solto, 0.9 fiel. Padrão: 0.8"},
                "denoise":     {"type": "number",  "description": "Quanto muda: 0.6 sutil, 0.85 agressivo (troca de roupa). Padrão: 0.6"},
                "max_mp":      {"type": "number",  "description": "Redimensiona a base p/ ~N megapixels antes do img2img (SDXL ~1 MP; evita OOM em 8 GB). Padrão: 1.0; 0 = nativo"},
                "hires":       {"type": "number",  "description": "Upscale GENERATIVO ~N× (hires-fix tiled, adiciona detalhe real). Ex: 2.0. 0 = off"},
                "hires_denoise": {"type": "number", "description": "Denoise do refino hires: 0.3 conserva, 0.45 reinventa. Padrão: 0.35"},
                "no_ipadapter": {"type": "boolean", "description": "Pula o IPAdapter (rosto) — p/ rigs sem CLIP-Vision ou roupa nova limpa"},
                "dry_run":     {"type": "boolean", "description": "Lista o que seria gerado sem executar (não requer ComfyUI)"},
            },
            "required": ["prompt"],
        },
    },
    {
        "name": "comfy_list",
        "description": (
            "Lista arquivos em output/inbox ou output/upscaled, "
            "filtrando opcionalmente por tipo e/ou slug."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "folder":     {"type": "string", "description": "Pasta raiz: 'output/inbox' ou 'output/upscaled'. Padrão: 'output'"},
                "asset_type": {"type": "string", "description": "Filtrar por tipo (kaeli, item, mob, background, logo, motion)"},
                "slug":       {"type": "string", "description": "Filtrar por slug (ex: velvet, sword-01)"},
            },
        },
    },
    {
        "name": "comfy_validate",
        "description": (
            "Valida assets em output/upscaled/<tipo>: verifica proporção, "
            "canal alpha (onde esperado) e set completo (ex: 8 arquivos por Kaeli)."
        ),
        "inputSchema": {
            "type": "object",
            "properties": {
                "asset_type": {
                    "type": "string",
                    "description": "Tipo de asset. Padrão: kaeli",
                    "enum": ASSET_TYPES,
                },
                "directory": {"type": "string", "description": "Pasta a validar. Padrão: output/upscaled/<asset_type>"},
            },
        },
    },
]

TOOL_HANDLERS = {
    "comfy_status":   tool_comfy_status,
    "comfy_upscale":  tool_comfy_upscale,
    "comfy_removebg": tool_comfy_removebg,
    "comfy_batch":    tool_comfy_batch,
    "comfy_skinvar":  tool_comfy_skinvar,
    "comfy_list":     tool_comfy_list,
    "comfy_validate": tool_comfy_validate,
}


# ── MCP JSON-RPC 2.0 server (stdio) ───────────────────────────────────────────

def _send(msg: dict):
    sys.stdout.write(json.dumps(msg) + "\n")
    sys.stdout.flush()


def _respond(req_id, result):
    _send({"jsonrpc": "2.0", "id": req_id, "result": result})


def _error(req_id, code: int, message: str):
    _send({"jsonrpc": "2.0", "id": req_id, "error": {"code": code, "message": message}})


def handle(request: dict):
    method = request.get("method", "")
    req_id = request.get("id")

    # Notifications have no id — no response expected
    if req_id is None:
        return

    if method == "initialize":
        _respond(req_id, {
            "protocolVersion": "2024-11-05",
            "capabilities": {"tools": {}},
            "serverInfo": {"name": "kaezan-comfyui", "version": "1.0.0"},
        })

    elif method == "tools/list":
        _respond(req_id, {"tools": TOOLS})

    elif method == "tools/call":
        params = request.get("params", {})
        name   = params.get("name", "")
        targs  = params.get("arguments", {})
        fn     = TOOL_HANDLERS.get(name)
        if not fn:
            _error(req_id, -32601, f"Tool não encontrada: {name}")
            return
        try:
            text = fn(targs)
            _respond(req_id, {
                "content": [{"type": "text", "text": text}],
                "isError": False,
            })
        except Exception as exc:
            _respond(req_id, {
                "content": [{"type": "text", "text": f"Erro interno: {exc}"}],
                "isError": True,
            })

    elif method == "ping":
        _respond(req_id, {})

    else:
        _error(req_id, -32601, f"Method not found: {method}")


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding="utf-8", errors="replace")

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            request = json.loads(line)
        except json.JSONDecodeError:
            _error(None, -32700, "Parse error")
            continue
        handle(request)


if __name__ == "__main__":
    main()
