import { Composition } from "remotion";
import { GachaSummon, SUMMON_FPS, SUMMON_DURATION } from "./GachaSummon";

// Single 5★ summon cutscene for now. The default props point at the active
// featured Kaeli, but the composition itself is reusable for future banners.
export const RemotionRoot: React.FC = () => {
  return (
    <Composition
      id="GachaSummon"
      component={GachaSummon}
      durationInFrames={SUMMON_DURATION}
      fps={SUMMON_FPS}
      width={1920}
      height={1080}
      defaultProps={{
        name: "VELVET",
        title: "Soberana da Catedral de Cristal",
        thumbSrc: "velvet/thumb.png",
        bgSrc: "velvet/bg-landscape.png",
      }}
    />
  );
};
