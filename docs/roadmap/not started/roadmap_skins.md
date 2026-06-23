# Roadmap � Skins das Kaelis (prompts de gera��o)

> **Como usar este arquivo.** Cada `SK-NN` � uma unidade auto-contida que produz **os 8 prompts de
> asset de UMA skin** (1 Kaeli � 1 tema). Dispare com a linha de despacho no fim do arquivo, ou com
> **"use a base.png como refer�ncia e implemente o prompt SK-NN do `docs/roadmap/not started/roadmap_skins.md`"**.
> Cada prompt declara **Modelo � Effort � Skill � Depende de � Aceite � Verifica��o**.
>
> **O que cada prompt gera:** um `.md` com os 8 prompts de imagem (idle-1/2/3, wallpaper,
> bg-landscape, bg-portrait, banner, thumb) prontos para colar no **GPT Image 2.0**, usando a
> `base.png` da Kaeli como refer�ncia. Isto gera **prompts**, n�o imagens.
>
> **Skill:** todos usam `kaeli-asset-prompts` no **Modo Skin** (congela rosto/cabelo/olhos/ra�a,
> substitui roupa + cen�rio pelo tema).
>
> **N�o confundir com:** `roadmap_producao_visual.md` (pipeline ComfyUI/p�s-processo) � este aqui �
> s� **brief de prompt** das skins. Tornar a skin **jog�vel** (`SkinDef` em `Waifus.cs`) � fora de escopo (ver **## Depois**).

## Modelos & quando usar

| Modelo | Quando | Effort |
|---|---|---|
| **Opus 4.8** | Todos os SK-NN � exigem coes�o criativa: corte de roupa, cen�rio ancorado e paleta coerentes entre as kaelis do mesmo tema | medium |

## Invariantes inegoci�veis
- **`base.png` � a fonte de verdade visual.** O texto s� refor�a. A paleta biol�gica (cabelo, olhos,
  pele, asas/escamas) j� est� pintada na base; s� o macac�o cinza � descart�vel (a roupa � substitu�da).
- **Refor�o de paleta no texto.** Cada prompt embute o *palette anchor* da Kaeli (cabelo, olhos, pele,
  acento) no bloco de identidade, para o GPT n�o derivar a cor.
- **Reconhec�vel como a mesma personagem.** Skin troca roupa + cen�rio; rosto/cabelo/olhos/ra�a ficam.
  Exce��o declarada: tema **Humanas** remove os tra�os n�o-humanos de prop�sito.
- **IDs est�veis.** `slug` = id sem prefixo (`waifu:rin` ? `rin`). N�o renomear.
- **Sa�da padronizada.** `docs/prompts/skins/<tema>/<slug>.md`, 8 prompts nomeados por arquivo de destino.
- **Coes�o de tema.** Kaelis da mesma linha compartilham o cen�rio ancorado e a linguagem de moda do
  tema (estilo League of Legends: mesma skin-line, identidade individual).

## Tese
20 sets de skin, **todos independentes entre si** (cada um l� s� a `base.png` da sua Kaeli e escreve
seu pr�prio arquivo). Logo: **uma onda �nica, 100% paraleliz�vel**. O roadmap n�o existe para ordenar
� existe para **congelar o briefing de cada tema**, garantindo que as kaelis da mesma linha fiquem
coesas mesmo geradas por agentes frios separados.

## Decis�es Fechadas
- **Refer�ncia de entrada:** s� `base.png` + refor�o de paleta no texto. (idle-1 como ref de estilo
  foi considerado e **descartado** por ora � reavaliar s� se a qualidade ficar ruim.)
- **5 temas / 20 sets:**

| Linha | Kaelis | Sets |
|---|---|---|
| **Celestiais Ca�das** | Rin � Rynna � Eloa | 3 |
| **Humanas** | Rin � Rynna � Eloa � Lunara | 4 |
| **Miko** | Velvet � Eloa | 2 |
| **Casual** | Velvet � Gaia � Lunara � Seren | 4 |
| **Ver�o** | todas as 7 | 7 |
| **Total** | | **20** |

---

## Palette anchor � as 7 Kaelis
Cole o trecho da Kaeli no bloco de identidade de cada prompt (tra�o imut�vel + acento de cor).

| Kaeli | Tra�os imut�veis | Acento |
|---|---|---|
| **eloa** | cabelo preto longo liso, olhos rosa brilhantes, pele clara, asas de penas preto-e-branco | monocrom�tico + brilho rosa / ouro celestial |
| **velvet** | cabelo roxo-escuro longo, olhos vermelhos brilhantes, pele clara, humana | roxo/violeta + preto |
| **seren** | cabelo branco-prateado longo em rabo de cavalo alto, olhos azuis, pele clara, humana | prata/branco/azul-gelo |
| **rin** | s�cubo: cabelo vermelho-carmim longo ondulado, olhos rosa-magenta brilhantes, pele clara, orelhas pontudas, chifres pretos com an�is dourados, asas de morcego, cauda com ponta de cora��o | carmim + preto + ouro |
| **rynna** | drag�o: pele escura, cabelo azul-el�trico longo, olhos violeta brilhantes, orelhas pontudas, chifres azuis estriados com an�is dourados, manchas de escama azul, asas membranosas roxas, cauda escamada | azul-el�trico + violeta + ouro |
| **lunara** | coelha-lua: cabelo lavanda-prateado longo, olhos azuis, pele clara, orelhas de coelho brancas | lavanda/prata/pastel suave |
| **gaia** | pele escura, cabelo preto longo ondulado, olhos verdes, listra de tinta verde sob um olho | verde-terra/terracota |

---

## Briefs dos 5 temas
Cada brief = **linguagem de moda compartilhada** + **cen�rio ancorado compartilhado** + a **varia��o
por Kaeli**. O cen�rio ancorado � o mesmo para a linha toda (a "terra natal" da skin-line).

### Celestiais Ca�das (Rin � Rynna � Eloa)
*True form: as asas em destaque, divino/demon�aco no auge da queda. Roupa = regalia ornada m�nima que
exp�e as asas; tecido esvoa�ante, filigrana dourada.*
**Cen�rio ancorado:** reino celestial em colapso � catedral nas nuvens se partindo, halos quebrados
flutuando, luz dourada vazando por fendas no c�u. Mood: �pico, dram�tico.
- **Rin:** dem�nio ascendido � regalia carmim-e-preto com filigrana de ouro, ombros nus, asas de
  morcego totalmente abertas; brasas subindo. Setor infernal do reino ca�do (fendas com brasa).
- **Rynna:** drag�o divino � regalia azul/violeta com placas de escama douradas, asas de drag�o
  abertas e luminosas. Ab�bada de tempestade, rel�mpagos, constela��es estilha�adas.
- **Eloa:** anjo ca�do � manto branco-e-ouro rasgado, halo escurecendo, asas de penas totalmente
  abertas; penas caindo na luz. Catedral celestial desabando.

### Humanas (Rin � Rynna � Eloa � Lunara)
*"E se fossem humanas?" � **remover** todos os tra�os n�o-humanos (asas, chifres, cauda, orelhas n�o
humanas) e dar orelhas humanas normais. Roupa = casual-chique elegante do dia a dia (n�o streetwear �
isso � Casual). Mant�m S� a cor de cabelo/olhos/pele como "tell".*
**Cen�rio ancorado:** cidade moderna comum, slice-of-life. Mood: suave, surpresa, cotidiano.
> No bloco de identidade, declare explicitamente: *no wings, no horns, no tail, normal human ears.*
- **Rin:** ruiva humana � vestido vermelho casual-chique. Caf� urbano � tarde, luz quente.
- **Rynna:** humana de cabelo azul � casaco/look urbano. Rua de cidade � noite, neon azul.
- **Eloa:** humana de cabelo preto � look de estudante elegante. Biblioteca/campus, luz suave.
- **Lunara:** humana (sem orelhas de coelho), cabelo lavanda-prata � look fofo casual-chique. Parque/cafeteria.

### Miko (Velvet � Eloa)
*Sacerdotisa de santu�rio japon�s. Subvers�o: criatura sombria/celestial como miko. Roupa = haori
branco + hakama colorido, ofuda, tassels, sand�lias.*
**Cen�rio ancorado:** santu�rio japon�s. Mood: sagrado com a tor��o da personagem.
- **Velvet:** miko g�tica � haori branco com hakama roxo-escuro, ofuda, spider-lilies roxas no cabelo.
  Santu�rio noturno amaldi�oado, lanternas roxas.
- **Eloa:** miko celestial � haori branco, hakama dourado-p�lido, motivos de pena; asas de penas
  presentes. Santu�rio de montanha ao amanhecer, torii, luz branca.

### Casual (Velvet � Gaia � Lunara � Seren)
*Roupa do dia a dia, slice-of-life, streetwear/cozy. Mant�m os tra�os de ra�a. Roupa individual por
personalidade.*
**Cen�rio ancorado:** cenas urbanas cotidianas. Mood: relaxado, off-duty.
- **Velvet:** g�tica casual � moletom preto oversized com detalhes roxos, saia, meias. Quarto g�tico
  aconchegante / loja de discos.
- **Gaia:** boho/earthy � top cropped, shorts jeans, sand�lias. Feira ao ar livre ensolarada.
- **Lunara:** cozy � moletom oversized, shorts, t�nis; orelhas de coelho presentes. Cafeteria aconchegante.
- **Seren:** minimalista chique � gola alta, casaco longo. Livraria silenciosa / rua de inverno.

### Ver�o (todas as 7)
*Praia/ver�o. Swimwear/beachwear por Kaeli. Mant�m todos os tra�os de ra�a (asas, orelhas, cauda).*
**Cen�rio ancorado:** praia tropical / festival de ver�o, sol forte, �gua azul-turquesa, palmeiras.
Mood: vibrante, alegre.
- **Eloa:** mai� branco/dourado elegante, asas de penas presentes.
- **Velvet:** biqu�ni preto-e-roxo, par�s de renda escura, sombrinha.
- **Seren:** mai� prata/azul-gelo, look de resort.
- **Rynna:** biqu�ni azul-el�trico, asas/cauda de drag�o � mostra, vibe surf/mar.
- **Rin:** biqu�ni vermelho, asas/cauda de s�cubo � mostra, vibe travessa.
- **Lunara:** biqu�ni pastel, orelhas de coelho, boia de coelho fofa.
- **Gaia:** biqu�ni/par� tropical terroso, flores no cabelo, coco/feira de praia.

---

## Mapa de prompts (escopo)
Todos: **Modelo Opus � Effort medium � Skill `kaeli-asset-prompts` (Modo Skin) � Depende de � � Onda �nica.**

| Prompt | Tema | Kaeli | Sa�da |
|---|---|---|---|
| SK-01 | Celestiais Ca�das | Rin | `docs/prompts/skins/celestiais-caidas/rin.md` |
| SK-02 | Celestiais Ca�das | Rynna | `docs/prompts/skins/celestiais-caidas/rynna.md` |
| SK-03 | Celestiais Ca�das | Eloa | `docs/prompts/skins/celestiais-caidas/eloa.md` |
| SK-04 | Humanas | Rin | `docs/prompts/skins/humanas/rin.md` |
| SK-05 | Humanas | Rynna | `docs/prompts/skins/humanas/rynna.md` |
| SK-06 | Humanas | Eloa | `docs/prompts/skins/humanas/eloa.md` |
| SK-07 | Humanas | Lunara | `docs/prompts/skins/humanas/lunara.md` |
| SK-08 | Miko | Velvet | `docs/prompts/skins/miko/velvet.md` |
| SK-09 | Miko | Eloa | `docs/prompts/skins/miko/eloa.md` |
| SK-10 | Casual | Velvet | `docs/prompts/skins/casual/velvet.md` |
| SK-11 | Casual | Gaia | `docs/prompts/skins/casual/gaia.md` |
| SK-12 | Casual | Lunara | `docs/prompts/skins/casual/lunara.md` |
| SK-13 | Casual | Seren | `docs/prompts/skins/casual/seren.md` |
| SK-14 | Ver�o | Eloa | `docs/prompts/skins/verao/eloa.md` |
| SK-15 | Ver�o | Velvet | `docs/prompts/skins/verao/velvet.md` |
| SK-16 | Ver�o | Seren | `docs/prompts/skins/verao/seren.md` |
| SK-17 | Ver�o | Rynna | `docs/prompts/skins/verao/rynna.md` |
| SK-18 | Ver�o | Rin | `docs/prompts/skins/verao/rin.md` |
| SK-19 | Ver�o | Lunara | `docs/prompts/skins/verao/lunara.md` |
| SK-20 | Ver�o | Gaia | `docs/prompts/skins/verao/gaia.md` |

## Execu��o paralela ?

```
Onda �nica � os 20 s�o independentes (l�em base.png distinta, escrevem arquivo distinto):

  SK-01 � SK-20   --?  todos em paralelo, sem depend�ncias, sem conflito de arquivo
```

**Conflitos que for�am sequencial:** nenhum. Cada prompt l� uma `base.png` diferente e escreve um
`.md` diferente.

**Lotes pr�ticos** (se rodar a m�o, agrupe por tema para revisar a coes�o da linha de uma vez):
Lote A = SK-01..03 (Celestiais) � Lote B = SK-04..07 (Humanas) � Lote C = SK-08..09 (Miko) �
Lote D = SK-10..13 (Casual) � Lote E = SK-14..20 (Ver�o).

---

## SK-01 � Celestiais Ca�das � Rin
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Gerar os 8 prompts de asset da skin "Celestiais Ca�das" da Rin � dem�nio ascendido, asas de morcego em destaque.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/rin/base.png`.

**Palette anchor (preservar):** s�cubo � cabelo vermelho-carmim longo ondulado, olhos rosa-magenta brilhantes, pele clara, orelhas pontudas, chifres pretos com an�is dourados, asas de morcego, cauda com ponta de cora��o. Acento: carmim + preto + ouro.

**Roupa + cen�rio (brief do tema "Celestiais Ca�das"):** regalia carmim-e-preto com filigrana de ouro, ombros nus, asas de morcego totalmente abertas; cen�rio ancorado do tema (reino celestial em colapso) no setor infernal � fendas com brasa, brasas subindo, luz dourada vazando.

**Sa�da:** `docs/prompts/skins/celestiais-caidas/rin.md` � os 8 prompts nomeados por arquivo de destino.

**Aceite:** 8 prompts (idle-1/2/3, wallpaper, bg-landscape, bg-portrait, banner, thumb); bloco de identidade id�ntico nos 8; asas em destaque; reconhec�vel como Rin.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade congelada + paleta no texto.

## SK-02 � Celestiais Ca�das � Rynna
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Celestiais Ca�das" da Rynna � drag�o divino, asas de drag�o luminosas em destaque.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/rynna/base.png`.

**Palette anchor (preservar):** drag�o de pele escura � cabelo azul-el�trico longo, olhos violeta brilhantes, orelhas pontudas, chifres azuis estriados com an�is dourados, manchas de escama azul, asas membranosas roxas, cauda escamada. Acento: azul-el�trico + violeta + ouro.

**Roupa + cen�rio (brief "Celestiais Ca�das"):** regalia azul/violeta com placas de escama douradas, asas de drag�o abertas e luminosas; cen�rio ancorado na ab�bada de tempestade � rel�mpagos, constela��es estilha�adas, luz dourada nas fendas.

**Sa�da:** `docs/prompts/skins/celestiais-caidas/rynna.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; asas em destaque; reconhec�vel como Rynna.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-03 � Celestiais Ca�das � Eloa
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Celestiais Ca�das" da Eloa � anjo ca�do, asas de penas em destaque.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/eloa/base.png`.

**Palette anchor (preservar):** cabelo preto longo liso, olhos rosa brilhantes, pele clara, asas de penas preto-e-branco. Acento: monocrom�tico + brilho rosa / ouro celestial.

**Roupa + cen�rio (brief "Celestiais Ca�das"):** manto branco-e-ouro rasgado, halo escurecendo, asas de penas totalmente abertas; cen�rio ancorado na catedral celestial desabando, penas caindo na luz dourada.

**Sa�da:** `docs/prompts/skins/celestiais-caidas/eloa.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; asas em destaque; reconhec�vel como Eloa.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-04 � Humanas � Rin
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Humanas" da Rin � vers�o humana, **sem** asas/chifres/cauda; s� a cor de cabelo/olhos como tell.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/rin/base.png`.

**Palette anchor (preservar S�):** cabelo vermelho-carmim longo ondulado, olhos rosa-magenta brilhantes, pele clara. **Remover:** asas, chifres, cauda, orelhas pontudas ? **orelhas humanas normais** (declarar no texto: *no wings, no horns, no tail, normal human ears*).

**Roupa + cen�rio (brief "Humanas"):** vestido vermelho casual-chique; caf� urbano � tarde, luz quente.

**Sa�da:** `docs/prompts/skins/humanas/rin.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; **sem tra�os de s�cubo**; reconhec�vel pela cor de cabelo.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + remo��o de asas/chifres/cauda expl�cita.

## SK-05 � Humanas � Rynna
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Humanas" da Rynna � vers�o humana, **sem** asas/escamas/chifres/cauda.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/rynna/base.png`.

**Palette anchor (preservar S�):** pele escura, cabelo azul-el�trico longo, olhos violeta brilhantes. **Remover:** asas, chifres, cauda, manchas de escama, orelhas pontudas ? **orelhas humanas normais** (*no wings, no horns, no tail, no scales, normal human ears*).

**Roupa + cen�rio (brief "Humanas"):** casaco/look urbano moderno; rua de cidade � noite, neon azul.

**Sa�da:** `docs/prompts/skins/humanas/rynna.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; **sem tra�os de drag�o**; reconhec�vel pela cor de cabelo/pele.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + remo��o de tra�os de drag�o expl�cita.

## SK-06 � Humanas � Eloa
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Humanas" da Eloa � vers�o humana, **sem** asas de anjo.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/eloa/base.png`.

**Palette anchor (preservar S�):** cabelo preto longo liso, olhos rosa brilhantes, pele clara. **Remover:** asas de penas (*no wings*).

**Roupa + cen�rio (brief "Humanas"):** look de estudante elegante; biblioteca/campus, luz suave.

**Sa�da:** `docs/prompts/skins/humanas/eloa.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; **sem asas**; reconhec�vel pela cor de cabelo/olhos.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + remo��o de asas expl�cita.

## SK-07 � Humanas � Lunara
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Humanas" da Lunara � vers�o humana, **sem** orelhas de coelho (a transforma��o mais impactante da linha).

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/lunara/base.png`.

**Palette anchor (preservar S�):** cabelo lavanda-prateado longo, olhos azuis, pele clara. **Remover:** orelhas de coelho ? **orelhas humanas normais** (*no rabbit ears, normal human ears*).

**Roupa + cen�rio (brief "Humanas"):** look fofo casual-chique; parque/cafeteria.

**Sa�da:** `docs/prompts/skins/humanas/lunara.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; **sem orelhas de coelho**; reconhec�vel pela cor de cabelo.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + remo��o de orelhas expl�cita.

## SK-08 � Miko � Velvet
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Miko" da Velvet � sacerdotisa g�tica.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/velvet/base.png`.

**Palette anchor (preservar):** cabelo roxo-escuro longo, olhos vermelhos brilhantes, pele clara, humana. Acento: roxo/violeta + preto.

**Roupa + cen�rio (brief "Miko"):** haori branco com hakama roxo-escuro, ofuda, spider-lilies roxas no cabelo; santu�rio japon�s noturno amaldi�oado, lanternas roxas.

**Sa�da:** `docs/prompts/skins/miko/velvet.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; trajes miko coerentes; reconhec�vel como Velvet.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-09 � Miko � Eloa
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Miko" da Eloa � sacerdotisa celestial, asas de penas presentes.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/eloa/base.png`.

**Palette anchor (preservar):** cabelo preto longo liso, olhos rosa brilhantes, pele clara, asas de penas preto-e-branco. Acento: monocrom�tico + rosa / ouro celestial.

**Roupa + cen�rio (brief "Miko"):** haori branco, hakama dourado-p�lido, motivos de pena; asas presentes; santu�rio de montanha ao amanhecer, torii, luz branca.

**Sa�da:** `docs/prompts/skins/miko/eloa.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; trajes miko + asas; reconhec�vel como Eloa.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-10 � Casual � Velvet
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Casual" da Velvet � g�tica off-duty.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/velvet/base.png`.

**Palette anchor (preservar):** cabelo roxo-escuro longo, olhos vermelhos brilhantes, pele clara, humana. Acento: roxo/violeta + preto.

**Roupa + cen�rio (brief "Casual"):** moletom preto oversized com detalhes roxos, saia, meias; quarto g�tico aconchegante / loja de discos.

**Sa�da:** `docs/prompts/skins/casual/velvet.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; look casual coerente; reconhec�vel como Velvet.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-11 � Casual � Gaia
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Casual" da Gaia � boho/earthy off-duty.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/gaia/base.png`.

**Palette anchor (preservar):** pele escura, cabelo preto longo ondulado, olhos verdes, listra de tinta verde sob um olho. Acento: verde-terra/terracota.

**Roupa + cen�rio (brief "Casual"):** top cropped, shorts jeans, sand�lias; feira ao ar livre ensolarada.

**Sa�da:** `docs/prompts/skins/casual/gaia.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; look casual coerente; reconhec�vel como Gaia.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-12 � Casual � Lunara
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Casual" da Lunara � cozy off-duty, orelhas de coelho presentes.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/lunara/base.png`.

**Palette anchor (preservar):** cabelo lavanda-prateado longo, olhos azuis, pele clara, orelhas de coelho brancas. Acento: lavanda/prata/pastel.

**Roupa + cen�rio (brief "Casual"):** moletom oversized, shorts, t�nis; orelhas de coelho presentes; cafeteria aconchegante.

**Sa�da:** `docs/prompts/skins/casual/lunara.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; orelhas presentes; reconhec�vel como Lunara.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-13 � Casual � Seren
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Casual" da Seren � minimalista chique off-duty.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/seren/base.png`.

**Palette anchor (preservar):** cabelo branco-prateado longo em rabo de cavalo alto, olhos azuis, pele clara, humana. Acento: prata/branco/azul-gelo.

**Roupa + cen�rio (brief "Casual"):** gola alta, casaco longo; livraria silenciosa / rua de inverno.

**Sa�da:** `docs/prompts/skins/casual/seren.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; look casual coerente; reconhec�vel como Seren.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-14 � Ver�o � Eloa
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Ver�o" da Eloa � praia, asas de penas presentes.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/eloa/base.png`.

**Palette anchor (preservar):** cabelo preto longo liso, olhos rosa brilhantes, pele clara, asas de penas. Acento: branco + rosa.

**Roupa + cen�rio (brief "Ver�o"):** mai� branco/dourado elegante, asas presentes; praia tropical / festival de ver�o, sol forte, �gua azul-turquesa.

**Sa�da:** `docs/prompts/skins/verao/eloa.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; tra�os de ra�a presentes; reconhec�vel como Eloa.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-15 � Ver�o � Velvet
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Ver�o" da Velvet � praia g�tica.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/velvet/base.png`.

**Palette anchor (preservar):** cabelo roxo-escuro longo, olhos vermelhos brilhantes, pele clara, humana. Acento: roxo + preto.

**Roupa + cen�rio (brief "Ver�o"):** biqu�ni preto-e-roxo, par�s de renda escura, sombrinha; praia tropical, sol forte, �gua azul-turquesa.

**Sa�da:** `docs/prompts/skins/verao/velvet.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; look de ver�o coerente; reconhec�vel como Velvet.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-16 � Ver�o � Seren
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Ver�o" da Seren � resort.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/seren/base.png`.

**Palette anchor (preservar):** cabelo branco-prateado longo em rabo de cavalo alto, olhos azuis, pele clara, humana. Acento: prata/azul-gelo.

**Roupa + cen�rio (brief "Ver�o"):** mai� prata/azul-gelo, look de resort; praia tropical, sol forte, �gua azul-turquesa.

**Sa�da:** `docs/prompts/skins/verao/seren.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; look de ver�o coerente; reconhec�vel como Seren.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-17 � Ver�o � Rynna
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Ver�o" da Rynna � praia, asas/cauda de drag�o � mostra.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/rynna/base.png`.

**Palette anchor (preservar):** pele escura, cabelo azul-el�trico longo, olhos violeta, chifres/escamas/asas/cauda de drag�o. Acento: azul-el�trico + violeta.

**Roupa + cen�rio (brief "Ver�o"):** biqu�ni azul-el�trico, asas/cauda � mostra, vibe surf/mar; praia tropical, sol forte, �gua azul-turquesa.

**Sa�da:** `docs/prompts/skins/verao/rynna.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; tra�os de drag�o presentes; reconhec�vel como Rynna.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-18 � Ver�o � Rin
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Ver�o" da Rin � praia, asas/cauda de s�cubo � mostra.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/rin/base.png`.

**Palette anchor (preservar):** cabelo vermelho-carmim longo ondulado, olhos rosa-magenta, chifres/asas de morcego/cauda com ponta de cora��o. Acento: carmim + preto + ouro.

**Roupa + cen�rio (brief "Ver�o"):** biqu�ni vermelho, asas/cauda � mostra, vibe travessa; praia tropical, sol forte, �gua azul-turquesa.

**Sa�da:** `docs/prompts/skins/verao/rin.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; tra�os de s�cubo presentes; reconhec�vel como Rin.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-19 � Ver�o � Lunara
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Ver�o" da Lunara � praia, orelhas de coelho presentes.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/lunara/base.png`.

**Palette anchor (preservar):** cabelo lavanda-prateado longo, olhos azuis, pele clara, orelhas de coelho. Acento: lavanda/pastel.

**Roupa + cen�rio (brief "Ver�o"):** biqu�ni pastel, orelhas de coelho, boia de coelho fofa; praia tropical, sol forte, �gua azul-turquesa.

**Sa�da:** `docs/prompts/skins/verao/lunara.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; orelhas presentes; reconhec�vel como Lunara.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

## SK-20 � Ver�o � Gaia
- **Modelo:** Opus � **Effort:** medium � **Skill:** `kaeli-asset-prompts` (Modo Skin) � **Depende de:** � � **Paraleliza com:** todos (Onda �nica)

**Objetivo:** Skin "Ver�o" da Gaia � praia tropical terrosa.

**Refer�ncia de entrada:** `frontend/public/assets/kaelis/gaia/base.png`.

**Palette anchor (preservar):** pele escura, cabelo preto longo ondulado, olhos verdes, listra de tinta verde sob um olho. Acento: verde-terra/terracota.

**Roupa + cen�rio (brief "Ver�o"):** biqu�ni/par� tropical terroso, flores no cabelo, coco/feira de praia; praia tropical, sol forte, �gua azul-turquesa.

**Sa�da:** `docs/prompts/skins/verao/gaia.md`.

**Aceite:** 8 prompts; identidade id�ntica nos 8; look de ver�o coerente; reconhec�vel como Gaia.

**Verifica��o:** abrir o `.md`, conferir 8 blocos + identidade + paleta.

---

## Despacho � copie a linha e rode (marque o checkbox ao concluir)

> Cada linha invoca a skill `kaeli-asset-prompts`, manda usar a `base.png` da Kaeli como refer�ncia e
> implementa o prompt correspondente. Marque `[x]` quando o `.md` de sa�da estiver gravado.

- [x] **SK-01** Celestiais � Rin � `/kaeli-asset-prompts use frontend/public/assets/kaelis/rin/base.png como refer�ncia e implemente o prompt SK-01 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/celestiais-caidas/rin.md`
- [ ] **SK-02** Celestiais � Rynna � `/kaeli-asset-prompts use frontend/public/assets/kaelis/rynna/base.png como refer�ncia e implemente o prompt SK-02 do docs/roadmap/not started/roadmap_skins.md`
- [x] **SK-03** Celestiais � Eloa � `/kaeli-asset-prompts use frontend/public/assets/kaelis/eloa/base.png como refer�ncia e implemente o prompt SK-03 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/celestiais-caidas/eloa.md`
- [x] **SK-04** Humanas � Rin � `/kaeli-asset-prompts use frontend/public/assets/kaelis/rin/base.png como refer�ncia e implemente o prompt SK-04 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/humanas/rin.md`
- [x] **SK-05** Humanas � Rynna � `/kaeli-asset-prompts use frontend/public/assets/kaelis/rynna/base.png como refer�ncia e implemente o prompt SK-05 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/humanas/rynna.md`
- [x] **SK-06** Humanas � Eloa � `/kaeli-asset-prompts use frontend/public/assets/kaelis/eloa/base.png como refer�ncia e implemente o prompt SK-06 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/humanas/eloa.md`
- [x] **SK-07** Humanas � Lunara � `/kaeli-asset-prompts use frontend/public/assets/kaelis/lunara/base.png como refer�ncia e implemente o prompt SK-07 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/humanas/lunara.md`
- [x] **SK-08** Miko � Velvet � `/kaeli-asset-prompts use frontend/public/assets/kaelis/velvet/base.png como refer�ncia e implemente o prompt SK-08 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/miko/velvet.md`
- [x] **SK-09** Miko � Eloa � `/kaeli-asset-prompts use frontend/public/assets/kaelis/eloa/base.png como refer�ncia e implemente o prompt SK-09 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/miko/eloa.md`
- [x] **SK-10** Casual � Velvet � `/kaeli-asset-prompts use frontend/public/assets/kaelis/velvet/base.png como refer�ncia e implemente o prompt SK-10 do docs/roadmap/not started/roadmap_skins.md` -> `docs/prompts/skins/casual/velvet.md`
- [ ] **SK-11** Casual � Gaia � `/kaeli-asset-prompts use frontend/public/assets/kaelis/gaia/base.png como refer�ncia e implemente o prompt SK-11 do docs/roadmap/not started/roadmap_skins.md`
- [x] **SK-12** Casual � Lunara � `/kaeli-asset-prompts use frontend/public/assets/kaelis/lunara/base.png como refer�ncia e implemente o prompt SK-12 do docs/roadmap/not started/roadmap_skins.md`
- [x] **SK-13** Casual · Seren — 8 prompts em `docs/prompts/skins/casual/seren.md` (gola alta + casaco bege, livraria de inverno)
- [x] **SK-14** Ver�o � Eloa � `/kaeli-asset-prompts use frontend/public/assets/kaelis/eloa/base.png como refer�ncia e implemente o prompt SK-14 do docs/roadmap/not started/roadmap_skins.md` -> `docs/prompts/skins/verao/eloa.md`
- [x] **SK-15** Ver�o � Velvet � `/kaeli-asset-prompts use frontend/public/assets/kaelis/velvet/base.png como refer�ncia e implemente o prompt SK-15 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/verao/velvet.md`
- [x] **SK-16** Ver�o � Seren � `/kaeli-asset-prompts use frontend/public/assets/kaelis/seren/base.png como refer�ncia e implemente o prompt SK-16 do docs/roadmap/not started/roadmap_skins.md` ? `docs/prompts/skins/verao/seren.md`
- [x] **SK-17** Ver�o � Rynna � `/kaeli-asset-prompts use frontend/public/assets/kaelis/rynna/base.png como refer�ncia e implemente o prompt SK-17 do docs/roadmap/not started/roadmap_skins.md` � `docs/prompts/skins/verao/rynna.md`
- [x] **SK-18** Ver�o � Rin � `/kaeli-asset-prompts use frontend/public/assets/kaelis/rin/base.png como refer�ncia e implemente o prompt SK-18 do docs/roadmap/not started/roadmap_skins.md` -> `docs/prompts/skins/verao/rin.md`
- [x] **SK-19** Ver�o � Lunara � `/kaeli-asset-prompts use frontend/public/assets/kaelis/lunara/base.png como refer�ncia e implemente o prompt SK-19 do docs/roadmap/not started/roadmap_skins.md` � `docs/prompts/skins/verao/lunara.md`
- [x] **SK-20** Ver�o � Gaia � 8 prompts em `docs/prompts/skins/verao/gaia.md` (beachwear terroso terracota-e-verde, flores no cabelo, praia tropical)

---

## Depois
- **Gerar as imagens:** colar cada prompt no GPT Image 2.0 com a `base.png` da Kaeli ? p�s-processo
  ComfyUI (upscale/removebg/crop) pela trilha do `roadmap_producao_visual.md`.
- **Salvar os assets:** `frontend/public/assets/kaelis/<slug>/skins/<tema>/` (idle-1/2/3, wallpaper,
  bg-landscape, bg-portrait, banner, thumb) � confirmar a conven��o de subpasta de skin no desktop.
- **Tornar jog�vel:** registrar cada skin como `SkinDef` em `Domain/Waifus.cs` + manifest do
  `KaeliArtService` (passo de desktop/backend, fora desta etapa de brief).
- **Linhas futuras consideradas:** Noiva (Eloa angelical / Velvet g�tica / Seren crep�sculo),
  Vampira, Idol, Deusas Primordiais (Gaia/Rynna/Seren) � s� quando estas 5 fecharem.
