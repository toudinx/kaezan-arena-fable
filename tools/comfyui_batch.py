#!/usr/bin/env python3
"""
comfyui_batch.py — Batch upscale via ComfyUI API

Sem pip install. O ComfyUI deve estar rodando em http://localhost:8188.

FILTROS COM --glob
  "*.png"        todas as PNGs
  "idle-*.png"   todos os idles
  "idle-1.png"   só o idle-1

SUBCOMANDOS
  upscale   Upscale 2x com Real-ESRGAN (ou outro modelo)
  run       Rodar qualquer workflow em API format
  restore   Restaurar backup dos originais
"""
from __future__ import annotations
import argparse, copy, json, pathlib, re, shutil, sys, time, uuid
import urllib.request, urllib.parse, urllib.error
from collections import defaultdict

# Garante UTF-8 no terminal Windows (evita UnicodeEncodeError com ->  etc.)
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# ── Config ────────────────────────────────────────────────────────────────
COMFY_URL = "http://localhost:8188"
CLIENT_ID  = str(uuid.uuid4())

DEFAULT_INPUT  = pathlib.Path("frontend/public/assets/kaelis")
DEFAULT_GLOB   = "idle-*.png"


# ── ComfyUI API ───────────────────────────────────────────────────────────
def _get(path: str) -> dict:
    with urllib.request.urlopen(f"{COMFY_URL}{path}", timeout=10) as r:
        return json.loads(r.read())

def _post(path: str, payload: dict) -> dict:
    data = json.dumps(payload).encode()
    req  = urllib.request.Request(
        f"{COMFY_URL}{path}", data=data,
        headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read())


def upload_image(img_path: pathlib.Path) -> str:
    """Faz upload da imagem para o ComfyUI/input. Retorna o nome armazenado."""
    bnd  = "ComfyBatch" + uuid.uuid4().hex
    data = img_path.read_bytes()
    body = (
        f"--{bnd}\r\n"
        f'Content-Disposition: form-data; name="image"; filename="{img_path.name}"\r\n'
        f"Content-Type: image/png\r\n\r\n"
    ).encode() + data + (
        f"\r\n--{bnd}\r\n"
        f'Content-Disposition: form-data; name="overwrite"\r\n\r\ntrue'
        f"\r\n--{bnd}--\r\n"
    ).encode()
    req = urllib.request.Request(
        f"{COMFY_URL}/upload/image", data=body,
        headers={"Content-Type": f"multipart/form-data; boundary={bnd}"},
        method="POST")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return json.loads(r.read())["name"]
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"Upload falhou (HTTP {e.code}): {body[:300]}")

def queue_prompt(workflow: dict) -> str:
    """Envia o workflow para a fila. Retorna o prompt_id."""
    data = json.dumps({"prompt": workflow, "client_id": CLIENT_ID}).encode()
    req  = urllib.request.Request(
        f"{COMFY_URL}/prompt", data=data,
        headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            res = json.loads(r.read())
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", errors="replace")
        try:
            parsed = json.loads(body)
            node_errs = parsed.get("node_errors", {})
            if node_errs:
                lines = []
                for nid, v in node_errs.items():
                    cls = v.get("class_type", "?")
                    for err in v.get("errors", []):
                        lines.append(f"  no {nid} ({cls}): {err.get('message','?')}")
                raise RuntimeError(
                    f"ComfyUI rejeitou o workflow (HTTP {e.code}):\n" + "\n".join(lines))
            errd = parsed.get("error", body[:400])
            if isinstance(errd, dict):
                errd = errd.get("message", str(errd))
            raise RuntimeError(f"ComfyUI HTTP {e.code}: {errd}")
        except (json.JSONDecodeError, AttributeError):
            raise RuntimeError(f"ComfyUI HTTP {e.code}: {body[:400]}")
    if "error" in res:
        raise RuntimeError(f"Erro ao enfileirar: {res['error']}")
    return res["prompt_id"]

def wait_done(prompt_id: str, timeout: float = 600.0) -> dict:
    """Aguarda o job terminar. Retorna o dict de saída do history."""
    deadline = time.time() + timeout
    dots = 0
    while time.time() < deadline:
        history = _get(f"/history/{prompt_id}")
        if prompt_id in history:
            job    = history[prompt_id]
            status = job.get("status", {})
            if status.get("completed"):
                return job
            if status.get("status_string") == "error":
                msgs = [m for msgs in status.get("messages", []) for m in msgs]
                raise RuntimeError("Job falhou: " + str(msgs))
        time.sleep(0.8)
        dots = (dots + 1) % 4
        print("." * (dots + 1) + "   \r", end="", flush=True)
    raise TimeoutError(f"Job {prompt_id} não terminou em {timeout:.0f}s")

def download_result(job: dict, dst: pathlib.Path):
    """Baixa a primeira imagem de saída do job e salva em dst."""
    for node_out in job.get("outputs", {}).values():
        for img in node_out.get("images", []):
            if img.get("type") == "output":
                params = urllib.parse.urlencode({
                    "filename": img["filename"],
                    "subfolder": img.get("subfolder", ""),
                    "type": "output",
                })
                dst.parent.mkdir(parents=True, exist_ok=True)
                with urllib.request.urlopen(f"{COMFY_URL}/view?{params}", timeout=60) as r:
                    dst.write_bytes(r.read())
                return
    raise ValueError("Nenhuma imagem de saída no job. Verifique se o workflow tem um nó SaveImage.")

# ── Workflow templates (API format) ───────────────────────────────────────
def _wf_upscale(scale_by: float = 0.5,
                model_name: str = "RealESRGAN_x4plus_anime_6B.pth") -> dict:
    """Real-ESRGAN 4x → ImageScaleBy(0.5) = resultado liquido 2x."""
    return {
        "1": {"class_type": "LoadImage",
              "inputs": {"image": "PLACEHOLDER", "upload": "image"}},
        "2": {"class_type": "UpscaleModelLoader",
              "inputs": {"model_name": model_name}},
        "3": {"class_type": "ImageUpscaleWithModel",
              "inputs": {"upscale_model": ["2", 0], "image": ["1", 0]}},
        "4": {"class_type": "ImageScaleBy",
              "inputs": {"image": ["3", 0], "upscale_method": "lanczos",
                         "scale_by": scale_by}},
        "5": {"class_type": "SaveImage",
              "inputs": {"images": ["4", 0], "filename_prefix": "_kzbatch"}},
    }


def _set_image(workflow: dict, filename: str):
    """Substitui o nome da imagem em todos os LoadImage do workflow."""
    for node in workflow.values():
        if isinstance(node, dict) and node.get("class_type") == "LoadImage":
            node["inputs"]["image"] = filename

# ── Backup ────────────────────────────────────────────────────────────────
BACKUP_SUBDIR = "_originais"
_VER_RE = re.compile(r"^(.+)-v(\d+)$")

def _next_backup_path(backup_dir: pathlib.Path, rel: pathlib.Path) -> pathlib.Path:
    """Retorna o próximo caminho versionado disponível: idle-1-v1.png, v2, ..."""
    parent = backup_dir / rel.parent
    v = 1
    while True:
        candidate = parent / f"{rel.stem}-v{v}{rel.suffix}"
        if not candidate.exists():
            return candidate
        v += 1

def backup_files(images: list[pathlib.Path], input_dir: pathlib.Path,
                 output_dir: pathlib.Path) -> pathlib.Path:
    """Copia originais para <output>/_originais/ com versionamento automático.

    Cada execução cria uma nova versão:
      idle-1-v1.png  (1ª vez)
      idle-1-v2.png  (2ª vez, sem sobrescrever a anterior)
    """
    backup_dir = output_dir / BACKUP_SUBDIR
    for src in images:
        rel = src.relative_to(input_dir)
        dst = _next_backup_path(backup_dir, rel)
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        print(f"    backup  {rel}  →  {dst.name}")
    print(f"  {len(images)} arquivo(s) em {backup_dir}\n")
    return backup_dir

# ── Batch processing ──────────────────────────────────────────────────────
def process_folder(
    workflow: dict,
    input_dir: pathlib.Path,
    glob: str,
    output_dir: pathlib.Path,
    *,
    backup: bool = False,
    dry_run: bool = False,
) -> list[pathlib.Path]:
    images = sorted(input_dir.rglob(glob))
    # Exclui a pasta _originais de runs anteriores
    images = [p for p in images if BACKUP_SUBDIR not in p.parts]

    if not images:
        print(f"  Nenhum arquivo '{glob}' em {input_dir}")
        return []

    if dry_run:
        print(f"  DRY RUN — {len(images)} arquivo(s) seriam processados:\n")
        for p in images:
            rel = p.relative_to(input_dir)
            dst = output_dir / rel.with_suffix(".png")
            print(f"    {rel}  →  {dst}")
        return []

    if backup:
        backup_files(images, input_dir, output_dir)
        print()

    ok_count = err_count = 0
    results: list[pathlib.Path] = []

    for i, src in enumerate(images, 1):
        rel = src.relative_to(input_dir)
        dst = output_dir / rel.with_suffix(".png")
        print(f"  [{i:2}/{len(images)}]  {rel}  →  {dst.name}  ", end="", flush=True)
        t0 = time.time()

        try:
            wf = copy.deepcopy(workflow)
            uploaded = upload_image(src)
            _set_image(wf, uploaded)
            pid = queue_prompt(wf)
            job = wait_done(pid)
            download_result(job, dst)
            print(f"✓  {time.time()-t0:.1f}s")
            results.append(dst)
            ok_count += 1
        except Exception as e:
            print(f"\n  ✗  ERRO: {e}")
            err_count += 1

    print(f"\n  Resultado: {ok_count} OK, {err_count} erros")
    return results

# ── CLI ───────────────────────────────────────────────────────────────────
def _common(sub, default_output: str):
    sub.add_argument("--input",   "-i", type=pathlib.Path, default=DEFAULT_INPUT)
    sub.add_argument("--glob",    "-g", default=DEFAULT_GLOB,
                     help='Filtro de arquivo. Ex: "idle-1.png", "idle-*.png", "*.png"')
    sub.add_argument("--output",  "-o", type=pathlib.Path,
                     default=pathlib.Path(default_output))
    sub.add_argument("--url",     default=COMFY_URL,
                     help="URL base do ComfyUI (default: http://localhost:8188)")
    sub.add_argument("--backup",  action="store_true",
                     help="Salva cópia dos originais em <output>/_originais/ antes de processar")
    sub.add_argument("--dry-run", action="store_true", dest="dry_run",
                     help="Lista o que seria processado sem executar nada")

def parse_args():
    p   = argparse.ArgumentParser(description="Batch ComfyUI — sem pip install")
    sub = p.add_subparsers(dest="mode", required=True)

    up = sub.add_parser("upscale", help="Upscale 2x com Real-ESRGAN")
    _common(up, "output/upscaled")
    up.add_argument("--scale", type=float, default=0.5,
                    help="Fator apos modelo 4x: 0.5=net 2x  1.0=net 4x  (default: 0.5)")
    up.add_argument("--upscale-model", default="RealESRGAN_x4plus_anime_6B.pth",
                    dest="upscale_model",
                    help="Nome do modelo em ComfyUI/models/upscale_models/")

    rn = sub.add_parser("run", help="Rodar qualquer workflow em API format")
    _common(rn, "output/resultado")
    rn.add_argument("--workflow", "-w", type=pathlib.Path, required=True,
                    help="Workflow JSON no formato API (ComfyUI Dev Mode → Save API Format)")

    rs = sub.add_parser("restore", help="Restaurar originais do backup (desfazer processamento)")
    rs.add_argument("--backup-dir",  type=pathlib.Path, required=True,
                    help="Pasta _originais gerada pelo --backup (ex: output/upscaled/_originais)")
    rs.add_argument("--restore-to",  type=pathlib.Path, required=True,
                    help="Pasta de destino (onde estavam os arquivos originais)")
    rs.add_argument("--version", "-v", type=int, default=0,
                    help="Versão específica a restaurar (ex: 2 restaura idle-1-v2.png). "
                         "Padrão: 0 = versão mais recente.")
    rs.add_argument("--dry-run", action="store_true", dest="dry_run",
                    help="Lista o que seria restaurado sem mover nada")
    rs.add_argument("--list", action="store_true", dest="list_versions",
                    help="Lista todas as versões disponíveis no backup sem restaurar nada")

    return p.parse_args()

def check_comfy(url: str):
    global COMFY_URL
    COMFY_URL = url
    try:
        _get("/system_stats")
    except Exception:
        print(f"\n  ERRO: ComfyUI não responde em {url}")
        print("  → Inicie o ComfyUI (python main.py) e tente novamente.\n")
        sys.exit(1)

def _scan_backup(backup_dir: pathlib.Path) -> dict:
    """Escaneia _originais/ e agrupa versões por caminho original.

    Retorna: {pathlib.Path(original_rel): [(version_int, backup_path), ...]}
    Versões estão ordenadas de menor para maior (v1 primeiro).
    """
    groups: dict = defaultdict(list)
    for src in sorted(backup_dir.rglob("*")):
        if not src.is_file():
            continue
        rel = src.relative_to(backup_dir)
        m   = _VER_RE.match(rel.stem)
        if not m:
            continue
        orig_rel = rel.parent / (m.group(1) + rel.suffix)
        groups[orig_rel].append((int(m.group(2)), src))
    for lst in groups.values():
        lst.sort(key=lambda x: x[0])
    return dict(groups)

def do_restore(backup_dir: pathlib.Path, restore_to: pathlib.Path,
               dry_run: bool, version: int = 0):
    """Restaura arquivos do backup para o local original.

    version=0  → restaura a versão mais recente de cada arquivo
    version=N  → restaura especificamente a vN
    """
    if not backup_dir.exists():
        print(f"  ERRO: pasta de backup não encontrada: {backup_dir}")
        return

    groups = _scan_backup(backup_dir)
    if not groups:
        print(f"  Nenhum backup versionado em {backup_dir}")
        print("  Os arquivos de backup devem ter o formato:  nome-v1.png, nome-v2.png …")
        return

    tag = "DRY RUN — " if dry_run else ""
    ver_label = f"v{version}" if version else "última versão"
    print(f"\n  {tag}Restaurando {len(groups)} arquivo(s) ({ver_label})"
          f"  →  {restore_to}\n")

    ok = err = 0
    for orig_rel, versions in sorted(groups.items()):
        if version:
            match = [(v, p) for v, p in versions if v == version]
            if not match:
                avail = [v for v, _ in versions]
                print(f"    ✗  {orig_rel}  — v{version} não existe  (disponíveis: v{', v'.join(str(v) for v in avail)})")
                err += 1
                continue
            ver_num, src = match[0]
        else:
            ver_num, src = versions[-1]   # mais recente

        dst = restore_to / orig_rel
        all_vers = ", ".join(f"v{v}" for v, _ in versions)
        mark = f"[{all_vers}  →  restaurando v{ver_num}]"
        print(f"    {orig_rel}  {mark}", end="  ")
        if dry_run:
            print("(simulado)")
        else:
            try:
                dst.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src, dst)
                print("✓")
                ok += 1
            except Exception as e:
                print(f"✗  {e}")
                err += 1

    if not dry_run:
        print(f"\n  Restauração: {ok} OK, {err} erros")
        print(f"  Backup preservado em: {backup_dir}")

def main():
    args = parse_args()

    # restore não precisa do ComfyUI
    if args.mode == "restore":
        if args.list_versions:
            groups = _scan_backup(args.backup_dir)
            if not groups:
                print(f"  Nenhum backup versionado em {args.backup_dir}")
            else:
                print(f"\n  Versões disponíveis em {args.backup_dir}:\n")
                for orig_rel, versions in sorted(groups.items()):
                    vers = "  ".join(f"v{v}" for v, _ in versions)
                    print(f"    {orig_rel}  [{vers}]")
                print()
        else:
            do_restore(args.backup_dir, args.restore_to, args.dry_run, args.version)
        return

    check_comfy(args.url)
    t_total = time.time()
    kw = dict(backup=args.backup, dry_run=args.dry_run)

    if args.dry_run:
        print(f"\n  DRY RUN — nenhuma imagem será processada.\n")

    if args.mode == "upscale":
        print(f"\n  Upscale ({args.upscale_model}) -- {args.input} -> {args.output}\n")
        process_folder(_wf_upscale(args.scale, args.upscale_model),
                       args.input, args.glob, args.output, **kw)

    elif args.mode == "run":
        wf_path: pathlib.Path = args.workflow
        if not wf_path.exists():
            print(f"  ERRO: workflow não encontrado: {wf_path}")
            sys.exit(1)
        workflow = json.loads(wf_path.read_text(encoding="utf-8"))
        print(f"\n  Workflow: {wf_path.name} — {args.input}  →  {args.output}\n")
        process_folder(workflow, args.input, args.glob, args.output, **kw)

    if not args.dry_run:
        print(f"\n  Tempo total: {time.time()-t_total:.1f}s\n")

if __name__ == "__main__":
    main()
