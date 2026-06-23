#!/usr/bin/env python3
"""
validate_assets.py — Crop centralizado e validação de assets por tipo
IMG-04 · roadmap_producao_visual.md

Sem pip install extra — usa Pillow (já instalado junto do ComfyUI) para o crop.
A validação funciona sem Pillow (parse do header PNG via stdlib).

SUBCOMANDOS
  validate  Verifica proporção, alpha e set completo; reporta OK/erros por asset.
  crop      Crop centralizado p/ a proporção-alvo de cada tipo/arquivo.

USO
  python tools/validate_assets.py validate --type kaeli --dir output/upscaled/kaeli
  python tools/validate_assets.py validate --type item  --dir output/upscaled/item
  python tools/validate_assets.py crop     --type kaeli --input output/upscaled/kaeli
  python tools/validate_assets.py crop     --type kaeli --input output/upscaled/kaeli --dry-run
"""
from __future__ import annotations
import argparse, pathlib, struct, sys

try:
    from PIL import Image
    PIL_OK = True
except ImportError:
    PIL_OK = False

# ── Garante UTF-8 no terminal Windows ─────────────────────────────────────────
if sys.stdout and hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

# ── Tolerância de proporção ────────────────────────────────────────────────────
RATIO_TOL = 0.07  # ±7 %

# ── Regras de arquivo: kaelis (8 assets por slug) ──────────────────────────────
KAELI_FILE_RULES: dict[str, dict] = {
    "idle-1.png":       {"ratio": 2 / 3,  "needs_alpha": True},
    "idle-2.png":       {"ratio": 2 / 3,  "needs_alpha": True},
    "idle-3.png":       {"ratio": 2 / 3,  "needs_alpha": True},
    "wallpaper.png":    {"ratio": 16 / 9, "needs_alpha": False},
    "bg-landscape.png": {"ratio": 16 / 9, "needs_alpha": False},
    "bg-portrait.png":  {"ratio": 9 / 16, "needs_alpha": False},
    "banner.png":       {"ratio": 2 / 1,  "needs_alpha": False},
    "thumb.png":        {"ratio": 1 / 1,  "needs_alpha": False},
}
KAELI_REQUIRED = list(KAELI_FILE_RULES.keys())

# ── Regras por tipo de asset ───────────────────────────────────────────────────
# slug_mode: True  → cada subpasta é um slug (kaeli/<slug>/arquivo.png)
# slug_mode: False → arquivos direto na pasta  (item/*.png)
# "*" em file_rules → wildcard (aplica a qualquer nome de arquivo)
# ratio: float | list[float] | None
ASSET_RULES: dict[str, dict] = {
    "kaeli": {
        "slug_mode":      True,
        "required_files": KAELI_REQUIRED,
        "file_rules":     KAELI_FILE_RULES,
    },
    "item": {
        "slug_mode":  False,
        "file_rules": {"*": {"ratio": 1 / 1,       "needs_alpha": True}},
    },
    "mob": {
        "slug_mode":  False,
        "file_rules": {"*": {"ratio": None,         "needs_alpha": True}},
    },
    "background": {
        "slug_mode":  False,
        "file_rules": {"*": {"ratio": [16 / 9, 9 / 16], "needs_alpha": False}},
    },
    "logo": {
        "slug_mode":  False,
        "file_rules": {"*": {"ratio": None,         "needs_alpha": True}},
    },
    "motion": {
        "slug_mode":  False,
        "file_rules": {"*": {"ratio": None,         "needs_alpha": False}},
    },
}

# ── Parse de header PNG (sem PIL) ─────────────────────────────────────────────
def _png_info(path: pathlib.Path) -> tuple[int, int, bool]:
    """Retorna (largura, altura, tem_alpha) lendo só o header PNG (stdlib)."""
    with open(path, "rb") as f:
        if f.read(8) != b"\x89PNG\r\n\x1a\n":
            raise ValueError("não é um PNG válido")
        f.read(4)  # tamanho do chunk IHDR
        if f.read(4) != b"IHDR":
            raise ValueError("chunk IHDR ausente")
        w, h = struct.unpack(">II", f.read(8))
        f.read(1)  # bit depth
        color_type = struct.unpack("B", f.read(1))[0]
        # 0=gray 2=RGB 3=indexed 4=gray+A 6=RGBA
        has_alpha = color_type in (4, 6)
    return w, h, has_alpha

# ── Helpers ────────────────────────────────────────────────────────────────────
def _ratio_ok(w: int, h: int, target) -> bool:
    if target is None:
        return True
    actual = w / h
    targets = target if isinstance(target, list) else [target]
    return any(abs(actual - t) / t <= RATIO_TOL for t in targets)

_KNOWN_RATIOS = [(16, 9), (9, 16), (2, 3), (3, 2), (2, 1), (1, 1), (4, 3), (3, 4)]

def _one_ratio(r: float) -> str:
    for n, d in _KNOWN_RATIOS:
        if abs(r - n / d) < 0.01:
            return f"{n}:{d}"
    return f"{r:.3f}"

def _ratio_label(target) -> str:
    if target is None:
        return "qualquer"
    targets = target if isinstance(target, list) else [target]
    return " ou ".join(_one_ratio(t) for t in targets)

def _check_file(path: pathlib.Path, rule: dict) -> list[str]:
    """Verifica um PNG contra as regras. Retorna lista de erros (vazia = OK)."""
    try:
        w, h, has_alpha = _png_info(path)
    except Exception as e:
        return [f"leitura falhou: {e}"]

    errs: list[str] = []
    if not _ratio_ok(w, h, rule.get("ratio")):
        errs.append(
            f"proporção {w}×{h} ({w/h:.3f}) ≠ {_ratio_label(rule['ratio'])} "
            f"(tol ±{RATIO_TOL*100:.0f}%)"
        )
    if rule.get("needs_alpha") and not has_alpha:
        errs.append("canal alpha ausente (esperado PNG transparente)")
    return errs

# ── Subcomando: validate ───────────────────────────────────────────────────────
def do_validate(args) -> int:
    target_dir = pathlib.Path(args.dir)
    cfg        = ASSET_RULES[args.type]
    slug_mode  = cfg.get("slug_mode", False)
    file_rules = cfg["file_rules"]
    required   = cfg.get("required_files", [])

    if not target_dir.exists():
        print(f"\n  ERRO: pasta não encontrada: {target_dir}\n")
        return 1

    print(f"\n  Validando [{args.type}]  {target_dir}\n")

    ok_count = err_count = miss_count = 0

    if slug_mode:
        slugs = sorted(
            d for d in target_dir.iterdir()
            if d.is_dir() and not d.name.startswith("_")
        )
        if not slugs:
            print(f"  AVISO: nenhum slug encontrado em {target_dir}")
            return 0

        for slug_dir in slugs:
            print(f"  ── {slug_dir.name}")

            for fname in required:
                fpath = slug_dir / fname
                rule  = file_rules.get(fname) or file_rules.get("*", {})

                if not fpath.exists():
                    print(f"      ✗  {fname:<22}  AUSENTE")
                    miss_count += 1
                    continue

                errs = _check_file(fpath, rule)
                if errs:
                    for e in errs:
                        print(f"      ✗  {fname:<22}  {e}")
                    err_count += 1
                else:
                    w, h, _ = _png_info(fpath)
                    print(f"      ✓  {fname:<22}  {w}×{h}")
                    ok_count += 1

            extras = [f for f in slug_dir.glob("*.png") if f.name not in required]
            for ex in sorted(extras):
                print(f"      -  {ex.name:<22}  (extra, não validado)")
            print()

    else:
        pngs = sorted(target_dir.glob("*.png"))
        if not pngs:
            print(f"  AVISO: nenhum PNG em {target_dir}")
            return 0

        wildcard = file_rules.get("*", {})
        for fpath in pngs:
            rule = file_rules.get(fpath.name, wildcard)
            errs = _check_file(fpath, rule)
            if errs:
                for e in errs:
                    print(f"  ✗  {fpath.name:<26}  {e}")
                err_count += 1
            else:
                w, h, _ = _png_info(fpath)
                print(f"  ✓  {fpath.name:<26}  {w}×{h}")
                ok_count += 1

    parts = ([f"{ok_count} OK"] if ok_count else []) + \
            ([f"{err_count} com erros"] if err_count else []) + \
            ([f"{miss_count} ausentes"] if miss_count else [])
    has_errors = err_count > 0 or miss_count > 0
    print(f"  Resultado: {', '.join(parts) or 'nada validado'}\n")
    return 1 if has_errors else 0

# ── Subcomando: crop ───────────────────────────────────────────────────────────
def _center_crop(img: "Image.Image", ratio: float) -> "Image.Image":
    """Recorta ao centro para atingir proporção W/H = ratio."""
    w, h = img.size
    if abs(w / h - ratio) / ratio <= RATIO_TOL:
        return img  # já dentro da tolerância
    if w / h > ratio:   # muito larga → cortar laterais
        nw   = int(h * ratio)
        left = (w - nw) // 2
        return img.crop((left, 0, left + nw, h))
    else:               # muito alta → cortar topo/base
        nh  = int(w / ratio)
        top = (h - nh) // 2
        return img.crop((0, top, w, top + nh))

def do_crop(args) -> int:
    if not PIL_OK:
        print("\n  ERRO: Pillow não encontrado.")
        print("  Ative o venv do ComfyUI ou: pip install pillow\n")
        return 1

    input_dir  = pathlib.Path(args.input)
    output_dir = pathlib.Path(args.output) if args.output else input_dir
    cfg        = ASSET_RULES[args.type]
    slug_mode  = cfg.get("slug_mode", False)
    file_rules = cfg["file_rules"]
    dry_run    = args.dry_run
    force      = args.force

    if not input_dir.exists():
        print(f"\n  ERRO: pasta não encontrada: {input_dir}\n")
        return 1

    tag = "DRY RUN — " if dry_run else ""
    print(f"\n  {tag}Crop [{args.type}]  {input_dir}  →  {output_dir}\n")

    ok = skip = err = 0

    def _process(src: pathlib.Path, dst: pathlib.Path, ratio: float):
        nonlocal ok, skip, err
        try:
            with Image.open(src) as img:
                w, h    = img.size
                cropped = _center_crop(img, ratio)
                cw, ch  = cropped.size
                label   = _one_ratio(ratio)
                already = (cw == w and ch == h)

                if not force and dst.exists() and already:
                    print(f"  SKIP  {src.name}  ({w}×{h} já correto)")
                    skip += 1
                    return

                if already:
                    print(f"  OK    {src.name}  ({w}×{h} já correto — sem crop)")
                else:
                    print(f"  CROP  {src.name}  {w}×{h} → {cw}×{ch}  ({label})")

                if not dry_run:
                    dst.parent.mkdir(parents=True, exist_ok=True)
                    cropped.save(dst, "PNG")
                ok += 1
        except Exception as e:
            print(f"  ERRO  {src.name}  —  {e}")
            err += 1

    if slug_mode:
        slugs = sorted(
            d for d in input_dir.iterdir()
            if d.is_dir() and not d.name.startswith("_")
        )
        for slug_dir in slugs:
            print(f"  ── {slug_dir.name}")
            for fname, rule in file_rules.items():
                if fname == "*":
                    continue
                ratio = rule.get("ratio")
                if ratio is None or isinstance(ratio, list):
                    continue
                src = slug_dir / fname
                if not src.exists():
                    continue
                dst = (output_dir / slug_dir.name / fname
                       if output_dir != input_dir else src)
                _process(src, dst, ratio)
            print()
    else:
        wildcard = file_rules.get("*", {})
        pngs = sorted(input_dir.glob("*.png"))
        for src in pngs:
            rule  = file_rules.get(src.name, wildcard)
            ratio = rule.get("ratio")
            if ratio is None or isinstance(ratio, list):
                print(f"  SKIP  {src.name}  (proporção variável — sem crop automático)")
                skip += 1
                continue
            dst = output_dir / src.name
            _process(src, dst, ratio)

    parts = ([f"{ok} processados"] if ok else []) + \
            ([f"{skip} pulados"] if skip else []) + \
            ([f"{err} erros"] if err else [])
    print(f"\n  Resultado: {', '.join(parts) or 'nada processado'}\n")
    return 1 if err else 0

# ── CLI ────────────────────────────────────────────────────────────────────────
def _parse():
    p = argparse.ArgumentParser(
        description="validate_assets — validação e crop de assets por tipo (IMG-04)")
    sub = p.add_subparsers(dest="mode", required=True)

    v = sub.add_parser("validate", help="Verifica proporção, alpha e set completo")
    v.add_argument("--type", "-t", choices=list(ASSET_RULES), required=True,
                   help="Tipo de asset: " + " | ".join(ASSET_RULES))
    v.add_argument("--dir",  "-d", type=pathlib.Path,
                   help="Pasta a validar (default: output/upscaled/<tipo>)")

    c = sub.add_parser("crop", help="Crop centralizado p/ a proporção-alvo")
    c.add_argument("--type",    "-t", choices=list(ASSET_RULES), required=True)
    c.add_argument("--input",   "-i", type=pathlib.Path,
                   help="Pasta de entrada (default: output/upscaled/<tipo>)")
    c.add_argument("--output",  "-o", type=pathlib.Path, default=None,
                   help="Pasta de saída (default: sobrescreve a entrada)")
    c.add_argument("--dry-run", action="store_true", dest="dry_run",
                   help="Mostra o que seria cropado sem salvar")
    c.add_argument("--force",   action="store_true",
                   help="Recorta mesmo que o destino já exista com proporção correta")

    return p.parse_args()

def main():
    args = _parse()
    base = pathlib.Path("output/upscaled")
    if args.mode == "validate" and not args.dir:
        args.dir = base / args.type
    elif args.mode == "crop" and not args.input:
        args.input = base / args.type
    sys.exit(do_validate(args) if args.mode == "validate" else do_crop(args))

if __name__ == "__main__":
    main()
