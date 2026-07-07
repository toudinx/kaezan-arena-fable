import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const flags = JSON.parse(readFileSync(new URL("../data/appearance-flags.json", import.meta.url), "utf8"));

test("appearance flags include tile dimensions for map importer validation", () => {
  const smallGround = flags["950"];
  assert.ok(smallGround, "expected fixture appearance 950");
  assert.equal(smallGround.w, 1);
  assert.equal(smallGround.h, 1);

  const largeEffectLikeObject = flags["20402"];
  assert.ok(largeEffectLikeObject, "expected fixture appearance 20402");
  assert.ok(largeEffectLikeObject.w >= 2 || largeEffectLikeObject.h >= 2);
});
