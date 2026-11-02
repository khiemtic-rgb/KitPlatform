/** Isolated durable VoicePhysicalStore for Node smokes. Not production App_Data. Not imported by UI. */

import { existsSync, mkdirSync, readdirSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import {
  assertVoicePhysicalStorageKey,
  VOICE_PHYSICAL_KEY_PREFIX,
  type VoicePhysicalStore,
} from './famixa-voice-physical-identity';

function fileOf(rootDir: string, key: string) {
  const id = assertVoicePhysicalStorageKey(key);
  return join(rootDir, `${id.replace(/:/g, '__')}.bin`);
}

export function createFileVoicePhysicalStore(rootDir: string): VoicePhysicalStore {
  mkdirSync(rootDir, { recursive: true });
  return {
    get(key) {
      const path = fileOf(rootDir, key);
      if (!existsSync(path)) return undefined;
      const raw = readFileSync(path);
      return raw.length ? new Uint8Array(raw) : undefined;
    },
    set(key, bytes) {
      mkdirSync(rootDir, { recursive: true });
      writeFileSync(fileOf(rootDir, key), Buffer.from(bytes));
    },
    delete(key) {
      const path = fileOf(rootDir, key);
      if (existsSync(path)) unlinkSync(path);
    },
    keys() {
      if (!existsSync(rootDir)) return [];
      const prefix = `${VOICE_PHYSICAL_KEY_PREFIX}:`;
      return readdirSync(rootDir)
        .filter((name) => name.endsWith('.bin'))
        .map((name) => name.slice(0, -4).replace(/__/g, ':'))
        .filter((key) => key.startsWith(prefix));
    },
  };
}
