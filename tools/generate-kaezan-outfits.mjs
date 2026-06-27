import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { deflateSync, inflateSync } from 'node:zlib';

const TIBIA_ROOT = join('frontend', 'public', 'assets', 'tibia');
const OUT_ROOT = join('frontend', 'public', 'assets', 'kaezan-outfits');
const OUTFIT_ROOT = join(OUT_ROOT, 'outfits');

const SOURCE_LOOKTYPE = 433;
const VELVET_LOOKTYPE = 900003;
const CELL = 64;
const COLS = 8;
const BASE_PATTERN_Y = 0;
const BASE_PATTERN_Z = 0;

const VELVET_COLORS = {
  head: 127,
  body: 109,
  legs: 109,
  feet: 71,
};

const manifest = {
  outfits: {},
  objects: {},
  effects: {},
  missiles: {},
  semantic: {},
  objectNames: {},
};

let sourceEntry;
let sourceAtlas;

function main() {
  const tibiaManifest = JSON.parse(readFileSync(join(TIBIA_ROOT, 'manifest.json'), 'utf8'));
  sourceEntry = tibiaManifest.outfits[String(SOURCE_LOOKTYPE)];
  if (!sourceEntry) throw new Error(`source lookType ${SOURCE_LOOKTYPE} is missing from the Tibia manifest`);

  sourceAtlas = decodePng(readFileSync(join(TIBIA_ROOT, sourceEntry.file)));
  const sourceIdle = sourceEntry.groups.find((group) => group.kind === 'idle');
  const sourceMoving = sourceEntry.groups.find((group) => group.kind === 'moving');
  if (!sourceIdle || !sourceMoving) throw new Error(`source lookType ${SOURCE_LOOKTYPE} needs idle and moving groups`);
  if (sourceEntry.cellW !== CELL || sourceEntry.cellH !== CELL) {
    throw new Error(`source lookType ${SOURCE_LOOKTYPE} must use ${CELL}x${CELL} cells`);
  }

  const idleGroup = slimGroup(sourceIdle, 0);
  const movingGroup = slimGroup(sourceMoving, idleGroup.count);
  const outputAtlas = buildAtlas([sourceIdle, sourceMoving], [idleGroup, movingGroup]);

  mkdirSync(OUTFIT_ROOT, { recursive: true });
  writeFileSync(join(OUTFIT_ROOT, `${VELVET_LOOKTYPE}.png`), encodePng(outputAtlas.width, outputAtlas.height, outputAtlas.rgba));

  manifest.outfits[String(VELVET_LOOKTYPE)] = {
    name: 'Velvet Kaezan V1',
    file: `outfits/${VELVET_LOOKTYPE}.png`,
    source: 'kaezan',
    tags: ['kaezan', 'v1', 'velvet', `reference:${SOURCE_LOOKTYPE}`],
    cellW: CELL,
    cellH: CELL,
    cols: COLS,
    groups: [idleGroup, movingGroup],
    flags: {},
  };

  writeFileSync(join(OUT_ROOT, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  console.log(`wrote Velvet Kaezan V1 lookType ${VELVET_LOOKTYPE} from reference ${SOURCE_LOOKTYPE}`);
}

function slimGroup(sourceGroup, start) {
  const phaseCount = phaseCountOf(sourceGroup);
  return {
    kind: sourceGroup.kind,
    patternX: sourceGroup.patternX,
    patternY: 1,
    patternZ: 1,
    layers: 1,
    phases: sourceGroup.phases.slice(0, phaseCount),
    start,
    count: phaseCount * sourceGroup.patternX,
  };
}

function phaseCountOf(group) {
  const perPhase = group.patternX * group.patternY * group.patternZ * group.layers;
  return Math.max(1, Math.floor(group.count / perPhase));
}

function buildAtlas(sourceGroups, outputGroups) {
  const totalCells = outputGroups.reduce((sum, group) => sum + group.count, 0);
  const width = COLS * CELL;
  const height = Math.ceil(totalCells / COLS) * CELL;
  const rgba = new Uint8Array(width * height * 4);
  const colors = {
    head: outfitColor(VELVET_COLORS.head),
    body: outfitColor(VELVET_COLORS.body),
    legs: outfitColor(VELVET_COLORS.legs),
    feet: outfitColor(VELVET_COLORS.feet),
  };

  for (let groupIndex = 0; groupIndex < sourceGroups.length; groupIndex++) {
    const sourceGroup = sourceGroups[groupIndex];
    const outputGroup = outputGroups[groupIndex];
    const phaseCount = phaseCountOf(sourceGroup);

    for (let phase = 0; phase < phaseCount; phase++) {
      for (let dir = 0; dir < sourceGroup.patternX; dir++) {
        const sourceBaseIndex = sourceGroup.start + sourceSpriteIndex(
          sourceGroup,
          phase,
          dir,
          BASE_PATTERN_Y,
          BASE_PATTERN_Z,
          0,
        );
        const sourceMaskIndex = sourceGroup.layers > 1
          ? sourceGroup.start + sourceSpriteIndex(sourceGroup, phase, dir, BASE_PATTERN_Y, BASE_PATTERN_Z, 1)
          : -1;
        const outputIndex = outputGroup.start + phase * outputGroup.patternX + dir;
        blitRecoloredCell(rgba, width, outputIndex, sourceBaseIndex, sourceMaskIndex, colors);
      }
    }
  }

  return { width, height, rgba };
}

function sourceSpriteIndex(group, phase, px, py, pz, layer) {
  return ((((phase * group.patternZ + pz) * group.patternY + py) * group.patternX + px) * group.layers + layer);
}

function blitRecoloredCell(output, outputWidth, outputIndex, sourceBaseIndex, sourceMaskIndex, colors) {
  const [dx, dy] = cellOrigin(outputIndex, COLS);
  const [sx, sy] = cellOrigin(sourceBaseIndex, sourceEntry.cols);
  const [mx, my] = sourceMaskIndex >= 0 ? cellOrigin(sourceMaskIndex, sourceEntry.cols) : [-1, -1];

  for (let y = 0; y < CELL; y++) {
    for (let x = 0; x < CELL; x++) {
      const sourceOffset = ((sy + y) * sourceAtlas.width + sx + x) * 4;
      const alpha = sourceAtlas.rgba[sourceOffset + 3];
      if (alpha === 0) continue;

      let red = sourceAtlas.rgba[sourceOffset];
      let green = sourceAtlas.rgba[sourceOffset + 1];
      let blue = sourceAtlas.rgba[sourceOffset + 2];
      const tint = tintForMaskPixel(mx, my, x, y, colors);
      if (tint) {
        red = Math.floor((red * tint[0]) / 255);
        green = Math.floor((green * tint[1]) / 255);
        blue = Math.floor((blue * tint[2]) / 255);
      }

      const outputOffset = ((dy + y) * outputWidth + dx + x) * 4;
      output[outputOffset] = red;
      output[outputOffset + 1] = green;
      output[outputOffset + 2] = blue;
      output[outputOffset + 3] = alpha;
    }
  }
}

function tintForMaskPixel(mx, my, x, y, colors) {
  if (mx < 0 || my < 0) return null;
  const maskOffset = ((my + y) * sourceAtlas.width + mx + x) * 4;
  if (sourceAtlas.rgba[maskOffset + 3] === 0) return null;

  const red = sourceAtlas.rgba[maskOffset];
  const green = sourceAtlas.rgba[maskOffset + 1];
  const blue = sourceAtlas.rgba[maskOffset + 2];
  if (red > 200 && green > 200 && blue < 60) return colors.head;
  if (red > 200 && green < 60 && blue < 60) return colors.body;
  if (red < 60 && green > 200 && blue < 60) return colors.legs;
  if (red < 60 && green < 60 && blue > 200) return colors.feet;
  return null;
}

function cellOrigin(index, cols) {
  return [(index % cols) * CELL, Math.floor(index / cols) * CELL];
}

function outfitColor(color) {
  const H_STEPS = 19;
  const SI_VALUES = 7;
  if (color >= H_STEPS * SI_VALUES) color = 0;

  let h = 0;
  let s = 0;
  let i = 0;
  if (color % H_STEPS !== 0) {
    h = (color % H_STEPS) / 18.0;
    s = 1;
    i = 1;
    switch (Math.floor(color / H_STEPS)) {
      case 0: s = 0.25; i = 1.0; break;
      case 1: s = 0.25; i = 0.75; break;
      case 2: s = 0.5; i = 0.75; break;
      case 3: s = 0.667; i = 0.75; break;
      case 4: s = 1.0; i = 1.0; break;
      case 5: s = 1.0; i = 0.75; break;
      case 6: s = 1.0; i = 0.5; break;
      default: break;
    }
  } else {
    i = 1 - color / H_STEPS / SI_VALUES;
  }

  if (i === 0) return [0, 0, 0];
  if (s === 0) {
    const value = Math.floor(i * 255);
    return [value, value, value];
  }

  let red = 0;
  let green = 0;
  let blue = 0;
  if (h < 1 / 6) {
    red = i;
    blue = i * (1 - s);
    green = blue + (i - blue) * 6 * h;
  } else if (h < 2 / 6) {
    green = i;
    blue = i * (1 - s);
    red = green - (i - blue) * (6 * h - 1);
  } else if (h < 3 / 6) {
    green = i;
    red = i * (1 - s);
    blue = red + (i - red) * (6 * h - 2);
  } else if (h < 4 / 6) {
    blue = i;
    red = i * (1 - s);
    green = blue - (i - red) * (6 * h - 3);
  } else if (h < 5 / 6) {
    blue = i;
    green = i * (1 - s);
    red = green + (i - green) * (6 * h - 4);
  } else {
    red = i;
    green = i * (1 - s);
    blue = red - (i - green) * (6 * h - 5);
  }

  return [Math.floor(red * 255), Math.floor(green * 255), Math.floor(blue * 255)];
}

function decodePng(buffer) {
  const signature = '89504e470d0a1a0a';
  if (buffer.subarray(0, 8).toString('hex') !== signature) throw new Error('invalid PNG signature');

  let width = 0;
  let height = 0;
  let bitDepth = 0;
  let colorType = 0;
  let interlace = 0;
  let pos = 8;
  const idatChunks = [];

  while (pos < buffer.length) {
    const length = buffer.readUInt32BE(pos);
    const type = buffer.subarray(pos + 4, pos + 8).toString('ascii');
    const data = buffer.subarray(pos + 8, pos + 8 + length);
    pos += 12 + length;

    if (type === 'IHDR') {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      interlace = data[12];
    } else if (type === 'IDAT') {
      idatChunks.push(data);
    } else if (type === 'IEND') {
      break;
    }
  }

  if (bitDepth !== 8 || colorType !== 6 || interlace !== 0) {
    throw new Error(`unsupported PNG format: bitDepth=${bitDepth}, colorType=${colorType}, interlace=${interlace}`);
  }

  const bytesPerPixel = 4;
  const stride = width * bytesPerPixel;
  const raw = inflateSync(Buffer.concat(idatChunks));
  const rgba = new Uint8Array(width * height * bytesPerPixel);
  let sourcePos = 0;

  for (let y = 0; y < height; y++) {
    const filter = raw[sourcePos++];
    const row = y * stride;
    const prevRow = row - stride;

    for (let x = 0; x < stride; x++) {
      const value = raw[sourcePos++];
      const left = x >= bytesPerPixel ? rgba[row + x - bytesPerPixel] : 0;
      const up = y > 0 ? rgba[prevRow + x] : 0;
      const upLeft = y > 0 && x >= bytesPerPixel ? rgba[prevRow + x - bytesPerPixel] : 0;

      let predictor = 0;
      switch (filter) {
        case 0: predictor = 0; break;
        case 1: predictor = left; break;
        case 2: predictor = up; break;
        case 3: predictor = Math.floor((left + up) / 2); break;
        case 4: predictor = paeth(left, up, upLeft); break;
        default: throw new Error(`unsupported PNG filter ${filter}`);
      }
      rgba[row + x] = (value + predictor) & 0xff;
    }
  }

  return { width, height, rgba };
}

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  if (pb <= pc) return b;
  return c;
}

function encodePng(width, height, rgba) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header[8] = 8;
  header[9] = 6;

  const stride = width * 4;
  const scanlines = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y++) {
    scanlines[y * (stride + 1)] = 0;
    Buffer.from(rgba.subarray(y * stride, (y + 1) * stride)).copy(scanlines, y * (stride + 1) + 1);
  }

  return Buffer.concat([
    Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(scanlines)),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

function chunk(type, data) {
  const out = Buffer.alloc(12 + data.length);
  out.writeUInt32BE(data.length, 0);
  out.write(type, 4, 4, 'ascii');
  data.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + data.length)), 8 + data.length);
  return out;
}

const CRC_TABLE = new Uint32Array(256).map((_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = (c & 1) ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});

function crc32(buf) {
  let crc = 0xffffffff;
  for (const byte of buf) crc = CRC_TABLE[(crc ^ byte) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

main();
