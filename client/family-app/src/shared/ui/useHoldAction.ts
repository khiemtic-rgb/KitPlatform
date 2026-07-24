import { useCallback, useRef, useState } from 'react';

const HOLD_MS = 1400;

/** Long-press gate — kids must hold, not tap, to leave their flow. */
export function useHoldAction(onComplete: () => void, holdMs = HOLD_MS) {
  const [progress, setProgress] = useState(0);
  const [holding, setHolding] = useState(false);
  const timerRef = useRef<number | null>(null);
  const startRef = useRef(0);
  const frameRef = useRef<number | null>(null);
  const doneRef = useRef(false);

  const clear = useCallback(() => {
    if (timerRef.current != null) window.clearTimeout(timerRef.current);
    if (frameRef.current != null) window.cancelAnimationFrame(frameRef.current);
    timerRef.current = null;
    frameRef.current = null;
    setHolding(false);
    setProgress(0);
    doneRef.current = false;
  }, []);

  const tick = useCallback(() => {
    const elapsed = performance.now() - startRef.current;
    const next = Math.min(1, elapsed / holdMs);
    setProgress(next);
    if (next < 1) {
      frameRef.current = window.requestAnimationFrame(tick);
    }
  }, [holdMs]);

  const start = useCallback(() => {
    clear();
    doneRef.current = false;
    setHolding(true);
    startRef.current = performance.now();
    frameRef.current = window.requestAnimationFrame(tick);
    timerRef.current = window.setTimeout(() => {
      if (doneRef.current) return;
      doneRef.current = true;
      setProgress(1);
      onComplete();
      clear();
    }, holdMs);
  }, [clear, holdMs, onComplete, tick]);

  const cancel = useCallback(() => {
    clear();
  }, [clear]);

  return { progress, holding, start, cancel, holdMs };
}
