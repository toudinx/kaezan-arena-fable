#!/usr/bin/env python3
"""
comfyui_batch.py — Batch upscale/removebg via ComfyUI API

Sem pip install. O ComfyUI deve estar rodando em http://localhost:8188.

FILTROS COM --glob
  "*.png"        todas as PNGs
  "idle-*.png"   todos os idles
  "idle-1.png"   só o idle-1

SUBCOMANDOS
  batch        Pipeline completo por tipo de asset (inbox → upscaled)
  upscale      Upscale 2x com Real-ESRGAN (ou outro modelo)
  removebg     Remove fundo em lote (ISNet-anime via comfyui-art-venture)
  facerestore  Face detailer/restore em lote (Impact Pack FaceDetailer)
  skinvar      (IMG-07, experimental) Variante de skin via img2img + ControlNet + IPAdapter
  run          Rodar qualquer workflow em API format (JSON no formato API, não UI)
  restore      Restaurar backup dos originais
"""
from __future__ import annotations
import argparse, copy, fnmatch, json, pathlib, re, shutil, subprocess, sys, time, uuid
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

# ── Tipos de asset e config de pós-processo ───────────────────────────────
# Matriz completa em docs/roadmap_producao_visual.md (IMG-03)
ASSET_TYPES: dict[str, dict] = {
    "kaeli": {
        "upscale":            True,
        "removebg":           True,
        "face_restore":       True,
        "glob":               "*.png",
        "removebg_glob":      "idle-*.png",   # só idles precisam de fundo removido
        "face_restore_glob":  "idle-*.png",   # só idles têm rosto relevante
        "notes":              "idle-1/2/3 (transparente) + wallpaper/banner/thumb",
    },
    "item": {
        "upscale":       True,
        "removebg":      True,
        "glob":          "*.png",
        "removebg_glob": "*.png",
        "notes":         "ícones 1:1 transparentes",
    },
    "mob": {
        "upscale":            True,
        "removebg":           True,
        "face_restore":       True,
        "glob":               "*.png",
        "removebg_glob":      "*.png",
        "face_restore_glob":  "*.png",        # toda art de mob pode ter face
        "notes":              "arte de card/bestiary — não é sprite in-game",
    },
    "background": {
        "upscale":       True,
        "removebg":      False,
        "glob":          "*.png",
        "notes":         "backgrounds 16:9 / 9:16",
    },
    "logo": {
        "upscale":       True,
        "removebg":      True,
        "glob":          "*.png",
        "removebg_glob": "*.png",
        "notes":         "logos e badges transparentes",
    },
    "motion": {
        "upscale":       False,
        "removebg":      False,
        "glob":          "*.png",
        "notes":         "refs/frames para cutscenes — sem pós-processo automático",
    },
}


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
    # Remove chaves de metadados de topo (ex: "_comment") que não são nós. O ComfyUI
    # espera que TODO valor do prompt seja um nó com class_type; uma string solta no
    # topo derruba o parsing do /prompt com um HTTP 500 cru (TypeError não tratado).
    workflow = {k: v for k, v in workflow.items()
                if isinstance(v, dict) and "class_type" in v}
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

def _exec_error_msg(status: dict) -> str:
    """Extrai a mensagem de erro de execução do status do /history (se houver)."""
    for entry in status.get("messages", []):
        if isinstance(entry, list) and len(entry) == 2 and entry[0] == "execution_error":
            data = entry[1] or {}
            node = data.get("node_type", data.get("node_id", "?"))
            return f"{node}: {data.get('exception_message', data)}"
    return str(status.get("messages", status))

def _has_exec_error(status: dict) -> bool:
    return any(isinstance(e, list) and e and e[0] == "execution_error"
              for e in status.get("messages", []))

def wait_done(prompt_id: str, timeout: float = 600.0) -> dict:
    """Aguarda o job terminar. Retorna o dict de saída do history."""
    deadline = time.time() + timeout
    dots = 0
    while time.time() < deadline:
        history = _get(f"/history/{prompt_id}")
        if prompt_id in history:
            job    = history[prompt_id]
            status = job.get("status", {})
            # A chave do ComfyUI é "status_str" (não "status_string"); na falha o
            # /history traz completed=False + uma mensagem "execution_error". Checar
            # o erro ANTES de completed evita girar até o timeout num job que falhou.
            if status.get("status_str") == "error" or _has_exec_error(status):
                raise RuntimeError("Job falhou - " + _exec_error_msg(status))
            if status.get("completed"):
                return job
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

def download_video(job: dict, dst: pathlib.Path) -> str:
    """Baixa o primeiro vídeo de saída do job e salva em dst.

    Diferente do SaveImage (chave 'images'), o VHS_VideoCombine emite o resultado
    na chave 'gifs' (e, em versões novas, 'videos') do /history. Cada entrada traz
    filename/subfolder/type/format. Retorna o `format` reportado (ex: 'video/av1-webm').
    """
    for node_out in job.get("outputs", {}).values():
        for vid in node_out.get("gifs", []) + node_out.get("videos", []):
            if vid.get("type") != "output":
                continue
            params = urllib.parse.urlencode({
                "filename": vid["filename"],
                "subfolder": vid.get("subfolder", ""),
                "type": "output",
            })
            dst.parent.mkdir(parents=True, exist_ok=True)
            with urllib.request.urlopen(f"{COMFY_URL}/view?{params}", timeout=120) as r:
                dst.write_bytes(r.read())
            return vid.get("format", "")
    raise ValueError("Nenhum vídeo de saída no job. O workflow tem um VHS_VideoCombine "
                     "com save_output=true? (a saída vem na chave 'gifs'/'videos' do history)")

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


def _wf_removebg(model: str = "isnet-anime") -> dict:
    """Remove fundo via AV_RemoveBackground (comfyui-art-venture). Output: PNG com alpha.

    Pré-requisito: ComfyUI Manager → Install Custom Nodes → comfyui-art-venture (sipherxyz).
    """
    return {
        "1": {"class_type": "LoadImage",
              "inputs": {"image": "PLACEHOLDER", "upload": "image"}},
        "2": {"class_type": "AV_RemoveBackground",
              "inputs": {"images": ["1", 0], "model": model, "alpha_matting": False}},
        "3": {"class_type": "SaveImage",
              "inputs": {"images": ["2", 0], "filename_prefix": "_kzbatch_nobg"}},
    }


def _wf_face_restore(
    checkpoint: str = "v1-5-pruned-emaonly.ckpt",
    bbox_model: str = "face_yolov8m.pt",
    denoise: float = 0.35,
    steps: int = 20,
    cfg_scale: float = 7.0,
    sampler: str = "euler",
    scheduler: str = "normal",
    guide_size: int = 256,
) -> dict:
    """Face detailer via FaceDetailer (ComfyUI-Impact-Pack).

    Pré-requisitos:
    - ComfyUI Manager → ComfyUI-Impact-Pack (ltdrdata)
    - models/ultralytics/bbox/face_yolov8m.pt
    - Checkpoint em models/checkpoints/ (anime recomendado para Kaelis)

    Fluxo: LoadImage → FaceDetailer (detecta + inpaint face region) → SaveImage.
    Roda ANTES do removebg para que o fundo ainda esteja presente na detecção.
    """
    return {
        "1": {"class_type": "LoadImage",
              "inputs": {"image": "PLACEHOLDER", "upload": "image"}},
        "2": {"class_type": "CheckpointLoaderSimple",
              "inputs": {"ckpt_name": checkpoint}},
        "3": {"class_type": "CLIPTextEncode",
              "inputs": {"text": "anime face, detailed, sharp, high quality",
                         "clip": ["2", 1]}},
        "4": {"class_type": "CLIPTextEncode",
              "inputs": {"text": "blurry, low quality, artifacts, deformed",
                         "clip": ["2", 1]}},
        "5": {"class_type": "UltralyticsDetectorProvider",
              "inputs": {"model_name": bbox_model}},
        "6": {"class_type": "FaceDetailer",
              "inputs": {
                  "image":                    ["1", 0],
                  "model":                    ["2", 0],
                  "clip":                     ["2", 1],
                  "vae":                      ["2", 2],
                  "positive":                 ["3", 0],
                  "negative":                 ["4", 0],
                  "bbox_detector":            ["5", 0],
                  "guide_size":               guide_size,
                  "guide_size_for":           True,
                  "max_size":                 768,
                  "seed":                     42,
                  "steps":                    steps,
                  "cfg":                      cfg_scale,
                  "sampler_name":             sampler,
                  "scheduler":                scheduler,
                  "denoise":                  denoise,
                  "feather":                  5,
                  "noise_mask":               True,
                  "force_inpaint":            True,
                  "bbox_threshold":           0.5,
                  "bbox_dilation":            10,
                  "bbox_crop_factor":         3.0,
                  "sam_detection_hint":       "center-1",
                  "sam_dilation":             0,
                  "sam_threshold":            0.93,
                  "sam_bbox_expansion":       0,
                  "sam_mask_hint_threshold":  0.7,
                  "sam_mask_hint_use_negative": "False",
                  "drop_size":                10,
                  "refiner_ratio":            0.2,
                  "cycle":                    1,
                  "inpaint_model":            False,
                  "noise_mask_feather":       20,
              }},
        "7": {"class_type": "SaveImage",
              "inputs": {"images": ["6", 0], "filename_prefix": "_kzbatch_face"}},
    }


# ── IMG-07: Variante de skin (img2img + ControlNet + IPAdapter) ────────────
# EXPERIMENTAL. Pega o idle-1 como base e troca roupa/cenário mantendo a POSE
# (ControlNet) e o ROSTO/identidade (IPAdapter). Alternativa grátis ao GPT para
# skins. Só vale se a consistência ficar boa; senão, skins seguem no GPT.
#
# Defaults calibrados para o rig do projeto: ComfyUI SDXL, RTX 4070 8 GB.
# Checkpoints SDXL (anime/illustrious) + IPAdapter SDXL (plus / plus-face vit-h).
#
# Pré-requisitos:
#   - ComfyUI_IPAdapter_plus (cubiq)  → IPAdapterUnifiedLoader, IPAdapter  [JÁ instalado]
#   - CLIP-Vision vit-h em models/clip_vision/                            [precisa baixar]
#     (CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors — exigido pelo IPAdapter)
#   - ControlNet SDXL em models/controlnet/                               [1 presente]
#   - control_type "canny" usa o node CORE `Canny` (sem custom node). Os tipos
#     openpose/lineart/depth precisam do pack `comfyui_controlnet_aux` (Fannovel16).

CONTROL_TYPES: dict[str, dict] = {
    # Sem custom node — usa o `Canny` do core do ComfyUI. Funciona já.
    "canny": {
        "preproc":        "Canny",
        "preproc_inputs": {"low_threshold": 0.4, "high_threshold": 0.8},
        "model":          "diffusion_pytorch_model.safetensors",
        "note":           "bordas (core, sem dependência) — mantém o contorno",
    },
    # Os tipos abaixo exigem o pack comfyui_controlnet_aux (Fannovel16).
    "openpose": {
        "preproc":        "OpenposePreprocessor",
        "preproc_inputs": {"detect_hand": "enable", "detect_body": "enable",
                           "detect_face": "enable", "resolution": 1024},
        "model":          "diffusion_pytorch_model.safetensors",
        "note":           "só a pose (esqueleto) — melhor p/ TROCAR roupa [requer controlnet_aux]",
    },
    "lineart": {
        "preproc":        "LineArtPreprocessor",
        "preproc_inputs": {"coarse": "disable", "resolution": 1024},
        "model":          "diffusion_pytorch_model.safetensors",
        "note":           "contorno — mantém também o formato da roupa [requer controlnet_aux]",
    },
    "depth": {
        "preproc":        "MiDaS-DepthMapPreprocessor",
        "preproc_inputs": {"a": 6.28, "bg_threshold": 0.1, "resolution": 1024},
        "model":          "diffusion_pytorch_model.safetensors",
        "note":           "volume/forma 3D do corpo [requer controlnet_aux]",
    },
}

QUALITY_SUFFIX   = ", masterpiece, best quality, highly detailed, sharp focus"
DEFAULT_NEGATIVE = ("lowres, bad anatomy, bad hands, text, error, missing fingers, "
                    "extra digit, fewer digits, cropped, worst quality, low quality, "
                    "jpeg artifacts, signature, watermark, blurry, deformed face")


def _wf_skin_variant(
    prompt: str,
    *,
    negative: str = DEFAULT_NEGATIVE,
    checkpoint: str = "waiIllustriousSDXL_v160.safetensors",
    control_type: str = "canny",
    control_model: str | None = None,
    control_strength: float = 0.7,
    ipadapter_preset: str = "PLUS FACE (portraits)",
    ipadapter_weight: float = 0.8,
    denoise: float = 0.6,
    steps: int = 28,
    cfg_scale: float = 6.0,
    sampler: str = "dpmpp_2m",
    scheduler: str = "karras",
    seed: int = 0,
    use_ipadapter: bool = True,
    target_mp: float = 1.0,
    hires_scale: float = 0.0,
    hires_denoise: float = 0.35,
    hires_steps: int = 16,
) -> dict:
    """img2img a partir da base + ControlNet (pose) + IPAdapter (rosto).

    Fluxo: LoadImage → (resize p/ ~target_mp) → [preprocessor → ControlNet] guia a
    pose; [IPAdapter] injeta a identidade; VAEEncode + KSampler(denoise) trocam
    roupa/cenário segundo `prompt`. `denoise` controla o quanto muda.

    target_mp: redimensiona a base para ~N megapixels ANTES do img2img. SDXL é
    treinado em ~1 MP (1024²); rodar em 2-3k px estoura VRAM em 8 GB e gera
    artefato/repetição. 0 = sem resize (usa a resolução nativa). Reupscale depois
    pelo pipeline de upscale, se quiser resolução cheia.

    use_ipadapter=False pula a metade do rosto (rigs sem CLIP-Vision / smoke test).
    """
    ct       = CONTROL_TYPES.get(control_type, CONTROL_TYPES["canny"])
    cn_model = control_model or ct["model"]
    pos_text = (prompt or "").strip()
    if pos_text and QUALITY_SUFFIX not in pos_text:
        pos_text += QUALITY_SUFFIX

    # Fonte de imagem: resize p/ ~target_mp (SDXL-friendly) ou base nativa.
    img_src: list = ["1", 0]
    wf = {
        "1":  {"class_type": "LoadImage",
               "inputs": {"image": "PLACEHOLDER", "upload": "image"}},
        "2":  {"class_type": "CheckpointLoaderSimple",
               "inputs": {"ckpt_name": checkpoint}},
        "3":  {"class_type": "CLIPTextEncode",
               "inputs": {"text": pos_text, "clip": ["2", 1]}},
        "4":  {"class_type": "CLIPTextEncode",
               "inputs": {"text": negative, "clip": ["2", 1]}},
        "6":  {"class_type": "ControlNetLoader",
               "inputs": {"control_net_name": cn_model}},
        "7":  {"class_type": "ControlNetApplyAdvanced",
               "inputs": {"positive": ["3", 0], "negative": ["4", 0],
                          "control_net": ["6", 0], "image": ["5", 0],
                          "strength": control_strength,
                          "start_percent": 0.0, "end_percent": 1.0}},
        "11": {"class_type": "KSampler",
               "inputs": {"model": ["2", 0],
                          "positive": ["7", 0], "negative": ["7", 1],
                          "latent_image": ["10", 0],
                          "seed": seed, "steps": steps, "cfg": cfg_scale,
                          "sampler_name": sampler, "scheduler": scheduler,
                          "denoise": denoise}},
        "12": {"class_type": "VAEDecode",
               "inputs": {"samples": ["11", 0], "vae": ["2", 2]}},
        "13": {"class_type": "SaveImage",
               "inputs": {"images": ["12", 0], "filename_prefix": "_kzbatch_skin"}},
    }

    if target_mp and target_mp > 0:
        wf["14"] = {"class_type": "ImageScaleToTotalPixels",
                    "inputs": {"image": ["1", 0], "upscale_method": "lanczos",
                               "megapixels": target_mp, "resolution_steps": 1}}
        img_src = ["14", 0]

    # Preprocessor de estrutura + VAEEncode usam a fonte (já redimensionada).
    wf["5"]  = {"class_type": ct["preproc"],
                "inputs": {"image": img_src, **ct["preproc_inputs"]}}
    wf["10"] = {"class_type": "VAEEncode",
                "inputs": {"pixels": img_src, "vae": ["2", 2]}}

    if use_ipadapter:
        # Insere o IPAdapter entre o checkpoint e o KSampler (preserva o rosto).
        wf["8"] = {"class_type": "IPAdapterUnifiedLoader",
                   "inputs": {"model": ["2", 0], "preset": ipadapter_preset}}
        wf["9"] = {"class_type": "IPAdapter",
                   "inputs": {"model": ["8", 0], "ipadapter": ["8", 1],
                              "image": img_src, "weight": ipadapter_weight,
                              "start_at": 0.0, "end_at": 1.0,
                              "weight_type": "standard"}}
        wf["11"]["inputs"]["model"] = ["9", 0]

    # Hires-fix generativo (opcional): amplia ~hires_scale× e refina com img2img de
    # denoise baixo, tiled (8 GB-safe via TiledDiffusion + VAEEncode/DecodeTiled).
    # É o que ADICIONA detalhe real (renda/tecido/rosto); ESRGAN sozinho só suaviza.
    if hires_scale and hires_scale > 1.0:
        wf["15"] = {"class_type": "ImageScaleBy",
                    "inputs": {"image": ["12", 0], "upscale_method": "lanczos",
                               "scale_by": hires_scale}}
        wf["16"] = {"class_type": "VAEEncodeTiled",
                    "inputs": {"pixels": ["15", 0], "vae": ["2", 2],
                               "tile_size": 512, "overlap": 64,
                               "temporal_size": 64, "temporal_overlap": 8}}
        wf["17"] = {"class_type": "TiledDiffusion",
                    "inputs": {"model": ["2", 0], "method": "Mixture of Diffusers",
                               "tile_width": 768, "tile_height": 768,
                               "tile_overlap": 64, "tile_batch_size": 1}}
        wf["18"] = {"class_type": "KSampler",
                    "inputs": {"model": ["17", 0],
                               "positive": ["3", 0], "negative": ["4", 0],
                               "latent_image": ["16", 0],
                               "seed": seed, "steps": hires_steps, "cfg": cfg_scale,
                               "sampler_name": sampler, "scheduler": scheduler,
                               "denoise": hires_denoise}}
        wf["19"] = {"class_type": "VAEDecodeTiled",
                    "inputs": {"samples": ["18", 0], "vae": ["2", 2],
                               "tile_size": 512, "overlap": 64,
                               "temporal_size": 64, "temporal_overlap": 8}}
        wf["13"]["inputs"]["images"] = ["19", 0]   # salva o resultado refinado

    return wf


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
        try:
            rel = src.relative_to(input_dir)
        except ValueError:
            rel = pathlib.Path(src.name)
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
    skip_existing: bool = False,
    files: list[pathlib.Path] | None = None,
) -> list[pathlib.Path]:
    """Processa um lote de imagens com o workflow dado.

    files: se fornecido, usa essa lista em vez de descobrir via glob.
           Útil para limitar o removebg aos arquivos recém-upscalados.
    skip_existing: pula arquivos cujo destino já existe (idempotência).
    """
    if files is not None:
        images = [f for f in files if BACKUP_SUBDIR not in f.parts]
    else:
        images = sorted(input_dir.rglob(glob))
        images = [p for p in images if BACKUP_SUBDIR not in p.parts]

    if not images:
        print(f"  Nenhum arquivo '{glob}' em {input_dir}")
        return []

    if dry_run:
        print(f"  DRY RUN — {len(images)} arquivo(s) seriam processados:\n")
        for p in images:
            try:
                rel = p.relative_to(input_dir)
            except ValueError:
                rel = pathlib.Path(p.name)
            dst = output_dir / rel.with_suffix(".png")
            skip_tag = "  (SKIP — já existe)" if skip_existing and dst.exists() else ""
            print(f"    {rel}  →  {dst}{skip_tag}")
        return []

    if backup:
        backup_files(images, input_dir, output_dir)
        print()

    ok_count = err_count = skip_count = 0
    results: list[pathlib.Path] = []

    for i, src in enumerate(images, 1):
        try:
            rel = src.relative_to(input_dir)
        except ValueError:
            rel = pathlib.Path(src.name)
        dst = output_dir / rel.with_suffix(".png")

        if skip_existing and dst.exists():
            print(f"  [{i:2}/{len(images)}]  {rel}  →  SKIP (já existe)")
            skip_count += 1
            continue

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

    parts = [f"{ok_count} OK", f"{err_count} erros"]
    if skip_count:
        parts.append(f"{skip_count} pulados")
    print(f"\n  Resultado: {', '.join(parts)}")
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

    bt = sub.add_parser("batch",
                        help="Pipeline completo por tipo de asset (inbox → upscaled)")
    bt.add_argument("--type", "-t", choices=list(ASSET_TYPES), required=True,
                    dest="asset_type",
                    help="Tipo: " + ", ".join(ASSET_TYPES))
    bt.add_argument("--input",  "-i", type=pathlib.Path, default=None,
                    help="Entrada (default: output/inbox/<tipo>)")
    bt.add_argument("--output", "-o", type=pathlib.Path, default=None,
                    help="Saída (default: output/upscaled/<tipo>)")
    bt.add_argument("--glob",   "-g", default=None,
                    help="Filtro de arquivo (default: config do tipo)")
    bt.add_argument("--url",    default=COMFY_URL)
    bt.add_argument("--scale",  type=float, default=0.5)
    bt.add_argument("--upscale-model", default="RealESRGAN_x4plus_anime_6B.pth",
                    dest="upscale_model")
    bt.add_argument("--removebg-model", default="isnet-anime", dest="removebg_model")
    bt.add_argument("--no-upscale",  action="store_true", dest="no_upscale",
                    help="Desabilita upscale mesmo que o tipo use")
    bt.add_argument("--no-removebg", action="store_true", dest="no_removebg",
                    help="Desabilita removebg mesmo que o tipo use")
    bt.add_argument("--face-restore", action="store_true", dest="face_restore",
                    help="Ativa face detailer (FaceDetailer do Impact Pack) após o upscale")
    bt.add_argument("--face-restore-checkpoint", default="v1-5-pruned-emaonly.ckpt",
                    dest="face_restore_checkpoint",
                    help="Checkpoint para inpainting de rosto (anime model recomendado)")
    bt.add_argument("--face-restore-bbox-model", default="face_yolov8m.pt",
                    dest="face_restore_bbox_model",
                    help="Modelo de detecção de rosto em models/ultralytics/bbox/")
    bt.add_argument("--face-restore-denoise", type=float, default=0.35,
                    dest="face_restore_denoise",
                    help="Força do inpainting: 0.2=sutil, 0.5=forte (default: 0.35)")
    bt.add_argument("--face-restore-steps",  type=int,   default=20,
                    dest="face_restore_steps")
    bt.add_argument("--face-restore-cfg",    type=float, default=7.0,
                    dest="face_restore_cfg")
    bt.add_argument("--force",   action="store_true",
                    help="Reprocessa mesmo se o arquivo já existe no destino")
    bt.add_argument("--backup",  action="store_true")
    bt.add_argument("--dry-run", action="store_true", dest="dry_run")

    up = sub.add_parser("upscale", help="Upscale 2x com Real-ESRGAN")
    _common(up, "output/upscaled")
    up.add_argument("--scale", type=float, default=0.5,
                    help="Fator apos modelo 4x: 0.5=net 2x  1.0=net 4x  (default: 0.5)")
    up.add_argument("--upscale-model", default="RealESRGAN_x4plus_anime_6B.pth",
                    dest="upscale_model",
                    help="Nome do modelo em ComfyUI/models/upscale_models/")

    fr = sub.add_parser("facerestore",
                        help="Face detailer/restore em lote (Impact Pack FaceDetailer)")
    _common(fr, "output/upscaled")
    fr.add_argument("--checkpoint",  default="v1-5-pruned-emaonly.ckpt",
                    help="Checkpoint para inpainting (anime model recomendado)")
    fr.add_argument("--bbox-model",  default="face_yolov8m.pt", dest="bbox_model",
                    help="Modelo de detecção de rosto (models/ultralytics/bbox/)")
    fr.add_argument("--denoise",     type=float, default=0.35,
                    help="Força do inpainting: 0.2=sutil, 0.5=forte (default: 0.35)")
    fr.add_argument("--steps",       type=int,   default=20)
    fr.add_argument("--cfg",         type=float, default=7.0)

    sv = sub.add_parser(
        "skinvar",
        help="IMG-07 (experimental): variante de skin via img2img + ControlNet + IPAdapter")
    sv.add_argument("--prompt", "-p", required=True,
                    help='Nova roupa/cenário. Ex: "elegant red winter dress, snowy castle"')
    sv.add_argument("--input",  "-i", type=pathlib.Path,
                    default=pathlib.Path("output/upscaled/kaeli"),
                    help="Pasta com a imagem base (default: output/upscaled/kaeli)")
    sv.add_argument("--glob",   "-g", default="idle-1.png",
                    help="Imagem base (default: idle-1.png)")
    sv.add_argument("--output", "-o", type=pathlib.Path,
                    default=pathlib.Path("output/skins"),
                    help="Saída (default: output/skins)")
    sv.add_argument("--url", default=COMFY_URL)
    sv.add_argument("--name", default="skin",
                    help="Rótulo da variante no nome do arquivo (default: skin)")
    sv.add_argument("--count", type=int, default=2,
                    help="Quantas variações (seeds) gerar por base (default: 2)")
    sv.add_argument("--seed",  type=int, default=0,
                    help="Seed base (0 = derivada do tempo)")
    sv.add_argument("--negative", default=DEFAULT_NEGATIVE,
                    help="Prompt negativo (default: anti-artefatos anime)")
    sv.add_argument("--checkpoint", default="waiIllustriousSDXL_v160.safetensors",
                    help="Checkpoint base SDXL (anime/illustrious recomendado p/ Kaelis)")
    sv.add_argument("--control-type", choices=list(CONTROL_TYPES), default="canny",
                    dest="control_type",
                    help="Como preservar a estrutura: " + ", ".join(CONTROL_TYPES) +
                         " (canny = sem custom node)")
    sv.add_argument("--control-model", default=None, dest="control_model",
                    help="Override do modelo ControlNet (default: do --control-type)")
    sv.add_argument("--control-strength", type=float, default=0.7,
                    dest="control_strength", help="Força do ControlNet (default: 0.7)")
    sv.add_argument("--ipadapter-preset", default="PLUS FACE (portraits)",
                    dest="ipadapter_preset",
                    help="Preset do IPAdapterUnifiedLoader (default: PLUS FACE (portraits))")
    sv.add_argument("--ipadapter-weight", type=float, default=0.8,
                    dest="ipadapter_weight",
                    help="Peso da identidade (rosto). 0.5 solto, 0.9 fiel (default: 0.8)")
    sv.add_argument("--no-ipadapter", action="store_true", dest="no_ipadapter",
                    help="Pula o IPAdapter (rosto) — p/ rigs sem CLIP-Vision ou smoke test só de ControlNet")
    sv.add_argument("--denoise", type=float, default=0.6,
                    help="Quanto muda: 0.5 sutil, 0.75 agressivo (default: 0.6)")
    sv.add_argument("--max-mp", type=float, default=1.0, dest="max_mp",
                    help="Redimensiona a base p/ ~N megapixels antes do img2img "
                         "(SDXL ~1 MP; evita OOM/artefato em 8 GB). 0 = resolução nativa")
    sv.add_argument("--hires", type=float, default=0.0, dest="hires_scale",
                    help="Upscale GENERATIVO ~N× (hires-fix tiled, adiciona detalhe real). "
                         "Ex: 2.0. 0 = desligado (use o subcomando upscale p/ ampliar sem refinar)")
    sv.add_argument("--hires-denoise", type=float, default=0.35, dest="hires_denoise",
                    help="Denoise do passe de refino: 0.3 conservador, 0.45 reinventa mais (default 0.35)")
    sv.add_argument("--hires-steps", type=int, default=16, dest="hires_steps")
    sv.add_argument("--steps",   type=int,   default=28)
    sv.add_argument("--cfg",     type=float, default=6.0)
    sv.add_argument("--dry-run", action="store_true", dest="dry_run")

    rb = sub.add_parser("removebg", help="Remove fundo em lote (ISNet-anime via comfyui-art-venture)")
    _common(rb, "output/resultado")
    rb.add_argument("--model", default="isnet-anime",
                    help="Modelo: isnet-anime (padrão) | isnet_is | u2net | u2netp")

    rn = sub.add_parser("run", help="Rodar qualquer workflow JSON em API format")
    _common(rn, "output/resultado")
    rn.add_argument("--workflow", "-w", type=pathlib.Path, required=True,
                    help="Workflow JSON no formato API (ComfyUI Dev Mode → Save API Format)")

    il = sub.add_parser(
        "idleloop",
        help="CUT-03 (experimental): loop de idle premium via LivePortrait → .webm (VP9 alpha)")
    il.add_argument("--image", "-i", type=pathlib.Path, required=True,
                    help="Imagem-base (idle-1). Ex: frontend/public/assets/kaelis/velvet/idle-1.png")
    il.add_argument("--workflow", "-w", type=pathlib.Path,
                    default=pathlib.Path("tools/workflows/idle_loop_liveportrait.json"),
                    help="Workflow de referência (default: idle_loop_liveportrait.json)")
    il.add_argument("--slug", default=None,
                    help="Slug da Kaeli (default: nome da pasta da imagem)")
    il.add_argument("--driving", "-d", type=pathlib.Path, default=None,
                    help="Override do clipe de movimento (default: o do workflow, d0.mp4)")
    il.add_argument("--output", "-o", type=pathlib.Path, default=None,
                    help="Caminho do webm CRU do VHS (default: output/cutscenes/<slug>/idle-loop-raw.webm)")
    il.add_argument("--final", type=pathlib.Path, default=None,
                    help="Caminho do webm final VP9 (default: ao lado do raw, idle-loop.webm)")
    il.add_argument("--format", default=None,
                    help="Override do formato do VHS_VideoCombine. Ex: 'video/h264-mp4' "
                         "se o encode av1 do rig falhar (default: o do workflow)")
    il.add_argument("--fps", type=int, default=None,
                    help="Override do frame_rate do VHS_VideoCombine")
    il.add_argument("--crf", type=int, default=34,
                    help="CRF do VP9 final: menor=melhor/maior (default: 34)")
    il.add_argument("--no-pingpong", action="store_true", dest="no_pingpong",
                    help="Desliga o pingpong (vai-e-volta) do VHS")
    il.add_argument("--no-transcode", action="store_true", dest="no_transcode",
                    help="Não roda o ffmpeg; mantém o webm cru do VHS")
    il.add_argument("--timeout", type=float, default=1200.0,
                    help="Timeout do job em segundos (default: 1200 — 1ª run carrega modelos)")
    il.add_argument("--url", default=COMFY_URL)

    wb = sub.add_parser(
        "wanbust",
        help="CUT-03 ALT (experimental): busto vivo via Wan I2V (respiração/peito/cabelo) → .webm")
    wb.add_argument("--image", "-i", type=pathlib.Path, required=True,
                    help="Thumb (busto). Ex: frontend/public/assets/kaelis/velvet/thumb.png")
    wb.add_argument("--workflow", "-w", type=pathlib.Path,
                    default=pathlib.Path("tools/workflows/idle_bust_wan_i2v.json"),
                    help="Workflow de referência (default: idle_bust_wan_i2v.json)")
    wb.add_argument("--slug", default=None,
                    help="Slug da Kaeli (default: nome da pasta da imagem)")
    wb.add_argument("--prompt", "-p", default=None,
                    help="Override do prompt de movimento (default: o do workflow)")
    wb.add_argument("--negative", default=None,
                    help="Override do prompt negativo")
    wb.add_argument("--frames", type=int, default=None,
                    help="Nº de frames (8 GB: 49-81; default: o do workflow)")
    wb.add_argument("--width",  type=int, default=None,
                    help="Largura (8 GB: ~480-512; default: o do workflow)")
    wb.add_argument("--height", type=int, default=None,
                    help="Altura (default: o do workflow)")
    wb.add_argument("--steps",  type=int, default=None,
                    help="Steps do sampler (default: o do workflow)")
    wb.add_argument("--cfg",    type=float, default=None,
                    help="CFG do sampler (default: o do workflow)")
    wb.add_argument("--shift",  type=float, default=None,
                    help="Shift do sampler Wan (default: o do workflow)")
    wb.add_argument("--seed",   type=int, default=0,
                    help="Seed (0 = derivada do tempo)")
    wb.add_argument("--blocks-swap", type=int, default=None, dest="blocks_swap",
                    help="WanVideoBlockSwap blocks_to_swap (mais alto = menos VRAM, mais lento)")
    wb.add_argument("--output", "-o", type=pathlib.Path, default=None,
                    help="Caminho do vídeo CRU do VHS (default: output/cutscenes/<slug>/bust-raw.mp4)")
    wb.add_argument("--final", type=pathlib.Path, default=None,
                    help="Caminho do webm final VP9 (default: ao lado do raw, bust.webm)")
    wb.add_argument("--format", default=None,
                    help="Override do formato do VHS_VideoCombine (ex: 'video/av1-webm')")
    wb.add_argument("--fps", type=int, default=None,
                    help="Override do frame_rate do VHS (Wan2.1 é treinado a 16 fps)")
    wb.add_argument("--crf", type=int, default=34,
                    help="CRF do VP9 final: menor=melhor/maior (default: 34)")
    wb.add_argument("--no-pingpong", action="store_true", dest="no_pingpong",
                    help="Desliga o pingpong (vai-e-volta) do VHS")
    wb.add_argument("--no-transcode", action="store_true", dest="no_transcode",
                    help="Não roda o ffmpeg; mantém o vídeo cru do VHS")
    wb.add_argument("--timeout", type=float, default=1800.0,
                    help="Timeout do job em segundos (default: 1800 — Wan + 1ª run de modelos)")
    wb.add_argument("--url", default=COMFY_URL)

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

# ── Batch por tipo ────────────────────────────────────────────────────────
def do_batch(args):
    """Pipeline completo por tipo de asset: upscale → removebg conforme config."""
    cfg = ASSET_TYPES[args.asset_type]

    inbox_base    = pathlib.Path("output/inbox")
    upscaled_base = pathlib.Path("output/upscaled")

    input_dir  = args.input  if args.input  else (inbox_base    / args.asset_type)
    output_dir = args.output if args.output else (upscaled_base / args.asset_type)
    glob_pat   = args.glob   if args.glob   else cfg["glob"]

    do_upscale      = cfg["upscale"]               and not args.no_upscale
    do_removebg     = cfg.get("removebg", False)   and not args.no_removebg
    do_face_restore = cfg.get("face_restore", False) and getattr(args, "face_restore", False)
    rbg_glob = cfg.get("removebg_glob", "*.png")
    fr_glob  = cfg.get("face_restore_glob", "*.png")

    skip_existing = not args.force
    kw = dict(backup=args.backup, dry_run=args.dry_run)

    print(f"\n  Tipo     : {args.asset_type}  —  {cfg.get('notes', '')}")
    print(f"  Config   : upscale={'sim' if do_upscale else 'não'}"
          f"  face-restore={'sim' if do_face_restore else 'não'}"
          f"  removebg={'sim' if do_removebg else 'não'}")
    print(f"  Entrada  : {input_dir}")
    print(f"  Saída    : {output_dir}")
    print(f"  Glob     : {glob_pat}")
    if do_face_restore:
        ckpt = getattr(args, "face_restore_checkpoint", "v1-5-pruned-emaonly.ckpt")
        print(f"  FR glob  : {fr_glob}  checkpoint: {ckpt}")
    if do_removebg:
        print(f"  RBG glob : {rbg_glob}")
    idempotente = "não (--force)" if args.force else "sim (pula arquivos já existentes)"
    print(f"  Idempotente: {idempotente}\n")

    if not do_upscale and not do_face_restore and not do_removebg:
        print(f"  Tipo '{args.asset_type}' não tem pós-processo automático.")
        print(f"  Coloque os arquivos finais em {output_dir} manualmente.\n")
        return

    if not input_dir.exists():
        print(f"  AVISO: {input_dir} não existe.")
        print(f"  Crie a pasta e adicione os assets antes de rodar o batch.\n")
        return

    newly_upscaled: list[pathlib.Path] = []
    total_steps = ((1 if do_upscale else 0) + (1 if do_face_restore else 0)
                   + (1 if do_removebg else 0))
    current_step = 0

    if do_upscale:
        current_step += 1
        step_tag = f"{current_step}/{total_steps}"
        print(f"  ── Etapa {step_tag}: Upscale ({args.upscale_model})"
              f" — {input_dir} → {output_dir}\n")
        newly_upscaled = process_folder(
            _wf_upscale(args.scale, args.upscale_model),
            input_dir, glob_pat, output_dir,
            skip_existing=skip_existing, **kw,
        )

    # Face restore: roda depois do upscale, antes do removebg.
    # O fundo presente durante a detecção de rosto melhora a precisão do FaceDetailer.
    if do_face_restore:
        current_step += 1
        step_tag = f"{current_step}/{total_steps}"
        fr_checkpoint  = getattr(args, "face_restore_checkpoint", "v1-5-pruned-emaonly.ckpt")
        fr_bbox_model  = getattr(args, "face_restore_bbox_model",  "face_yolov8m.pt")
        fr_denoise     = getattr(args, "face_restore_denoise",      0.35)
        fr_steps       = getattr(args, "face_restore_steps",        20)
        fr_cfg         = getattr(args, "face_restore_cfg",          7.0)
        fr_src         = output_dir if do_upscale else input_dir
        print(f"\n  ── Etapa {step_tag}: Face Restore ({fr_checkpoint})"
              f" — glob: {fr_glob}\n")

        if do_upscale and skip_existing:
            fr_files = [p for p in newly_upscaled if fnmatch.fnmatch(p.name, fr_glob)]
            if not fr_files:
                print("  Nenhum arquivo novo para face restore. Pulando.\n")
            else:
                newly_fr = process_folder(
                    _wf_face_restore(fr_checkpoint, fr_bbox_model, fr_denoise,
                                     fr_steps, fr_cfg),
                    output_dir, fr_glob, output_dir,
                    skip_existing=False, files=fr_files, **kw,
                )
                if newly_fr:
                    newly_upscaled = newly_fr
        else:
            newly_fr = process_folder(
                _wf_face_restore(fr_checkpoint, fr_bbox_model, fr_denoise,
                                 fr_steps, fr_cfg),
                fr_src, fr_glob, output_dir,
                skip_existing=False, **kw,
            )
            if newly_fr:
                newly_upscaled = newly_fr

    if do_removebg:
        current_step += 1
        step_tag = f"{current_step}/{total_steps}"
        rbg_src = output_dir if do_upscale else input_dir
        print(f"\n  ── Etapa {step_tag}: Remove BG ({args.removebg_model})"
              f" — glob: {rbg_glob}\n")

        if do_upscale and skip_existing:
            # Só processa arquivos recém-criados pelo upscale que casam com rbg_glob
            rbg_files = [p for p in newly_upscaled if fnmatch.fnmatch(p.name, rbg_glob)]
            if not rbg_files:
                print(f"  Nenhum arquivo novo para removebg. Pulando.\n")
                return
            process_folder(
                _wf_removebg(args.removebg_model),
                output_dir, rbg_glob, output_dir,
                skip_existing=False, files=rbg_files, **kw,
            )
        else:
            process_folder(
                _wf_removebg(args.removebg_model),
                rbg_src, rbg_glob, output_dir,
                skip_existing=False, **kw,
            )

# ── IMG-07: Skin variant ──────────────────────────────────────────────────
def do_skinvar(args):
    """IMG-07 — gera variantes de skin via img2img + ControlNet + IPAdapter.

    Para cada imagem base (idle-1 por padrão) gera `count` variações com seeds
    incrementais, salvando como <stem>-<name>-<i>.png. EXPERIMENTAL: avaliar a
    consistência de rosto/pose antes de adotar (ver roadmap IMG-07).
    """
    input_dir  = args.input
    output_dir = args.output
    glob_pat   = args.glob
    name       = (args.name or "skin").strip().replace(" ", "-") or "skin"
    count      = max(1, args.count)
    base_seed  = args.seed if args.seed else int(time.time()) % 1_000_000

    images = sorted(input_dir.rglob(glob_pat))
    images = [p for p in images if BACKUP_SUBDIR not in p.parts]
    if not images:
        print(f"  Nenhum arquivo '{glob_pat}' em {input_dir}")
        return

    ct = CONTROL_TYPES.get(args.control_type, CONTROL_TYPES["canny"])
    cn_model = args.control_model or ct["model"]
    use_ip   = not getattr(args, "no_ipadapter", False)

    print(f"\n  IMG-07 — Variante de skin (EXPERIMENTAL)")
    print(f"  Prompt     : {args.prompt}")
    print(f"  Base       : {input_dir}  (glob: {glob_pat}, {len(images)} arquivo(s))")
    print(f"  Saída      : {output_dir}")
    print(f"  Checkpoint : {args.checkpoint}")
    print(f"  Control    : {args.control_type} ({ct['note']})")
    print(f"               model={cn_model}  strength={args.control_strength}")
    if use_ip:
        print(f"  IPAdapter  : {args.ipadapter_preset}  weight={args.ipadapter_weight}")
    else:
        print(f"  IPAdapter  : DESLIGADO (--no-ipadapter; só ControlNet)")
    print(f"  Sampler    : denoise={args.denoise}  steps={args.steps}  cfg={args.cfg}")
    mp = getattr(args, "max_mp", 1.0)
    print(f"  Resize     : {('~%.2f MP (SDXL-friendly)' % mp) if mp and mp > 0 else 'nativo (sem resize)'}")
    hs = getattr(args, "hires_scale", 0.0)
    if hs and hs > 1.0:
        print(f"  Hires-fix  : {hs:.2f}x generativo (tiled)  denoise={getattr(args,'hires_denoise',0.35)}")
    print(f"  Variações  : {count}  (seed base {base_seed})  rótulo '{name}'\n")

    def _dst(src: pathlib.Path, i: int) -> pathlib.Path:
        try:
            rel = src.relative_to(input_dir)
        except ValueError:
            rel = pathlib.Path(src.name)
        return output_dir / rel.parent / f"{src.stem}-{name}-{i}.png"

    if args.dry_run:
        print("  DRY RUN — nada será gerado:\n")
        for src in images:
            for i in range(1, count + 1):
                print(f"    {src.name}  →  {_dst(src, i)}  (seed {base_seed + i - 1})")
        return

    ok = err = 0
    for src in images:
        for i in range(1, count + 1):
            seed = base_seed + i - 1
            dst  = _dst(src, i)
            print(f"  {src.name}  →  {dst.name}  (seed {seed})  ", end="", flush=True)
            t0 = time.time()
            try:
                wf = _wf_skin_variant(
                    args.prompt, negative=args.negative, checkpoint=args.checkpoint,
                    control_type=args.control_type, control_model=args.control_model,
                    control_strength=args.control_strength,
                    ipadapter_preset=args.ipadapter_preset,
                    ipadapter_weight=args.ipadapter_weight,
                    denoise=args.denoise, steps=args.steps, cfg_scale=args.cfg,
                    seed=seed, use_ipadapter=use_ip,
                    target_mp=getattr(args, "max_mp", 1.0),
                    hires_scale=getattr(args, "hires_scale", 0.0),
                    hires_denoise=getattr(args, "hires_denoise", 0.35),
                    hires_steps=getattr(args, "hires_steps", 16),
                )
                uploaded = upload_image(src)
                _set_image(wf, uploaded)
                pid = queue_prompt(wf)
                job = wait_done(pid)
                download_result(job, dst)
                print(f"✓  {time.time()-t0:.1f}s")
                ok += 1
            except Exception as e:
                print(f"\n  ✗  ERRO: {e}")
                err += 1

    print(f"\n  Resultado: {ok} OK, {err} erros")
    print("  ⚠  Experimental: avalie consistência de rosto/pose. "
          "Se ficar fraco, mantenha skins no GPT (roadmap IMG-07).")


# ── CUT-03: Idle loop premium (LivePortrait → .webm) ──────────────────────
def _transcode_vp9(src: pathlib.Path, dst: pathlib.Path, crf: int = 34,
                   pix_fmt: str = "yuva420p") -> None:
    """Transcoda o vídeo do VHS para VP9 via ffmpeg do sistema.

    pix_fmt=yuva420p preserva alpha p/ a arte transparente sobrepor o bg-portrait
    (idle-loop do LivePortrait). pix_fmt=yuv420p para fontes RGB sem alpha — caso
    do Wan I2V (busto vivo), cujo decode não tem canal alpha.
    """
    if not shutil.which("ffmpeg"):
        raise RuntimeError("ffmpeg não encontrado no PATH. Instale (winget/Gyan) ou use --no-transcode.")
    dst.parent.mkdir(parents=True, exist_ok=True)
    cmd = ["ffmpeg", "-y", "-i", str(src),
           "-c:v", "libvpx-vp9", "-pix_fmt", pix_fmt,
           "-b:v", "0", "-crf", str(crf), "-an", str(dst)]
    res = subprocess.run(cmd, capture_output=True, text=True)
    if res.returncode != 0:
        tail = (res.stderr or "").strip().splitlines()[-8:]
        raise RuntimeError("ffmpeg falhou:\n  " + "\n  ".join(tail))


def do_idleloop(args):
    """CUT-03 — gera 1 loop de idle premium (LivePortrait) a partir do idle-1.

    Enfileira o workflow de referência (idle_loop_liveportrait.json), baixa o `.webm`
    do VHS_VideoCombine (chave 'gifs'/'videos' do history) e transcoda p/ VP9 yuva420p.
    Roda **um job por vez** — o LivePortrait + VideoCombine são pesados em 8 GB.
    """
    image: pathlib.Path = args.image
    if not image.exists():
        print(f"  ERRO: imagem-base não encontrada: {image}")
        sys.exit(1)

    wf_path: pathlib.Path = args.workflow
    if not wf_path.exists():
        print(f"  ERRO: workflow não encontrado: {wf_path}")
        sys.exit(1)
    workflow = json.loads(wf_path.read_text(encoding="utf-8"))
    # (queue_prompt remove chaves de metadados de topo como "_comment" antes de enviar.)

    slug    = (args.slug or image.parent.name or "kaeli").strip()
    raw_dst = args.output if args.output else pathlib.Path(f"output/cutscenes/{slug}/idle-loop-raw.webm")
    final   = (args.final if args.final
               else raw_dst.with_name("idle-loop.webm"))

    # Override de driving clip / formato / fps / crf / pingpong no workflow (sem
    # editar o arquivo de referência). Procura nós por class_type.
    for node in workflow.values():
        if not isinstance(node, dict):
            continue
        ct = node.get("class_type")
        if ct == "VHS_LoadVideoPath" and args.driving:
            node["inputs"]["video"] = str(args.driving)
        if ct == "VHS_VideoCombine":
            if args.format:
                node["inputs"]["format"] = args.format
            if args.fps:
                node["inputs"]["frame_rate"] = args.fps
            if args.no_pingpong:
                node["inputs"]["pingpong"] = False

    print(f"\n  CUT-03 — Idle loop premium (LivePortrait)  —  Kaeli '{slug}'")
    print(f"  Workflow   : {wf_path}")
    print(f"  Base       : {image}")
    drv = args.driving or "(driving clip do workflow)"
    print(f"  Driving    : {drv}")
    print(f"  Raw        : {raw_dst}")
    if not args.no_transcode:
        print(f"  Final (VP9): {final}  (yuva420p, crf {args.crf})")
    else:
        print(f"  Transcode  : DESLIGADO (--no-transcode; fica o webm cru do VHS)")
    print()

    t0 = time.time()
    print(f"  Enfileirando… (1ª run carrega os modelos LivePortrait — pode levar minutos)")
    uploaded = upload_image(image)
    _set_image(workflow, uploaded)
    pid = queue_prompt(workflow)
    job = wait_done(pid, timeout=args.timeout)
    fmt = download_video(job, raw_dst)
    print(f"  ✓ vídeo baixado ({fmt or 'formato desconhecido'})  {raw_dst}  [{time.time()-t0:.1f}s]")

    if not args.no_transcode:
        print(f"  Transcodando → VP9 yuva420p…")
        _transcode_vp9(raw_dst, final, args.crf)
        size_kb = final.stat().st_size / 1024
        print(f"  ✓ {final}  ({size_kb:.0f} KB)")
        print(f"\n  Próximo passo (integração, opt-in): copie para")
        print(f"    frontend/public/assets/kaelis/{slug}/idle-loop.webm")
        print(f"  e adicione \"idle-loop\" à lista dessa Kaeli em manifest.json (ver tools/README.md CUT-03).")

    print(f"\n  Tempo total: {time.time()-t0:.1f}s\n")


# ── CUT-03 ALT: Busto vivo (Wan I2V → .webm) ──────────────────────────────
def do_wanbust(args):
    """CUT-03 ALT — gera 1 clipe de 'busto vivo' (Wan I2V) a partir da THUMB.

    Diferente do LivePortrait (só rosto), o Wan I2V move peito/cabelo/respiração.
    Enfileira o workflow de referência (idle_bust_wan_i2v.json), faz override por
    class_type (prompt, frames, resolução, fps, seed, block-swap), baixa o vídeo do
    VHS_VideoCombine e transcoda p/ VP9 yuv420p (RGB, sem alpha — Wan não tem canal
    alpha). Roda **um job por vez**: Wan2.1 I2V é pesado em 8 GB.
    """
    image: pathlib.Path = args.image
    if not image.exists():
        print(f"  ERRO: thumb não encontrada: {image}")
        sys.exit(1)

    wf_path: pathlib.Path = args.workflow
    if not wf_path.exists():
        print(f"  ERRO: workflow não encontrado: {wf_path}")
        sys.exit(1)
    workflow = json.loads(wf_path.read_text(encoding="utf-8"))
    # Remove chaves de metadados de topo (_comment, _status, …) — não são nós.
    workflow = {k: v for k, v in workflow.items()
                if isinstance(v, dict) and "class_type" in v}

    slug    = (args.slug or image.parent.name or "kaeli").strip()
    raw_dst = args.output if args.output else pathlib.Path(f"output/cutscenes/{slug}/bust-raw.mp4")
    final   = (args.final if args.final
               else raw_dst.with_name("bust.webm"))

    # Override por class_type (tolerante a drift de schema do WanVideoWrapper).
    for node in workflow.values():
        if not isinstance(node, dict):
            continue
        ct = node.get("class_type")
        ins = node.get("inputs", {})
        if ct == "WanVideoTextEncode":
            if args.prompt:
                ins["positive_prompt"] = args.prompt
            if args.negative:
                ins["negative_prompt"] = args.negative
        elif ct == "WanVideoImageClipEncode":
            if args.frames:
                ins["num_frames"] = args.frames
            if args.width:
                ins["width"] = args.width
            if args.height:
                ins["height"] = args.height
        elif ct == "WanVideoSampler":
            if args.steps:
                ins["steps"] = args.steps
            if args.cfg is not None:
                ins["cfg"] = args.cfg
            if args.shift is not None:
                ins["shift"] = args.shift
            ins["seed"] = args.seed if args.seed else int(time.time()) % 1_000_000
        elif ct == "WanVideoBlockSwap":
            if args.blocks_swap is not None:
                ins["blocks_to_swap"] = args.blocks_swap
        elif ct == "VHS_VideoCombine":
            if args.format:
                ins["format"] = args.format
            if args.fps:
                ins["frame_rate"] = args.fps
            if args.no_pingpong:
                ins["pingpong"] = False

    print(f"\n  CUT-03 ALT — Busto vivo (Wan I2V)  —  Kaeli '{slug}'")
    print(f"  Workflow   : {wf_path}")
    print(f"  Thumb      : {image}")
    print(f"  Raw        : {raw_dst}")
    if not args.no_transcode:
        print(f"  Final (VP9): {final}  (yuv420p RGB, crf {args.crf})")
    else:
        print(f"  Transcode  : DESLIGADO (--no-transcode; fica o vídeo cru do VHS)")
    print()

    t0 = time.time()
    print(f"  Enfileirando… (1ª run carrega Wan2.1 + T5 — pode levar minutos em 8 GB)")
    uploaded = upload_image(image)
    _set_image(workflow, uploaded)
    pid = queue_prompt(workflow)
    job = wait_done(pid, timeout=args.timeout)
    fmt = download_video(job, raw_dst)
    print(f"  ✓ vídeo baixado ({fmt or 'formato desconhecido'})  {raw_dst}  [{time.time()-t0:.1f}s]")

    if not args.no_transcode:
        print(f"  Transcodando → VP9 yuv420p (RGB)…")
        _transcode_vp9(raw_dst, final, args.crf, pix_fmt="yuv420p")
        size_kb = final.stat().st_size / 1024
        print(f"  ✓ {final}  ({size_kb:.0f} KB)")
        print(f"\n  Saída é RGB (sem alpha). Pra compor sobre o bg-portrait, keyar por frame")
        print(f"  (a thumb tem fundo gradiente simples). Pra cena WW-style de summon, use direto.")

    print(f"\n  Tempo total: {time.time()-t0:.1f}s\n")


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

    # batch com tipo sem pós-processo não precisa do ComfyUI
    if args.mode == "batch":
        cfg = ASSET_TYPES[args.asset_type]
        needs_comfy = (
            (cfg["upscale"] and not args.no_upscale) or
            (cfg.get("face_restore", False) and getattr(args, "face_restore", False)) or
            (cfg.get("removebg", False) and not args.no_removebg)
        )
        if not needs_comfy:
            do_batch(args)
            return

    # skinvar em dry-run não precisa do ComfyUI
    if args.mode == "skinvar" and args.dry_run:
        do_skinvar(args)
        return

    check_comfy(args.url)
    t_total = time.time()

    # skinvar tem args próprios (sem --backup) — despacha antes do kw genérico
    if args.mode == "skinvar":
        do_skinvar(args)
        print(f"\n  Tempo total: {time.time()-t_total:.1f}s\n")
        return

    # idleloop (CUT-03) também tem args próprios e imprime seu próprio tempo
    if args.mode == "idleloop":
        do_idleloop(args)
        return

    # wanbust (CUT-03 ALT) — idem
    if args.mode == "wanbust":
        do_wanbust(args)
        return

    kw = dict(backup=args.backup, dry_run=args.dry_run)

    if args.dry_run:
        print(f"\n  DRY RUN — nenhuma imagem será processada.\n")

    if args.mode == "batch":
        do_batch(args)

    elif args.mode == "upscale":
        print(f"\n  Upscale ({args.upscale_model}) -- {args.input} -> {args.output}\n")
        process_folder(_wf_upscale(args.scale, args.upscale_model),
                       args.input, args.glob, args.output, **kw)

    elif args.mode == "facerestore":
        wf = _wf_face_restore(args.checkpoint, args.bbox_model, args.denoise,
                               args.steps, args.cfg)
        print(f"\n  Face Restore ({args.checkpoint}) -- {args.input}  →  {args.output}\n")
        process_folder(wf, args.input, args.glob, args.output, **kw)

    elif args.mode == "removebg":
        print(f"\n  Remove BG ({args.model}) -- {args.input} -> {args.output}\n")
        process_folder(_wf_removebg(args.model),
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
