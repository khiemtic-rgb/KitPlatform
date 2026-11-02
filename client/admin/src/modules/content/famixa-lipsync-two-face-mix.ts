/** Two-face CGI still sends Fal (bounding box + take frame count). Mute overlay is not a fix. */

export const LIPSYNC_TWO_FACE_MIX_ID = 'FAMIXA_LIPSYNC_TWO_FACE_MIX_ONLY_V1';

/** Leftover mute-close flag. Opening the shot clears it so Khớp môi can run. */
export function leftoverTwoFaceMixOnly(run?: { lipsyncMixOnly?: boolean; lipsyncStatus?: string }) {
  return Boolean(run?.lipsyncMixOnly) || (run?.lipsyncStatus || '').toUpperCase() === 'MIX_ONLY';
}

export function clearTwoFaceLipsyncMixOnly(): { lipsyncMixOnly: false; lipsyncStatus: undefined } {
  return { lipsyncMixOnly: false, lipsyncStatus: undefined };
}
