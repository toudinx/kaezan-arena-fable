# Roadmap Web — Social / Instagram do Kaezan  🟡 Precisa GPT Image + seu tempo

> **Etiqueta: 🟡.** A saída é material de **post** (prompt de imagem + caption + hashtags) que
> **você** gera no GPT Image 2.0 e publica. Puxe num dia com tempo + gerador. Leia
> [CLAUDE_WEB.md](CLAUDE_WEB.md) antes.
>
> **Meta:** construir e alimentar o **Instagram oficial do Kaezan Arena Fable**, no espírito da
> página da **Wuthering Waves** — arte de personagem premium, lore atmosférica, anúncios de
> banner/evento e repost de comunidade.
>
> **Como disparar:** *"rode o prompt SO-NN do `docs_web/roadmap_web_social.md`"*.
> **Skill:** `kaeli-social-prompts`. **Lê:** README.md + roster_digest.md.
> **Escreve:** `docs_web/social/<arquivo>.md`.

## Voz e estética do perfil
- **Tom visual:** dark-fantasy premium, "Cathedral Ink + Aurum" (igual ao jogo). Feed coeso —
  fundos e acento de cada Kaeli vêm do `roster_digest.md`.
- **Voz das captions:** a personalidade da Kaeli em 1ª pessoa quando fizer sentido (ver digest);
  curta, com gancho na 1ª linha. PT primário + EN.
- **Formatos:** feed **1:1** e **4:5**; **carrossel** para reveals/lore; **reels/story 9:16** quando houver clipe.
  - Clipes em vídeo (reels/teaser) são produzidos no PC — ver `docs/roadmap_producao_visual.md`
    (Etapa 2, CUT-06) e o brief de movimento da skill `kaeli-motion-prompts`. Aqui o foco é imagem + copy.

## Pilares de conteúdo (o que postar)

| Pilar | O que é | Fonte no projeto | Frequência sug. |
|---|---|---|---|
| **Kaeli Spotlight** | hero art + 1 linha de lore + traço | roster_digest | 1×/semana |
| **Lore Drop** | cena atmosférica + fragmento de lore | roadmap_web_lore | 1×/semana |
| **Banner / Evento** | teaser → reveal → "no ar" | roadmap_web_marketing (copy) | por banner |
| **Skin Showcase** | skin sazonal da Kaeli | roadmap_web_skins | por skin |
| **Feature / Gameplay** | print/clip de dungeon, combate, gacha | README (features reais) | quando houver |
| **Comunidade / UGC** | repost de fanart, concursos | — | contínuo |
| **Bastidores** | concept, "making of", antes/depois (idle → arte final) | output/ + DESIGN_NOTES | 1×/quinzena |

## Sequência de lançamento (cold start, estilo WuWa pré-launch)
Uma conta nova ganha tração com uma narrativa, não com posts soltos. Ordem sugerida:

- [ ] **SO-L0 — Identidade do perfil** → `docs_web/social/perfil-setup.md`
  - Bio, @handle sugerido, foto de perfil (usar um thumb), destaque de cores, 1 frase de posicionamento. (Claude-only; não precisa de GPT.)
- [ ] **SO-L1 — Teaser do mundo** → `docs_web/social/launch-01-teaser.md`
  - 1–3 posts atmosféricos só de cenário (sem personagem), criando mistério. Usa os `bg-*` existentes.
- [ ] **SO-L2 — "Conheça as Kaelis" (série)** → `docs_web/social/launch-02-kaelis.md`
  - 1 Kaeli por post, na ordem que você quiser, cada uma com hero art + lore + traço. (7 posts.)
- [ ] **SO-L3 — Reveal de feature** → `docs_web/social/launch-03-features.md`
  - Caça/dungeon, gacha com pity, coleção — o que o README descreve, em 2–3 posts.
- [ ] **SO-L4 — Banner de estreia** → `docs_web/social/launch-04-banner.md`
  - Hype do primeiro banner (Velvet é a 5★ em destaque hoje). teaser → reveal → no ar.

## Posts recorrentes (depois do lançamento)
- [ ] **SO-01 — Kaeli do dia** → `docs_web/social/kaeli-do-dia-<slug>.md`
- [ ] **SO-02 — Lore drop** → `docs_web/social/lore-drop-<tema>.md` (puxa de `roadmap_web_lore`)
- [ ] **SO-03 — Evento sazonal** → `docs_web/social/evento-<tema>.md` (cruza com `roadmap_web_skins`)
- [ ] **SO-04 — Antes/depois (bastidores)** → `docs_web/social/bastidores-<tema>.md`
- [ ] **SO-NEW — Campanha avulsa** → `docs_web/social/<slug>-<campanha>.md`
  - Dispare com: *"rode SO-NEW: campanha `<tema>` com `<Kaeli(s)>`"*.

## Benchmark (rode quando quiser afinar a estratégia)
- [ ] **SO-BENCH — Mapear o IG da WuWa (ou outro gacha)** → `docs_web/research/ig-<jogo>.md`
  - Use a skill `franchise-mining` (modo livre) p/ destrinchar pilares, formatos, cadência e
    ganchos de copy do perfil de referência, e adaptar à nossa voz. (Vira ajuste deste roadmap.)

## Boas práticas (a skill aplica)
- Consistência de personagem via bloco de identidade do digest (rosto/cabelo/olhos travados).
- Caption: gancho → corpo curto → CTA; voz da Kaeli. Hashtags: 8–15, mix nicho + marca, sem spam.
- **Só nossas Kaelis** nas imagens — nunca IP de terceiros.
- Reaproveitar assets já prontos (`frontend/public/assets/kaelis/`, `output/upscaled/`) quando o
  post não pedir arte nova — economiza geração.
