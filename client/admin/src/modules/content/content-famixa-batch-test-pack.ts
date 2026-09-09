/** 3-beat pack to test I2V BATCH. New bản dựng only — does not overwrite EP01. */

import { newSeriesBuild } from './content-famixa-build';
import { parseFamixaPack, replaceStoryFromParse, type SeriesPilotState } from './content-famixa-series';

export const BATCH_TEST_PACK = `VIDEO TITLE: TEST BATCH 3 SHORT
EPISODE: 99
FORMAT: Family Short Film

CHAR-001 — Minh
CHAR-003 — Linh

SC01 — PHÒNG KHÁCH - TỐI

Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.
MINH
Mẹ xem giúp con tờ này.

Linh ngồi ghế sofa, không ngẩng, vẫn nhìn điện thoại.
LINH
Để đấy.

Minh bước tới bàn ăn, đặt tờ giấy xuống, đứng im.
MINH
Dạ mẹ.
`;

/** Empty row + pack. Keeps Canon/Voice. Drops EP01 shots/KF/memory so inheritance does not block. */
export function applyBatchTestPack(prev: SeriesPilotState): SeriesPilotState {
  const parsed = parseFamixaPack(BATCH_TEST_PACK);
  if (parsed.error) throw new Error(parsed.error);
  return replaceStoryFromParse(
    { ...newSeriesBuild(prev), storyMemory: undefined, outputAspect: '16:9' },
    parsed,
    BATCH_TEST_PACK,
  );
}
