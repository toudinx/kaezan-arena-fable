import { Config } from "@remotion/cli/config";

// Cinematics for Kaezan Arena Fable. Output is a .webm copied into
// frontend/public/assets/cinematics/ — never part of the Angular bundle.
Config.setVideoImageFormat("png");
Config.setCodec("vp8");
Config.overrideWebpackConfig((c) => c);
