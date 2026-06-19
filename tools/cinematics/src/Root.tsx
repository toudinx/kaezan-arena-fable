import { Composition } from "remotion";
import { VelvetSummon, SUMMON_FPS, SUMMON_DURATION } from "./VelvetSummon";

// Single 5★ summon cutscene for now. New Kaelis can be added as parametrized
// compositions (pass the waifu's thumb/bg/name as defaultProps) once they have art.
export const RemotionRoot: React.FC = () => {
  return (
    <Composition
      id="VelvetSummon"
      component={VelvetSummon}
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
