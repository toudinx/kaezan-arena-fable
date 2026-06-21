Vou te enviar UMA imagem de personagem.

Sua tarefa é analisar a personagem da imagem e gerar um pacote de prompts visuais para os seguintes assets:
["wallpaper","bg-landscape","bg-portrait","banner","thumb"]

Objetivo:
Criar prompts consistentes entre si, mantendo a identidade visual da personagem e do cenário, para uso em geração de imagens.

## Instruções gerais

1. Primeiro, extraia da imagem um **Bloco de identidade** curto e objetivo, descrevendo a personagem de forma visual e precisa.
   - Inclua: cabelo, olhos, expressão/base mood, roupa, acessórios, cores principais, elementos marcantes.
   - Não invente detalhes que não estejam visíveis.
   - Não use nome da personagem; descreva apenas o design.
   - Comece com:
   `Using this character as reference, keep her exact design:`

2. Depois, crie um **Cenário ancorado** coerente com a personagem.
   - Defina: ambiente, iluminação, atmosfera, acento de cor e mood.
   - O cenário deve combinar com a identidade visual da personagem.

3. Em seguida, gere 5 prompts separados, exatamente nesta ordem:
   - wallpaper
   - bg-landscape
   - bg-portrait
   - banner
   - thumb

4. Todos os prompts devem:
   - ser escritos em inglês
   - ser prontos para uso em geradores de imagem
   - manter consistência estética entre si
   - preservar fielmente a personagem quando ela estiver presente
   - ter instruções claras de composição, enquadramento, iluminação, mood e aspect ratio
   - ter alta qualidade visual, estilo anime premium / painterly / gacha-like
   - evitar texto, watermark, logo e UI, exceto quando o prompt pedir espaço livre para overlay

5. Regras específicas por asset:

### wallpaper
- Mostrar a personagem em uma cena completa e cinematográfica
- Personagem visível em full body ou quase full body
- Fundo rico e detalhado
- Aspect ratio: 16:9 landscape

### bg-landscape
- Criar SOMENTE o background
- Não incluir personagem, silhueta ou pessoas
- O cenário deve parecer pronto para receber a personagem depois
- Aspect ratio: 16:9 landscape

### bg-portrait
- Criar SOMENTE o background
- Não incluir personagem, silhueta ou pessoas
- Composição vertical/tall
- O centro deve ficar levemente preparado para possível composição posterior
- Aspect ratio: 9:16 portrait

### banner
- Banner estilo gacha game
- Aspect ratio: 2:1 landscape
- Personagem posicionada preferencialmente na DIREITA
- Lado esquerdo menos poluído, com espaço para texto/UI overlay
- Mostrar a personagem em 3/4 body ou half-to-3/4 composition
- Background mais gráfico/decorativo, menos complexo que o wallpaper

### thumb
- Thumb quadrada 1:1
- Mostrar apenas rosto e parte superior do busto/peito
- Leitura clara em tamanho pequeno
- Fundo simples, limpo, com poucos elementos
- Grande foco na expressão e identidade facial

6. Para os prompts de background-only:
   - comece com:
   `Using this image as STYLE reference, create ONLY the background scene, NO character present.`
   - deixe explícito:
   `No characters, no silhouettes, no people.`

7. Formato de saída:
- Entregue exatamente nesta estrutura:

## Bloco de identidade