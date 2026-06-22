# roster_digest — Snapshot do roster (para o web)

> **O que é.** Um espelho **read-only** dos metadados das Kaelis jogáveis, para o Claude Code Web
> não precisar ler `backend/.../Domain/Waifus.cs` (código). Skins/social/lore puxam identidade
> daqui. **Fonte de verdade dos metadados:** `Domain/Waifus.cs`; **da identidade visual:** os
> assets em `frontend/public/assets/kaelis/<slug>/`. Quando o roster ou a arte mudar, atualize
> este arquivo **no desktop**.
>
> **Estado dos blocos de identidade:** as 7 Kaelis têm arte em uso, e os blocos abaixo foram
> escritos **a partir das imagens reais** (idle + thumb). Cole o bloco **sem alterar** no topo dos
> prompts da skill `kaeli-asset-prompts` / `kaeli-social-prompts`. No **modo skin**, preserve só
> rosto/cabelo/olhos/raça do bloco e troque roupa+cenário.

Roster atual: **7 Kaelis, todas 5★**, fechando a matriz elemental. Papel é o eixo mecânico
(Mage / Archer / Knight). IDs `waifu:*` e `skin:*` são **estáveis** — nunca renomear.

---

## waifu:eloa — Eloa · "Serafim do Julgamento"
- **Elemento:** holy · **Raridade:** 5★ · **Papel:** Mage · **Arma (cosmético):** wand
- **Acento:** ouro + rosa · **Mood:** dark-holy, etéreo, solene (anjo sombrio, não alvorada clara)
- **Personalidade:** solene, gentil sem ser mole, paciente; julga sem ódio ("deixar terminar").
- **Bloco de identidade (das imagens):**
  ```
  Using this character as reference, keep her exact design:
  very long straight black hair, glowing pink eyes, dark-angel outfit — a white-and-gold filigree
  corset bodice over a layered white/pink/black ruffled skirt, black opera gloves, black thigh-high
  boots; a golden thorned halo and braided gold hair ornaments; large black-and-white feathered wings.
  ```
- **Cenário ancorado:** catedral/bosque sombrio com luz rosada pálida entre arcos altos, motivos
  de espinho dourado, penas e partículas douradas à deriva.
- **Skins in-game:** Serafim do Julgamento (default) · Manto da Absolvição · Vigília do Crepúsculo.

## waifu:seren — Seren · "Cavaleira Astral"
- **Elemento:** physical · **Raridade:** 5★ · **Papel:** Knight · **Arma:** melee
- **Acento:** azul + ouro/prata · **Mood:** marcial, astral, preciso
- **Personalidade:** disciplinada ao ponto da teimosia, formal, calorosa só quando baixa a guarda.
- **Bloco de identidade (das imagens):**
  ```
  Using this character as reference, keep her exact design:
  very long silver-white hair in a high ponytail, blue eyes, blue-and-white plate armor with gold
  trim and star motifs over a fitted breastplate, white thigh-high stockings, silver armored
  knee-high boots, a flowing blue star-patterned cape with red lining.
  ```
- **Cenário ancorado:** salão de duelos sob céu estrelado, constelações vivas, piso de pedra
  polida refletindo as estrelas.
- **Skins in-game:** Cavaleira Astral (default) · Vanguarda do Zênite · Lâmina do Eclipse.

## waifu:velvet — Velvet · "Arauto do Pesadelo"
- **Elemento:** death · **Raridade:** 5★ · **Papel:** Mage · **Arma:** wand
- **Acento:** roxo · **Mood:** dark, ethereal, melancholic
- **Personalidade:** voz baixa, cortesia antiga, humor de lápide; sabe seu nome antes de você dizer.
- **Bloco de identidade (das imagens):**
  ```
  Using this character as reference, keep her exact design:
  very long dark purple hair, glowing red eyes, black-and-purple gothic lolita dress with
  lace and ruffles, purple flower hair ornament and cat-ear tiara crown, black thigh-high
  stockings, black lace heels.
  ```
- **Cenário ancorado:** catedral gótica noturna — lustres de cristal roxo, correntes, runas roxas
  no chão, vitrais violeta, névoa rasa.
- **Skins in-game:** Vestes do Lago Negro (default) · Pesadelo Carmesim · Irmandade do Abismo.

## waifu:rin — Rin · "Súcubus do Pacto"
- **Elemento:** fire · **Raridade:** 5★ · **Papel:** Mage · **Arma:** wand
- **Acento:** vermelho + preto + ouro · **Mood:** quente, charmoso, perigoso-elegante
- **Personalidade:** provocadora, espirituosa, leal de um jeito inesperado; cumpre a palavra; pede consentimento.
- **Bloco de identidade (das imagens):**
  ```
  Using this character as reference, keep her exact design:
  succubus with very long wavy crimson-red hair, glowing pink-magenta eyes, pointed ears, curved
  black spiky demon horns ringed in gold, large bat wings, and a long demon tail with a heart-shaped
  tip; black dress with gold trim and black lace, a pink heart-gem choker and gold chains, black
  thigh-high boots.
  ```
- **Cenário ancorado:** salão gótico vermelho à luz de chamas, corações rosa flutuando, filigrana
  dourada, brilho carmesim quente.
- **Skins in-game:** Súcubus do Pacto (default) · Selo do Contrato · Asas de Cinza.

## waifu:rynna — Rynna · "Dragoa do Trovão"
- **Elemento:** energy · **Raridade:** 5★ · **Papel:** Knight · **Arma:** melee
- **Acento:** azul-profundo + violeta + ouro (relâmpago roxo) · **Mood:** tempestade, elétrico, imponente
- **Personalidade:** impetuosa, barulhenta, generosa com lealdade e mesquinha com paciência; engaja primeiro.
- **Bloco de identidade (das imagens):**
  ```
  Using this character as reference, keep her exact design:
  dark-skinned dragon-girl with very long electric-blue hair, glowing violet eyes, pointed ears,
  ridged blue dragon horns ringed in gold, blue scale patches on her skin, large purple membranous
  dragon wings crackling with violet lightning, and a long scaled dragon tail; a deep-blue and gold
  armored bodysuit with filigree and gold-trimmed blue thigh-high armored boots.
  ```
- **Cenário ancorado:** céu de tempestade sobre um círculo mágico brilhante, arcos de relâmpago
  violeta, nuvens carregadas.
- **Skins in-game:** Dragoa do Trovão (default) · Fúria da Tempestade · Forjada no Céu.

## waifu:lunara — Lunara · "Lebre Lunar"
- **Elemento:** ice · **Raridade:** 5★ · **Papel:** Archer · **Arma:** bow
- **Acento:** lavanda + azul-pálido + prata · **Mood:** luar, frio, gracioso
- **Personalidade:** brincalhona, esquiva, melancólica nas horas quietas; foge da pergunta e volta com a resposta.
- **Bloco de identidade (das imagens):**
  ```
  Using this character as reference, keep her exact design:
  moon-hare girl with long silver-lavender hair, blue eyes, and large white rabbit ears; a golden
  crescent-moon hairpin; an elegant white-and-lavender gown with sheer flowing layers, blue ribbons,
  bell-and-bead ornaments and crescent/star motifs, white thigh-high stockings, dainty heeled shoes.
  ```
- **Cenário ancorado:** jardim noturno ao luar sob uma grande lua crescente, neve e gelo fino
  brilhando, luz estelar lavanda.
- **Skins in-game:** Lebre Lunar (default) · Dança do Crescente · Véu da Lua Nova.

## waifu:gaia — Gaia · "Arqueira dos Monólitos"
- **Elemento:** earth · **Raridade:** 5★ · **Papel:** Archer · **Arma:** bow
- **Acento:** verde-oliva/floresta + bronze/ouro · **Mood:** telúrico, sólido, paciente
- **Personalidade:** paciente como rocha, econômica nas palavras, certeira; espera o tiro certo a vida toda.
- **Bloco de identidade (das imagens):**
  ```
  Using this character as reference, keep her exact design:
  dark-skinned ranger with very long wavy black hair in side braids decorated with feathers and
  beads, green eyes, a green face-paint stripe under one eye, feather earrings; an olive-green
  leather outfit with gold ornaments and bronze jewelry, a mossy hooded cloak/wrap, fingerless
  wraps, green thigh-highs with garter straps, and tall laced leather boots.
  ```
- **Cenário ancorado:** planalto de menires ao entardecer, torres de pedras empilhadas em
  equilíbrio, veios de quartzo, luz dourada-esverdeada quente.
- **Skins in-game:** Arqueira dos Monólitos (default) · Guarda da Rocha-Mãe · Veio de Quartzo.

---

## Matriz elemental (referência rápida)

| Elemento | Kaeli | Papel | Raça/silhueta | Cabelo / olhos |
|---|---|---|---|---|
| holy | Eloa | Mage | anjo sombrio (asas) | preto / rosa |
| physical | Seren | Knight | humana (cavaleira) | prata-branco / azul |
| death | Velvet | Mage | humana (gótica) | roxo escuro / vermelho |
| fire | Rin | Mage | súcubus (chifres/asas/cauda) | vermelho / rosa-magenta |
| energy | Rynna | Knight | dragoa, pele escura | azul-elétrico / violeta |
| ice | Lunara | Archer | lebre (orelhas) | prata-lavanda / azul |
| earth | Gaia | Archer | humana, pele escura | preto / verde |
