/** ActingBeat → compiled prompt wrappers. Does not rewrite Director-saved beat fields. */

export type ActingSpeakerProfile = {
  characterId?: string;
  name?: string;
  gender?: string;
  pronoun?: string;
};

type DialogueShot = { dialogueSegmentIds?: string[] };

type DialogueState = {
  characters?: Array<{ id?: string; name?: string; gender?: string; pronoun?: string }>;
  roles?: Array<{ characterId?: string; name?: string; title?: string; gender?: string; pronoun?: string }>;
  lines?: Array<{ id?: string; characterId?: string; name?: string }>;
};

/** Spoken-shot speaker from the locked Dialogue/Line/Cue — not roster order. */
export function actingSpeakerFromDialogue(state: DialogueState, shot: DialogueShot): ActingSpeakerProfile {
  const ids = shot.dialogueSegmentIds;
  const line = Array.isArray(ids) && ids[0] ? (state.lines ?? []).find((row) => row.id === ids[0]) : undefined;
  if (!line?.characterId) return { name: '' };
  const id = line.characterId;
  const character = (state.characters ?? []).find((row) => row.id === id);
  const role = (state.roles ?? []).find((row) => row.characterId === id);
  return {
    characterId: id,
    name: (line.name || character?.name || role?.name || role?.title || '').trim(),
    gender: (character?.gender || role?.gender || '').trim() || undefined,
    pronoun: (character?.pronoun || role?.pronoun || '').trim() || undefined,
  };
}

/** Only when profile carries canonical gender/pronoun. Do not infer from the name. */
export function actingSpeakerPronounOf(profile: ActingSpeakerProfile) {
  const name = (profile.name || '').trim();
  const raw = `${profile.pronoun || ''} ${profile.gender || ''}`.trim().toLowerCase();
  if (/^(he|him|his)\b/.test(raw) || /\b(male|nam|boy|bé trai)\b/.test(raw)) {
    return { subject: 'He', possessive: 'His', name };
  }
  if (/^(she|her|hers)\b/.test(raw) || /\b(female|nữ|nu|girl|bé gái)\b/.test(raw)) {
    return { subject: 'She', possessive: 'Her', name };
  }
  return { name };
}

export function actingPerformanceLine(emotion: string | undefined, profile: ActingSpeakerProfile) {
  const mood = (emotion || '').trim();
  if (!mood) return undefined;
  const who = actingSpeakerPronounOf(profile);
  if (who.possessive) return `${who.possessive} performance is ${mood} and contained.`;
  if (who.name) return `${who.name}'s performance is ${mood} and contained.`;
  return `The performance is ${mood} and contained.`;
}

export function actingSpeechTimingLine(spoken: boolean, profile: ActingSpeakerProfile) {
  if (!spoken) return undefined;
  const who = actingSpeakerPronounOf(profile);
  if (who.subject) return `${who.subject} begins speaking after a short beat.`;
  if (who.name) return `${who.name} begins speaking after a short beat.`;
  return 'Begins speaking after a short beat.';
}
