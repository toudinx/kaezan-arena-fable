import { Composition } from "remotion";
import { GachaSummon, SUMMON_FPS, SUMMON_DURATION } from "./GachaSummon";
import { KAELIS } from "./kaelis";

// One 5★ summon cutscene per Kaeli. Every composition reuses the same
// GachaSummon component — only the props differ: name/title from the roster
// and the element accent (build-up color) derived from the --el-* tokens in
// kaelis.ts. The 5★ climax stays AURUM gold across all of them. Composition
// ids are `summon-<slug>`; render-all.mjs renders each one.
export const RemotionRoot: React.FC = () => {
  return (
    <>
      {KAELIS.map((k) => (
        <Composition
          key={k.slug}
          id={`summon-${k.slug}`}
          component={GachaSummon}
          durationInFrames={SUMMON_DURATION}
          fps={SUMMON_FPS}
          width={1920}
          height={1080}
          defaultProps={{
            name: k.name,
            title: k.title,
            thumbSrc: k.thumbSrc,
            bgSrc: k.bgSrc,
            accent: k.accent,
            accentDeep: k.accentDeep,
          }}
        />
      ))}
    </>
  );
};
