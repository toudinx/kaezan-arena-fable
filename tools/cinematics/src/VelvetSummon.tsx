import React from "react";
import {
  AbsoluteFill,
  Img,
  Sequence,
  interpolate,
  random,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
  Easing,
} from "remotion";
import { loadFont as loadFraunces } from "@remotion/google-fonts/Fraunces";
import { loadFont as loadSora } from "@remotion/google-fonts/Sora";

const { fontFamily: FRAUNCES } = loadFraunces();
const { fontFamily: SORA } = loadSora();

export const SUMMON_FPS = 30;
export const SUMMON_DURATION = 450; // 15s

// Design tokens mirrored from frontend/src/styles.css ("Cathedral Ink + Aurum").
const INK = "#07070d";
const IRIS = "#7b6bf2"; // UI / anticipation
const IRIS_DEEP = "#4a2a9e";
const AURUM = "#e8a93c"; // 5★ reward — the climax color
const AURUM_HOT = "#ffe6a8";

export type VelvetSummonProps = {
  name: string;
  title: string;
  thumbSrc: string;
  bgSrc: string;
};

// ----------------------------------------------------------------------------
// Layer 0 — the cathedral, slowly waking up
// ----------------------------------------------------------------------------
const Backdrop: React.FC<{ bgSrc: string }> = ({ bgSrc }) => {
  const frame = useCurrentFrame();

  // Dark → revealed; faint continuous push-in for life.
  const brightness = interpolate(frame, [0, 60, 170, 220], [0.12, 0.18, 0.5, 0.72], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const scale = interpolate(frame, [0, SUMMON_DURATION], [1.12, 1.22]);

  return (
    <AbsoluteFill>
      <Img
        src={staticFile(bgSrc)}
        style={{
          width: "100%",
          height: "100%",
          objectFit: "cover",
          transform: `scale(${scale})`,
          filter: `brightness(${brightness}) saturate(1.1)`,
        }}
      />
      {/* vignette for legibility */}
      <AbsoluteFill
        style={{
          background:
            "radial-gradient(120% 90% at 50% 40%, transparent 30%, rgba(7,7,13,0.55) 72%, rgba(7,7,13,0.92) 100%)",
        }}
      />
    </AbsoluteFill>
  );
};

// ----------------------------------------------------------------------------
// Layer 1 — arcane summoning circle (build-up). Iris purple, two rings rotating.
// ----------------------------------------------------------------------------
const ArcaneCircle: React.FC = () => {
  const frame = useCurrentFrame();

  // Grow + glow while charging, then blow out at the burst.
  const grow = interpolate(frame, [40, 150], [0.2, 1], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: Easing.bezier(0.16, 1, 0.3, 1),
  });
  const blowout = interpolate(frame, [150, 172], [1, 1.9], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const opacity = interpolate(frame, [40, 70, 150, 178], [0, 0.9, 0.9, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const charge = interpolate(frame, [60, 150], [0.4, 1.4], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });

  const ticks = Array.from({ length: 24 });

  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center" }}>
      <svg
        width={900}
        height={900}
        viewBox="0 0 900 900"
        style={{
          opacity,
          transform: `scale(${grow * blowout}) rotateX(68deg)`,
          filter: `drop-shadow(0 0 ${18 * charge}px ${IRIS})`,
        }}
      >
        <g transform="translate(450 450)">
          <circle r="400" fill="none" stroke={IRIS} strokeWidth="2" opacity="0.5" />
          <g transform={`rotate(${frame * 0.7})`}>
            <circle r="360" fill="none" stroke={IRIS} strokeWidth="3" opacity="0.85" strokeDasharray="6 26" />
          </g>
          <g transform={`rotate(${-frame * 1.1})`}>
            <circle r="300" fill="none" stroke={AURUM} strokeWidth="2" opacity="0.55" strokeDasharray="2 18" />
            <polygon points="0,-300 260,150 -260,150" fill="none" stroke={IRIS} strokeWidth="2" opacity="0.6" />
            <polygon points="0,300 260,-150 -260,-150" fill="none" stroke={IRIS} strokeWidth="2" opacity="0.6" />
          </g>
          <g transform={`rotate(${frame * 1.6})`}>
            {ticks.map((_, i) => {
              const a = (i / ticks.length) * Math.PI * 2;
              return (
                <circle
                  key={i}
                  cx={Math.cos(a) * 340}
                  cy={Math.sin(a) * 340}
                  r={4}
                  fill={AURUM}
                  opacity="0.8"
                />
              );
            })}
          </g>
          <circle r="180" fill="none" stroke={IRIS} strokeWidth="1.5" opacity="0.4" />
        </g>
      </svg>
    </AbsoluteFill>
  );
};

// ----------------------------------------------------------------------------
// Layer 2 — vertical energy column gathering at center during charge
// ----------------------------------------------------------------------------
const EnergyColumn: React.FC = () => {
  const frame = useCurrentFrame();
  const w = interpolate(frame, [70, 150, 168], [4, 90, 320], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: Easing.in(Easing.quad),
  });
  const opacity = interpolate(frame, [70, 120, 158, 175], [0, 0.7, 0.95, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const hue = interpolate(frame, [70, 168], [0, 1], { extrapolateRight: "clamp" });
  const color = hue > 0.7 ? AURUM_HOT : IRIS;
  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center" }}>
      <div
        style={{
          width: w,
          height: "120%",
          opacity,
          background: `linear-gradient(to top, transparent, ${color} 30%, #fff 50%, ${color} 70%, transparent)`,
          filter: `blur(${interpolate(frame, [70, 168], [2, 40])}px)`,
          mixBlendMode: "screen",
        }}
      />
    </AbsoluteFill>
  );
};

// ----------------------------------------------------------------------------
// Layer 3 — the 5★ burst: white→gold flash. Confirms the rarity.
// ----------------------------------------------------------------------------
const Burst: React.FC = () => {
  const frame = useCurrentFrame(); // local to its Sequence
  const flash = interpolate(frame, [0, 6, 26], [0, 1, 0], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
    easing: Easing.out(Easing.quad),
  });
  const ringScale = interpolate(frame, [0, 30], [0.1, 2.4], {
    extrapolateRight: "clamp",
    easing: Easing.out(Easing.cubic),
  });
  const ringOpacity = interpolate(frame, [2, 30], [0.9, 0], { extrapolateRight: "clamp" });
  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center" }}>
      <AbsoluteFill
        style={{
          opacity: flash,
          background: `radial-gradient(circle at 50% 48%, #fff 0%, ${AURUM_HOT} 25%, ${AURUM} 45%, transparent 70%)`,
        }}
      />
      <div
        style={{
          width: 700,
          height: 700,
          borderRadius: "50%",
          border: `6px solid ${AURUM_HOT}`,
          opacity: ringOpacity,
          transform: `scale(${ringScale})`,
          filter: `blur(2px) drop-shadow(0 0 30px ${AURUM})`,
        }}
      />
    </AbsoluteFill>
  );
};

// ----------------------------------------------------------------------------
// Layer 4 — the summon card: Velvet's portrait (thumb) in an aurum 5★ frame
// that rises out of the burst with a glow + shimmer sweep. Self-contained
// nameplate, so it reads as a gacha "result" card.
// ----------------------------------------------------------------------------
const SummonCard: React.FC<{ thumbSrc: string; name: string; title: string }> = ({
  thumbSrc,
  name,
  title,
}) => {
  const frame = useCurrentFrame(); // local to its Sequence
  const { fps } = useVideoConfig();

  const entrance = spring({ frame, fps, config: { damping: 14, mass: 0.85 }, durationInFrames: 42 });
  const opacity = interpolate(frame, [0, 14], [0, 1], { extrapolateRight: "clamp" });
  const rise = interpolate(entrance, [0, 1], [70, 0]);
  const scale = interpolate(entrance, [0, 1], [0.8, 1]);
  const float = Math.sin(frame / 30) * 6;
  const glow = 0.7 + Math.sin(frame / 24) * 0.3;

  // gold light sweep across the art, once, on entrance
  const sweep = interpolate(frame, [8, 46], [-1.4, 1.4], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });

  // nameplate reveals after the card has landed
  const starsProgress = interpolate(frame, [26, 60], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const nameSpring = spring({ frame: frame - 34, fps, config: { damping: 200 }, durationInFrames: 36 });
  const nameOpacity = interpolate(frame, [34, 52], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const titleOpacity = interpolate(frame, [52, 74], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  const CARD_W = 560;
  const ART_H = 540;

  return (
    <AbsoluteFill style={{ alignItems: "center", justifyContent: "center" }}>
      {/* aurum halo behind the card */}
      <div
        style={{
          position: "absolute",
          width: 900,
          height: 900,
          opacity: opacity * 0.85 * glow,
          background: `radial-gradient(circle, ${AURUM} 0%, ${IRIS_DEEP} 30%, transparent 60%)`,
          filter: "blur(40px)",
          mixBlendMode: "screen",
          transform: `translateY(${float * 0.5}px)`,
        }}
      />

      <div
        style={{
          width: CARD_W,
          opacity,
          transform: `translateY(${rise + float}px) scale(${scale})`,
          borderRadius: 26,
          overflow: "hidden",
          border: `2px solid ${AURUM}`,
          background: "rgba(12,12,21,0.62)",
          boxShadow: `0 0 ${64 * glow}px rgba(232,169,60,0.45), 0 0 0 6px rgba(232,169,60,0.09), 0 30px 80px rgba(0,0,0,0.6)`,
        }}
      >
        {/* portrait */}
        <div style={{ position: "relative", width: "100%", height: ART_H, overflow: "hidden" }}>
          <Img src={staticFile(thumbSrc)} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
          {/* shimmer */}
          <div
            style={{
              position: "absolute",
              top: 0,
              bottom: 0,
              left: "-20%",
              width: "40%",
              transform: `translateX(${sweep * 220}%) skewX(-16deg)`,
              background: "linear-gradient(90deg, transparent, rgba(255,231,168,0.4), transparent)",
              mixBlendMode: "screen",
            }}
          />
          {/* fade the portrait into the nameplate */}
          <AbsoluteFill
            style={{ background: "linear-gradient(to bottom, transparent 58%, rgba(12,12,21,0.92))" }}
          />
        </div>

        {/* nameplate */}
        <div style={{ padding: "10px 20px 26px", textAlign: "center" }}>
          <div
            style={{
              fontFamily: SORA,
              letterSpacing: "0.42em",
              paddingLeft: "0.42em",
              fontSize: 14,
              color: AURUM,
              textTransform: "uppercase",
              marginBottom: 10,
              opacity: starsProgress,
            }}
          >
            Invocação
          </div>
          <RarityStars progress={starsProgress} />
          <div
            style={{
              fontFamily: FRAUNCES,
              fontWeight: 600,
              fontSize: 76,
              lineHeight: 1,
              color: "#fff",
              marginTop: 6,
              opacity: nameOpacity,
              transform: `translateY(${(1 - nameSpring) * 18}px)`,
              letterSpacing: "0.03em",
              textShadow: "0 0 30px rgba(123,107,242,0.5)",
            }}
          >
            {name}
          </div>
          <div
            style={{
              fontFamily: SORA,
              fontSize: 18,
              color: "rgba(255,255,255,0.82)",
              opacity: titleOpacity,
              marginTop: 10,
              letterSpacing: "0.04em",
            }}
          >
            {title}
          </div>
        </div>
      </div>
    </AbsoluteFill>
  );
};

// ----------------------------------------------------------------------------
// Layer 5 — particles. Embers that rise after the reveal; mode "converge"
// pulls dust inward during the charge.
// ----------------------------------------------------------------------------
const Particles: React.FC<{ count: number; mode: "converge" | "rise"; color: string; seed: string }> = ({
  count,
  mode,
  color,
  seed,
}) => {
  const frame = useCurrentFrame();
  const { width, height } = useVideoConfig();
  const items = Array.from({ length: count });
  return (
    <AbsoluteFill style={{ mixBlendMode: "screen" }}>
      {items.map((_, i) => {
        const r1 = random(`${seed}-x-${i}`);
        const r2 = random(`${seed}-y-${i}`);
        const r3 = random(`${seed}-s-${i}`);
        const r4 = random(`${seed}-p-${i}`);
        const size = 2 + r3 * 5;

        if (mode === "converge") {
          const angle = r1 * Math.PI * 2;
          const dist = (0.3 + r2 * 0.7) * (width * 0.55);
          const t = (frame / 110 + r4) % 1;
          const x = width / 2 + Math.cos(angle) * dist * (1 - t);
          const y = height / 2 + Math.sin(angle) * dist * (1 - t);
          const opacity = interpolate(t, [0, 0.1, 0.9, 1], [0, 0.9, 0.9, 0]) * 0.8;
          return (
            <div
              key={i}
              style={{
                position: "absolute",
                left: x,
                top: y,
                width: size,
                height: size,
                borderRadius: "50%",
                background: color,
                opacity,
                boxShadow: `0 0 ${size * 2}px ${color}`,
              }}
            />
          );
        }

        // rise
        const x = r1 * width;
        const speed = 0.4 + r2 * 0.9;
        const y = (height + 80 - ((frame * speed + r4 * height) % (height + 120)));
        const drift = Math.sin((frame / 30 + r4 * 10)) * 24;
        const opacity = 0.25 + r3 * 0.6;
        return (
          <div
            key={i}
            style={{
              position: "absolute",
              left: x + drift,
              top: y,
              width: size,
              height: size,
              borderRadius: "50%",
              background: color,
              opacity,
              boxShadow: `0 0 ${size * 2.5}px ${color}`,
            }}
          />
        );
      })}
    </AbsoluteFill>
  );
};

// ----------------------------------------------------------------------------
// ★★★★★ rarity row (cascades in), used inside the summon card nameplate
// ----------------------------------------------------------------------------
const RarityStars: React.FC<{ progress: number }> = ({ progress }) => {
  return (
    <div style={{ display: "flex", gap: 10, justifyContent: "center" }}>
      {Array.from({ length: 5 }).map((_, i) => {
        const local = interpolate(progress, [i * 0.12, i * 0.12 + 0.3], [0, 1], {
          extrapolateLeft: "clamp",
          extrapolateRight: "clamp",
        });
        return (
          <span
            key={i}
            style={{
              fontSize: 38,
              color: AURUM,
              opacity: local,
              transform: `translateY(${(1 - local) * 14}px) scale(${0.6 + local * 0.4})`,
              textShadow: `0 0 18px ${AURUM}, 0 0 36px rgba(232,169,60,0.6)`,
            }}
          >
            ★
          </span>
        );
      })}
    </div>
  );
};

// ----------------------------------------------------------------------------
// Composition
// ----------------------------------------------------------------------------
export const VelvetSummon: React.FC<VelvetSummonProps> = ({ name, title, thumbSrc, bgSrc }) => {
  const frame = useCurrentFrame();

  // final cinematic fade in/out
  const fadeIn = interpolate(frame, [0, 18], [0, 1], { extrapolateRight: "clamp" });
  const fadeOut = interpolate(frame, [SUMMON_DURATION - 24, SUMMON_DURATION], [1, 0], {
    extrapolateLeft: "clamp",
  });

  return (
    <AbsoluteFill style={{ backgroundColor: INK, opacity: fadeIn * fadeOut }}>
      <Backdrop bgSrc={bgSrc} />

      {/* build-up: converging dust + arcane circle + energy column */}
      <Sequence durationInFrames={180}>
        <Particles count={70} mode="converge" color={IRIS} seed="charge" />
      </Sequence>
      <Sequence durationInFrames={180}>
        <ArcaneCircle />
      </Sequence>
      <Sequence durationInFrames={180}>
        <EnergyColumn />
      </Sequence>

      {/* the 5★ burst */}
      <Sequence from={152} durationInFrames={40}>
        <Burst />
      </Sequence>

      {/* embers rise as the card emerges */}
      <Sequence from={172}>
        <Particles count={48} mode="rise" color={AURUM} seed="embers" />
      </Sequence>

      {/* reveal: the summon card (portrait + 5★ frame + nameplate) */}
      <Sequence from={166}>
        <SummonCard thumbSrc={thumbSrc} name={name} title={title} />
      </Sequence>
    </AbsoluteFill>
  );
};
