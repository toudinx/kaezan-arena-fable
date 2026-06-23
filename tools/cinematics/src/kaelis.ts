import roster from "./kaelis.json";

// Element accent palette — mirrored from frontend/src/styles.css (--el-*), the
// in-game element colors. The accent tints the *anticipation* layers of the
// summon (arcane circle, energy column, converging dust). The 5★ climax stays
// AURUM gold (see GachaSummon) so the rarity language reads the same for every
// Kaeli — only the build-up is element-coded. `deep` is a darkened tone used
// for the halo gradient behind the reveal card.
export type Element = "physical" | "fire" | "ice" | "energy" | "earth" | "death" | "holy";

export const ELEMENT_ACCENTS: Record<Element, { accent: string; deep: string }> = {
  physical: { accent: "#c8bba6", deep: "#6e6450" },
  fire: { accent: "#ff6a3d", deep: "#8a2a14" },
  ice: { accent: "#6fd6ff", deep: "#2a6e94" },
  energy: { accent: "#2fe0c4", deep: "#126b5c" },
  earth: { accent: "#8cbf4d", deep: "#45611f" },
  death: { accent: "#a662ff", deep: "#4a2a9e" },
  holy: { accent: "#ffe39c", deep: "#a8822c" },
};

export type Kaeli = {
  slug: string;
  name: string;
  element: Element;
  title: string;
  accent: string;
  accentDeep: string;
  thumbSrc: string;
  bgSrc: string;
};

// Resolve the raw roster into render-ready configs: element → accent colors and
// slug → asset paths (synced into public/<slug>/ by sync-assets.mjs from the
// frontend's source-of-truth art).
export const KAELIS: Kaeli[] = (roster as Array<{
  slug: string;
  name: string;
  element: Element;
  title: string;
}>).map((k) => {
  const pal = ELEMENT_ACCENTS[k.element];
  return {
    ...k,
    accent: pal.accent,
    accentDeep: pal.deep,
    thumbSrc: `${k.slug}/thumb.png`,
    bgSrc: `${k.slug}/bg-landscape.png`,
  };
});
